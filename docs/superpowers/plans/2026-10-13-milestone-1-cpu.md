# Milestone 1: CPU Implementation Plan

> **Executor:** the owner writes all code by hand (see `AGENTS.md`). Claude may write the files marked *(Claude may write)*: `tests/roms/fetch.sh`, `tests/roms/checksums.txt`, docs, CI. Everything under `src/` and `tests/OddFrame.Core.Tests/` is owner-written; this plan describes shape, bus-access sequences, and tests, not full code. Steps use `- [ ]` for tracking.

**Goal:** A cycle-stepped 6502 (`OddFrame.Core.Cpu.Cpu`) with all 256 opcodes, interrupts, and the reset sequence. nestest log identical for all 8991 lines. All 16 `instr_test-v5/rom_singles` ROMs pass on a RAM-only bus.

**Architecture:** `Cpu.Tick()` does one bus access or one internal cycle per call. A 256-entry `OpcodeTable` maps each opcode to `(Mnemonic, AddressingMode, Operation, Kind, Unofficial)`. Addressing modes are small cycle-stepped state machines, one per `(Mode, Kind)` pair; operations are pure register/flag functions applied on the final cycle. The same table drives the nestest disassembler. `Device` is not wired in this milestone (that is M2); every test talks to `Cpu` through `IBus` fakes.

**Tech Stack:** .NET 10, C# 14, xUnit v3 under Microsoft.Testing.Platform, no packages in Core.

**Spec:** `docs/superpowers/specs/2026-10-10-nes-emulator-architecture-design.md`, sections 2 (CPU), 3 (Components, Construction, Reset vs power-on), 5 (Trace log, Snapshots), 7, 8.

**Hardware references:**
- Cycle-by-cycle bus sequences: https://www.nesdev.org/6502_cpu.txt (section "6510 Instruction Timing"; the 2A03 is identical here)
- Status flags and bits 4-5: https://www.nesdev.org/wiki/Status_flags
- Interrupt polling and the branch quirk: https://www.nesdev.org/wiki/CPU_interrupts
- Power-up and reset state: https://www.nesdev.org/wiki/CPU_power_up_state
- Unofficial opcodes: https://www.nesdev.org/wiki/CPU_unofficial_opcodes and https://www.nesdev.org/undocumented_opcodes.txt
- nestest result codes: https://www.nesdev.org/wiki/Emulator_tests (nestest.txt)
- blargg result protocol: `AGENTS.md`, Key references

---

## Decisions locked for this plan

Change any of these before starting; the tasks assume them.

| Question | Choice | Why |
|---|---|---|
| Dispatch | 256-entry table + per-mode state machines, `Operation` as an enum switched in one method | Owner's choice. Table doubles as the disassembler. No delegates, so nothing to serialize. |
| M1 exit ROMs | the 16 `instr_test-v5/rom_singles/*.nes` | `all_instrs.nes` and `official_only.nes` are mapper 1 (MMC1, 256 KB PRG; header byte 6 is `$10`). They cannot run on a RAM stub. The singles are mapper 0, 32 KB PRG. `all_instrs.nes` becomes an M6 acceptance test. |
| P register storage | bits 4 (B) and 5 (U) never stored; `P` property returns the 6 real flags | Matches hardware. PHP/BRK push `P | $30`, IRQ/NMI push `P | $20`, PLP/RTI mask both out. Formatter prints `P | $20`, so nestest's `P:24` falls out without special-casing. |
| Power-on P | `$04` stored (I set) | Spec says `$34`; identical once bits 4-5 are not stored. |
| nestest start | write `$C000` into the RAM reset vector at `$FFFC`, then run the real reset sequence | After 7 cycles the CPU is at `$C000`, S=`$FD`, P=`$24`, CYC=7, exactly the first log line. No test-only PC setter needed. |
| PPU column in M1 | formatter receives `(scanline, dot)` from the caller; the nestest test computes them from `TotalCycles * 3` | No PPU exists yet. In M3 the Device-level sink passes real values. `CpuTraceEntry` carries CPU state and `TotalCycles` only. |
| Test class layout | folder `tests/OddFrame.Core.Tests/Cpu/` with several `Cpu*Tests` classes | A single `CpuTests` would be 2000+ lines. Deviates from "one test class per component" in `AGENTS.md`; amend that line when the milestone closes. |
| Operand peeks in formatter | `IBus.Peek` only | Formatter must never trigger side effects. On RAM there are none, but the rule starts now. |
| Unofficial mnemonics | nestest spelling: `NOP LAX SAX SBC DCP ISB SLO RLA SRE RRA ANC ALR ARR AXS SHY SHX LXA XAA AHX TAS LAS JAM` | The log needs `ISB`, not `ISC`. |

## Known traps (verified 2026-10-13)

- blargg ROMs start with `wait_vbl`: `bit $2002 / bpl` until bit 7 is set. A zero-returning bus hangs forever. `BlarggStubBus` returns `$80` for reads of `$2002`.
- blargg status `$6000` is `0` from RAM before the ROM writes anything. Do not stop on "not `$80`" alone; require the `$DE $B0 $61` signature at `$6001-$6003` first.
- `nestest.nes` has 16 KB PRG. Load it at both `$8000` and `$C000`; the ROM jumps between the mirrors.
- The log is exactly 8991 lines and ends at `C66E RTS`, `CYC:26554`. Do not run past it. The stack is empty at that RTS, so the CPU jumps into zero page afterwards.
- `*` for unofficial opcodes sits at column 15 (0-based); the mnemonic always starts at column 16. Register block starts at column 48. PPU fields are `%3d,%3d`.
- Unofficial opcodes instr_test-v5 does **not** test (marked `?` in its readme): `$8B XAA`, `$93/$9F AHX`, `$9B TAS`, `$BB LAS`. Implement a documented behaviour and move on. It **does** test `$9C SHY`, `$9E SHX`, `$AB LXA`, `$0B/$2B ANC`, `$4B ALR`, `$6B ARR`, `$CB AXS`, `$EB SBC`.
- `dotnet test` exits 8 when a filter matches nothing. All-skipped is exit 0.

## File structure

```
src/OddFrame.Core/Cpu/StatusFlags.cs          [Flags] enum byte: C=1 Z=2 I=4 D=8 B=16 U=32 V=64 N=128
src/OddFrame.Core/Cpu/AddressingMode.cs       enum: Implied Accumulator Immediate ZeroPage ZeroPageX ZeroPageY
                                              Absolute AbsoluteX AbsoluteY IndirectX IndirectY Indirect Relative
src/OddFrame.Core/Cpu/Operation.cs            enum: one member per distinct operation (ADC, AND, ... , JAM)
src/OddFrame.Core/Cpu/InstructionKind.cs      enum: Read ReadModifyWrite Write Implied Branch Jump JumpIndirect
                                              Jsr Rts Rti Brk Push Pull
src/OddFrame.Core/Cpu/OpcodeInfo.cs           readonly record struct (Mnemonic, Mode, Op, Kind, Unofficial); Length from Mode
src/OddFrame.Core/Cpu/OpcodeTable.cs          static readonly OpcodeInfo[256]
src/OddFrame.Core/Cpu/Cpu.cs                  registers, lines, PowerOn/Reset, Tick, interrupt poll, IStateful
src/OddFrame.Core/Cpu/Cpu.Addressing.cs       partial: per-(Mode,Kind) micro-steps
src/OddFrame.Core/Cpu/Cpu.Operations.cs       partial: ExecuteOperation switch, flag helpers
src/OddFrame.Core/Debug/CpuTraceEntry.cs      readonly struct: PC A X Y S P Opcode Operand1 Operand2 Length TotalCycles
src/OddFrame.Core/Debug/ITraceSink.cs         void OnInstruction(in CpuTraceEntry entry)
src/OddFrame.Core/Debug/NestestFormatter.cs   static string Format(in CpuTraceEntry e, IBus bus, int scanline, int dot)

tests/OddFrame.Core.Tests/Fakes/RecordingBus.cs       IBus over RamBus, logs (Address, Value, IsWrite) per access
tests/OddFrame.Core.Tests/Fakes/BlarggStubBus.cs      IBus over RamBus, $2002 reads return $80
tests/OddFrame.Core.Tests/Fakes/ListTraceSink.cs      ITraceSink collecting entries
tests/OddFrame.Core.Tests/Cpu/CpuTestHarness.cs       helpers: NewCpuAt(ushort pc), RunInstruction, RunCycles
tests/OddFrame.Core.Tests/Cpu/OpcodeTableTests.cs
tests/OddFrame.Core.Tests/Cpu/CpuResetTests.cs
tests/OddFrame.Core.Tests/Cpu/CpuAddressingTests.cs   bus-access sequences per (Mode, Kind)
tests/OddFrame.Core.Tests/Cpu/CpuInstructionTests.cs  flags and results per operation
tests/OddFrame.Core.Tests/Cpu/CpuControlFlowTests.cs  branches, JMP, JSR/RTS, stack, BRK/RTI
tests/OddFrame.Core.Tests/Cpu/CpuInterruptTests.cs
tests/OddFrame.Core.Tests/Cpu/CpuStateTests.cs        Save/Load, determinism
tests/OddFrame.Core.Tests/Debug/NestestFormatterTests.cs
tests/OddFrame.Core.Tests/Roms/NestestTests.cs
tests/OddFrame.Core.Tests/Roms/BlarggRunner.cs        shared $6000 protocol loop
tests/OddFrame.Core.Tests/Roms/InstrTestV5Tests.cs    [Theory] over 16 singles
tests/roms/fetch.sh, checksums.txt                     +16 rom_singles entries (done 2026-10-13)
```

## `Cpu` public surface

From spec section 3, pinned here so tests and later milestones agree:

```csharp
public sealed partial class Cpu : IStateful
{
    public Cpu(IBus bus);
    public byte A, X, Y, S;  public ushort PC;  public StatusFlags P;   // properties; P never has B or U set
    public bool Nmi { get; set; }        // level from Device; CPU edge-detects internally
    public bool IrqLine { get; set; }    // level
    public bool Halted { get; }
    public bool IsInstructionBoundary { get; }   // cycle == 0
    public ulong TotalCycles { get; }
    public ITraceSink? TraceSink { get; set; }
    public void PowerOn();  public void Reset();  public void Tick();
}
```

`Reset()` and `PowerOn()` only arm the 7-cycle sequence; the cycles happen in the following `Tick()` calls so traces and `TotalCycles` see them.

## Bus-access sequences (the test oracle)

Cycle 1 is always the opcode fetch at PC, PC++. "dummy" means the value is discarded but the access happens. `lo+X` means 8-bit add, carry noted separately. Source: 6502_cpu.txt.

| Mode / Kind | Cycles | Sequence after the fetch |
|---|---|---|
| Implied, Accumulator | 2 | 2: dummy read PC (no increment) |
| Immediate | 2 | 2: read PC++ |
| ZeroPage read / write | 3 | 2: read addr at PC++. 3: read or write addr |
| ZeroPage RMW | 5 | 3: read. 4: **write old value**. 5: write new value |
| ZeroPage,X / ,Y read / write | 4 | 2: read zp at PC++. 3: dummy read zp, add index (wrap in page 0). 4: access |
| ZeroPage,X RMW | 6 | 4: read. 5: write old. 6: write new |
| Absolute read / write | 4 | 2: lo. 3: hi. 4: access |
| Absolute RMW | 6 | 4: read. 5: write old. 6: write new |
| Absolute,X / ,Y read | 4 or 5 | 2: lo. 3: hi, lo+=index. 4: read `hi:lo` (unfixed). If no carry, done, value used. 5: read fixed address |
| Absolute,X / ,Y write | 5 | 4: **dummy read unfixed**. 5: write fixed |
| Absolute,X / ,Y RMW | 7 | 4: dummy read unfixed. 5: read fixed. 6: write old. 7: write new |
| (Indirect,X) read / write | 6 | 2: ptr at PC++. 3: dummy read ptr, ptr+=X (wrap). 4: lo from ptr. 5: hi from ptr+1 (wrap). 6: access |
| (Indirect,X) RMW | 8 | 6: read. 7: write old. 8: write new |
| (Indirect),Y read | 5 or 6 | 2: ptr. 3: lo from ptr. 4: hi from ptr+1 (wrap), lo+=Y. 5: read unfixed. 6 only on carry: read fixed |
| (Indirect),Y write | 6 | 5: dummy read unfixed. 6: write fixed |
| (Indirect),Y RMW | 8 | 5: dummy read unfixed. 6: read fixed. 7: write old. 8: write new |
| Relative (branch) | 2, 3, 4 | 2: read offset at PC++. Not taken: done. 3: dummy read at PC, PCL+=offset. No page change: done. 4: dummy read at `oldPCH:newPCL`, fix PCH |
| JMP abs | 3 | 2: lo. 3: hi, PC=target |
| JMP (ind) | 5 | 2: ptr lo. 3: ptr hi. 4: lo from ptr. 5: hi from `(ptr & $FF00) | ((ptr+1) & $FF)` |
| JSR | 6 | 2: lo at PC++. 3: dummy read `$0100+S`. 4: push PCH. 5: push PCL. 6: hi at PC, PC=target. Pushed PC points at the JSR's last byte |
| RTS | 6 | 2: dummy read PC. 3: dummy read `$0100+S`, S++. 4: pull PCL. 5: pull PCH. 6: dummy read PC, PC++ |
| RTI | 6 | 2: dummy read PC. 3: dummy read stack, S++. 4: pull P (mask B,U). 5: pull PCL. 6: pull PCH |
| PHA / PHP | 3 | 2: dummy read PC. 3: write `$0100+S`, S-- |
| PLA / PLP | 4 | 2: dummy read PC. 3: dummy read `$0100+S`, S++. 4: read `$0100+S` |
| BRK | 7 | 2: read PC++ (padding byte). 3: push PCH. 4: push PCL. 5: push `P|$30`. 6: lo from `$FFFE`, set I. 7: hi from `$FFFF` |
| IRQ / NMI | 7 | as BRK but: 1 and 2 dummy reads at PC without increment, push `P|$20`, vector `$FFFE` or `$FFFA` |
| Reset | 7 | as IRQ but pushes are **reads** of `$0100+S`, S still decrements 3 times, vector `$FFFC` |

Push writes `$0100+S` then S--. Pull S++ then reads `$0100+S`.

---

### Task 1: Test fakes

**Files:**
- Create: `tests/OddFrame.Core.Tests/Fakes/RecordingBus.cs`
- Create: `tests/OddFrame.Core.Tests/Fakes/BlarggStubBus.cs`
- Create: `tests/OddFrame.Core.Tests/Fakes/ListTraceSink.cs` (after Task 4 defines `ITraceSink`; listed here for completeness)
- Create: `tests/OddFrame.Core.Tests/RecordingBusTests.cs`

Every addressing-mode test in this milestone asserts an exact access list. Build the recorder first.

- [ ] **Step 1: Write failing `RecordingBusTests`**

- `Read_IsRecordedWithValue`: load `0x5A` at `0x1234` via inner `RamBus`, `Read(0x1234)`, log is one entry `(0x1234, 0x5A, IsWrite: false)`.
- `Write_IsRecorded`: `Write(0x0200, 0x01)`, log is `(0x0200, 0x01, true)` and inner memory holds `0x01`.
- `Peek_IsNotRecorded`: `Peek` returns the value and the log stays empty.
- `Clear_EmptiesLog`.
- `BlarggStubBus_Read2002_ReturnsBit7`: `Read(0x2002) == 0x80` on a fresh bus. Other addresses behave like `RamBus`.

- [ ] **Step 2: Run, confirm compile failure**

Run: `dotnet test --filter "FullyQualifiedName~RecordingBusTests"`

- [ ] **Step 3: Write the fakes**

`RecordingBus : IBus` wraps a `RamBus` (expose it as `Ram` so tests can `Load`). `readonly record struct Access(ushort Address, byte Value, bool IsWrite)`; `List<Access> Log`. `Read` records after reading, `Write` records then writes. `Peek` passes through silently.

`BlarggStubBus : IBus` wraps `RamBus`; `Read` and `Peek` return `0x80` when `address == 0x2002`; everything else passes through.

- [ ] **Step 4: Run, confirm green**

Run: `dotnet test --filter "Category!=Rom"`

- [ ] **Step 5: Commit**

```bash
git add tests/OddFrame.Core.Tests/Fakes tests/OddFrame.Core.Tests/RecordingBusTests.cs
git commit -m "test: add RecordingBus and BlarggStubBus fakes"
```

---

### Task 2: Enums and `OpcodeTable`

**Files:**
- Create: `src/OddFrame.Core/Cpu/StatusFlags.cs`, `AddressingMode.cs`, `Operation.cs`, `InstructionKind.cs`, `OpcodeInfo.cs`, `OpcodeTable.cs`
- Create: `tests/OddFrame.Core.Tests/Cpu/OpcodeTableTests.cs`

Fill all 256 entries now, official and unofficial. It is data entry; mistakes surface as nestest diffs later and are cheap to fix. Use the opcode matrix on the NESdev unofficial-opcodes page as the source; it lists every slot.

- [ ] **Step 1: Write failing `OpcodeTableTests`**

- `Table_Has256Entries_NoneDefault`: every entry has a non-empty mnemonic.
- `OfficialCount_Is151`: `Count(e => !e.Unofficial) == 151`.
- `Length_FollowsMode`: Implied/Accumulator 1; Immediate, ZeroPage*, IndirectX, IndirectY, Relative 2; Absolute*, Indirect 3.
- `SpotChecks`: `0x4C` = `JMP Absolute Jump`, `0xA1` = `LDA IndirectX Read`, `0x9D` = `STA AbsoluteX Write`, `0xFE` = `INC AbsoluteX ReadModifyWrite`, `0xEB` = `SBC Immediate Read Unofficial`, `0x04` = `NOP ZeroPage Read Unofficial`, `0x02` = `JAM`.
- `UnofficialNops_HaveNestestMnemonic`: `0x1A`, `0x80`, `0x0C`, `0x1C` all `"NOP"`.

- [ ] **Step 2: Run, confirm compile failure**

- [ ] **Step 3: Write the enums and table**

`OpcodeInfo` is a `readonly record struct` with a computed `Length` property switching on `Mode`. `OpcodeTable.Entries` is `static readonly OpcodeInfo[]` built from 256 initialiser lines, one per opcode in hex order, so a diff against the matrix is a line-by-line read. Keep a local helper like `static OpcodeInfo Op(string m, AddressingMode mode, Operation op, InstructionKind kind, bool unofficial = false)` to keep lines short.

Kind assignment rules: loads, ALU, compares, BIT, LAX, NOP-with-operand, ANC/ALR/ARR/AXS/LXA → `Read`. Stores, SAX, SHY, SHX, AHX, TAS → `Write`. Shifts/INC/DEC/SLO/RLA/SRE/RRA/DCP/ISB in memory modes → `ReadModifyWrite`. Shifts on `A`, transfers, flag ops, INX/INY/DEX/DEY, 1-byte NOPs, JAM → `Implied`. Everything else by name.

- [ ] **Step 4: Run, confirm green**

Run: `dotnet test --filter "FullyQualifiedName~OpcodeTableTests"`

- [ ] **Step 5: Commit**

```bash
git add src/OddFrame.Core/Cpu tests/OddFrame.Core.Tests/Cpu/OpcodeTableTests.cs
git commit -m "feat: add 6502 opcode table and CPU enums"
```

---

### Task 3: `Cpu` skeleton, power-on, reset sequence

**Files:**
- Create: `src/OddFrame.Core/Cpu/Cpu.cs`
- Create: `tests/OddFrame.Core.Tests/Cpu/CpuTestHarness.cs`
- Create: `tests/OddFrame.Core.Tests/Cpu/CpuResetTests.cs`

- [ ] **Step 1: Write failing `CpuResetTests`**

Harness: `NewCpu(out RecordingBus bus)` constructs both, writes `$00 $80` at `$FFFC`, calls `PowerOn()`, then `RunCycles(7)`, then `bus.Log.Clear()`. Tests that need the raw sequence skip the clear.

- `PowerOn_AfterSevenCycles_RegistersMatchSpec`: `A == X == Y == 0`, `S == 0xFD`, `P == StatusFlags.I`, `PC == 0x8000`, `TotalCycles == 7`, `IsInstructionBoundary`.
- `PowerOn_SequenceIsSevenReadsNoWrites`: log has 7 entries, none `IsWrite`; entries 3-5 are reads of `0x01FF`, `0x01FE`, `0x01FD` (S starts at `$00` and wraps); entries 6-7 read `0xFFFC`, `0xFFFD`.
- `Reset_FromRunningState_DecrementsSByThreeAndSetsI`: after power-on set `S = 0x80`, clear I by running `58 CLI`, call `Reset()`, run 7 cycles, expect `S == 0x7D`, I set, A/X/Y untouched, PC from vector.
- `Constructor_IsEquivalentToPowerOn`: a fresh `Cpu` ticked 7 times without calling `PowerOn()` reaches the same state as one where `PowerOn()` was called.

- [ ] **Step 2: Run, confirm compile failure**

- [ ] **Step 3: Write `Cpu` with reset only**

Shape:
- Fields: registers, `_cycle` (0 = boundary), `_opcode`, `_address`, `_pointer`, `_operand`, `_pageCrossed`, `_pendingInterrupt` (enum None/Irq/Nmi/Reset), `_nmiPrev`, `_nmiLatched`, `_interruptSampled`, `TotalCycles`.
- Constructor calls `PowerOn()`. `PowerOn()`: zero registers, `P = I`, `S = 0`, arm Reset. `Reset()`: arm Reset only.
- `Tick()`: `TotalCycles++`. If `_cycle == 0`: if an interrupt is pending, start the interrupt pseudo-instruction, else fetch. Otherwise step the current instruction. The reset pseudo-instruction follows the IRQ sequence with reads instead of writes. Last step sets `_cycle = 0`.
- Stack helpers: `Push(byte)` writes `$0100 | S` then `S--`; `Pull()` does `S++` then reads. Reset uses a `PushDummy()` that reads instead.

- [ ] **Step 4: Run, confirm green**

- [ ] **Step 5: Commit** — `feat: add Cpu skeleton with reset sequence`

---

### Task 4: Trace sink and `NestestFormatter`

**Files:**
- Create: `src/OddFrame.Core/Debug/CpuTraceEntry.cs`, `ITraceSink.cs`, `NestestFormatter.cs`
- Create: `tests/OddFrame.Core.Tests/Fakes/ListTraceSink.cs`
- Create: `tests/OddFrame.Core.Tests/Debug/NestestFormatterTests.cs`
- Modify: `src/OddFrame.Core/Cpu/Cpu.cs` (fire the sink at cycle 0, before the fetch, using `Peek`)

Build the diff tool before the instructions. The nestest harness in Task 5 then reports "first mismatch at line N" from the first opcode onward.

- [ ] **Step 1: Write failing `NestestFormatterTests`**

Each test constructs a `CpuTraceEntry` by hand, loads the operand bytes and any referenced memory into a `RamBus`, calls `NestestFormatter.Format(entry, bus, scanline, dot)`, and asserts string equality against a line copied verbatim from `tests/roms/other/nestest.log`. Use these lines (line numbers for reference):

| Line | Covers |
|---|---|
| 1 `C000  4C F5 C5  JMP $C5F5 ... P:24 SP:FD PPU:  0, 21 CYC:7` | absolute jump without `= vv`, PPU padding |
| 37 `C780  85 01     STA $01 = 00` | zero page with value |
| 976 `CEFC  4A        LSR A` | accumulator |
| 1087 `CFDB  A1 80     LDA ($80,X) @ 80 = 0200 = 5A` | (ind,X) |
| 3041 `D922  B1 89     LDA ($89),Y = 0300 @ 0300 = 89` | (ind),Y |
| 3328 `DB7B  6C 00 02  JMP ($0200) = DB7E` | indirect jump |
| 3366 `DF60  B9 00 03  LDA $0300,Y @ 0300 = 89` | abs,Y |
| 3639 `DBCD  B4 33     LDY $33,X @ 33 = AA` | zp,X |
| 4306 `DEFE  96 80     STX $80,Y @ 7F = 00` | zp,Y with wrap |
| 5004 `C6BD  04 A9    *NOP $A9 = 00` | unofficial marker |
| 5038 `C6EA  80 89    *NOP #$89` | unofficial immediate |
| 9 `C72F  B0 04     BCS $C735` | relative shows target |

Also `PpuColumn_PadsToThree`: `PPU:134,134` and `PPU:  0, 21`.

- [ ] **Step 2: Run, confirm compile failure**

- [ ] **Step 3: Write the Debug types**

`CpuTraceEntry` (readonly struct): `PC, A, X, Y, S, P (raw 6 flags), Opcode, Operand1, Operand2, Length, TotalCycles`.

`NestestFormatter.Format` builds, in order:
1. `PC:X4`, two spaces.
2. Opcode bytes: `Length` bytes as `XX ` joined, padded to 9 characters total (so the field ends at column 14 inclusive of trailing space).
3. `*` if `OpcodeTable.Entries[Opcode].Unofficial` else space. Column 15.
4. Mnemonic, space, operand text by mode:
   - Immediate `#$vv`
   - ZeroPage `$aa = vv`
   - ZeroPageX/Y `$aa,X @ ee = vv` (`ee` two hex digits, wrapped)
   - Absolute `$aaaa = vv`, except Jump/Jsr kinds: `$aaaa`
   - AbsoluteX/Y `$aaaa,X @ eeee = vv`
   - IndirectX `($aa,X) @ pp = eeee = vv` (`pp` is `aa+X` wrapped)
   - IndirectY `($aa),Y = bbbb @ eeee = vv` (`bbbb` before adding Y, `eeee` after)
   - Indirect `($aaaa) = tttt` with the page-wrap bug applied to the high byte fetch
   - Relative `$tttt` (PC + 2 + signed offset)
   - Accumulator `A`; Implied nothing.
   `vv` is always `bus.Peek(effective)`.
5. Pad the disassembly so the register block starts at column 48.
6. `A:{A:X2} X:{X:X2} Y:{Y:X2} P:{P|0x20:X2} SP:{S:X2} PPU:{scanline,3},{dot,3} CYC:{TotalCycles}`.

`Cpu.Tick()` at `_cycle == 0` with no pending interrupt: if `TraceSink` is not null, build the entry with `Peek(PC)`, `Peek(PC+1)`, `Peek(PC+2)` and call it, then do the real fetch.

- [ ] **Step 4: Run, confirm green**

- [ ] **Step 5: Commit** — `feat: add trace sink and nestest formatter`

---

### Task 5: nestest harness (stays red until Task 11)

**Files:**
- Create: `tests/OddFrame.Core.Tests/Roms/NestestTests.cs`

- [ ] **Step 1: Write `NestestTests`** `[Trait("Category", "Rom")]`

- `Trace_MatchesReferenceLog`:
  1. `TestRoms.Require("other/nestest.nes")` and `"other/nestest.log"`.
  2. PRG = file bytes `[16 .. 16 + 0x4000)`. `bus.Ram.Load(0x8000, prg)` and `Load(0xC000, prg)`. Then overwrite `0xFFFC` with `00 C0`.
  3. `cpu.PowerOn()`, run 7 cycles. Attach `ListTraceSink` that formats each entry immediately with `scanline = (int)(cycles * 3 / 341 % 262)`, `dot = (int)(cycles * 3 % 341)`.
  4. Loop: tick until the sink has 8991 lines or a cap of 100 000 cycles is hit (the log ends at 26 554; the cap catches runaway loops).
  5. Compare line by line against the expected file. On the first mismatch fail with: line number, expected, actual, and the previous expected line for context. Compare all lines even when counts differ, then assert counts.
  6. After the loop assert `bus.Peek(0x0002) == 0` and `bus.Peek(0x0003) == 0`, with the nestest result-code meaning in the message (`$02` official failures, `$03` unofficial).

- [ ] **Step 2: Run, read the first mismatch**

Run: `dotnet test --filter "FullyQualifiedName~NestestTests"`
Expected now: fails at line 1 or 2 (no instructions implemented). Each later task moves this number forward. Keep the previous number in the commit message body.

- [ ] **Step 3: Commit** — `test: add nestest log diff harness`

---

### Task 6: Immediate and implied instructions

**Files:**
- Create: `src/OddFrame.Core/Cpu/Cpu.Addressing.cs`, `Cpu.Operations.cs`
- Create: `tests/OddFrame.Core.Tests/Cpu/CpuAddressingTests.cs`, `CpuInstructionTests.cs`
- Modify: `Cpu.cs` (fetch + dispatch)

Operations in this task: LDA/LDX/LDY (imm), TAX/TAY/TXA/TYA/TSX/TXS, INX/INY/DEX/DEY, CLC/SEC/CLI/SEI/CLD/SED/CLV, NOP, ADC/SBC/AND/ORA/EOR/CMP/CPX/CPY (imm), ASL/LSR/ROL/ROR on A.

- [ ] **Step 1: Write failing tests**

`CpuAddressingTests`:
- `Implied_TwoCycles_DummyReadsPcWithoutIncrement`: `EA` at `$8000`, run one instruction, log is `[(0x8000, EA, r), (0x8001, ?, r)]`, `PC == 0x8001`, `TotalCycles` advanced by 2.
- `Immediate_TwoCycles_ReadsOperand`: `A9 42`, log `[(0x8000), (0x8001)]`, `A == 0x42`, `PC == 0x8002`.

`CpuInstructionTests` (register/flag results, cycle count via `TotalCycles` delta):
- `LDA_SetsZ_WhenZero`, `LDA_SetsN_WhenBit7`, `LDX`, `LDY` likewise.
- `Transfers`: `TXS` sets no flags; `TSX` sets Z/N; others set Z/N.
- `INX_WrapsFFToZero_SetsZ`; `DEY_WrapsZeroToFF_SetsN`.
- `FlagOps`: each of the seven sets or clears exactly its flag; `SED` sets D and `ADC` still adds in binary.
- `ADC` table as `[Theory]` rows `(a, m, carryIn, expectedA, C, Z, V, N)`: `(0x50,0x10,0,0x60,0,0,0,0)`, `(0x50,0x50,0,0xA0,0,0,1,1)`, `(0xD0,0x90,0,0x60,1,0,1,0)`, `(0xFF,0x01,0,0x00,1,1,0,0)`, `(0x00,0x00,1,0x01,0,0,0,0)`.
- `SBC_IsAdcOfComplement`: `(0x50,0xF0,C=1 -> 0x60, C=0, V=0)`, `(0x50,0xB0,C=1 -> 0xA0, C=0, V=1)`, `(0x00,0x01,C=1 -> 0xFF, C=0)`.
- `CMP_SetsCWhenAGreaterOrEqual`, `CPX`, `CPY`: `(A=0x10,m=0x10) -> C,Z`; `(0x10,0x20) -> N only`; `(0x20,0x10) -> C`.
- `AND_ORA_EOR` immediates, Z/N.
- `ASL_A_ShiftsBit7IntoC`, `LSR_A_ShiftsBit0IntoC_ClearsN`, `ROL_A_RotatesThroughCarry`, `ROR_A`.

- [ ] **Step 2: Run, confirm failures**

- [ ] **Step 3: Implement dispatch and these operations**

Shape:
- `Tick()` non-zero cycle: `var info = OpcodeTable.Entries[_opcode]; StepAddressing(info)`; the addressing step calls `ExecuteOperation(info.Op, operand)` on the cycle where the operand is available (`Read`) or on the dummy-read cycle (`Implied`), then ends the instruction.
- `Cpu.Operations.cs`: `SetZN(byte)`, `ExecuteOperation(Operation op, byte value)` returning the result for RMW/accumulator ops. `Adc(byte m)`: `sum = A + m + C`; `V = ((A ^ sum) & (m ^ sum) & 0x80) != 0`; `C = sum > 0xFF`. `Sbc(m) = Adc((byte)~m)`.
- Compare: `C = reg >= m`, `SetZN(reg - m)`.

- [ ] **Step 4: Run; confirm green; check nestest progress**

Run: `dotnet test --filter "Category!=Rom"` then the nestest filter. Line 1 is `JMP`, so the diff still fails at line 1 until Task 9. That is expected; the unit tests are the signal here.

- [ ] **Step 5: Commit** — `feat: implied and immediate instructions`

---

### Task 7: Read addressing modes

**Files:**
- Modify: `Cpu.Addressing.cs`, `CpuAddressingTests.cs`, `CpuInstructionTests.cs`

Modes: ZeroPage, ZeroPageX, ZeroPageY, Absolute, AbsoluteX, AbsoluteY, IndirectX, IndirectY for `Read` kind. Operations: all loads, ALU, compares, BIT.

- [ ] **Step 1: Write failing access-sequence tests** (one per row of the oracle table, `Read` column)

Assert the full `RecordingBus.Log` as a list of `(address, isWrite)` and the final `TotalCycles` delta. Specific values to pin:
- `ZeroPageX_WrapsInPageZero`: `B5 FF` with `X=2` reads `$00FF` (dummy) then `$0001`.
- `AbsoluteX_NoCross_FourCycles`: `BD 00 02`, `X=1`, log ends with read `$0201`, 4 cycles.
- `AbsoluteX_Cross_FiveCycles_DummyReadUnfixed`: `BD FF 02`, `X=1`, reads `$0200` then `$0300`, 5 cycles.
- `AbsoluteY` same two.
- `IndirectX_WrapsPointer`: `A1 FF`, `X=1`: dummy read `$00FF`, lo from `$0000`, hi from `$0001`.
- `IndirectY_NoCross_FiveCycles`, `IndirectY_Cross_SixCycles_DummyReadUnfixed`, `IndirectY_PointerFF_HighByteFromZero`: `B1 FF` reads lo at `$00FF`, hi at `$0000`.
- `BIT_SetsNVFromMemory_ZFromAnd`: `24 10` with `$10 = 0xC0`, `A = 0x3F` → Z set, N set, V set; A unchanged.

- [ ] **Step 2: Run, confirm failures**

- [ ] **Step 3: Implement the `Read` state machines**

One switch per mode on `_cycle`, following the oracle table literally. Keep `_address` as the effective address, `_pointer` for the zero-page pointer in indirect modes, `_pageCrossed` from the low-byte carry. The "no cross → use the value read this cycle" shortcut is what makes 4-vs-5 cycles fall out naturally: on cycle 4 read `unfixed`; if `!_pageCrossed` execute with that value and end; else keep going.

- [ ] **Step 4: Run; green; nestest progress** (still line 1 until JMP exists; fine)

- [ ] **Step 5: Commit** — `feat: read addressing modes`

---

### Task 8: Write and read-modify-write modes

**Files:**
- Modify: `Cpu.Addressing.cs`, `Cpu.Operations.cs`, tests as above

Operations: STA/STX/STY; ASL/LSR/ROL/ROR/INC/DEC in memory.

- [ ] **Step 1: Write failing tests**

- `STA_AbsoluteX_AlwaysFiveCycles_DummyReadUnfixed`: `9D FF 02`, `X=1`: read `$0200`, write `$0300`. And a no-cross case still 5 cycles with the dummy read at the (correct) address.
- `STA_IndirectY_AlwaysSixCycles`.
- `STX_ZeroPageY_Wraps`: `96 80` with `Y=0xFF` writes `$007F` (nestest line 4306).
- `INC_ZeroPage_WritesOldThenNew`: `E6 10` with `$10 = 0x41`: reads `$10`, writes `$10 = 0x41`, writes `$10 = 0x42`. 5 cycles.
- `INC_AbsoluteX_SevenCycles_NoCross` and `_Cross`: both 7; dummy read at unfixed address in both.
- `ASL_Memory_SetsCFromBit7`, `ROR_Memory_UsesCarryIn`, `DEC_WrapsToFF_SetsN`.
- Stores set no flags.

- [ ] **Step 2: Run, confirm failures**

- [ ] **Step 3: Implement**

Write kind: same address pipeline, but the "unfixed" cycle is always taken and is a read; the final cycle writes. RMW: read, write old, write new; `ExecuteOperation` returns the new byte.

- [ ] **Step 4: Run; green**

- [ ] **Step 5: Commit** — `feat: write and read-modify-write modes`

---

### Task 9: Control flow and stack

**Files:**
- Create: `tests/OddFrame.Core.Tests/Cpu/CpuControlFlowTests.cs`
- Modify: `Cpu.Addressing.cs`, `Cpu.Operations.cs`

Operations: all 8 branches, JMP abs, JMP (ind), JSR, RTS, RTI, BRK, PHA, PHP, PLA, PLP.

- [ ] **Step 1: Write failing tests** (full access lists from the oracle table)

- `Branch_NotTaken_TwoCycles`: `B0 05` at `$8000` with C clear: reads `$8000`, `$8001`; PC `$8002`.
- `Branch_Taken_ThreeCycles_DummyReadAtNextOpcode`: same with C set: reads `$8000`, `$8001`, dummy `$8002`; PC `$8007`.
- `Branch_TakenCross_FourCycles_DummyReadAtUnfixedPc`: `B0 05` at `$80FD` with C set: reads `$80FD`, `$80FE`, dummy `$80FF`, dummy `$8004` (old PCH `$80`, new PCL `$04`); PC `$8104`.
- `Branch_TakenBackwardCross`: `90 F0` (BCC -16) at `$8100` with C clear: dummy `$8102`, dummy `$81F2` (unfixed), PC `$80F2`.
- `JMP_Indirect_PageWrapBug`: pointer `$02FF` reads lo from `$02FF`, hi from `$0200`.
- `JSR_PushesReturnMinusOne_SixCycles`: `20 00 90` at `$8000` pushes `$80`, `$02`; PC `$9000`; dummy stack read on cycle 3.
- `RTS_PullsAndIncrements_SixCycles`.
- `RTI_PullsP_IgnoringBitsFourAndFive`: push `0xFF` as P → `P == 0xCF` after RTI.
- `PHP_PushesWithBAndUSet`: with `P = C`, pushed byte is `0x31`.
- `PLP_IgnoresBitsFourAndFive`, `PLA_SetsZN`.
- `BRK_SevenCycles_PushesPcPlusTwoAndPWithB_SetsI_LoadsFFFE`.

- [ ] **Step 2: Run, confirm failures**

- [ ] **Step 3: Implement**

Branch condition from the opcode: bits 7-6 select N/V/C/Z, bit 5 selects the required value. Relative target is computed on cycle 3 with an 8-bit add to PCL; the carry/borrow decides whether cycle 4 happens. BRK shares the interrupt sequence code with a flag for "B set, increment PC, vector `$FFFE`".

- [ ] **Step 4: Run; green; nestest progress**

Expected: the diff now walks well into the official section. Fix whatever the first mismatch is until it reaches line 5004 (`*NOP $A9`, the first unofficial). Typical culprits: a flag after `CMP`, `ROR` carry-in, `PLP`/`RTI` masking, `BIT` V.

- [ ] **Step 5: Commit** — `feat: control flow, stack, BRK/RTI`

---

### Task 10: Unofficial opcodes

**Files:**
- Modify: `Cpu.Operations.cs`, `Cpu.Addressing.cs`, `CpuInstructionTests.cs`

- [ ] **Step 1: Write failing tests**

- `NOP_WithOperand_PerformsRead`: `04 10` reads `$10`; `1C FF 02` with `X=1` takes 5 cycles and reads `$0200` then `$0300`.
- `LAX_LoadsBoth`, `SAX_StoresAAndX_NoFlags`.
- `DCP`, `ISB`, `SLO`, `RLA`, `SRE`, `RRA`: one each, asserting the double write and the combined result/flags. `RRA` is `ROR` then `ADC` with the carry produced by the `ROR`.
- `SBC_EB_BehavesLikeE9`.
- `ANC_SetsCFromN`, `ALR_AndThenLsr`, `ARR_AndRorWithVAndCFromBits6And5`, `AXS_SubtractsWithoutBorrow_SetsC`.
- `SHY_StoresYAndHighPlusOne`: `9C 00 02` with `Y=0xFF`, `X=0` writes `0xFF & 0x03 = 0x03` at `$0200`, 5 cycles.
- `SHY_PageCross_ValueReplacesHighByte`: `9C FF 02` with `X=1`, `Y=0xF0`: value `0xF0 & 0x03 = 0x00`; the crossed address `$0300` has its high byte replaced by the value, so the write lands at `$0000` with `0x00`. Dummy read on cycle 4 is still at `$0200`.
- `SHX_SameWithXAndY`: `9E 00 02` with `X=0xFF`, `Y=0` writes `0x03` at `$0200`.
- `JAM_SetsHalted_AndStaysHalted`: `02`, tick 10 times, `Halted`, PC unchanged.

- [ ] **Step 2: Run, confirm failures**

- [ ] **Step 3: Implement**

Rules: `LXA ($AB)`: start with `A = X = operand`; if 03-immediate later reports `$AB`, switch to `A = X = (A | 0xEE) & operand`. `XAA ($8B)`: `A = (A | 0xEE) & X & operand`. `AHX`, `TAS`, `LAS`: the formulas on the NESdev page; untested by both suites. `SHX`/`SHY`: value `= reg & (high + 1)`; if the index add crossed a page, the write address high byte becomes that value.

- [ ] **Step 4: nestest green**

Run the nestest filter until it passes all 8991 lines and `$02 == $03 == 0`. Then run everything.

- [ ] **Step 5: Commit** — `feat: unofficial opcodes; nestest log identical`

---

### Task 11: Interrupts

**Files:**
- Create: `tests/OddFrame.Core.Tests/Cpu/CpuInterruptTests.cs`
- Modify: `Cpu.cs`

Rule to implement literally: at the **end of every cycle** compute `sampled = _nmiLatched || (IrqLine && !I)` and store it. At the start of an instruction's **last** cycle, read the value stored at the end of the previous cycle (the second-to-last) into `_pendingInterrupt`. When `_cycle` returns to 0 with a pending interrupt, run the 7-cycle sequence instead of fetching. NMI edge: each cycle `if (Nmi && !_nmiPrev) _nmiLatched = true; _nmiPrev = Nmi;` and clear `_nmiLatched` when the NMI sequence starts.

Branches are the one exception (NESdev CPU_interrupts, "Branch instructions and interrupts"): they poll at the start of cycle 2 using the sample from the end of cycle 1. A taken branch does not poll again at cycle 3. If it crosses a page it polls once more at the start of cycle 4 (sample from the end of cycle 3). Implement by letting the branch step record its poll result on cycle 2 and, for the no-cross taken case, carry that result into the end of the instruction instead of re-sampling. The CLI/SEI/PLP/RTI behaviours below follow from the general rule without special cases.

- [ ] **Step 1: Write failing tests**

- `Irq_WhenIClear_SevenCycleSequence_PushesPWithBClearUSet_SetsI_LoadsFFFE`: program `EA EA`, `IrqLine = true` before the first NOP; after the NOP, the next 7 cycles are: 2 dummy reads at PC, pushes of PCH, PCL, `P | 0x20`, reads `$FFFE`, `$FFFF`. PC equals the vector.
- `Irq_WhenISet_Ignored`.
- `Irq_LevelSensitive_FiresAgainWhileHeld`: keep `IrqLine` true through an `RTI`; a second sequence starts right after.
- `Nmi_EdgeTriggered_FiresOnce`: set `Nmi = true` and keep it; exactly one sequence, vector `$FFFA`, even with I set.
- `Nmi_RearmsAfterFallingAndRisingAgain`.
- `Cli_DelaysIrqByOneInstruction`: `78 SEI` done, `IrqLine = true`, then `58 CLI`, `EA NOP`: the NOP executes before the IRQ sequence.
- `Sei_DoesNotBlockIrqAlreadySampled`: `IrqLine = true` with I clear, run `78 SEI`: the IRQ sequence still starts after SEI.
- `Plp_SameDelayAsCli`; `Rti_NoDelay`: after RTI clears I with IRQ held, the sequence starts immediately.
- `TakenBranch_NoPageCross_DelaysIrqOneInstruction`: `IrqLine` raised during cycle 2 of a taken branch; the next instruction runs first. (This encodes the branch quirk; skip for now if it fights the simpler rule and note it for `cpu_interrupts_v2` in M2.)
- `Nmi_HijacksBrk`: NMI asserted during BRK's cycle 3: vector `$FFFA` is used, B still set in the pushed P. Optional in M1; required before M3.

- [ ] **Step 2: Run, confirm failures**

- [ ] **Step 3: Implement** per the rule above. Share the sequence with BRK and Reset through a small `InterruptKind` parameter.

- [ ] **Step 4: Run all unit tests and nestest** (nestest must stay identical; it never raises interrupts, so this is a regression check).

- [ ] **Step 5: Commit** — `feat: NMI and IRQ with second-to-last-cycle polling`

---

### Task 12: `IStateful` and determinism

**Files:**
- Create: `tests/OddFrame.Core.Tests/Cpu/CpuStateTests.cs`
- Modify: `Cpu.cs`

- [ ] **Step 1: Write failing tests**

- `SaveLoad_MidInstruction_ContinuesIdentically`: run nestest setup, step 1234 **cycles** (deliberately mid-instruction), `Save` to bytes, continue 500 cycles collecting the trace into list A. Build a second `Cpu` on a bus loaded with a byte copy of the first bus's RAM taken at the save point, `Load`, continue 500 cycles into list B. `A == B` and both CPUs' registers equal.
- `Save_IsDeterministic`: two fresh CPUs, same program, same cycles, identical bytes.
- `Load_Truncated_ThrowsEndOfStream`: `Cpu.Load` lets `EndOfStreamException` propagate; `Device.LoadState` already converts it to `InvalidDataException`.

- [ ] **Step 2: Run, confirm failures**

- [ ] **Step 3: Implement `Save`/`Load`**: every field listed in Task 3 plus `_nmiPrev`, `_nmiLatched`, `_interruptSampled`, `_pendingInterrupt`, `Halted`, `TotalCycles`. `IrqLine` and `Nmi` are rewired by `Device` each tick, so saving them is optional; be consistent and document it in a comment.

- [ ] **Step 4: Run; green**

- [ ] **Step 5: Commit** — `feat: Cpu save/load state`

---

### Task 13: blargg runner and `instr_test-v5` singles

**Files:**
- Create: `tests/OddFrame.Core.Tests/Roms/BlarggRunner.cs`
- Create: `tests/OddFrame.Core.Tests/Roms/InstrTestV5Tests.cs`

- [ ] **Step 1: Write the runner and a failing theory**

`BlarggRunner.Run(string romRelativePath, ulong maxCycles)` returns `(byte status, string text)`:
1. `TestRoms.Require`. PRG size from header byte 4 × 16 KB; assert mapper 0 (`(byte6 >> 4) | (byte7 & 0xF0) == 0`), fail with the mapper number otherwise.
2. `BlarggStubBus`; load PRG at `$8000`; if PRG is 16 KB also at `$C000`.
3. `cpu.PowerOn()`; loop `Tick()` until `Peek($6001..$6003) == DE B0 61` and `Peek($6000) != 0x80`, or `maxCycles` reached → fail with "timeout".
4. Status `$81` means "reset requested": fail with that message in M1 (no single needs it).
5. Text: bytes from `$6004` until `0`, ASCII.

`InstrTestV5Tests` `[Trait("Category", "Rom")]`, `[Theory]` with `[InlineData]` for the 16 names `01-basics` … `16-special`: `Assert.Equal(0, status)` with `text` in the message. `maxCycles` 500_000_000 (the singles finish far sooner; the cap only catches hangs).

- [ ] **Step 2: Run the theory**

Run: `dotnet test --filter "FullyQualifiedName~InstrTestV5Tests"`
Expected: most pass if nestest is identical. Likely stragglers: `03-immediate` (`$AB`, `$6B ARR` flags), `07-abs_xy` (`$9C`/`$9E`), `11-stack`, `15-brk`, `16-special`. Read `text` for the failing opcode list, fix in `Cpu.Operations.cs`, add a unit test for each fix.

- [ ] **Step 3: All 16 green; full suite green**

Run: `dotnet test`

- [ ] **Step 4: Commit** — `test: instr_test-v5 singles pass on RAM bus`

---

### Task 14: Done check and docs

- [ ] `dotnet build` with zero warnings (Core has warnings-as-errors; the test project should also be clean).
- [ ] `dotnet test --filter "Category!=Rom"` and `dotnet test --filter "Category=Rom"` both green locally and in CI. The CI ROM cache key includes `fetch.sh`, which changed, so the first run refetches.
- [ ] Prove the fresh-clone promise: move `tests/roms/instr_test-v5` aside, run the ROM filter, expect 16 skipped, move it back.
- [ ] Spec amendments *(Claude may write, after owner approval)*: see the next section.
- [ ] `AGENTS.md`: Milestones entry for 1 mentions the singles; Conventions line about one test class per component allows a folder per component.
- [ ] README: nothing new until `trace` exists in M2.
- [ ] Open the PR from `milestone-1` to `master`.

---

## Deliberately deferred

- OAM DMA stall (`$4014`, 513/514 cycles) lives in the CPU tick path per spec section 2 but arrives in M4 with the PPU's OAM. Leave a `_dmaPending` style hook only if it costs nothing; no code otherwise.
- `Device.Tick()` stays as-is. Wiring `Cpu` into `Device` needs `CpuBus` (M2). `Device.StepInstruction()` arrives with it.
- `cpu_timing_test6`, `instr_misc`, `cpu_interrupts_v2` need the real bus, APU frame IRQ, or PPU NMI. M2 and M3.
- Open bus: unmapped reads return 0 (spec section 4). Nothing to do in M1.

## Spec amendments this plan needs (pending owner approval)

1. **Section 8, milestone 1 row.** "blargg `instr_test-v5` passes with a RAM-only cartridge stub" → "all 16 `instr_test-v5/rom_singles` pass on a RAM-only bus stub (`$2002` reads return `$80`). `all_instrs.nes` requires MMC1 and moves to milestone 6."
2. **Section 5, Trace log.** `CpuTraceEntry` carries CPU registers, opcode bytes, and total CPU cycles. PPU scanline and dot are supplied by the caller of `NestestFormatter` (Device-level sink from M3; computed from cycles in M1 tests).
3. **Section 3, Reset vs power-on.** Note that P is stored without bits 4 and 5; "`P = $34`" describes the value as pushed by PHP, the stored value is `$04`.
4. **Section 7, Layer 2 table.** nestest row: "Force PC to `$C000`" → "point the RAM reset vector at `$C000` and run the real reset sequence".

## Order summary

| Task | Deliverable | Signal |
|---|---|---|
| 1 | fakes | unit |
| 2 | opcode table | unit |
| 3 | reset sequence | unit |
| 4 | formatter | unit |
| 5 | nestest harness | first mismatch line |
| 6 | imm/implied | unit |
| 7 | read modes | unit |
| 8 | write/RMW | unit |
| 9 | control flow | nestest reaches 5004 |
| 10 | unofficial | nestest 8991, `$02`/`$03` zero |
| 11 | interrupts | unit |
| 12 | save/load | unit |
| 13 | blargg singles | 16/16 |
| 14 | docs, CI, PR | green |
