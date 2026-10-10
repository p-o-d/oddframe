# OddFrame: NES Emulator Architecture Design

Date: 2026-10-10
Status: Approved

## Purpose

OddFrame is a NES emulator written in C# on .NET 10. It is a personal coding exercise: the owner writes all code by hand. The emulator must:

- Play commercial NTSC ROMs, starting with simple mappers and growing coverage over time.
- Have a simple, modular architecture where each component can be understood and tested in isolation.
- Support convenient debugging: cycle-level stepping, breakpoints, trace logs, save states, rewind, and PPU viewers.
- Keep the emulation core as a UI-agnostic class library consumed by several apps (desktop visualiser, CLI, tests).

Out of scope for this design: PAL and Dendy timing (see section 2, Region), Famicom Disk System, Zapper, netplay, shaders, mappers beyond the six listed in section 4.

## Key decisions

| Decision | Choice | Reason |
|---|---|---|
| Timing model | Cycle-stepped with a master clock | True cycle-level debugging, highest accuracy, one place decides component ordering. |
| Core structure | Classes with explicit buses, constructor injection, no DI container | Conventional, readable, each component testable with a fake bus. |
| Front end | Avalonia desktop app, plus a headless CLI | Cross-platform; debugger UI needs real widgets. |
| Debug scope | Step/breakpoints/trace, save states + rewind, PPU/APU viewers | Chosen by owner. Input record/replay deferred. |
| Audio | APU frame counter from the start, channels later | Frame counter IRQ affects CPU behaviour; sample output can wait. |
| Host model | Pull, not push | Core has no events, threads, or wall-clock timing. Host drives the frame loop. |

## 1. Solution layout

```
oddframe.sln
src/
  OddFrame.Core/          Class library. Zero non-BCL dependencies. All emulation logic.
    Device.cs             Top-level device: owns components, master clock, Tick() and Step*() helpers.
    Cpu/                  Cpu, instruction table, addressing modes, trace entry struct.
    Ppu/                  Ppu, built-in palette, framebuffer writer.
    Apu/                  Apu (frame counter now, channels later).
    Bus/                  IBus, CpuBus, PpuBus.
    Cartridge/            InesLoader, RomImage, Cartridge, IMapper, MapperBase, MapperFactory, Mappers/.
    Input/                Buttons enum, StandardController.
    Debug/                ObservableBus, ITraceSink, NestestFormatter, BreakpointSet, RewindBuffer, PpuDebugRenderer.
  OddFrame.App/           Avalonia desktop app. Screen, input, debugger panels.
  OddFrame.Cli/           Console app. Headless run, trace dump, screenshot, test-ROM runner.
tests/
  OddFrame.Core.Tests/    xUnit. Unit tests per component plus test-ROM harness.
  roms/                   Test ROMs fetched by script. Gitignored except fetch.sh and checksums.
docs/
  superpowers/specs/      Design documents.
```

Rules:

- `OddFrame.Core` references only the BCL. No Avalonia, no System.Drawing, no ImageSharp.
- `App` and `Cli` reference `Core`. Never the reverse.
- `Core` exposes no UI types. Framebuffer is `uint[]` / `ReadOnlySpan<uint>`; audio is `float[]`; input is a `byte` bitmask.
- One namespace per folder: `OddFrame.Core.Cpu`, `OddFrame.Core.Ppu`, etc. `Device` lives in `OddFrame.Core`.
- Debug helpers live inside `Core` under `Debug/`. They need bus internals and must cost nothing when unused.
- Project settings: `net10.0`, `<Nullable>enable</Nullable>`, `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` in Core, no unsafe code.

## 2. Timing and clock

NTSC only at first. Master clock 21.477272 MHz. CPU divider 12, PPU divider 4, so one CPU cycle equals three PPU dots. Dividers and scanline counts come from a `TimingProfile` (see Region below), not from constants, so other regions can be added later.

```
Device.Tick()               One master clock. Hot path.
  masterClock++
  if (masterClock % 4 == 0)  ppu.Tick()     // one dot
  if (masterClock % 12 == 0) cpu.Tick()     // one CPU cycle; APU ticks with it

Device.StepCpuCycle()       Tick until the CPU advanced one cycle.
Device.StepInstruction()    Tick until the CPU is at an instruction boundary.
Device.StepScanline()       Tick until ppu.Scanline changes.
Device.StepFrame()          Tick until ppu.FrameComplete.
Device.Run(Func<bool> stop) Generic loop; the helpers above are sugar over it.
```

Optimisation (later, not now): replace `%` with countdown counters, or tick the PPU three times per CPU cycle. `Tick()` is the only place that decides ordering, so this is a local change.

### CPU

The CPU is cycle-stepped. `Cpu.Tick()` performs exactly one bus access (read or write) or one internal cycle.

- State: registers A, X, Y, S, PC, P; `cycle` index within the current instruction; `opcode`; temporaries (`address`, `operand`).
- `Tick()`: if `cycle == 0` fetch the opcode (this is the cycle's read). Otherwise dispatch on `opcode` and `cycle` to a micro-step. The last micro-step resets `cycle` to 0.
- Dummy reads and writes are emulated exactly. They matter for mappers and PPU registers.
- Interrupts: NMI is edge-detected, IRQ is level-sensitive. Both are sampled at the end of the second-to-last cycle of an instruction. `Cpu` exposes `Nmi` (edge input) and `IrqLine` (level input).
- OAM DMA (`$4014`): the CPU is stalled for 513 or 514 cycles. The DMA unit lives inside the CPU's tick path so traces see it as real cycles.
- `IsInstructionBoundary` is true when `cycle == 0`.
- Reset and power-on go through the real 7-cycle reset sequence.

### PPU

`Ppu.Tick()` advances one dot. Tracks `Scanline` (-1 to 260), `Dot` (0 to 340), and the odd/even frame flag. Writes pixels to the framebuffer during visible scanlines. Raises `NmiOutput` at scanline 241, dot 1 when enabled. Sets `FrameComplete` once per frame; `Device` clears it.

### APU

`Apu.Tick()` runs once per CPU cycle. First iteration implements only the frame counter sequencer (4-step and 5-step modes) and its IRQ flag. Channels, length counters, mixer, and resampler come in milestone 7.

### Region (future)

NTSC only is implemented first, but the design keeps the door open for Dendy and PAL. To make that cheap later, the following must be parameters read from a `TimingProfile` (selected by `DeviceOptions.Region`), never hard-coded constants scattered through components:

| Parameter | NTSC | Dendy | PAL |
|---|---|---|---|
| Master clock (Hz) | 21 477 272 | 26 601 712 | 26 601 712 |
| CPU divider | 12 | 15 | 16 |
| PPU divider | 4 | 5 | 5 |
| PPU dots per CPU cycle | 3 | 3 | 3.2 |
| Scanlines per frame | 262 | 312 | 312 |
| NMI scanline | 241 | 291 | 241 |
| Odd-frame dot skip | yes | no | no |
| APU frame counter, DMC rate, noise period tables | NTSC | NTSC | PAL |

Dendy is the cheap one: same 3:1 ratio as NTSC, only scanline count, NMI line, and the skip differ. PAL changes the ratio and the APU tables. Add Dendy first if 50 Hz is wanted; add PAL only when a specific PAL-exclusive game is wanted. Test ROMs are NTSC, so the test ladder always runs in NTSC.

### Determinism

No `Random`, `DateTime`, `Stopwatch`, or threads inside Core. Power-on RAM is zeroed unless the host supplies a fill pattern. The host throttles to 60.0988 fps outside Core.

## 3. Buses and components

Two address spaces, two buses. Each bus routes by address range to owners. Components never reference each other directly, only their bus.

```csharp
public interface IBus
{
    byte Read(ushort address);   // may have side effects (PPU registers, controllers)
    byte Peek(ushort address);   // no side effects; used by debugger and viewers
    void Write(ushort address, byte value);
}
```

### CpuBus

| Range | Owner |
|---|---|
| `$0000-$1FFF` | 2 KB internal RAM, mirrored four times. Owned by CpuBus. |
| `$2000-$3FFF` | PPU registers, eight registers mirrored. `ppu.ReadRegister(addr & 7)` / `WriteRegister`. |
| `$4000-$4013`, `$4015`, `$4017` | APU registers. |
| `$4014` | OAM DMA trigger. Bus tells the CPU to start DMA with the page value. |
| `$4016` | Controller 1 read; write is strobe for both controllers. |
| `$4017` | Controller 2 read; write goes to the APU. |
| `$4020-$FFFF` | Cartridge. `cartridge.CpuRead` / `CpuWrite`. Mapper decides. |

### PpuBus

| Range | Owner |
|---|---|
| `$0000-$1FFF` | Pattern tables. Cartridge CHR ROM or CHR RAM, mapper may bank. |
| `$2000-$2FFF` | Nametables. 2 KB internal VRAM owned by PpuBus. Mirroring from `cartridge.Mirroring`, may change at runtime. |
| `$3000-$3EFF` | Mirror of `$2000-$2EFF`. |
| `$3F00-$3FFF` | Palette RAM, 32 bytes, owned by the PPU itself because reads bypass the normal read buffer. |

### Components

| Component | Depends on | Exposes |
|---|---|---|
| `Cpu` | `IBus` (CpuBus) | `Tick()`, `Nmi`, `IrqLine`, registers, `Halted`, `IsInstructionBoundary`, `TotalCycles` |
| `Ppu` | `IBus` (PpuBus) | `Tick()`, `ReadRegister` / `WriteRegister`, `Scanline`, `Dot`, `Frame`, `NmiOutput`, `FrameComplete`, `Framebuffer`, read-only `Oam` and `Palette` |
| `Apu` | none (DMC reads via a callback into CpuBus, added later) | `Tick()`, `ReadRegister` / `WriteRegister`, `IrqOutput` |
| `Cartridge` | `IMapper` | `CpuRead` / `CpuWrite`, `PpuRead` / `PpuWrite`, `Mirroring`, `IrqOutput`, `PrgRam` |
| `StandardController` | none | `Write(strobe)`, `Read()` serial bit, `SetButtons(byte)` |
| `Device` | all of the above | `Tick()`, `Step*()`, `Run`, `Reset()`, `PowerOn()`, `SaveState()` / `LoadState()`, `Framebuffer`, `SetController` |

### Interrupt wiring

Lives in `Device.Tick()`, not in components. After ticking the PPU, Device copies `ppu.NmiOutput` to `cpu.Nmi`. Before ticking the CPU, Device sets `cpu.IrqLine = apu.IrqOutput || cartridge.IrqOutput`. The wiring is visible in one place and components stay ignorant of each other.

### Construction

Constructor injection, no DI container. `Device` builds components in dependency order: cartridge, PpuBus(cartridge), Ppu(ppuBus), Apu, controllers, CpuBus(ppu, apu, cartridge, controllers), Cpu(cpuBus). Tests can hand a fake `IBus` to `Cpu` and run nestest against RAM alone.

### Reset vs power-on

`PowerOn()` sets the documented power-up state (P = `$34`, S = `$FD`, RAM zeroed). `Reset()` does only what the reset line does (PC from `$FFFC`, S decremented by 3, I flag set). Both run the CPU reset sequence as real cycles.

## 4. Cartridge and mappers

### Loader

`InesLoader.Load(byte[])` parses iNES 1.0 and detects NES 2.0 (header byte 7, bits 2-3 equal to 2). It returns a `RomImage` record: PRG ROM bytes, CHR ROM bytes (empty means 8 KB CHR RAM), mapper number, mirroring (horizontal, vertical, four-screen), battery flag. Trainers are skipped. An unknown mapper throws `UnsupportedMapperException` with the mapper number in the message.

### Cartridge

Thin shell. Owns the `RomImage`, a CHR RAM buffer when needed, 8 KB PRG RAM when the mapper wants it, and one `IMapper`. Forwards all bus traffic to the mapper.

```csharp
public interface IMapper : IStateful
{
    byte CpuRead(ushort address);        // $4020-$FFFF; return 0 (open bus) if unmapped
    void CpuWrite(ushort address, byte value);
    byte PpuRead(ushort address);        // $0000-$1FFF only
    void PpuWrite(ushort address, byte value);
    Mirroring Mirroring { get; }         // may change at runtime (MMC1, MMC3)
    bool IrqPending { get; }             // MMC3 scanline counter; false for most
    void OnPpuA12Rise();                 // MMC3 hook; no-op default. Called by PpuBus.
}
```

Mappers receive the `RomImage` and the cartridge RAM buffers through their constructor. `MapperFactory.Create(RomImage, Cartridge)` switches on mapper number so registration is in one place.

`MapperBase` holds shared bank-window helpers (`Prg16k`, `Prg32k`, `Chr4k`, `Chr8k`), the mirroring field, and no-op IRQ and A12 defaults. Concrete mappers override only what differs.

### Mapper order

| # | Name | Why this order | Example games |
|---|---|---|---|
| 0 | NROM | Fixed banks, no registers. First milestone. | Donkey Kong, Super Mario Bros, Balloon Fight, Ice Climber, Excitebike |
| 2 | UxROM | One PRG bank register, CHR RAM. Simplest banking. | Mega Man, Castlevania, Contra, DuckTales, Metal Gear |
| 3 | CNROM | One CHR bank register. | Arkanoid, Gradius, Solomon's Key |
| 1 | MMC1 | Serial shift register, switchable mirroring. Large library. | Zelda, Metroid, Mega Man 2, Final Fantasy, Kid Icarus |
| 7 | AxROM | Single-screen mirroring. Cheap once MMC1 exists. | Marble Madness, Battletoads |
| 4 | MMC3 | Fine banking plus scanline IRQ via PPU A12. Needs accurate PPU timing. | SMB2, SMB3, Mega Man 3-6, Kirby's Adventure |

These six cover roughly 80 percent of the commercial library.

### Open bus and battery saves

Unmapped cartridge reads return 0 for now. A real open-bus value can be added later as a single field in `CpuBus`. `Cartridge.PrgRam` is exposed; the host reads it on close and restores it on load when `RomImage.HasBattery`. Core does no file IO.

## 5. Debug infrastructure

All in `OddFrame.Core.Debug`. Zero cost when unused.

### Stepping

Covered by `Device.Step*()` from section 2. The debugger UI calls them directly.

### Observation

`CpuBus` and `PpuBus` are wrapped, not modified.

```csharp
public sealed class ObservableBus : IBus
{
    public ObservableBus(IBus inner) { ... }
    public event Action<ushort, byte>? OnRead;    // address, value
    public event Action<ushort, byte>? OnWrite;
    // Read and Write delegate to inner, then raise the event. Peek never raises.
}
```

`Device` takes `DeviceOptions { bool EnableDebug }`. When true it inserts an `ObservableBus` between CPU and CpuBus, and between PPU and PpuBus. When false the CPU talks to the raw bus. One decision at construction time, no per-cycle overhead in normal play.

### Trace log

`ITraceSink { void OnInstruction(in CpuTraceEntry entry); }`. The CPU calls it at each instruction boundary when a sink is attached, passing registers, PC, opcode bytes, current PPU scanline and dot, and total CPU cycles. `NestestFormatter` turns an entry into the standard nestest log line. Formatting stays out of the CPU.

### Breakpoints

`BreakpointSet` holds execute-at-PC, read-at-address, and write-at-address breakpoints, each with an optional value condition. The debugger builds a predicate for `Device.Run` that checks `cpu.IsInstructionBoundary && breakpoints.MatchesPc(cpu.PC)`, and subscribes to `ObservableBus` events to set a hit flag for memory breakpoints. Device stops after the current `Tick()` when the flag is set. Scanline breakpoints are a predicate on `ppu.Scanline` and `ppu.Dot`.

### Snapshots

```csharp
public interface IStateful
{
    void Save(BinaryWriter writer);
    void Load(BinaryReader reader);
}
```

Every stateful class implements it. `Device.SaveState()` returns `byte[]`: a header (magic, version, ROM hash) followed by each component in fixed order. `LoadState(byte[])` verifies the hash and reads back. Mapper state is included through `Cartridge`. The framebuffer is included so the screen is correct immediately after load.

### Rewind

`RewindBuffer` is a ring of N snapshots. The host decides N and pushes `device.SaveState()` every frame or every few frames, then calls `device.LoadState(buffer.Pop())` while the rewind key is held. An NROM state is around 20 KB, so thousands of frames fit comfortably.

### Viewers

`Ppu` exposes `ReadOnlySpan<byte> Oam` and `ReadOnlySpan<byte> Palette`. Pattern tables and nametables are read via `Peek` on the PpuBus. `PpuDebugRenderer` (static, in Debug) renders pattern tables, nametables, and palettes into `uint[]` buffers so no viewer logic ends up in the App. APU viewer follows the same pattern once channels exist.

### Determinism tests

Given the same ROM, power-on state, and per-frame input, two runs must produce identical snapshots. Tests assert this (section 7).

## 6. Host contracts and the App

Core pushes nothing. The host pulls after each frame.

- **Video.** `Device.Framebuffer` is `ReadOnlySpan<uint>` of 256 × 240 pixels in `0xAARRGGBB`. Conversion from NES 6-bit colour to ARGB happens in Core using a built-in palette table. `Device.Palette` setter lets a host load a `.pal` file later.
- **Input.** `Device.SetController(int port, byte buttons)` with `[Flags] enum Buttons : byte { A = 1, B = 2, Select = 4, Start = 8, Up = 16, Down = 32, Left = 64, Right = 128 }`. Host calls it once per frame before `StepFrame()`. Core latches on strobe.
- **Audio.** Nothing in the stub phase. Later: `Device.AudioSamples` as `ReadOnlySpan<float>` of mono samples generated this frame at `DeviceOptions.SampleRate` (default 44100). The APU resamples internally.

Host frame loop:

```csharp
while (running)
{
    device.SetController(0, keyboard.ToButtons());
    device.StepFrame();
    bitmap.CopyFrom(device.Framebuffer);
    rewind.Push(device.SaveState());      // optional
    WaitForNextFrame(60.0988);            // host-side throttling
}
```

### OddFrame.App

Avalonia, MVVM with CommunityToolkit.Mvvm.

- `EmulatorService` owns the `Device` and runs the frame loop on a dedicated thread. Exposes `Pause`, `Resume`, `Step*`, `LoadRom`, `LoadState`, `SaveState`. All Device access goes through this thread via a command queue; the UI never touches Device concurrently. Debugger panels receive read-only copies (register struct, memory dump) produced while paused or at frame boundaries.
- Views: `MainWindow` (screen, menu), `DebuggerWindow` (disassembly, registers, breakpoints, step buttons), `PpuViewerWindow` (pattern tables, nametables, palettes, OAM), `MemoryWindow` (hex dump via Peek). Each has its own view model subscribing to a `FrameRendered` event from the service.
- Keyboard mapping is a `Dictionary<Key, Buttons>`, hard-coded first, settings file later.

### OddFrame.Cli

`System.CommandLine`, one command per use case:

- `run <rom> --frames N --screenshot out.png` runs headless and writes a PNG.
- `trace <rom> --instructions N` prints nestest-format trace to stdout.
- `test <rom>` runs a blargg-style test ROM and reports the status byte and text at `$6004`.

PNG encoding is the only non-BCL dependency in Cli: either `SixLabors.ImageSharp` or a minimal hand-written PNG writer.

## 7. Testing strategy

All tests in `OddFrame.Core.Tests` (xUnit).

### Layer 1: unit tests with fake buses

`RamBus : IBus` is a flat 64 KB array. Tests load a few opcodes, tick the CPU a known number of cycles, and assert registers, flags, and exact cycle count. Same approach for PPU register behaviour (`$2002` read clears vblank and the address latch, `$2005` / `$2006` latch toggle, `$2007` read buffer) and mapper banking. Fast; run on every build.

### Layer 2: test ROMs

`TestRomRunner` loads a ROM into a real `Device`, runs it, and checks a result.

| ROM | Check | Covers |
|---|---|---|
| `nestest.nes` | Force PC to `$C000`, trace through `NestestFormatter`, diff line by line against `nestest.log`; then assert `$02` and `$03` are zero. | All official and unofficial opcodes, cycle counts |
| blargg `instr_test-v5`, `cpu_timing_test6`, `instr_misc`, `cpu_interrupts_v2` | Run until `$6000` leaves `$80`, read status byte, read text at `$6004`. | CPU edge cases, dummy reads, interrupt timing |
| blargg `ppu_vbl_nmi`, `sprite_hit_tests`, `sprite_overflow_tests`, `oam_read`, `ppu_open_bus` | Same `$6000` protocol. | PPU timing, sprite 0, NMI edge |
| blargg `apu_test`, `apu_reset`, `dmc_dma_during_read4` | Same protocol. | APU frame counter, later channels |
| `mmc3_test_2` | Same protocol. | MMC3 scanline IRQ |

Test ROMs are free and redistributable. `tests/roms/fetch.sh` downloads them and verifies SHA-256 against `tests/roms/checksums.txt`. ROM files are gitignored. Tests skip (not fail) when a ROM is missing, so `dotnet test` works on a fresh clone.

### Layer 3: determinism and regression

- Run a ROM N frames twice from power-on; assert identical `SaveState()` bytes.
- Save a state, load it, run 100 more frames; compare against an uninterrupted run.
- Once games work, store a framebuffer hash per game at frame N as a golden value in a JSON file. Any CPU or PPU change that alters output is caught immediately.

### Organisation

One test class per component, named after it. `[Trait("Category", "Rom")]` on ROM tests so `dotnet test --filter Category!=Rom` runs the fast set.

## 8. Milestones

| # | Milestone | Done when |
|---|---|---|
| 0 | Skeleton | Solution, three projects, test project, `Device` with empty `Tick()`. `dotnet build && dotnet test` green. ROM fetch script works. |
| 1 | CPU | All 256 opcodes cycle-stepped, interrupts, reset sequence. nestest log diff identical for all 8991 lines. blargg `instr_test-v5` passes with a RAM-only cartridge stub. |
| 2 | Bus + NROM + CLI | `InesLoader`, `Cartridge`, `Nrom`, real `CpuBus` with RAM mirroring. `oddframe trace` on nestest matches through the real bus. `cpu_timing_test6` passes. |
| 3 | PPU background | Scanline/dot state machine, vblank and NMI, `$2000-$2007`, background fetch pipeline, scrolling, palette. `oddframe run --screenshot` shows the Donkey Kong title screen. `ppu_vbl_nmi` passes. |
| 4 | PPU sprites + input + App | OAM DMA, sprite evaluation, sprite 0 hit, 8×16 sprites, controllers. Avalonia window with keyboard. Donkey Kong, SMB, Balloon Fight playable. `sprite_hit_tests` pass. |
| 5 | Debugger | `ObservableBus`, trace sink, breakpoints, step buttons, disassembly, memory view, PPU viewers. Save/load state and rewind in the App. Determinism tests green. |
| 6 | Mappers | UxROM, CNROM, MMC1, AxROM. Mega Man, Zelda, Metroid, Castlevania boot and play. |
| 7 | APU | Pulse, triangle, noise, DMC, mixer, resampler, audio output in the App. `apu_test` passes. |
| 8 | MMC3 | PPU A12 tracking, scanline IRQ. SMB3, Mega Man 3. `mmc3_test_2` passes. |

Milestone 1 is the largest and depends on nothing else: a fake bus and the nestest reference log are enough to iterate. Every later milestone adds one component against an already-passing base.

## Reference material

- NESdev wiki: https://www.nesdev.org/wiki/ (CPU, PPU, APU, mapper, and iNES format references)
- 6502 opcode reference with cycle-by-cycle breakdown: https://www.nesdev.org/6502_cpu.txt
- nestest ROM and log: https://www.nesdev.org/wiki/Emulator_tests
- blargg test ROMs: https://github.com/christopherpow/nes-test-roms
- PPU rendering timing diagram: https://www.nesdev.org/wiki/File:Ntsc_timing.png
