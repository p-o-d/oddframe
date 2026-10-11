# The NES from scratch: what each part is for and why it works that way

A beginner's walk through the NES hardware, written for someone who knows software but has never looked inside a computer. Read this before `nes-architecture.md`, which is the dense reference.

## 0. How any 1980s computer works

### The clock

A chip is a web of transistors. On its own it does nothing. A **clock** signal, a square wave going high-low-high-low millions of times a second, tells every part of the chip "now advance one step". Everything that happens inside a chip happens on a clock edge. When we say "the CPU takes 3 cycles", we mean the clock went high three times and the CPU did one small thing on each.

The NES has one crystal oscillating at 21.477272 MHz. Different chips divide that down: the CPU takes every 12th pulse, the PPU every 4th. That is why the PPU does exactly three things for every one the CPU does. The emulator's `Device.Tick()` is that crystal.

### The bus

Chips talk through shared wires called a **bus**. The CPU's bus is:

- 16 **address** wires. The CPU puts a number 0-65535 on them, meaning "I want to talk to location N".
- 8 **data** wires. One byte travels here, either direction.
- A **read/write** wire. 1 means "someone, put the byte at N on the data wires", 0 means "someone, take this byte and store it at N".

That is the whole protocol. Every cycle the CPU drives the address and read/write wires, and either reads or writes the data wires. There is no "idle" state in the 6502; it always does one of the two.

### Memory-mapped everything

Here is the single most important idea for understanding the NES. The address wires go to every chip. Each chip has a small circuit that watches the address and decides "is this one for me?". RAM answers for `$0000-$07FF`. The PPU answers for `$2000-$3FFF`. The cartridge answers for `$8000-$FFFF`.

So from the CPU's point of view, writing a byte to RAM and writing a byte to the PPU look identical: put an address on the wires, put a byte on the wires, pulse write. The CPU does not know or care that `$2000` is not memory. The PPU just happens to be listening there, and when it sees a write it changes its behaviour instead of storing the byte.

A location like that, where a chip listens and reacts rather than stores, is called a **register**. "PPU register `$2001`" means "the PPU's control circuit that reacts when address `$2001` is written". In the emulator, this is why the bus is `IBus.Read/Write` dispatching by address range, and why `Ppu.WriteRegister` exists.

### Why mirrors exist

The NES has 2 KB of RAM, which needs 11 address bits. The CPU puts out 16. Checking all 16 bits to decide "is this for RAM?" needs extra logic gates, and gates cost money in 1983. So the designers checked only the top 3 bits (`$0000-$1FFF` means "RAM's range") and let the RAM chip see only the low 11. Result: `$0800` and `$0000` hit the same RAM cell. Not a feature, a cost saving. Same reason PPU registers repeat every 8 bytes up to `$3FFF`.

## 1. The CPU: a very fast, very simple clerk

### What it does

The CPU does one thing forever: read the byte at address PC, treat it as an instruction, do what it says, move PC forward. That is the **fetch-decode-execute loop**. Each instruction is tiny: "load a byte from here into A", "add A and this", "if the result was zero, jump there". A game is a few hundred thousand of these.

### Why registers

Memory is slow and far away (a whole bus cycle per byte). Registers are a handful of 8-bit storage cells inside the CPU itself, reachable in the same cycle. The 6502 has very few: A for arithmetic, X and Y for counting and offsetting, S for the stack, PC for where we are, P for flags. Every program is written as "pull from memory into a register, work on it, push back".

### Why flags

After almost every operation the CPU records a few facts about the result: was it zero (Z), negative (N), did it overflow 8 bits (C for carry, V for signed overflow). Branch instructions look only at these flags. "If equal" is really "if the last subtraction set Z". The flags are the CPU's short-term memory of what just happened.

### Why a stack

Subroutines need to know where to return to. The stack is a region of RAM (`$0100-$01FF`) with a pointer S. `JSR` pushes the return address and jumps; `RTS` pops it and jumps back. Interrupts use the same mechanism to save where they interrupted. One page, 256 bytes, because that was enough and more pointer bits cost gates.

### Why cycles and dummy accesses

The 6502 is not a program running on something else; it is a fixed sequence of hardware states. An instruction like "load from `$1234,X`" goes: cycle 1 fetch opcode, cycle 2 fetch low address byte, cycle 3 fetch high byte and add X to the low byte, cycle 4 read from the address. If adding X carried into the high byte, the address read in cycle 4 was wrong, so cycle 5 reads the right one. The hardware cannot "skip" cycle 4 once it is in that state, so it does the wrong read anyway, discards it, and that is a **dummy read**.

Why does the emulator care? Because dummy reads go on the bus. If the wrong address happens to be `$2002`, the PPU sees a read and clears the vblank flag. Real games trip on this; so do the test ROMs. Cycle-stepping the CPU reproduces these naturally, which is why the design chose it.

### Why interrupts

A game needs to react to "the screen just finished drawing" once every frame. Option one: sit in a loop reading `$2002` until the vblank bit appears. That wastes all the CPU time between frames. Option two: a dedicated wire into the CPU that, when pulled low, makes the CPU finish its current instruction, save PC and flags on the stack, and jump to a handler address stored at a fixed place (`$FFFA`). That is an **interrupt**, and the game code can do useful work until it fires.

There are two such wires because two kinds of urgency exist:

- **NMI** (non-maskable). Cannot be ignored. The PPU drives this every vblank. Missing it means missing the only safe window to update graphics, so the hardware refuses to let software block it. It is **edge-triggered**: the CPU reacts to the line going from high to low, once, so a long-held line does not re-trigger.
- **IRQ** (interrupt request). Can be disabled by the I flag. The APU and cartridge mappers use it for less critical timing. It is **level-triggered**: as long as the line is low and I is clear, the CPU keeps taking the interrupt, so the handler must tell the source to release the line.

Both are checked at a fixed point near the end of each instruction. That timing is observable and games depend on it, which is why `cpu_interrupts_v2` exists.

### Why OAM DMA exists

Sprite positions live in the PPU's 256-byte OAM. Updating all 64 sprites through the `$2004` register one byte at a time would take about 1500 CPU cycles, almost the whole vblank. So the 2A03 has a tiny dedicated copier: write a page number to `$4014` and it halts the CPU and copies 256 bytes itself, one read and one write per byte, 513 cycles. The CPU is frozen because the copier is using the CPU's bus. This is the first thing almost every game does in its NMI handler.

### Why unofficial opcodes

The 6502 decodes instructions with a grid of logic, not a lookup table. Opcodes the designers never intended still land somewhere in that grid and do *something*, usually two operations at once. Nobody documented them, but they are deterministic, and a few games (and all the test ROMs) use them. The emulator must implement them because they are just rows in the same decode table.

## 2. RAM: 2 KB and why that was enough

Two kilobytes for the whole game state. In 1983 RAM was the single most expensive component. Games were designed around it: a few hundred bytes for the hero and enemies, a few hundred for the level, the stack, and scratch space. Any larger storage lives on the cartridge as ROM, which is cheap because it is manufactured with the data already in it.

## 3. The PPU: a chip that draws so the CPU does not have to

### Why a separate graphics chip at all

A 256×240 screen at 60 Hz is 3.7 million pixels a second. The CPU runs at 1.79 million cycles a second. It cannot even look at each pixel, let alone compute it. So a second chip, the PPU, generates the picture by itself from a compact description, and the CPU only edits the description.

### Why the PPU has its own bus and memory

If the PPU had to share the CPU's bus, every pixel fetch would steal a CPU cycle. Instead it gets its own 14-bit address bus and its own 2 KB of VRAM, plus access to the cartridge's graphics ROM. The CPU reaches PPU memory only indirectly: it writes an address to `$2006` and then bytes to `$2007`, and the PPU does the actual VRAM access on the CPU's behalf, when it is not busy.

"When it is not busy" is the catch. During the 240 visible lines the PPU is using its bus every single dot to fetch tiles. Writing `$2007` then corrupts the fetch and glitches the screen. The only safe time is **vblank**, the 20 scanlines after the picture where the PPU is idle. This is why games cram all graphics updates into the NMI handler and why vblank is the heartbeat of every NES game.

### Why vblank exists in the first place

A CRT television draws by sweeping an electron beam left to right, top to bottom. After the bottom line, the beam needs time to travel back to the top. The signal has to keep going during that travel, so the TV standard includes about 20 lines' worth of "nothing" at the end of each frame. The PPU has nothing to draw then. Hardware idle time became the software's update window.

### Why tiles instead of pixels

Storing the screen as pixels, even at 2 bits per pixel, is 256 × 240 × 2 / 8 = 15 360 bytes. The whole NES has 2 KB of VRAM. So the screen is not stored as pixels. It is stored as a 32×30 grid of **tile numbers**, 960 bytes, and the tiles themselves (8×8 pixel patterns) live in the cartridge's graphics ROM. A brick wall is one 16-byte tile and a few hundred one-byte references to it. This is why NES games look the way they do: everything snaps to a grid and repeats.

Three memory regions follow from this:

- **Pattern tables** (`$0000-$1FFF`, on the cartridge). The 8×8 tile artwork. 16 bytes each: two 8-byte bit planes, so each pixel gets 2 bits, a value 0-3.
- **Nametables** (`$2000-$2FFF`, in VRAM). The grid of which tile goes where. Called "name" tables because a tile number is the tile's name.
- **Attribute tables** (the last 64 bytes of each nametable). Two more bits per 16×16 block choosing which of 4 palettes that block uses.

### Why palettes

Two bits per pixel is 4 values, and value 0 means transparent, so a tile has 3 real colours. That is too few for a whole game. Palettes fix it cheaply: the 2 pixel bits plus 2 attribute bits form a 4-bit index into a 32-byte **palette RAM**, which holds the actual colour numbers. Change one palette byte and every tile using it changes colour at once (this is how the NES does cheap flashing and the Mario/Luigi colour swap). The 64 colours the PPU can output are fixed in silicon; the palette RAM just picks which 25 are in use.

### Why the PPU fetches ahead and uses shift registers

The PPU must emit one pixel every dot, no exceptions, because the TV beam does not wait. But fetching a tile takes 4 memory reads (nametable, attribute, pattern low, pattern high), 2 dots each, 8 dots total, exactly the width of one tile. So the PPU runs a pipeline: while it outputs tile N's 8 pixels from a pair of **shift registers**, it fetches tile N+1 behind them. Each dot it shifts one bit out of each register. The two prefetched tiles at the end of each line are so the pipeline is already full when the next line begins.

The scroll "fine X" value (0-7) is just which bit of the shift register is read as the current pixel. That is why horizontal scrolling by a pixel costs nothing: the PPU reads bit 7-x instead of bit 7.

### Why scrolling works through strange internal registers

Scrolling means "start drawing the grid from tile (X, Y) instead of (0, 0)". The PPU's fetch logic has a current address register `v` that says which nametable byte to fetch next. Scrolling is simply setting `v` to a different start before each line. The `$2005` scroll register writes the desired start into a temporary `t`; at the start of each line the PPU copies the horizontal part of `t` into `v`, and once per frame the vertical part. Games that split the screen (a fixed status bar above a scrolling world) change `t` mid-frame so the next copy lands differently.

Why so convoluted? Because the designers reused the same address register for `$2006` VRAM writes and for rendering, to save transistors. The `v`/`t`/`x`/`w` model on the NESdev wiki is the exact description of those shared registers. It is the hardest part of the PPU to get right and the one every scrolling game depends on.

### Why sprites are separate and why only 8 per line

Backgrounds snap to the grid. Characters need to move by one pixel anywhere. So the PPU has a second system: 64 **sprites**, each an 8×8 or 8×16 tile at an arbitrary X, Y, stored in a 256-byte memory called **OAM** (object attribute memory) inside the PPU.

Drawing a sprite needs its own shift register pair and X counter. The 2C02 has 8 such units. So each scanline, during the visible part, the PPU scans OAM and picks the first 8 sprites that will appear on the *next* line (sprite evaluation), then during the right-hand idle part fetches their patterns into the 8 units. On the next line the units count down X and shift out pixels when they reach zero. A ninth sprite on a line is dropped, which is the famous flicker in crowded games. The overflow flag that reports this has a hardware bug in how it scans; test ROMs check for the bug.

### Why sprite 0 hit exists

The PPU has no "tell me when you reach scanline N" interrupt. But games need to know when the beam passes a certain line to change scroll for a split screen. The cheap trick the designers provided: a flag that sets the moment any opaque pixel of sprite number 0 overlaps an opaque background pixel. Put sprite 0 at the split line, poll `$2002` until the flag appears, then write the new scroll. Super Mario Bros does exactly this for its status bar. Getting the flag's timing right to the dot is what `sprite_hit_tests` checks.

### Why odd frames are one dot shorter

An NTSC colour signal has a phase relationship between the colour carrier and the scanline rate. To keep colours stable, the PPU alternates frame lengths by one dot. This is invisible to the player but shifts the CPU/PPU alignment every frame, and timing-sensitive tests notice.

## 4. The APU: sound as five tiny machines

### Why no samples

Digital audio as we know it (streams of samples) needs memory and bandwidth the NES does not have. Instead the APU contains five **generators**, each a small circuit that produces one kind of waveform forever when told its pitch and volume: two square waves, a triangle wave, a noise source, and one channel that does play crude 1-bit samples from ROM. Music is the CPU writing new pitch and volume values a few times a frame. The sound of the NES is the sound of these five circuits.

### Why the frame counter matters even without sound

The APU includes a slow timer, the **frame counter**, ticking about 240 times a second. It automatically fades volumes (envelopes), bends pitch (sweeps), and stops notes (length counters) so the CPU does not have to. In one mode it also raises an IRQ. Games use that IRQ as a general-purpose timer, and some wait for it to proceed. Without the frame counter they hang. That is why the emulator implements it in the first iteration while skipping the waveforms.

### Why the APU is inside the CPU chip

Cost. Nintendo had one custom chip budget for "CPU plus extras". The 6502 core, the audio generators, the OAM DMA copier, and the controller latches all went into the same package, the 2A03.

## 5. The cartridge and mappers: how 32 KB of address space holds 512 KB of game

### Why ROM

ROM is memory manufactured with its contents fixed. It is much cheaper per byte than RAM, needs no power to keep data, and cannot be corrupted by a crash. A game ships as ROM chips on a board: **PRG ROM** for code and data on the CPU bus, **CHR ROM** for tile artwork on the PPU bus.

### Why banking

The CPU has 16 address bits, so 64 KB total, and after RAM and registers only `$8000-$FFFF` (32 KB) is left for program ROM. The PPU has room for 8 KB of tiles. Early games fit (NROM: no banking, Super Mario Bros is 32 KB + 8 KB). Later games did not. The solution is a chip on the cartridge, the **mapper**, that sits between the console's address wires and the ROM's address wires. It holds a few **bank registers**; when the CPU asks for `$8000-$BFFF`, the mapper translates that into "bank number R, offset" on the real ROM, which can be as large as the mapper supports. Swap R and a different 16 KB of the ROM appears in the same window.

### Why writing to ROM is how you talk to the mapper

ROM ignores writes; there is nothing to store into. But the mapper sees every bus cycle, including writes to ROM addresses. So the convention is: a write anywhere in `$8000-$FFFF` is a message to the mapper. Which address and which bits mean what varies by mapper. UxROM has one register. MMC1 has four, but only one data pin, so you write five times, one bit each, and it shifts them in. MMC3 has a bank-select register and a data register and a scanline counter.

### Why mirroring is a cartridge property

There is 2 KB of VRAM but 4 KB of nametable address space. Which 2 KB alias which is decided by how the cartridge wires one PPU address line. Vertical mirroring lets a game scroll horizontally without seams; horizontal mirroring lets it scroll vertically. A cartridge that needs both has a mapper that can flip the wire under software control (MMC1, MMC3), or carries its own extra 2 KB (four-screen).

### Why MMC3 has a scanline counter and why it is hard

MMC3 can raise an IRQ after N scanlines, giving games a real raster interrupt instead of the sprite 0 trick. But the cartridge has no scanline signal from the PPU. It watches PPU address line A12 instead. Backgrounds are usually fetched from pattern table 0 (`$0000-$0FFF`, A12 low) and sprites from table 1 (`$1000-$1FFF`, A12 high). So A12 rises once per scanline, when sprite fetches start at dot 257. The mapper counts those rises. This only works if the emulator's PPU fetches the right addresses at the right dots, which is why MMC3 is the last milestone.

### Why PRG RAM and batteries

Some games need more than 2 KB of state or want to save progress. The cartridge can carry 8 KB of RAM at `$6000-$7FFF`, optionally with a battery so it survives power-off. Test ROMs borrow this region to report results because it is RAM that is not the console's.

## 6. Controllers: eight buttons down one wire

The controller connector has few pins. Instead of eight wires for eight buttons, the controller contains a **shift register**: a chip that snapshots all eight button states when told to (strobe), then reports them one at a time on a single data wire, advancing to the next on each read. The CPU writes 1 then 0 to `$4016` to strobe, then reads `$4016` eight times, taking bit 0 each time: A, B, Select, Start, Up, Down, Left, Right. The emulator's `StandardController` is literally this 8-bit register.

## 7. Putting it together: one frame

1. The CPU is somewhere in the game's main loop, computing physics, reading its own RAM, and preparing the graphics changes it wants in a buffer.
2. The PPU reaches scanline 241. It sets the vblank flag and pulls the NMI wire low.
3. The CPU finishes its current instruction, pushes PC and P, jumps to the NMI handler.
4. The handler reads `$2002` (acknowledging), writes `$4014` to copy sprites (CPU frozen 513 cycles), writes tile changes via `$2006`/`$2007`, sets scroll via `$2005`, then returns.
5. Around scanline 261 the PPU copies the vertical scroll and begins fetching tiles for line 0.
6. For 240 lines the PPU fetches tile data from the cartridge through its bus, shifts pixels out, evaluates sprites for the next line, and emits one pixel per dot. The CPU, meanwhile, runs game logic on its own bus, never touching the PPU.
7. Repeat, 60 times a second.

## 8. Why the emulator is shaped the way it is

- `Device.Tick()` is the crystal. It is the only place that knows the clock dividers, so it is the only place that decides who runs when.
- `IBus` is the bus: an address, a byte, read or write. `CpuBus` and `PpuBus` are the address-decoding logic that routes each access to the chip that listens there.
- `Cpu`, `Ppu`, `Apu` are the chips. They see only their bus, exactly as the real chips do. That is why they can be tested alone with a fake bus.
- `Cartridge` and `IMapper` are the physical cartridge: ROM arrays plus the chip that translates addresses and listens to writes.
- Interrupt lines are wires, so they are set in `Device.Tick()` where both ends are visible.
- `Peek` exists because the debugger must read without the side effects a real read has, something real hardware cannot do but an emulator can.
- Save states are a copy of every register and memory cell, because that is all the machine *is*: a few thousand bytes of state advanced one clock at a time.
