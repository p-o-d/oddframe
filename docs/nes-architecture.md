# NES hardware architecture, as it maps to OddFrame

A compact hardware reference for the components OddFrame emulates. For the emulator's own structure see `superpowers/specs/2026-10-10-nes-emulator-architecture-design.md`. All numbers are NTSC unless stated.

## 1. System overview

The NES has two chips doing almost all the work, plus memory and a cartridge:

| Chip | Contains | OddFrame component |
|---|---|---|
| Ricoh 2A03 | MOS 6502 core (no decimal mode), APU (audio), OAM DMA unit, controller port latches | `Cpu`, `Apu`, part of `CpuBus` |
| Ricoh 2C02 (PPU) | Picture generation, 256 bytes OAM, 32 bytes palette RAM | `Ppu` |
| 2 KB work RAM | CPU scratch space at `$0000-$07FF` | `CpuBus` |
| 2 KB video RAM | Nametables at `$2000-$27FF` | `PpuBus` |
| Cartridge | PRG ROM (code), CHR ROM or RAM (graphics), optional PRG RAM, mapper chip | `Cartridge`, `IMapper` |
| Controllers | Two shift-register joypads | `StandardController` |

There are two completely separate address buses. The CPU bus is 16-bit and the CPU is the only master on it. The PPU bus is 14-bit and the PPU is the only master on it. The CPU reaches PPU memory only indirectly, through the PPU's registers at `$2006`/`$2007`. The cartridge sits on both buses.

### Clocks

One crystal at 21.477272 MHz. The CPU runs at 1/12 of it (1.789773 MHz), the PPU at 1/4 (5.369318 MHz). So the PPU does exactly 3 dots per CPU cycle. One frame is 262 scanlines × 341 dots = 89 342 dots ≈ 29 780.5 CPU cycles ≈ 60.0988 frames per second.

## 2. CPU: 6502 core

### Registers

| Register | Width | Role |
|---|---|---|
| A | 8 | Accumulator, all arithmetic and logic |
| X, Y | 8 | Index registers, loop counters, addressing offsets |
| S | 8 | Stack pointer; stack lives in page 1, `$0100 + S`, grows downward |
| PC | 16 | Program counter |
| P | 8 | Status flags |

Flags in P, bit 7 to bit 0: N (negative), V (overflow), unused (reads as 1), B (break, only exists on the stack copy), D (decimal; the 2A03 ignores it but the flag bit still exists), I (IRQ disable), Z (zero), C (carry).

### Memory map (CPU side)

| Range | What |
|---|---|
| `$0000-$00FF` | Zero page. Faster addressing modes use it. |
| `$0100-$01FF` | Stack. |
| `$0200-$07FF` | Rest of the 2 KB RAM. |
| `$0800-$1FFF` | Mirrors of `$0000-$07FF` (address lines A11-A12 are not decoded). |
| `$2000-$2007` | PPU registers. |
| `$2008-$3FFF` | Mirrors of the 8 PPU registers every 8 bytes. |
| `$4000-$4017` | APU and I/O registers. |
| `$4018-$401F` | Normally disabled test registers. |
| `$4020-$FFFF` | Cartridge space. Usually PRG RAM at `$6000-$7FFF`, PRG ROM at `$8000-$FFFF`. |

Three vectors at the top of memory tell the CPU where to jump: NMI at `$FFFA-$FFFB`, reset at `$FFFC-$FFFD`, IRQ/BRK at `$FFFE-$FFFF`. All 16-bit values are little-endian.

### Instruction execution

The 6502 has no instruction cache or pipeline. Every cycle is exactly one bus access: a read or a write. There are no idle cycles; when the CPU has nothing useful to do it performs a dummy read. This is what makes cycle-stepping natural: `Cpu.Tick()` is "do this cycle's bus access".

An instruction takes 2 to 7 cycles (8 for some unofficial ones):

1. Cycle 1 always fetches the opcode at PC.
2. Cycles 2..n fetch operand bytes, compute the effective address, and perform the operation.

Addressing modes decide the middle cycles:

| Mode | Syntax | Bytes | Notes |
|---|---|---|---|
| Implied / accumulator | `CLC`, `ASL A` | 1 | Second cycle is a dummy read of PC+1. |
| Immediate | `LDA #$10` | 2 | |
| Zero page | `LDA $10` | 2 | |
| Zero page,X / ,Y | `LDA $10,X` | 2 | Wraps within page 0. |
| Absolute | `LDA $1234` | 3 | |
| Absolute,X / ,Y | `LDA $1234,X` | 3 | Reads: +1 cycle only if page crossed, and a dummy read at the wrong address happens first. Writes and read-modify-write: always the extra cycle. |
| Indirect | `JMP ($1234)` | 3 | Bug: if the pointer is at `$xxFF`, the high byte is read from `$xx00`, not the next page. |
| (Indirect,X) | `LDA ($10,X)` | 2 | Pointer in zero page, wraps within it. |
| (Indirect),Y | `LDA ($10),Y` | 2 | Same page-cross rule as absolute,Y. |
| Relative | `BNE label` | 2 | Branch: 2 cycles not taken, 3 taken, 4 taken across a page. |

Read-modify-write instructions (`INC`, `DEC`, `ASL`, `LSR`, `ROL`, `ROR` on memory, plus unofficial ones) write the original value back first, then the modified value. That double write is observable by mappers and PPU registers, so emulate it.

Dummy reads matter too. Reading `$2002` clears the vblank flag; a dummy read of it during an indexed access has the same effect. Several mappers latch on any access, so dummy accesses must hit the bus.

### Unofficial opcodes

105 of the 256 opcodes are undocumented. Most are combinations (`LAX` = `LDA` + `LDX`, `SAX`, `DCP`, `ISC`, `SLO`, `RLA`, `SRE`, `RRA`), some are unstable (`XAA`, `AHX`, `TAS`, `LAS`), and 12 are `KIL`/`JAM` that halt the CPU. nestest covers the stable ones. A few commercial games use them, so implement them; they share the same addressing-mode cycle structure as official ones.

### Interrupts

Three sources: reset, NMI (from PPU vblank), IRQ (from APU frame counter, APU DMC, or mapper). BRK is a software interrupt sharing the IRQ vector with the B flag set on the pushed status.

- NMI is edge-triggered on the falling edge of the NMI line. It cannot be masked by the I flag.
- IRQ is level-triggered. Taken only when I is clear, and stays pending as long as the line is held low.
- Both are sampled at the end of the second-to-last cycle of every instruction. If asserted, the next "instruction" is the 7-cycle interrupt sequence: two dummy reads, push PCH, push PCL, push P, fetch vector low, fetch vector high. I is set during this.
- A branch that is not taken has one fewer cycle, which changes when the sampling point falls. The exact behaviour is in `cpu_interrupts_v2` and matters for games that rely on NMI timing.
- Reset runs the same sequence but with writes suppressed (S still decrements by 3) and reads the reset vector.

### OAM DMA

Writing a page number to `$4014` halts the CPU and copies 256 bytes from `$XX00-$XXFF` to PPU OAM via `$2004`. It takes 513 CPU cycles (plus 1 if the write landed on an odd cycle): one wait cycle, then 256 pairs of read-then-write. During this the CPU performs no instruction cycles. Almost every game does this once per frame during vblank.

## 3. PPU: 2C02

The PPU produces a 256×240 image, one pixel per dot, by combining a background layer of 8×8 tiles with up to 64 sprites. It has its own memory space and its own clock. The CPU programs it only through 8 registers.

### Registers (`$2000-$2007`)

| Address | Name | Direction | Purpose |
|---|---|---|---|
| `$2000` | PPUCTRL | W | NMI enable (bit 7), sprite size 8×8/8×16 (bit 5), background pattern table (bit 4), sprite pattern table (bit 3), VRAM address increment 1 or 32 (bit 2), base nametable (bits 0-1). |
| `$2001` | PPUMASK | W | Colour emphasis (bits 5-7), show sprites (4), show background (3), show sprites in leftmost 8 px (2), show background in leftmost 8 px (1), greyscale (0). Rendering is "enabled" when bit 3 or 4 is set. |
| `$2002` | PPUSTATUS | R | Vblank started (bit 7), sprite 0 hit (6), sprite overflow (5). Reading clears bit 7 and resets the `$2005`/`$2006` write latch. |
| `$2003` | OAMADDR | W | OAM address for `$2004`. |
| `$2004` | OAMDATA | R/W | Read or write OAM byte at OAMADDR; writes increment OAMADDR. |
| `$2005` | PPUSCROLL | W×2 | First write X scroll, second write Y scroll. |
| `$2006` | PPUADDR | W×2 | First write high byte, second write low byte of the VRAM address. |
| `$2007` | PPUDATA | R/W | Read or write VRAM at the current address, then increment by 1 or 32. Reads are buffered: you get the previous read's value, except palette reads which return directly. |

`$2005` and `$2006` share one write-toggle latch (call it `w`). They also write into the same internal address registers that rendering uses, which is why scrolling and VRAM access interfere with each other. This is the "loopy" model:

- `v`: current 15-bit VRAM address. Used both for `$2007` access and, during rendering, as the position being fetched.
- `t`: temporary VRAM address. `$2000`, `$2005`, `$2006` write into it.
- `x`: 3-bit fine X scroll.
- `w`: write toggle.

At dot 257 of every rendering scanline the horizontal bits of `t` are copied into `v`; at dots 280-304 of the pre-render line the vertical bits are copied. Games that split the screen write `$2005`/`$2006` mid-frame to change `t` and therefore where the next copy lands. Get this exactly right or every scrolling game breaks.

### PPU memory map

| Range | Content |
|---|---|
| `$0000-$0FFF` | Pattern table 0: 256 tiles × 16 bytes. Usually CHR ROM on the cartridge. |
| `$1000-$1FFF` | Pattern table 1. |
| `$2000-$23FF` | Nametable 0: 32×30 tile indices (960 bytes) + 64-byte attribute table. |
| `$2400-$27FF` | Nametable 1. |
| `$2800-$2BFF` | Nametable 2. |
| `$2C00-$2FFF` | Nametable 3. |
| `$3000-$3EFF` | Mirror of `$2000-$2EFF`. |
| `$3F00-$3F1F` | Palette RAM: 16 background + 16 sprite entries. `$3F10/14/18/1C` mirror `$3F00/04/08/0C`. |
| `$3F20-$3FFF` | Mirrors of palette RAM. |

There is only 2 KB of VRAM for 4 KB of nametable space, so two nametables are mirrors of the other two. Horizontal mirroring (`$2000`=`$2400`, `$2800`=`$2C00`) gives vertical scrolling; vertical mirroring gives horizontal scrolling. The cartridge chooses, and some mappers switch it at runtime. Four-screen cartridges add their own 2 KB.

### Tiles, patterns, attributes, palettes

- A tile is 8×8 pixels, 2 bits per pixel, stored as two 8-byte bit planes: 16 bytes. Bit plane 0 gives the low bit of each pixel, plane 1 the high bit. Pixel value 0 is transparent.
- A nametable byte is a tile index into the selected pattern table.
- The attribute table gives 2 more bits per 16×16 pixel block (4 tiles): the palette number. Each attribute byte covers a 32×32 area, 2 bits per quadrant.
- Final colour index = palette number (2 bits) × 4 + pixel value (2 bits), looked up in palette RAM, giving a 6-bit NES colour (64 colours, 54 distinct). Core converts that to RGB with a fixed table.
- Background colour (index 0 of every palette) is shared: `$3F00`.

### Frame structure

262 scanlines of 341 dots each:

| Scanlines | Name | What happens |
|---|---|---|
| -1 (261) | Pre-render | Same fetches as a visible line, nothing drawn. Clears vblank, sprite 0, overflow flags at dot 1. Copies vertical scroll from `t` to `v` at dots 280-304. On odd frames, when rendering is enabled, the last dot is skipped (frame is one dot shorter). |
| 0-239 | Visible | Renders 256 pixels at dots 1-256. Fetches for the next line's sprites at 257-320. Fetches the first two tiles of the next line at 321-336. |
| 240 | Post-render | Idle. |
| 241-260 | Vblank | Vblank flag set at dot 1 of 241; NMI fires if enabled. The CPU uploads graphics now. |

### Background rendering pipeline

During each visible scanline the PPU repeats an 8-dot cycle for each of 32 tiles plus 2 prefetched for the next line. Each memory fetch takes 2 dots:

1. Dots 1-2: nametable byte at `v`.
2. Dots 3-4: attribute byte.
3. Dots 5-6: pattern low plane.
4. Dots 7-8: pattern high plane. Then `v` horizontal increment (coarse X, with nametable wrap).

Fetched bytes feed two 16-bit shift registers (one per bit plane) plus attribute latches. Every dot the pixel at bit `15 - x` is pulled from the shift registers and they shift left. This is why fine X scroll is free and why the PPU prefetches two tiles ahead. At dot 256 `v` is incremented vertically (fine Y, then coarse Y, then nametable flip at row 29). At dot 257 horizontal `t` → `v`.

### Sprites

OAM holds 64 sprites × 4 bytes: Y position (minus 1), tile index, attributes (palette bits 0-1, priority bit 5, flip H bit 6, flip V bit 7), X position.

Each visible scanline does two things for the *next* line:

1. **Sprite evaluation**, dots 65-256: scan all 64 OAM entries, copy the first 8 that are in range of the next scanline into secondary OAM. If a ninth is found, set the overflow flag. The real hardware has a bug in the overflow check that scans with a diagonal pattern after 8 are found; `sprite_overflow_tests` checks for this bug.
2. **Sprite fetches**, dots 257-320: for each of the 8 secondary OAM entries, fetch the pattern bytes (with vertical flip and 8×16 handled here) into 8 pairs of shift registers with X counters.

During the line, each sprite's X counter counts down; when it hits 0 the sprite starts shifting out pixels. Where a non-zero sprite pixel and a non-zero background pixel overlap and the sprite is index 0, the sprite 0 hit flag is set. Games poll this flag to time mid-frame scroll changes (the SMB status bar). Priority bit decides whether the sprite draws in front of or behind the background; sprites earlier in OAM win among themselves regardless of priority, which produces the "behind background through a hole" effect.

Sprites on scanline 0 are not visible because OAM Y is the line *above* the sprite.

### Odd/even frames

When rendering is enabled, odd frames skip dot 340 of the pre-render line. Over time this shifts CPU/PPU alignment by one dot each frame. Several timing tests depend on it.

## 4. APU: audio in the 2A03

Five channels, mixed non-linearly, output as one analogue signal:

| Channel | Registers | Sound |
|---|---|---|
| Pulse 1 | `$4000-$4003` | Square wave, 4 duty cycles, volume envelope, sweep, length counter. |
| Pulse 2 | `$4004-$4007` | Same. |
| Triangle | `$4008-$400B` | 32-step triangle, linear counter instead of envelope. |
| Noise | `$400C-$400F` | LFSR noise, 16 periods, two modes (long/short). |
| DMC | `$4010-$4013` | 1-bit delta samples fetched from CPU memory by its own DMA, which steals CPU cycles. |

`$4015` is status (read: which length counters are non-zero, frame IRQ, DMC IRQ) and channel enable (write). `$4017` sets the **frame counter**: a sequencer clocked every ~7457 CPU cycles that drives envelopes, length counters, and sweeps in either a 4-step (with IRQ) or 5-step mode. Writing `$4017` resets the sequencer with a 3-4 cycle delay. Many games rely on the frame IRQ for timing, which is why the frame counter is in the first iteration even without sound.

Length counters and the frame sequencer must be cycle-accurate for `apu_test`. Channel waveform generation is much more forgiving. The mixer formula is on the NESdev wiki; linear approximation is fine at first.

## 5. Cartridge and mappers

The cartridge is not passive storage. The 6502 can only address 32 KB of ROM at `$8000-$FFFF` and the PPU only 8 KB of CHR, so larger games include a **mapper** chip that swaps banks into those windows when the CPU writes to ROM addresses (writes to ROM do nothing to the ROM itself, so the mapper uses them as register writes).

- **PRG ROM**: program code and data, in 16 KB units. Lives on the CPU bus.
- **CHR ROM**: tile graphics, in 8 KB units. Lives on the PPU bus. Some carts have CHR RAM instead, and the game draws tiles at runtime.
- **PRG RAM**: optional 8 KB at `$6000-$7FFF`, optionally battery-backed for saves. Test ROMs use it for results.
- **Mirroring**: the cartridge wires the PPU's A10 or A11 to decide nametable mirroring; mappers like MMC1 and MMC3 let software change this.

Mapper responsibilities in order of complexity: fixed layout (NROM), one bank register (UxROM for PRG, CNROM for CHR), serial 5-write register with multiple modes (MMC1), many registers plus a scanline counter that watches the PPU's A12 line to raise IRQs (MMC3). The A12 trick works because the PPU fetches background from one pattern table and sprites from the other, so A12 toggles at a predictable dot each scanline. This is why MMC3 is last: its IRQ only works when PPU fetch timing is right.

### iNES file format

16-byte header: `NES\x1A`, PRG size in 16 KB units, CHR size in 8 KB units, flags 6 (mirroring, battery, trainer, four-screen, low mapper nibble), flags 7 (high mapper nibble, NES 2.0 marker), then optional 512-byte trainer, then PRG, then CHR. NES 2.0 extends sizes and adds submapper and RAM size fields.

## 6. Controllers

Each standard controller is a 4021 8-bit shift register. Writing 1 then 0 to `$4016` strobes: the register latches all 8 button states. Each subsequent read of `$4016` (or `$4017` for port 2) returns one button in bit 0, in order A, B, Select, Start, Up, Down, Left, Right, then 1s forever. While strobe is held high, reads always return A. Upper bits of the read are open bus (typically `$40` because of the address), which some games rely on and some test ROMs check.

## 7. How a frame flows through OddFrame

1. Host calls `Device.SetController(0, buttons)`.
2. Host calls `Device.StepFrame()`, which loops `Tick()`.
3. Each `Tick()` increments the master clock, ticks the PPU every 4 clocks and the CPU (and APU) every 12, copies `ppu.NmiOutput` to `cpu.Nmi`, and ORs the IRQ sources into `cpu.IrqLine`.
4. The CPU runs the game's NMI handler during vblank: reads `$2002`, writes OAM via `$4014`, updates nametables via `$2006`/`$2007`, sets scroll via `$2005`, writes `$2000`/`$2001`.
5. The PPU renders 240 lines into `Framebuffer`, reading pattern and nametable bytes through `PpuBus`, which the cartridge mapper may bank.
6. The PPU sets `FrameComplete` on the last dot of the frame; `StepFrame()` returns.
7. Host copies `Framebuffer` to the screen and sleeps until the next 60.0988 Hz tick.

## 8. Where to read more

- CPU: https://www.nesdev.org/wiki/CPU, cycle tables https://www.nesdev.org/6502_cpu.txt, unofficial opcodes https://www.nesdev.org/wiki/CPU_unofficial_opcodes, interrupts https://www.nesdev.org/wiki/CPU_interrupts
- PPU: https://www.nesdev.org/wiki/PPU_rendering, scrolling https://www.nesdev.org/wiki/PPU_scrolling, sprite evaluation https://www.nesdev.org/wiki/PPU_sprite_evaluation, registers https://www.nesdev.org/wiki/PPU_registers, frame timing https://www.nesdev.org/wiki/PPU_frame_timing
- APU: https://www.nesdev.org/wiki/APU, frame counter https://www.nesdev.org/wiki/APU_Frame_Counter, mixer https://www.nesdev.org/wiki/APU_Mixer
- Mappers: https://www.nesdev.org/wiki/Mapper, NROM, UxROM, CNROM, MMC1, MMC3 pages under it; iNES https://www.nesdev.org/wiki/INES
- Controllers: https://www.nesdev.org/wiki/Standard_controller
