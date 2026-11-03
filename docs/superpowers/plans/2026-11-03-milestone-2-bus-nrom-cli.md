# Milestone 2: Bus + NROM + CLI Implementation Plan

> **Executor:** the owner writes all code by hand (see `AGENTS.md`). Claude may write docs and, when asked, `tests/roms/fetch.sh`, `tests/roms/checksums.txt`, and CI. Everything under `src/` and `tests/OddFrame.Core.Tests/` is owner-written; this plan describes shape, edge cases, and tests, not full code. Each task is presented in the detailed explanatory format before it is implemented. Steps use `- [ ]` for tracking.

**Goal:** Load an iNES file into a `Cartridge` with the NROM mapper, wire `Cartridge → CpuBus → Cpu` inside `Device`, and add the `trace` CLI command. nestest traced through the real bus matches `nestest.log` for all 8991 lines. The 16 `instr_test-v5` singles and `instr_misc` singles 01-02 pass on the real `CpuBus` and NROM.

**Architecture:** `InesLoader` turns bytes into a `RomImage`. `Cartridge` owns the RAM buffers and one `IMapper` built by `MapperFactory`; NROM is the only mapper. `CpuBus` routes `$0000-$1FFF` to 2 KB of mirrored RAM and `$4020-$FFFF` to the cartridge. The PPU and APU ranges are placeholders until milestones 3, 4, and 7. `Device` builds the components in spec order and ticks the CPU every `CpuDivider` master clocks. There is no PPU in this milestone (scope B, chosen 2026-11-03).

**Tech Stack:** .NET 10, C# 14, xUnit v3. Core stays BCL-only (`System.Security.Cryptography.SHA256` is BCL). Cli adds `System.CommandLine` (latest stable 2.x).

**Spec:** `docs/superpowers/specs/2026-10-10-nes-emulator-architecture-design.md`, sections 1, 2 (clock), 3 (CpuBus, Components, Construction, Reset vs power-on), 4 (Loader, Cartridge), 5 (Trace log, Snapshots), 6 (OddFrame.Cli), 7, 8.

**Hardware references:**
- iNES header: https://www.nesdev.org/wiki/INES
- NES 2.0 header and the detection procedure: https://www.nesdev.org/wiki/NES_2.0#Identification
- NROM: https://www.nesdev.org/wiki/NROM
- CPU memory map: https://www.nesdev.org/wiki/CPU_memory_map
- Mirroring names: https://www.nesdev.org/wiki/Mirroring#Nametable_Mirroring
- Open bus: https://www.nesdev.org/wiki/Open_bus_behavior

---

## Decisions locked for this plan

Change any of these before starting; the tasks assume them.

| Question | Choice | Why |
|---|---|---|
| Scope | Spec scope, no PPU (option B) | Owner's choice 2026-11-03. Options A (PPU timing core in M2) and C (throwaway `$2002` stub in production) were rejected. |
| Exit ROMs | nestest through `Device` and through `oddframe trace`; 16 `instr_test-v5` singles; `instr_misc` singles `01-abs_x_wrap`, `02-branch_wrap` | All are NROM. The blargg ROMs run on the real `CpuBus` + `Cartridge` behind a test decorator that answers `$2002` with `$80`. |
| `cpu_timing_test6` | Moves to milestone 3 | Times instructions by counting loop passes between PPU NMIs and reports on screen only. No `$6000` protocol: the ROM never writes `DE B0 61`. Verified 2026-11-03 by byte scan and readme. |
| `Device` constructor | `Device(DeviceOptions options, RomImage rom)`; one `Device` per ROM | No "no cartridge" state to handle anywhere. The App swaps the whole `Device` on ROM load. |
| `Device` surface added | `Cpu Cpu`, `Cartridge Cartridge`, `IBus CpuMemory` (the raw `CpuBus`), `PowerOn()`, `Reset()`, `StepInstruction()`, `Run(Func<bool>)` | The ROM runner, the CLI, and the M5 debugger all need register access and side-effect-free memory peeks. `StepFrame` and `StepScanline` need the PPU and wait for M3. |
| Unsupported mapper | `MapperFactory.Create` throws `UnsupportedMapperException`; `InesLoader` accepts any mapper number | The loader stays mapper-agnostic, so M6 adds mappers without touching it. Spec section 4 amended. |
| PRG RAM | NROM always gets 8 KB at `$6000-$7FFF`, owned by `Cartridge` | blargg ROMs report results there. Every emulator provides it for NROM. |
| CHR | CHR size 0 in the header → `Cartridge` allocates 8 KB CHR RAM; otherwise CHR ROM, writes ignored | iNES 1.0 convention. `cpu_timing_test6` (M3) is CHR RAM; nestest and the blargg singles are CHR ROM. |
| Placeholder ranges in `CpuBus` | `$2000-$3FFF` and `$4000-$401F`: reads return 0, writes ignored. `$4014` ignored. | M3 plugs in the PPU, M4 OAM DMA and controllers, M7 the APU. Open bus stays 0 (spec section 4). |
| nestest start | `device.StepInstruction()` runs the 7-cycle reset sequence, then `device.Cpu.PC = 0xC000` | The ROM's reset vector is `$C004` (GUI mode, waits on the PPU). A ROM vector cannot be patched like M1's RAM vector. The first traced line is then `C000 ... P:24 SP:FD PPU:  0, 21 CYC:7`, exactly line 1. |
| nestest `= FF` lines | `NestestFormatter` prints `FF` for operand peeks in `$4000-$4017` | Lines 8981-8989 show `STA $4015 = FF` etc. That is Nintendulator's value, not hardware. The bus stays honest (peeks 0); the nestest quirk lives in the nestest formatter. `NestestBus` is deleted. |
| Trace sink | `OddFrame.Core.Debug.NestestTraceSink(IBus bus, TextWriter output)`; PPU position from `TotalCycles` (`× 3 / 341 % 262`, `× 3 % 341`) | Shared by `NestestTests` and the CLI, so the cycle → dot formula exists once. M3 changes it to read the real PPU. |
| Save state | Version 2. `Device.Save` writes `MasterClock`, `Cpu`, `CpuBus`, `Cartridge`. Header hash = SHA-256 of PRG ROM followed by CHR ROM. `LoadState` rejects a different hash. | ROM bytes never go into the state. Hashing PRG+CHR (not the file) ignores header junk and trainers. |
| NES 2.0 | Detected; mapper bits 8-11 from byte 8; PRG/CHR size MSB nibbles in byte 9 must be 0, else `InvalidDataException` | Non-zero MSB means ROMs of 4 MB and up; nothing on the roadmap needs them. |
| Archaic iNES | Not NES 2.0 and bytes 12-15 not all zero → ignore byte 7 (mapper high nibble = 0) | "DiskDude!" headers put ASCII in bytes 7-15 and would read as mapper 64+. NESdev detection procedure. |
| CLI | `System.CommandLine` 2.x: `trace <rom> --instructions <n> [--pc <hex>]` | `test` and `run` need the PPU and arrive in M3. |
| Bank helpers in `MapperBase` | Not yet | NROM has fixed banks. `Prg16k`/`Chr8k` etc. arrive with UxROM and CNROM in M6. |

## Known traps (verified 2026-11-03)

- Every blargg ROM with the `$6000` protocol (`instr_test-v5`, `instr_misc`) starts with `bit $2002 / bpl` and hangs forever on a bus that reads `$2002` as 0. Production `CpuBus` returns 0 there in M2; the test decorator returns `$80`.
- `instr_misc/instr_misc.nes` and `cpu_interrupts_v2/cpu_interrupts.nes` are mapper 1 (MMC1). Their `rom_singles` are NROM (32 KB PRG, 8 KB CHR, vertical). `instr_misc` `03-dummy_reads` needs the `$2002` read side effect (M3); `04-dummy_reads_apu` needs the APU. The `cpu_interrupts_v2` singles need the APU frame IRQ, PPU NMI, and OAM DMA.
- The 16 `instr_test-v5` singles are 32 KB PRG, 8 KB CHR ROM, vertical mirroring. nestest is 16 KB PRG (NROM-128), 8 KB CHR.
- `nestest.log` has LF line endings and 8991 lines ending at `C66E RTS`. Do not run past it.
- "Horizontal" mirroring (iNES byte 6 bit 0 = 0) means `$2000` = `$2400` and `$2800` = `$2C00`: the nametables are arranged vertically. Only the enum value matters in M2; the PpuBus mapping is M3.
- `Device.SaveState` currently writes 46 bytes; `DeviceTests.SaveState_HeaderLayout` hard-codes that length.
- `OddFrame.Cli/Program.cs` calls `new Device(new DeviceOptions())` and stops compiling when the constructor changes.
- `System.CommandLine` 2.0 GA changed the API from the widely copied 2.0-beta4 samples: `SetAction(parseResult => ...)`, `parseResult.GetValue(option)`, `rootCommand.Parse(args).Invoke()`. `SetHandler` and `InvocationContext` no longer exist.
- `dotnet run` writes build output to stdout when it builds. Build first and pass `--no-build` before piping the trace into `diff`.

## File structure

```
src/OddFrame.Core/Cartridges/Mirroring.cs                    enum: Horizontal, Vertical, FourScreen
src/OddFrame.Core/Cartridges/RomImage.cs                     sealed record: Prg, Chr, Mapper, Mirroring, HasBattery, IsNes20
src/OddFrame.Core/Cartridges/InesLoader.cs                   static RomImage Load(byte[] image)
src/OddFrame.Core/Cartridges/UnsupportedMapperException.cs   exception carrying the mapper number
src/OddFrame.Core/Cartridges/IMapper.cs                      spec section 4 interface
src/OddFrame.Core/Cartridges/MapperBase.cs                   abstract: buffers, Mirroring, no-op IrqPending / OnPpuA12Rise, empty IStateful
src/OddFrame.Core/Cartridges/MapperFactory.cs                static IMapper Create(RomImage rom, Cartridge cartridge)
src/OddFrame.Core/Cartridges/Cartridge.cs                    owns RomImage, PRG RAM, CHR RAM, mapper; forwards; IStateful
src/OddFrame.Core/Cartridges/Mappers/Nrom.cs                 mapper 0
src/OddFrame.Core/Bus/CpuBus.cs                              RAM + routing; IBus, IStateful, PowerOn()
src/OddFrame.Core/Device.cs                                  modify: wiring, stepping, save state v2
src/OddFrame.Core/Debug/NestestFormatter.cs                  modify: $4000-$4017 print FF
src/OddFrame.Core/Debug/NestestTraceSink.cs                  ITraceSink writing nestest lines to a TextWriter
src/OddFrame.Cli/Program.cs                                  root command
src/OddFrame.Cli/TraceCommand.cs                             trace command

tests/OddFrame.Core.Tests/Fakes/InesBuilder.cs               builds iNES byte[] for tests
tests/OddFrame.Core.Tests/InesBuilderTests.cs
tests/OddFrame.Core.Tests/Cartridges/InesLoaderTests.cs
tests/OddFrame.Core.Tests/Cartridges/NromTests.cs
tests/OddFrame.Core.Tests/Cartridges/CartridgeTests.cs
tests/OddFrame.Core.Tests/Cartridges/MapperFactoryTests.cs
tests/OddFrame.Core.Tests/Bus/CpuBusTests.cs
tests/OddFrame.Core.Tests/DeviceTests.cs                     modify: ROM in constructor, stepping
tests/OddFrame.Core.Tests/DeviceStateTests.cs                save state v2
tests/OddFrame.Core.Tests/Debug/NestestFormatterTests.cs     modify: FF quirk
tests/OddFrame.Core.Tests/Fakes/BlarggStubBus.cs             modify: decorator over any IBus
tests/OddFrame.Core.Tests/BlarggStubBusTests.cs              modify
tests/OddFrame.Core.Tests/Roms/BlarggRunner.cs               modify: InesLoader + Cartridge + CpuBus
tests/OddFrame.Core.Tests/Roms/InstrMiscTests.cs             new theory over 01, 02
tests/OddFrame.Core.Tests/Roms/NestestTests.cs               modify: through Device
tests/OddFrame.Core.Tests/Fakes/NestestBus.cs                delete
tests/OddFrame.Core.Tests/NestestBusTests.cs                 delete
tests/roms/fetch.sh, checksums.txt                           +2 instr_misc singles
```

## Public surfaces

Pinned here so tests and later milestones agree.

```csharp
public sealed record RomImage(byte[] Prg, byte[] Chr, int Mapper, Mirroring Mirroring, bool HasBattery, bool IsNes20);

public static class InesLoader { public static RomImage Load(byte[] image); }

public sealed class Cartridge : IStateful
{
    public Cartridge(RomImage rom);                 // builds RAM buffers, then MapperFactory.Create(rom, this)
    public RomImage Rom { get; }
    public byte[] PrgRam { get; }                   // 8 KB; host persists it when Rom.HasBattery
    public byte[] ChrRam { get; }                   // 8 KB when Rom.Chr is empty, else empty
    public Mirroring Mirroring { get; }             // from the mapper
    public bool IrqOutput { get; }                  // mapper.IrqPending
    public byte CpuRead(ushort address);  public void CpuWrite(ushort address, byte value);   // $4020-$FFFF
    public byte PpuRead(ushort address);  public void PpuWrite(ushort address, byte value);   // $0000-$1FFF
}

public sealed class CpuBus : IBus, IStateful
{
    public CpuBus(Cartridge cartridge);             // M3 adds Ppu, M4 controllers, M7 Apu
    public void PowerOn();                          // zero RAM
}

public sealed class Device : IStateful
{
    public Device(DeviceOptions options, RomImage rom);
    public Cpu Cpu { get; }
    public Cartridge Cartridge { get; }
    public IBus CpuMemory { get; }
    public void PowerOn();  public void Reset();
    public void StepInstruction();  public void Run(Func<bool> stop);
}
```

## CPU memory map in M2

| Range | Read | Write | Becomes real in |
|---|---|---|---|
| `$0000-$1FFF` | `ram[address & 0x07FF]` | same | now |
| `$2000-$3FFF` | 0 | ignored | M3 (`ppu.ReadRegister(address & 7)`) |
| `$4000-$4013`, `$4015` | 0 | ignored | M7 (APU) |
| `$4014` | 0 | ignored | M4 (OAM DMA) |
| `$4016`, `$4017` | 0 | ignored | M4 (controllers; `$4017` write goes to the APU in M7) |
| `$4018-$401F` | 0 | ignored | never (CPU test mode, disabled on retail units) |
| `$4020-$5FFF` | NROM: 0 | NROM: ignored | per mapper |
| `$6000-$7FFF` | PRG RAM | PRG RAM | now |
| `$8000-$FFFF` | `prg[address & (prg.Length - 1)]` | ignored | now |

---

### Task 1: `InesBuilder` test helper

**Files:**
- Create: `tests/OddFrame.Core.Tests/Fakes/InesBuilder.cs`
- Create: `tests/OddFrame.Core.Tests/InesBuilderTests.cs`

Every later test needs a ROM image: the loader tests need malformed headers, the mapper tests need marker bytes in known banks, and the Device tests need a reset vector and a tiny program. Build it first so no test hand-assembles 16-byte headers.

Shape: settable properties with defaults that produce a valid NROM-128 image (`PrgBanks = 1`, `ChrBanks = 1`, `Mapper = 0`, `Vertical = false`, `Battery = false`, `FourScreen = false`, `Trainer = null`, `Nes20 = false`, `HeaderTail` for bytes 8-15). Methods: `PutPrg(ushort cpuAddress, params byte[] bytes)` (maps a CPU address to the PRG offset with the NROM mirror rule), `PutChr(int offset, params byte[] bytes)`, `ResetVector(ushort target)`, `Build()`. For malformed headers (bad magic, DiskDude bytes, truncation) tests patch or slice the built array; the builder only produces well-formed images.

- [ ] **Step 1: Write failing `InesBuilderTests`**

| Test | Arrange | Expected | Catches |
|---|---|---|---|
| `Default_IsValidNrom128` | `new InesBuilder().Build()` | length `16 + 0x4000 + 0x2000`; bytes 0-3 `NES\x1A`; byte 4 = 1; byte 5 = 1 | wrong default sizes |
| `Mapper_SplitsIntoNibbles` | `Mapper = 0x42` | byte 6 high nibble `$2`, byte 7 high nibble `$4` | swapped nibbles in the builder hide the same bug in the loader |
| `PutPrg_AtC000_OnNrom128_LandsAtOffset0` | `PrgBanks = 1`, `PutPrg(0xC000, 0xAA)` | byte at `16 + 0` is `$AA` | builder ignoring the mirror |
| `ResetVector_WritesLittleEndianAtFFFC` | `ResetVector(0x8123)` | last PRG bytes `-4`, `-3` are `$23 $81` | big-endian vector |
| `Trainer_InsertedBeforePrg` | `Trainer = 512 bytes of $EE` | byte 6 bit 2 set; bytes `16..527` are `$EE`; PRG starts at 528 | |

- [ ] **Step 2: Run, confirm compile failure.** `dotnet test --filter "FullyQualifiedName~InesBuilderTests"`
- [ ] **Step 3: Write the builder**
- [ ] **Step 4: Run, confirm green**
- [ ] **Step 5: Commit** — `test: add InesBuilder for synthetic ROMs`

---

### Task 2: `RomImage`, `Mirroring`, `InesLoader`

**Files:**
- Create: `src/OddFrame.Core/Cartridges/Mirroring.cs`, `RomImage.cs`, `InesLoader.cs`
- Create: `tests/OddFrame.Core.Tests/Cartridges/InesLoaderTests.cs`

Header layout (iNES 1.0): bytes 0-3 `NES\x1A`; 4 PRG size in 16 KB units; 5 CHR size in 8 KB units (0 = CHR RAM); 6 flags (bit 0 mirroring, 1 battery, 2 trainer, 3 four-screen, 4-7 mapper low nibble); 7 flags (bits 2-3 format, 4-7 mapper high nibble); 8-15 rarely used in 1.0. Data: header, 512-byte trainer if flagged, PRG, CHR, then anything (PlayChoice data, padding), which is ignored.

Format detection, in this order: `(b7 & 0x0C) == 0x08` → NES 2.0 (mapper bits 8-11 from `b8 & 0x0F`; byte 9 nibbles must be 0). Otherwise, bytes 12-15 all zero → iNES 1.0. Otherwise → archaic, ignore byte 7.

Copy PRG and CHR into new arrays (`AsSpan(...).ToArray()`) so callers cannot alias the input buffer.

- [ ] **Step 1: Write failing `InesLoaderTests`**

| Test | Arrange (via `InesBuilder`) | Expected | Catches |
|---|---|---|---|
| `Load_BadMagic_Throws` | byte 3 = `$1B` | `InvalidDataException` | accepting any file |
| `Load_ShorterThanHeader_Throws` | 10 bytes | `InvalidDataException` | `IndexOutOfRange` instead of a clean error |
| `Load_Truncated_Throws` | header says 2 PRG banks, file has 1 | `InvalidDataException` | slicing past the end |
| `Load_ZeroPrg_Throws` | byte 4 = 0 | `InvalidDataException` | an empty PRG that later divides by zero or masks with -1 |
| `Load_Sizes` | 2 PRG, 1 CHR | `Prg.Length == 0x8000`, `Chr.Length == 0x2000` | unit mix-up (8 vs 16 KB) |
| `Load_ChrZero_ChrIsEmpty` | `ChrBanks = 0` | `Chr.Length == 0` | inventing CHR bytes |
| `Load_PrgContent` | marker `$5A` at PRG offset 0, `$A5` at last | both found | off-by-header offset |
| `Load_Trainer_IsSkipped` | trainer of `$EE`, PRG marker `$5A` at 0 | `Prg[0] == 0x5A` | PRG read from byte 16 when a trainer exists |
| `Load_Mirroring` | theory: `(b6 bit0, bit3)` → `(0,0) Horizontal`, `(1,0) Vertical`, `(0,1) FourScreen`, `(1,1) FourScreen` | enum value | bit 3 not overriding bit 0; swapped names |
| `Load_Battery` | bit 1 set | `HasBattery` | |
| `Load_MapperNibbles` | theory: 0, 1, 4, `0x42`, `0xFF` | `Mapper` equals input | missing `& 0xF0` or shift on byte 7 |
| `Load_Nes20_ReadsMapperBits8To11` | `Nes20 = true`, mapper `0x123` | `IsNes20`, `Mapper == 0x123` | NES 2.0 not detected |
| `Load_Nes20_HugeSize_Throws` | `Nes20`, byte 9 = `$01` | `InvalidDataException` | silently loading a wrong size |
| `Load_Archaic_IgnoresByte7` | mapper 0, byte 7 = `$44` (`'D'`), bytes 8-15 `"iskDude!"` | `Mapper == 0`, not 64 | DiskDude headers loading as mapper 64 |
| `Load_TrailingData_Ignored` | 100 extra bytes after CHR | loads, sizes unchanged | rejecting PlayChoice dumps |
| `Load_UnknownMapper_DoesNotThrow` | mapper 200 | `Mapper == 200` | loader enforcing the mapper list (the factory does) |
| `Load_CopiesBuffers` | load, then overwrite the input array | `Prg` unchanged | aliasing the caller's buffer |

- [ ] **Step 2: Run, confirm compile failure.** `dotnet test --filter "FullyQualifiedName~InesLoaderTests"`
- [ ] **Step 3: Write the types and the loader**
- [ ] **Step 4: Run, confirm green**
- [ ] **Step 5: Commit** — `feat: add iNES loader and RomImage`

---

### Task 3: `IMapper`, `MapperBase`, `Nrom`, `MapperFactory`, `Cartridge`

**Files:**
- Create: `src/OddFrame.Core/Cartridges/IMapper.cs`, `MapperBase.cs`, `MapperFactory.cs`, `Cartridge.cs`, `UnsupportedMapperException.cs`, `Mappers/Nrom.cs`
- Create: `tests/OddFrame.Core.Tests/Cartridges/NromTests.cs`, `CartridgeTests.cs`, `MapperFactoryTests.cs`

NROM (https://www.nesdev.org/wiki/NROM): PRG 16 KB (NROM-128, mirrored into both `$8000` and `$C000`) or 32 KB (NROM-256, linear). CHR 8 KB, ROM or RAM. No registers. `address & (prg.Length - 1)` covers both PRG sizes because both are powers of two; subtracting `$8000` alone breaks NROM-128 at `$C000`.

Ownership (spec section 4): `Cartridge` allocates PRG RAM (8 KB) and CHR RAM (8 KB when `rom.Chr` is empty), then calls `MapperFactory.Create(rom, this)`. The mapper gets the buffers from the cartridge and decides what each address means. `Cartridge` forwards every call and saves its RAM buffers and then the mapper's state. NROM has no state of its own, so its `Save`/`Load` write nothing (`MapperBase` default).

`Nrom` validates its image: PRG 16 or 32 KB, CHR 0 or 8 KB, else `InvalidDataException`. A header that says mapper 0 with 64 KB of PRG is a bad dump, not an NROM.

- [ ] **Step 1: Write failing tests**

`NromTests` (through `Cartridge`, built from `InesBuilder` images):

| Test | Arrange | Expected | Catches |
|---|---|---|---|
| `Nrom128_C000MirrorsC8000` | 1 PRG bank, `$5A` at offset 0, `$A5` at `$3FFF` | `CpuRead(0x8000) == CpuRead(0xC000) == 0x5A`; `CpuRead(0xBFFF) == CpuRead(0xFFFF) == 0xA5` | `- 0x8000` without the mirror; index out of range at `$C000` |
| `Nrom256_IsLinear` | 2 banks, markers `$11` at 0, `$22` at `$4000` | `CpuRead(0x8000) == 0x11`, `CpuRead(0xC000) == 0x22` | mask `0x3FFF` applied to 32 KB |
| `PrgRam_ReadsBackWrites` | write `$6000 = $12`, `$7FFF = $34` | both read back | PRG RAM missing; off-by-`$6000` |
| `PrgRom_WriteIgnored` | write `$8000 = $FF` | `CpuRead(0x8000)` unchanged, no throw | writable ROM; throwing on games that write ROM |
| `Expansion_ReadsZero_WriteIgnored` | `$4020`, `$5FFF` | read 0, write does not throw | routing `$4020-$5FFF` into PRG RAM |
| `ChrRom_Read` | CHR `$77` at offset `$1FFF` | `PpuRead(0x1FFF) == 0x77` | |
| `ChrRom_WriteIgnored` | `PpuWrite(0x0000, 0xFF)` | unchanged | writable CHR ROM corrupts graphics |
| `ChrRam_ReadWrite` | `ChrBanks = 0`; write `$0000`, `$1FFF` | read back | CHR RAM not allocated |
| `Mirroring_FromHeader` | vertical image | `Mirroring == Vertical` | |
| `IrqOutput_IsFalse` | | `false` | |
| `BadSize_Throws` | 3 PRG banks, mapper 0 | `InvalidDataException` | silently running a bad dump |

`MapperFactoryTests`:

| Test | Expected | Catches |
|---|---|---|
| `Mapper0_IsNrom` | `Create` returns `Nrom` | |
| `Mapper1_Throws` | `UnsupportedMapperException`; message contains `1` | unhelpful errors when a user loads Zelda |

`CartridgeTests`:

| Test | Arrange | Expected | Catches |
|---|---|---|---|
| `SaveLoad_RoundTripsPrgRam` | write PRG RAM, `Save`, new cartridge, `Load` | bytes equal | PRG RAM missing from state |
| `SaveLoad_RoundTripsChrRam` | CHR RAM image | same | CHR RAM missing |
| `Save_DoesNotIncludeRom` | NROM-128, CHR ROM image | saved length ≤ `0x2000 + 64` (PRG RAM plus length prefixes) | PRG or CHR ROM dumped into every save state (and the rewind ring) |
| `PrgRam_IsExposed` | write `$6000` via `CpuWrite` | `PrgRam[0]` holds it | host cannot persist battery saves |

- [ ] **Step 2: Run, confirm compile failure.**
- [ ] **Step 3: Write the types**
- [ ] **Step 4: Run, confirm green.** `dotnet test --filter "Category!=Rom"`
- [ ] **Step 5: Commit** — `feat: add Cartridge and NROM mapper`

---

### Task 4: `CpuBus`

**Files:**
- Create: `src/OddFrame.Core/Bus/CpuBus.cs`
- Create: `tests/OddFrame.Core.Tests/Bus/CpuBusTests.cs`

Routing per the memory map table above. Check ranges from low to high with `<=` on the upper bound; `$401F` is still I/O and `$4020` is cartridge. `Read` and `Peek` are identical in M2 because nothing on the bus has side effects yet. Keep them separate methods anyway: M3 makes `Read($2002)` clear vblank and `Peek($2002)` must not.

`PowerOn()` zeroes RAM (spec: determinism; real RAM is random). `Save`/`Load` write the 2 KB RAM.

- [ ] **Step 1: Write failing `CpuBusTests`**

| Test | Arrange | Expected | Catches |
|---|---|---|---|
| `Ram_IsMirroredFourTimes` | write `$0001 = $AB` | reads at `$0801`, `$1001`, `$1801` are `$AB` | mask `$0FFF` or no mirroring |
| `Ram_MirrorWritesAlias` | write `$1FFF = $CD` | `Read(0x07FF) == 0xCD` | |
| `PpuRange_ReadsZero_WriteDoesNotTouchRam` | `Write(0x2000, 0x55)`, `Write(0x3FFF, 0x55)` | `Read(0x2002) == 0`; `Read(0x0000) == 0`, `Read(0x0007) == 0` | RAM mask leaking past `$1FFF` |
| `IoRange_ReadsZero_WriteIgnored` | theory over `$4000`, `$4014`, `$4015`, `$4016`, `$4017`, `$401F` | read 0; write does not throw | throwing on the APU writes every game makes |
| `Cartridge_PrgRom` | ROM marker at `$8000` | `Read(0x8000)` returns it | wrong boundary at `$4020` or `$8000` |
| `Cartridge_PrgRam` | `Write(0x6000, 0x42)` | `Read(0x6000) == 0x42` | |
| `Peek_EqualsRead` | theory over one address per range | `Peek == Read` | `Peek` routing differently |
| `PowerOn_ZeroesRam` | write, `PowerOn()` | 0 | |
| `SaveLoad_RoundTripsRam` | write 3 addresses, save, new bus, load | equal | |

- [ ] **Step 2: Run, confirm compile failure.**
- [ ] **Step 3: Write `CpuBus`**
- [ ] **Step 4: Run, confirm green**
- [ ] **Step 5: Commit** — `feat: add CpuBus with RAM mirroring`

---

### Task 5: `Device` wiring and stepping

**Files:**
- Modify: `src/OddFrame.Core/Device.cs`
- Modify: `src/OddFrame.Cli/Program.cs` (keep it compiling: load nothing yet, print usage)
- Modify: `tests/OddFrame.Core.Tests/DeviceTests.cs`

Construction in spec order: `Cartridge(rom)`, `CpuBus(cartridge)`, `Cpu(cpuBus)`. The `Cpu` constructor already arms the reset sequence, so the constructor does not call `PowerOn()` again.

`Tick()`: `MasterClock++`; PPU line stays commented; before the CPU tick set `cpu.IrqLine = cartridge.IrqOutput` (APU joins in M7); `if (MasterClock % cpuDivider == 0) cpu.Tick()`. Cache the divider as `ulong` in the constructor; read it from `Timing`, never a literal 12.

Stepping:
- `StepCpuCycle()` exists.
- `StepInstruction()`: one `StepCpuCycle()` unconditionally, then repeat while `!cpu.IsInstructionBoundary && !cpu.Halted`. The unconditional first step matters: right after power-on, and after every instruction, `IsInstructionBoundary` is already true.
- `Run(Func<bool> stop)`: `Tick()` until `stop()` is true, checked after each tick.

`PowerOn()`: `MasterClock = 0`, `cpuBus.PowerOn()`, `cpu.PowerOn()`. PRG RAM is left alone (battery RAM survives power cycles; the host restores it). `Reset()`: `cpu.Reset()` only. RAM survives a reset on hardware.

Synthetic ROM for these tests (via `InesBuilder`): reset vector `$8000`; program at `$8000`: `E6 00` (`INC $00`, 5 cycles), `4C 00 80` (`JMP $8000`, 3 cycles).

- [ ] **Step 1: Write failing tests** (update the existing ones to pass a ROM; they keep their meaning)

| Test | Arrange | Expected | Catches |
|---|---|---|---|
| `StepInstruction_FromPowerOn_RunsResetSequence` | new device, `StepInstruction()` | `Cpu.TotalCycles == 7`, `Cpu.PC == 0x8000`, `MasterClock == 84` | reset vector not read through the cartridge; returning at once because the CPU starts at a boundary; CPU ticking on master clock 1 |
| `StepInstruction_RunsOneInstruction` | after reset, `StepInstruction()` | `TotalCycles == 12`, `CpuMemory.Peek(0x0000) == 1`, `PC == 0x8002` | stepping by cycles instead of instructions |
| `StepInstruction_OnHaltedCpu_Returns` | program `02` (JAM) | returns; `Cpu.Halted` | infinite loop on JAM |
| `Run_StopsWhenPredicateTrue` | `Run(() => device.MasterClock == 30)` | `MasterClock == 30` | predicate checked per CPU cycle instead of per tick |
| `CpuTicksEveryDividerClocks` | 24 `Tick()` calls after reset | `TotalCycles == 9` | `%` on the wrong value |
| `Reset_KeepsRam_PowerOnClearsIt` | run until `$00 == 2`; `Reset()`, step reset sequence | `$00 == 2`; then `PowerOn()`, step → `$00 == 0` | Reset clearing RAM, or PowerOn not clearing it |
| `Reset_DecrementsSByThree` | note `S` before, `Reset()`, `StepInstruction()` | `S` is 3 lower, `PC == 0x8000` | Device reset not reaching the CPU |

- [ ] **Step 2: Run, confirm compile failure.**
- [ ] **Step 3: Write the wiring**
- [ ] **Step 4: Run, confirm green.** `dotnet build` (the Cli must compile) and `dotnet test --filter "Category!=Rom"`
- [ ] **Step 5: Commit** — `feat: wire Cartridge, CpuBus and Cpu in Device`

---

### Task 6: Save state v2

**Files:**
- Modify: `src/OddFrame.Core/Device.cs`
- Create: `tests/OddFrame.Core.Tests/DeviceStateTests.cs` (move the save-state tests out of `DeviceTests`)

`StateVersion = 2`. Header: magic, version, 32-byte SHA-256 of `rom.Prg` followed by `rom.Chr` (compute once in the constructor; `SHA256.HashData` over a concatenated buffer or an incremental hash). `Save` writes `MasterClock`, then `cpu`, `cpuBus`, `cartridge` in that order; `Load` reads the same order. `LoadState` compares the stored hash with its own and throws `InvalidDataException("save state is for a different ROM")` on mismatch.

- [ ] **Step 1: Write failing `DeviceStateTests`**

| Test | Arrange | Expected | Catches |
|---|---|---|---|
| `SaveLoad_ContinuesIdentically` | device A runs 100 instructions, save; A runs 50 more, save as `expected`. Device B (same ROM) loads the first state, runs 50 | `B.SaveState()` equals `expected` | a component missing from `Save`; `MasterClock` phase lost (CPU ticks on the wrong master clock after load) |
| `LoadState_DifferentRom_Throws` | state from ROM X loaded into a device for ROM Y (one PRG byte differs) | `InvalidDataException` | hash written but never checked |
| `Header_HashIsSha256OfPrgThenChr` | | bytes 6-37 equal `SHA256.HashData([..prg, ..chr])` | hashing the file, or PRG only (CNROM games share PRG) |
| `SaveState_IsDeterministic` | two devices, same steps | equal bytes | |
| existing `RejectsWrongMagic`, `RejectsWrongVersion` (now: version 1 rejected), `Truncated_Throws`, `TrailingBytes_Throws` | | unchanged | |
| `HeaderLayout` | | magic, version 2; drop the hard-coded total length | test pinned to the M0 layout |

- [ ] **Step 2: Run, confirm failure.**
- [ ] **Step 3: Write it**
- [ ] **Step 4: Run, confirm green**
- [ ] **Step 5: Commit** — `feat: save state v2 with ROM hash`

---

### Task 7: ROM tests on the real parts

**Files:**
- Modify: `src/OddFrame.Core/Debug/NestestFormatter.cs`
- Create: `src/OddFrame.Core/Debug/NestestTraceSink.cs`
- Modify: `tests/OddFrame.Core.Tests/Debug/NestestFormatterTests.cs`
- Modify: `tests/OddFrame.Core.Tests/Fakes/BlarggStubBus.cs`, `tests/OddFrame.Core.Tests/BlarggStubBusTests.cs`
- Modify: `tests/OddFrame.Core.Tests/Roms/BlarggRunner.cs`, `NestestTests.cs`
- Create: `tests/OddFrame.Core.Tests/Roms/InstrMiscTests.cs`
- Delete: `tests/OddFrame.Core.Tests/Fakes/NestestBus.cs`, `tests/OddFrame.Core.Tests/NestestBusTests.cs`
- Modify: `tests/roms/fetch.sh`, `tests/roms/checksums.txt`

This is the integration point: the M1 ROM tests switch from RAM fakes to the real loader, cartridge, and bus.

**Formatter quirk.** One private helper used by every operand peek in `NestestFormatter`: `address is >= 0x4000 and <= 0x4017 ? 0xFF : bus.Peek(address)`. Comment it as the Nintendulator value the reference log contains.

**`NestestTraceSink(IBus bus, TextWriter output)`.** `OnInstruction` computes `scanline = (int)(TotalCycles * 3 / 341 % 262)` and `dot = (int)(TotalCycles * 3 % 341)`, formats, writes one line, increments `Count`. The formula was in `NestestTests.FormattingSink`; it moves here.

**`BlarggStubBus(IBus inner)`.** `Read` and `Peek` return `$80` for `$2002`, otherwise forward. `Write` forwards. No more internal `RamBus`.

**`BlarggRunner`.** `InesLoader.Load` → `new Cartridge(rom)` → `new CpuBus(cartridge)` → `new BlarggStubBus(cpuBus)` → `new Cpu(stub)`. It composes the parts directly because the decorator must sit between the CPU and the bus, and `Device` has no hook for that until `ObservableBus` in M5. The private iNES parser goes away. Results are read with `cpuBus.Peek`.

**`NestestTests`.** `InesLoader.Load` → `new Device(new DeviceOptions(), rom)` → `StepInstruction()` → `Cpu.PC = 0xC000` → attach `NestestTraceSink(device.CpuMemory, writer)` → `StepInstruction()` until 8991 lines or a cycle cap. Diff and the `$02`/`$03` checks stay as they are.

**New ROMs.** Add to `fetch.sh` `files` and to `checksums.txt` (hashes at the pinned commit `95d8f62`, verified 2026-11-03):

```
892e892b3b5d3526913b5491252f628d987baf44509dd28a7d109efc8a16bacc  instr_misc/rom_singles/01-abs_x_wrap.nes
2e535f572a16ab0e27bf6e076de6aaa2a9885c9ae1b02acda7853810529b24c5  instr_misc/rom_singles/02-branch_wrap.nes
```

`instr_misc/instr_misc.nes` (MMC1) stays in the list for M6.

- [ ] **Step 1: Write failing unit tests**

| Test | Arrange | Expected | Catches |
|---|---|---|---|
| `NestestFormatter_ApuIoOperand_PrintsFF` | entry `8D 15 40` (`STA $4015`), bus peeks 0 everywhere | line contains `STA $4015 = FF` | nestest lines 8981-8989 on an honest bus |
| `NestestFormatter_4018_PeeksBus` | `AD 18 40`, bus `$4018 = $12` | `= 12` | quirk range too wide |
| `NestestTraceSink_FirstLine` | entry with `TotalCycles = 7` | ends with `PPU:  0, 21 CYC:7` | formula moved wrong |
| `BlarggStubBus_2002_Is80_ElseForwards` | inner `RamBus` with `$2002 = 0`, `$0000 = $11` | `Read(0x2002) == 0x80`, `Peek(0x2002) == 0x80`, `Read(0) == 0x11`; a write reaches the inner bus | |

- [ ] **Step 2: Implement formatter, sink, decorator; unit tests green.**
- [ ] **Step 3: Switch `BlarggRunner` and `NestestTests`; delete `NestestBus`; run the ROM suite.** `tests/roms/fetch.sh && dotnet test --filter "Category=Rom"`. Expected: nestest identical; 16/16 singles. If a single fails now but passed in M1, the difference is the bus (RAM mirrors, ROM no longer writable, `$4020-$5FFF` no longer RAM). Read its text output.
- [ ] **Step 4: Add `InstrMiscTests`** (`[Theory]`, `[Trait("Category", "Rom")]`, `01-abs_x_wrap`, `02-branch_wrap`, same assertion as `InstrTestV5Tests`; a comment names why 03 and 04 wait). Expected green. `01` writes through `$FFFF,X` into RAM at `$0000`; `02` branches across `$FFFF`/`$0000`.
- [ ] **Step 5: Commit** — `test: ROM suites run on real CpuBus and NROM`

---

### Task 8: CLI `trace`

**Files:**
- Modify: `src/OddFrame.Cli/OddFrame.Cli.csproj` (package, added by the owner: `dotnet add src/OddFrame.Cli package System.CommandLine`)
- Modify: `src/OddFrame.Cli/Program.cs`
- Create: `src/OddFrame.Cli/TraceCommand.cs`

`oddframe trace <rom> --instructions <n> [--pc <hex>]`:
1. Read the file (all IO is in the Cli), `InesLoader.Load`, `new Device(...)`.
2. `StepInstruction()` for the reset sequence; if `--pc` was given, set `Cpu.PC`.
3. Attach `NestestTraceSink(device.CpuMemory, stdout)`; `StepInstruction()` until `Count == n` or `Cpu.Halted`.
4. Errors (`FileNotFoundException`, `InvalidDataException`, `UnsupportedMapperException`) → one line to stderr, exit code 1. No stack traces for user errors.

Parse `--pc` as a string and convert with `ushort.Parse(text, NumberStyles.HexNumber)`; accept an optional `$` or `0x` prefix. Wrap stdout in a `StreamWriter` with `AutoFlush = false` and flush at the end; 8991 auto-flushed `WriteLine` calls are noticeably slow.

No unit tests in Cli (no test project). The acceptance check is the diff.

- [ ] **Step 1: Write the command.**
- [ ] **Step 2: Verify**

```bash
dotnet build
dotnet run --project src/OddFrame.Cli --no-build -- trace tests/roms/other/nestest.nes --instructions 8991 --pc C000 | diff - tests/roms/other/nestest.log && echo IDENTICAL
dotnet run --project src/OddFrame.Cli --no-build -- trace missing.nes --instructions 1; echo "exit $?"     # one error line, exit 1
dotnet run --project src/OddFrame.Cli --no-build -- trace tests/roms/instr_misc/instr_misc.nes --instructions 1; echo "exit $?"   # mapper 1 unsupported, exit 1
```

- [ ] **Step 3: Commit** — `feat: add trace command to CLI`

---

### Task 9: Done check and docs

- [ ] `dotnet build` with zero warnings.
- [ ] `dotnet test --filter "Category!=Rom"` and `dotnet test --filter "Category=Rom"` green locally and in CI. `fetch.sh` changed, so the CI ROM cache key changes and the first run refetches.
- [ ] Optional CI step in the `roms` job: the `trace | diff` command from Task 8.
- [ ] Fresh-clone promise: move `tests/roms/instr_misc` aside, the ROM filter reports skips, not failures.
- [ ] `AGENTS.md` Commands: `trace` line gains `--pc C000`.
- [ ] README: status line for M2; `trace` usage.
- [ ] Spec: confirm the amendments below match what was built.
- [ ] Open the PR from the implementation branch to `master`.

---

## Deliberately deferred

- PPU: `$2000-$3FFF` routing, `PpuBus`, `StepFrame`, `StepScanline`, the `run` and `test` CLI commands, and real PPU coordinates in the trace. M3.
- `cpu_timing_test6` (PPU NMI; result read from the nametable) and `instr_misc` `03-dummy_reads` (`$2002` read side effect). M3.
- CPU/PPU phase in the trace. With the PPU ticking at master clocks 4, 8, 12 and the CPU at 12, 24, a sink reading the live PPU at the 8th CPU tick sees 23 or 24 dots, not nestest's 21. Decide the phase in M3 against `ppu_vbl_nmi`; keep the cycle formula until then.
- OAM DMA (`$4014`) and controllers (`$4016`/`$4017`). M4.
- `ObservableBus` and `DeviceOptions.EnableDebug`. M5.
- `MapperBase` bank helpers. M6.
- APU registers and `instr_misc` `04-dummy_reads_apu`. M7. The spec does not yet say which milestone gets the APU frame counter; the `cpu_interrupts_v2` singles and the NMI hijack check need it.
- Open bus. Unmapped reads return 0 (spec section 4).

## Spec amendments this plan needs (approved and applied 2026-11-03)

1. **Section 8, milestones 2 and 3.** M2 exits on nestest through `oddframe trace` and the real bus, plus the 16 `instr_test-v5` singles and `instr_misc` 01-02 on the real `CpuBus` and NROM. `cpu_timing_test6` moves to M3.
2. **Section 2, CPU.** The NMI hijack window is checked against the `cpu_interrupts_v2` singles once PPU NMI, OAM DMA, and the APU frame IRQ exist, not in milestone 2.
3. **Section 4, Loader.** `MapperFactory.Create` throws `UnsupportedMapperException`; the loader accepts any mapper number. Detection of NES 2.0 and archaic headers spelled out. NROM always has PRG RAM.
4. **Section 3, Components and Construction.** `Device(DeviceOptions, RomImage)`; `Device` exposes `Cpu`, `Cartridge`, and `CpuMemory`.
5. **Section 5, Trace log.** The nestest `FF` quirk for `$4000-$4017` lives in `NestestFormatter`; nestest starts by setting PC to `$C000` after the reset sequence.
6. **Section 7, Layer 2.** `BlarggRunner` runs on the real `CpuBus` and `Cartridge` behind a `$2002` decorator until M3. `instr_misc` and `cpu_interrupts_v2` use their singles. `cpu_timing_test6` has its own row: no `$6000` protocol, result read from the screen.

## Order summary

| Task | Deliverable | Signal |
|---|---|---|
| 1 | `InesBuilder` | unit |
| 2 | `InesLoader` | unit |
| 3 | `Cartridge`, NROM | unit |
| 4 | `CpuBus` | unit |
| 5 | `Device` wiring | unit, Cli compiles |
| 6 | save state v2 | unit |
| 7 | ROM tests on real parts | nestest 8991 identical, 16 + 2 singles |
| 8 | `trace` CLI | `diff` empty |
| 9 | docs, CI, PR | green |
