using OddFrame.Core.Processor;
using OddFrame.Core.Tests.Fakes;

namespace OddFrame.Core.Tests.Processor;

public class CpuAddressingTests
{
    [Fact]
    public void Implied_TwoCycles_DummyReadsPcWithoutIncrement()
    {
        var cpu = CpuTestHarness.NewCpuWithProgram(out var bus, 0xEA, 0x55);
        cpu.RunInstruction();
        Assert.Equal(2, bus.Log.Count);
        Assert.Equal(
            [
                ((ushort)0x8000, (byte)0xEA, false), 
                ((ushort)0x8001, (byte)0x55, false)
            ], 
            bus.Log.Select(a => (a.Address, a.Value, a.IsWrite)));
        Assert.Equal((ushort)0x8001, cpu.PC);
    }
    
    [Fact]
    public void Accumulator_TwoCycles_DummyReadsPc()
    {
        var cpu = CpuTestHarness.NewCpuWithProgram(out var bus, 0x0A, 0x55);
        cpu.A = 0x01;
        cpu.RunInstruction();
        Assert.Equal(2, bus.Log.Count);
        Assert.Equal(
            [
                ((ushort)0x8000, (byte)0x0A, false), 
                ((ushort)0x8001, (byte)0x55, false)
            ], 
            bus.Log.Select(a => (a.Address, a.Value, a.IsWrite)));
        Assert.Equal((ushort)0x8001, cpu.PC);
        Assert.Equal((byte)0x02, cpu.A);
    }
    
    [Fact]
    public void Immediate_TwoCycles_ReadsOperand()
    {
        var cpu = CpuTestHarness.NewCpuWithProgram(out var bus, 0xA9, 0x42);
        cpu.RunInstruction();
        Assert.Equal(2, bus.Log.Count);
        Assert.Equal(
            [
                ((ushort)0x8000, (byte)0xA9, false), 
                ((ushort)0x8001, (byte)0x42, false)
            ], 
            bus.Log.Select(a => (a.Address, a.Value, a.IsWrite)));
        Assert.Equal((ushort)0x8002, cpu.PC);
        Assert.Equal((byte)0x42, cpu.A);
    }

    [Fact]
    public void ZeroPage_ThreeCycles()
    {
        var cpu = CpuTestHarness.NewCpuWithProgram(out var bus, 0xA5, 0x10);
        bus.Ram.Load(0x0010, [0x42]);
        cpu.RunInstruction();
        Assert.Equal(3, bus.Log.Count);
        Assert.Equal([(0x8000, false), (0x8001, false), (0x0010, false)], bus.Addresses());
        Assert.Equal((byte)0x42, cpu.A);
        Assert.Equal((ushort)0x8002, cpu.PC);
    }

    [Fact]
    public void ZeroPageX_DummyReadsBase()
    {
        var cpu = CpuTestHarness.NewCpuWithProgram(out var bus, 0xB5, 0x10);
        cpu.X = 0x02;
        bus.Ram.Load(0x0010, [0x99]);
        bus.Ram.Load(0x0012, [0x42]);
        cpu.RunInstruction();
        Assert.Equal(4, bus.Log.Count);
        Assert.Equal([(0x8000, false), (0x8001, false), (0x0010, false), (0x0012, false)], bus.Addresses());
        Assert.Equal((byte)0x42, cpu.A);
        Assert.Equal((ushort)0x8002, cpu.PC);
    }

    [Fact]
    public void ZeroPageX_WrapsInPageZero()
    {
        var cpu = CpuTestHarness.NewCpuWithProgram(out var bus, 0xB5, 0xFF);
        cpu.X = 0x02;
        bus.Ram.Load(0x0001, [0x42]);
        bus.Ram.Load(0x0101, [0x99]);
        cpu.RunInstruction();
        Assert.Equal(4, bus.Log.Count);
        Assert.Equal([(0x8000, false), (0x8001, false), (0x00FF, false), (0x0001, false)], bus.Addresses());
        Assert.Equal((byte)0x42, cpu.A);
    }

    [Fact]
    public void ZeroPageY_LDX_WrapsInPageZero()
    {
        var cpu = CpuTestHarness.NewCpuWithProgram(out var bus, 0xB6, 0xFF);
        cpu.Y = 0x02;
        bus.Ram.Load(0x0001, [0x42]);
        bus.Ram.Load(0x0101, [0x99]);
        cpu.RunInstruction();
        Assert.Equal(4, bus.Log.Count);
        Assert.Equal([(0x8000, false), (0x8001, false), (0x00FF, false), (0x0001, false)], bus.Addresses());
        Assert.Equal((byte)0x42, cpu.X);
    }

    [Fact]
    public void Absolute_FourCycles()
    {
        var cpu = CpuTestHarness.NewCpuWithProgram(out var bus, 0xAD, 0x34, 0x12);
        bus.Ram.Load(0x1234, [0x42]);
        bus.Ram.Load(0x3412, [0x99]);
        cpu.RunInstruction();
        Assert.Equal(4, bus.Log.Count);
        Assert.Equal([(0x8000, false), (0x8001, false), (0x8002, false), (0x1234, false)], bus.Addresses());
        Assert.Equal((byte)0x42, cpu.A);
        Assert.Equal((ushort)0x8003, cpu.PC);
    }

    [Fact]
    public void AbsoluteX_NoCross_FourCycles()
    {
        var cpu = CpuTestHarness.NewCpuWithProgram(out var bus, 0xBD, 0x00, 0x02);
        cpu.X = 0x01;
        bus.Ram.Load(0x0201, [0x42]);
        cpu.RunInstruction();
        Assert.Equal(4, bus.Log.Count);
        Assert.Equal([(0x8000, false), (0x8001, false), (0x8002, false), (0x0201, false)], bus.Addresses());
        Assert.Equal((byte)0x42, cpu.A);
        Assert.Equal((ushort)0x8003, cpu.PC);
    }

    [Fact]
    public void AbsoluteX_Cross_FiveCycles_DummyReadUnfixed()
    {
        var cpu = CpuTestHarness.NewCpuWithProgram(out var bus, 0xBD, 0xFF, 0x02);
        cpu.X = 0x01;
        bus.Ram.Load(0x0200, [0x99]);
        bus.Ram.Load(0x0300, [0x42]);
        cpu.RunInstruction();
        Assert.Equal(5, bus.Log.Count);
        Assert.Equal(
            [(0x8000, false), (0x8001, false), (0x8002, false), (0x0200, false), (0x0300, false)],
            bus.Addresses());
        Assert.Equal((byte)0x42, cpu.A);
        Assert.Equal((ushort)0x8003, cpu.PC);
    }

    [Fact]
    public void AbsoluteY_NoCross_FourCycles()
    {
        var cpu = CpuTestHarness.NewCpuWithProgram(out var bus, 0xB9, 0x00, 0x02);
        cpu.Y = 0x01;
        bus.Ram.Load(0x0201, [0x42]);
        cpu.RunInstruction();
        Assert.Equal(4, bus.Log.Count);
        Assert.Equal([(0x8000, false), (0x8001, false), (0x8002, false), (0x0201, false)], bus.Addresses());
        Assert.Equal((byte)0x42, cpu.A);
    }

    [Fact]
    public void AbsoluteY_Cross_FiveCycles_DummyReadUnfixed()
    {
        var cpu = CpuTestHarness.NewCpuWithProgram(out var bus, 0xB9, 0xFF, 0x02);
        cpu.Y = 0x01;
        bus.Ram.Load(0x0200, [0x99]);
        bus.Ram.Load(0x0300, [0x42]);
        cpu.RunInstruction();
        Assert.Equal(5, bus.Log.Count);
        Assert.Equal(
            [(0x8000, false), (0x8001, false), (0x8002, false), (0x0200, false), (0x0300, false)],
            bus.Addresses());
        Assert.Equal((byte)0x42, cpu.A);
    }

    [Fact]
    public void AbsoluteX_CrossAtFFFF_WrapsToZero()
    {
        var cpu = CpuTestHarness.NewCpuWithProgram(out var bus, 0xBD, 0xFF, 0xFF);
        cpu.X = 0x01;
        bus.Ram.Load(0xFF00, [0x99]);
        bus.Ram.Load(0x0000, [0x42]);
        cpu.RunInstruction();
        Assert.Equal(5, bus.Log.Count);
        Assert.Equal(
            [(0x8000, false), (0x8001, false), (0x8002, false), (0xFF00, false), (0x0000, false)],
            bus.Addresses());
        Assert.Equal((byte)0x42, cpu.A);
    }

    [Fact]
    public void IndirectX_SixCycles()
    {
        var cpu = CpuTestHarness.NewCpuWithProgram(out var bus, 0xA1, 0x20);
        cpu.X = 0x04;
        bus.Ram.Load(0x0020, [0x00, 0x04]);
        bus.Ram.Load(0x0024, [0x00, 0x03]);
        bus.Ram.Load(0x0300, [0x42]);
        bus.Ram.Load(0x0400, [0x99]);
        cpu.RunInstruction();
        Assert.Equal(6, bus.Log.Count);
        Assert.Equal(
            [(0x8000, false), (0x8001, false), (0x0020, false), (0x0024, false), (0x0025, false), (0x0300, false)],
            bus.Addresses());
        Assert.Equal((byte)0x42, cpu.A);
        Assert.Equal((ushort)0x8002, cpu.PC);
    }

    [Fact]
    public void IndirectX_PointerWraps()
    {
        var cpu = CpuTestHarness.NewCpuWithProgram(out var bus, 0xA1, 0xFF);
        cpu.X = 0x01;
        bus.Ram.Load(0x0000, [0x00, 0x03]);
        bus.Ram.Load(0x0100, [0x00, 0x04]);
        bus.Ram.Load(0x0300, [0x42]);
        bus.Ram.Load(0x0400, [0x99]);
        cpu.RunInstruction();
        Assert.Equal(6, bus.Log.Count);
        Assert.Equal(
            [(0x8000, false), (0x8001, false), (0x00FF, false), (0x0000, false), (0x0001, false), (0x0300, false)],
            bus.Addresses());
        Assert.Equal((byte)0x42, cpu.A);
    }

    [Fact]
    public void IndirectX_PointerFF_HighByteFromZero()
    {
        var cpu = CpuTestHarness.NewCpuWithProgram(out var bus, 0xA1, 0xFE);
        cpu.X = 0x01;
        bus.Ram.Load(0x00FF, [0x00]);
        bus.Ram.Load(0x0000, [0x03]);
        bus.Ram.Load(0x0100, [0x04]);
        bus.Ram.Load(0x0300, [0x42]);
        bus.Ram.Load(0x0400, [0x99]);
        cpu.RunInstruction();
        Assert.Equal(6, bus.Log.Count);
        Assert.Equal(
            [(0x8000, false), (0x8001, false), (0x00FE, false), (0x00FF, false), (0x0000, false), (0x0300, false)],
            bus.Addresses());
        Assert.Equal((byte)0x42, cpu.A);
    }

    [Fact]
    public void IndirectY_NoCross_FiveCycles()
    {
        var cpu = CpuTestHarness.NewCpuWithProgram(out var bus, 0xB1, 0x20);
        cpu.Y = 0x01;
        bus.Ram.Load(0x0020, [0x00, 0x03]);
        bus.Ram.Load(0x0301, [0x42]);
        cpu.RunInstruction();
        Assert.Equal(5, bus.Log.Count);
        Assert.Equal(
            [(0x8000, false), (0x8001, false), (0x0020, false), (0x0021, false), (0x0301, false)],
            bus.Addresses());
        Assert.Equal((byte)0x42, cpu.A);
        Assert.Equal((ushort)0x8002, cpu.PC);
    }

    [Fact]
    public void IndirectY_Cross_SixCycles_DummyReadUnfixed()
    {
        var cpu = CpuTestHarness.NewCpuWithProgram(out var bus, 0xB1, 0x20);
        cpu.Y = 0x01;
        bus.Ram.Load(0x0020, [0xFF, 0x02]);
        bus.Ram.Load(0x0200, [0x99]);
        bus.Ram.Load(0x0300, [0x42]);
        cpu.RunInstruction();
        Assert.Equal(6, bus.Log.Count);
        Assert.Equal(
            [(0x8000, false), (0x8001, false), (0x0020, false), (0x0021, false), (0x0200, false), (0x0300, false)],
            bus.Addresses());
        Assert.Equal((byte)0x42, cpu.A);
    }

    [Fact]
    public void IndirectY_PointerFF_HighByteFromZero()
    {
        var cpu = CpuTestHarness.NewCpuWithProgram(out var bus, 0xB1, 0xFF);
        bus.Ram.Load(0x00FF, [0x00]);
        bus.Ram.Load(0x0000, [0x03]);
        bus.Ram.Load(0x0100, [0x04]);
        bus.Ram.Load(0x0300, [0x42]);
        bus.Ram.Load(0x0400, [0x99]);
        cpu.RunInstruction();
        Assert.Equal(5, bus.Log.Count);
        Assert.Equal(
            [(0x8000, false), (0x8001, false), (0x00FF, false), (0x0000, false), (0x0300, false)],
            bus.Addresses());
        Assert.Equal((byte)0x42, cpu.A);
    }

    [Fact]
    public void UnofficialNop_AbsX_Cross_FiveCycles()
    {
        var cpu = CpuTestHarness.NewCpuWithProgram(out var bus, 0x1C, 0xFF, 0x02);
        cpu.X = 0x01;
        cpu.P = StatusFlags.None;
        bus.Ram.Load(0x0300, [0x80]);
        cpu.RunInstruction();
        Assert.Equal(5, bus.Log.Count);
        Assert.Equal(
            [(0x8000, false), (0x8001, false), (0x8002, false), (0x0200, false), (0x0300, false)],
            bus.Addresses());
        Assert.Equal((byte)0x00, cpu.A);
        Assert.Equal((byte)0x01, cpu.X);
        Assert.Equal((byte)0x00, cpu.Y);
        Assert.Equal(StatusFlags.None, cpu.P);
        Assert.Equal((ushort)0x8003, cpu.PC);
    }

    // Stores never read their target. Indexed stores always take the fix-up cycle,
    // a dummy read, because a write to the wrong page cannot be undone.
    [Fact]
    public void STA_ZeroPage_ThreeCycles()
    {
        var cpu = CpuTestHarness.NewCpuWithProgram(out var bus, 0x85, 0x10);
        cpu.A = 0x42;
        cpu.RunInstruction();
        Assert.Equal(3, bus.Log.Count);
        Assert.Equal([(0x8000, false), (0x8001, false), (0x0010, true)], bus.Addresses());
        Assert.Equal([(0x0010, 0x42)], bus.Writes());
        Assert.Equal((ushort)0x8002, cpu.PC);
    }

    [Fact]
    public void STA_ZeroPageX_WrapsInPageZero()
    {
        var cpu = CpuTestHarness.NewCpuWithProgram(out var bus, 0x95, 0xFF);
        cpu.A = 0x42;
        cpu.X = 0x02;
        cpu.RunInstruction();
        Assert.Equal(4, bus.Log.Count);
        Assert.Equal([(0x8000, false), (0x8001, false), (0x00FF, false), (0x0001, true)], bus.Addresses());
        Assert.Equal([(0x0001, 0x42)], bus.Writes());
    }

    // nestest line 4306: STX $80,Y @ 7F
    [Fact]
    public void STX_ZeroPageY_Wraps()
    {
        var cpu = CpuTestHarness.NewCpuWithProgram(out var bus, 0x96, 0x80);
        cpu.X = 0x69;
        cpu.Y = 0xFF;
        cpu.RunInstruction();
        Assert.Equal(4, bus.Log.Count);
        Assert.Equal([(0x8000, false), (0x8001, false), (0x0080, false), (0x007F, true)], bus.Addresses());
        Assert.Equal([(0x007F, 0x69)], bus.Writes());
    }

    [Fact]
    public void STY_Absolute_FourCycles()
    {
        var cpu = CpuTestHarness.NewCpuWithProgram(out var bus, 0x8C, 0x34, 0x12);
        cpu.Y = 0x42;
        cpu.RunInstruction();
        Assert.Equal(4, bus.Log.Count);
        Assert.Equal([(0x8000, false), (0x8001, false), (0x8002, false), (0x1234, true)], bus.Addresses());
        Assert.Equal([(0x1234, 0x42)], bus.Writes());
        Assert.Equal((ushort)0x8003, cpu.PC);
    }

    [Fact]
    public void STA_AbsoluteX_NoCross_FiveCycles_DummyReadsTarget()
    {
        var cpu = CpuTestHarness.NewCpuWithProgram(out var bus, 0x9D, 0x00, 0x02);
        cpu.A = 0x42;
        cpu.X = 0x01;
        cpu.RunInstruction();
        Assert.Equal(5, bus.Log.Count);
        Assert.Equal(
            [(0x8000, false), (0x8001, false), (0x8002, false), (0x0201, false), (0x0201, true)],
            bus.Addresses());
        Assert.Equal([(0x0201, 0x42)], bus.Writes());
        Assert.Equal((ushort)0x8003, cpu.PC);
    }

    [Fact]
    public void STA_AbsoluteX_Cross_FiveCycles_DummyReadsUnfixed()
    {
        var cpu = CpuTestHarness.NewCpuWithProgram(out var bus, 0x9D, 0xFF, 0x02);
        cpu.A = 0x42;
        cpu.X = 0x01;
        bus.Ram.Load(0x0200, [0x99]);
        cpu.RunInstruction();
        Assert.Equal(5, bus.Log.Count);
        Assert.Equal(
            [(0x8000, false), (0x8001, false), (0x8002, false), (0x0200, false), (0x0300, true)],
            bus.Addresses());
        Assert.Equal([(0x0300, 0x42)], bus.Writes());
        Assert.Equal((byte)0x99, bus.Ram.Peek(0x0200));
    }

    [Fact]
    public void STA_AbsoluteY_Cross_FiveCycles()
    {
        var cpu = CpuTestHarness.NewCpuWithProgram(out var bus, 0x99, 0xFF, 0x02);
        cpu.A = 0x42;
        cpu.Y = 0x01;
        cpu.RunInstruction();
        Assert.Equal(5, bus.Log.Count);
        Assert.Equal(
            [(0x8000, false), (0x8001, false), (0x8002, false), (0x0200, false), (0x0300, true)],
            bus.Addresses());
        Assert.Equal([(0x0300, 0x42)], bus.Writes());
    }

    [Fact]
    public void STA_IndirectX_SixCycles()
    {
        var cpu = CpuTestHarness.NewCpuWithProgram(out var bus, 0x81, 0x20);
        cpu.A = 0x42;
        cpu.X = 0x04;
        bus.Ram.Load(0x0020, [0x00, 0x04]);
        bus.Ram.Load(0x0024, [0x00, 0x03]);
        cpu.RunInstruction();
        Assert.Equal(6, bus.Log.Count);
        Assert.Equal(
            [(0x8000, false), (0x8001, false), (0x0020, false), (0x0024, false), (0x0025, false), (0x0300, true)],
            bus.Addresses());
        Assert.Equal([(0x0300, 0x42)], bus.Writes());
        Assert.Equal((ushort)0x8002, cpu.PC);
    }

    [Fact]
    public void STA_IndirectY_NoCross_SixCycles()
    {
        var cpu = CpuTestHarness.NewCpuWithProgram(out var bus, 0x91, 0x20);
        cpu.A = 0x42;
        cpu.Y = 0x01;
        bus.Ram.Load(0x0020, [0x00, 0x03]);
        cpu.RunInstruction();
        Assert.Equal(6, bus.Log.Count);
        Assert.Equal(
            [(0x8000, false), (0x8001, false), (0x0020, false), (0x0021, false), (0x0301, false), (0x0301, true)],
            bus.Addresses());
        Assert.Equal([(0x0301, 0x42)], bus.Writes());
    }

    [Fact]
    public void STA_IndirectY_Cross_SixCycles()
    {
        var cpu = CpuTestHarness.NewCpuWithProgram(out var bus, 0x91, 0x20);
        cpu.A = 0x42;
        cpu.Y = 0x01;
        bus.Ram.Load(0x0020, [0xFF, 0x02]);
        bus.Ram.Load(0x0200, [0x99]);
        cpu.RunInstruction();
        Assert.Equal(6, bus.Log.Count);
        Assert.Equal(
            [(0x8000, false), (0x8001, false), (0x0020, false), (0x0021, false), (0x0200, false), (0x0300, true)],
            bus.Addresses());
        Assert.Equal([(0x0300, 0x42)], bus.Writes());
        Assert.Equal((byte)0x99, bus.Ram.Peek(0x0200));
    }

    // RMW: read, write the old value back while the ALU works, then write the new value.
    [Fact]
    public void INC_ZeroPage_WritesOldThenNew()
    {
        var cpu = CpuTestHarness.NewCpuWithProgram(out var bus, 0xE6, 0x10);
        bus.Ram.Load(0x0010, [0x41]);
        cpu.RunInstruction();
        Assert.Equal(5, bus.Log.Count);
        Assert.Equal(
            [(0x8000, false), (0x8001, false), (0x0010, false), (0x0010, true), (0x0010, true)],
            bus.Addresses());
        Assert.Equal([(0x0010, 0x41), (0x0010, 0x42)], bus.Writes());
        Assert.Equal((byte)0x42, bus.Ram.Peek(0x0010));
        Assert.Equal((ushort)0x8002, cpu.PC);
    }

    [Fact]
    public void INC_ZeroPageX_WrapsInPageZero()
    {
        var cpu = CpuTestHarness.NewCpuWithProgram(out var bus, 0xF6, 0xFF);
        cpu.X = 0x02;
        bus.Ram.Load(0x0001, [0x41]);
        bus.Ram.Load(0x0101, [0x99]);
        cpu.RunInstruction();
        Assert.Equal(6, bus.Log.Count);
        Assert.Equal(
            [(0x8000, false), (0x8001, false), (0x00FF, false), (0x0001, false), (0x0001, true), (0x0001, true)],
            bus.Addresses());
        Assert.Equal([(0x0001, 0x41), (0x0001, 0x42)], bus.Writes());
        Assert.Equal((byte)0x99, bus.Ram.Peek(0x0101));
    }

    [Fact]
    public void INC_Absolute_SixCycles()
    {
        var cpu = CpuTestHarness.NewCpuWithProgram(out var bus, 0xEE, 0x34, 0x12);
        bus.Ram.Load(0x1234, [0x41]);
        cpu.RunInstruction();
        Assert.Equal(6, bus.Log.Count);
        Assert.Equal(
            [(0x8000, false), (0x8001, false), (0x8002, false), (0x1234, false), (0x1234, true), (0x1234, true)],
            bus.Addresses());
        Assert.Equal([(0x1234, 0x41), (0x1234, 0x42)], bus.Writes());
        Assert.Equal((ushort)0x8003, cpu.PC);
    }

    [Fact]
    public void INC_AbsoluteX_NoCross_SevenCycles()
    {
        var cpu = CpuTestHarness.NewCpuWithProgram(out var bus, 0xFE, 0x00, 0x02);
        cpu.X = 0x01;
        bus.Ram.Load(0x0201, [0x41]);
        cpu.RunInstruction();
        Assert.Equal(7, bus.Log.Count);
        Assert.Equal(
            [
                (0x8000, false), (0x8001, false), (0x8002, false),
                (0x0201, false), (0x0201, false), (0x0201, true), (0x0201, true)
            ],
            bus.Addresses());
        Assert.Equal([(0x0201, 0x41), (0x0201, 0x42)], bus.Writes());
    }

    [Fact]
    public void INC_AbsoluteX_Cross_SevenCycles()
    {
        var cpu = CpuTestHarness.NewCpuWithProgram(out var bus, 0xFE, 0xFF, 0x02);
        cpu.X = 0x01;
        bus.Ram.Load(0x0200, [0x99]);
        bus.Ram.Load(0x0300, [0x41]);
        cpu.RunInstruction();
        Assert.Equal(7, bus.Log.Count);
        Assert.Equal(
            [
                (0x8000, false), (0x8001, false), (0x8002, false),
                (0x0200, false), (0x0300, false), (0x0300, true), (0x0300, true)
            ],
            bus.Addresses());
        Assert.Equal([(0x0300, 0x41), (0x0300, 0x42)], bus.Writes());
        Assert.Equal((byte)0x99, bus.Ram.Peek(0x0200));
    }

    // Modify must run once: a second ROL would turn $81 into $02.
    [Fact]
    public void ROL_ZeroPage_ModifiesOnce()
    {
        var cpu = CpuTestHarness.NewCpuWithProgram(out var bus, 0x26, 0x10);
        cpu.P = StatusFlags.C;
        bus.Ram.Load(0x0010, [0x40]);
        cpu.RunInstruction();
        Assert.Equal([(0x0010, 0x40), (0x0010, 0x81)], bus.Writes());
        Assert.Equal(StatusFlags.N, cpu.P);
    }

    // Every official opcode of a kind, from the table. The cycle counts live here,
    // independent of the CPU code.
    public static TheoryData<byte> OfficialReads => [.. OfficialOpcodes(InstructionKind.Read, _ => true)];

    public static TheoryData<byte> OfficialIndexedReads => [.. OfficialOpcodes(InstructionKind.Read, IsIndexed)];

    public static TheoryData<byte> OfficialWrites => [.. OfficialOpcodes(InstructionKind.Write, _ => true)];

    public static TheoryData<byte> OfficialIndexedWrites => [.. OfficialOpcodes(InstructionKind.Write, IsIndexed)];

    public static TheoryData<byte> OfficialModifies =>
        [.. OfficialOpcodes(InstructionKind.ReadModifyWrite, _ => true)];

    public static TheoryData<byte> OfficialIndexedModifies =>
        [.. OfficialOpcodes(InstructionKind.ReadModifyWrite, IsIndexed)];

    [Theory]
    [MemberData(nameof(OfficialReads))]
    public void OfficialRead_NoCross_BaseCycles(byte op)
    {
        var info = OpcodeTable.Entries[op];
        var cpu = CpuTestHarness.NewCpuWithProgram(out _, op, 0x10, 0x00);
        cpu.X = 0x00;
        cpu.Y = 0x00;
        cpu.P = StatusFlags.None;
        Assert.Equal(BaseCycles(info.Mode), cpu.RunInstruction());
        Assert.Equal((ushort)(0x8000 + info.Length), cpu.PC);
    }

    [Theory]
    [MemberData(nameof(OfficialIndexedReads))]
    public void OfficialRead_PageCross_AddsOneCycle(byte op)
    {
        var info = OpcodeTable.Entries[op];
        var cpu = NewCrossingCpu(op, out _);
        Assert.Equal(BaseCycles(info.Mode) + 1, cpu.RunInstruction());
        Assert.Equal((ushort)(0x8000 + info.Length), cpu.PC);
    }

    [Theory]
    [MemberData(nameof(OfficialWrites))]
    public void OfficialWrite_Cycles_WritesOnceLast(byte op)
    {
        var info = OpcodeTable.Entries[op];
        var cpu = CpuTestHarness.NewCpuWithProgram(out var bus, op, 0x10, 0x00);
        cpu.X = 0x00;
        cpu.Y = 0x00;
        Assert.Equal(WriteCycles(info.Mode), cpu.RunInstruction());
        Assert.Single(bus.Writes());
        Assert.True(bus.Log[^1].IsWrite);
        Assert.Equal((ushort)(0x8000 + info.Length), cpu.PC);
    }

    [Theory]
    [MemberData(nameof(OfficialIndexedWrites))]
    public void OfficialWrite_PageCross_SameCycles(byte op)
    {
        var info = OpcodeTable.Entries[op];
        var cpu = NewCrossingCpu(op, out var bus);
        Assert.Equal(WriteCycles(info.Mode), cpu.RunInstruction());
        Assert.Equal([(0x0200, false), (0x0300, true)], bus.Addresses()[^2..]);
    }

    [Theory]
    [MemberData(nameof(OfficialModifies))]
    public void OfficialModify_Cycles_ReadsThenWritesTwice(byte op)
    {
        var info = OpcodeTable.Entries[op];
        var cpu = CpuTestHarness.NewCpuWithProgram(out var bus, op, 0x10, 0x00);
        cpu.X = 0x00;
        Assert.Equal(ModifyCycles(info.Mode), cpu.RunInstruction());
        var target = bus.Log[^1].Address;
        Assert.Equal([(target, false), (target, true), (target, true)], bus.Addresses()[^3..]);
        Assert.Equal(2, bus.Writes().Length);
        Assert.Equal((ushort)(0x8000 + info.Length), cpu.PC);
    }

    [Theory]
    [MemberData(nameof(OfficialIndexedModifies))]
    public void OfficialModify_PageCross_SameCycles(byte op)
    {
        var info = OpcodeTable.Entries[op];
        var cpu = NewCrossingCpu(op, out var bus);
        Assert.Equal(ModifyCycles(info.Mode), cpu.RunInstruction());
        Assert.Equal(
            [(0x0200, false), (0x0300, false), (0x0300, true), (0x0300, true)],
            bus.Addresses()[^4..]);
    }

    // abs,X / abs,Y: base $02FF + 1. (zp),Y: pointer $10 -> $02FF, + 1. All land on $0300.
    private static Cpu NewCrossingCpu(byte op, out RecordingBus bus)
    {
        var cpu = OpcodeTable.Entries[op].Mode == AddressingMode.IndirectY
            ? CpuTestHarness.NewCpuWithProgram(out bus, op, 0x10)
            : CpuTestHarness.NewCpuWithProgram(out bus, op, 0xFF, 0x02);
        bus.Ram.Load(0x0010, [0xFF, 0x02]);
        cpu.X = 0x01;
        cpu.Y = 0x01;
        cpu.P = StatusFlags.None;
        return cpu;
    }

    private static bool IsIndexed(AddressingMode mode) =>
        mode is AddressingMode.AbsoluteX or AddressingMode.AbsoluteY or AddressingMode.IndirectY;

    private static IEnumerable<byte> OfficialOpcodes(InstructionKind kind, Func<AddressingMode, bool> modeFilter) =>
        Opcodes(kind, unofficial: false, modeFilter);

    private static IEnumerable<byte> Opcodes(InstructionKind kind, bool unofficial, Func<AddressingMode, bool> modeFilter) =>
        Enumerable.Range(0, 256)
            .Select(op => (byte)op)
            .Where(op => OpcodeTable.Entries[op] is var info
                         && info.Unofficial == unofficial
                         && info.Kind == kind
                         && info.Mode != AddressingMode.Immediate
                         && modeFilter(info.Mode));

    private static int BaseCycles(AddressingMode mode) => mode switch
    {
        AddressingMode.ZeroPage => 3,
        AddressingMode.ZeroPageX or AddressingMode.ZeroPageY => 4,
        AddressingMode.Absolute or AddressingMode.AbsoluteX or AddressingMode.AbsoluteY => 4,
        AddressingMode.IndirectX => 6,
        AddressingMode.IndirectY => 5,
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null),
    };

    private static int WriteCycles(AddressingMode mode) => mode switch
    {
        AddressingMode.ZeroPage => 3,
        AddressingMode.ZeroPageX or AddressingMode.ZeroPageY => 4,
        AddressingMode.Absolute => 4,
        AddressingMode.AbsoluteX or AddressingMode.AbsoluteY => 5,
        AddressingMode.IndirectX or AddressingMode.IndirectY => 6,
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null),
    };

    private static int ModifyCycles(AddressingMode mode) => mode switch
    {
        AddressingMode.ZeroPage => 5,
        AddressingMode.ZeroPageX or AddressingMode.Absolute => 6,
        AddressingMode.AbsoluteX or AddressingMode.AbsoluteY => 7,
        AddressingMode.IndirectX or AddressingMode.IndirectY => 8,
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null),
    };

    // SHY SHX AHX TAS store reg & (H + 1); on a page cross the value replaces the address's high byte.
    [Fact]
    public void SHY_StoresYAndHighPlusOne()
    {
        var cpu = CpuTestHarness.NewCpuWithProgram(out var bus, 0x9C, 0x00, 0x02);
        cpu.X = 0x00;
        cpu.Y = 0xFF;
        Assert.Equal(5, cpu.RunInstruction());
        Assert.Equal(
            [(0x8000, false), (0x8001, false), (0x8002, false), (0x0200, false), (0x0200, true)],
            bus.Addresses());
        Assert.Equal([(0x0200, 0x03)], bus.Writes());
    }

    [Fact]
    public void SHY_PageCross_ValueReplacesHighByte()
    {
        var cpu = CpuTestHarness.NewCpuWithProgram(out var bus, 0x9C, 0xFF, 0x02);
        cpu.X = 0x01;
        cpu.Y = 0xF0;
        Assert.Equal(5, cpu.RunInstruction());
        Assert.Equal(
            [(0x8000, false), (0x8001, false), (0x8002, false), (0x0200, false), (0x0000, true)],
            bus.Addresses());
        Assert.Equal([(0x0000, 0x00)], bus.Writes());
    }

    [Fact]
    public void SHX_StoresXAndHighPlusOne()
    {
        var cpu = CpuTestHarness.NewCpuWithProgram(out var bus, 0x9E, 0x00, 0x02);
        cpu.X = 0xFF;
        cpu.Y = 0x00;
        Assert.Equal(5, cpu.RunInstruction());
        Assert.Equal([(0x0200, 0x03)], bus.Writes());
    }

    [Fact]
    public void SHX_PageCross_ValueReplacesHighByte()
    {
        var cpu = CpuTestHarness.NewCpuWithProgram(out var bus, 0x9E, 0xFF, 0x02);
        cpu.X = 0x05;
        cpu.Y = 0x01;
        Assert.Equal(5, cpu.RunInstruction());
        Assert.Equal([(0x0100, 0x01)], bus.Writes());
    }

    [Fact]
    public void TAS_SetsS_StoresSAndHighPlusOne()
    {
        var cpu = CpuTestHarness.NewCpuWithProgram(out var bus, 0x9B, 0x00, 0x02);
        cpu.A = 0xF3;
        cpu.X = 0x3F;
        cpu.Y = 0x00;
        Assert.Equal(5, cpu.RunInstruction());
        Assert.Equal([(0x0200, 0x03)], bus.Writes());
        Assert.Equal((byte)0x33, cpu.S);
    }

    [Fact]
    public void AHX_AbsoluteY_StoresAAndXAndHighPlusOne()
    {
        var cpu = CpuTestHarness.NewCpuWithProgram(out var bus, 0x9F, 0x00, 0x02);
        cpu.A = 0xFF;
        cpu.X = 0x0E;
        cpu.Y = 0x00;
        Assert.Equal(5, cpu.RunInstruction());
        Assert.Equal([(0x0200, 0x02)], bus.Writes());
    }

    [Fact]
    public void AHX_IndirectY_SixCycles()
    {
        var cpu = CpuTestHarness.NewCpuWithProgram(out var bus, 0x93, 0x20);
        cpu.A = 0xFF;
        cpu.X = 0xFF;
        cpu.Y = 0x01;
        bus.Ram.Load(0x0020, [0x00, 0x02]);
        Assert.Equal(6, cpu.RunInstruction());
        Assert.Equal([(0x0201, 0x03)], bus.Writes());
    }

    [Fact]
    public void DCP_IndirectX_EightCycles()
    {
        var cpu = CpuTestHarness.NewCpuWithProgram(out var bus, 0xC3, 0x20);
        cpu.X = 0x04;
        bus.Ram.Load(0x0024, [0x00, 0x03]);
        bus.Ram.Load(0x0300, [0x43]);
        Assert.Equal(8, cpu.RunInstruction());
        Assert.Equal(
            [
                (0x8000, false), (0x8001, false), (0x0020, false), (0x0024, false), (0x0025, false),
                (0x0300, false), (0x0300, true), (0x0300, true)
            ],
            bus.Addresses());
        Assert.Equal([(0x0300, 0x43), (0x0300, 0x42)], bus.Writes());
    }

    [Fact]
    public void DCP_IndirectY_Cross_EightCycles()
    {
        var cpu = CpuTestHarness.NewCpuWithProgram(out var bus, 0xD3, 0x20);
        cpu.Y = 0x01;
        bus.Ram.Load(0x0020, [0xFF, 0x02]);
        bus.Ram.Load(0x0200, [0x99]);
        bus.Ram.Load(0x0300, [0x43]);
        Assert.Equal(8, cpu.RunInstruction());
        Assert.Equal(
            [
                (0x8000, false), (0x8001, false), (0x0020, false), (0x0021, false), (0x0200, false),
                (0x0300, false), (0x0300, true), (0x0300, true)
            ],
            bus.Addresses());
        Assert.Equal([(0x0300, 0x43), (0x0300, 0x42)], bus.Writes());
        Assert.Equal((byte)0x99, bus.Ram.Peek(0x0200));
    }

    [Fact]
    public void DCP_AbsoluteY_NoCross_SevenCycles()
    {
        var cpu = CpuTestHarness.NewCpuWithProgram(out var bus, 0xDB, 0x00, 0x02);
        cpu.Y = 0x01;
        bus.Ram.Load(0x0201, [0x43]);
        Assert.Equal(7, cpu.RunInstruction());
        Assert.Equal(
            [
                (0x8000, false), (0x8001, false), (0x8002, false),
                (0x0201, false), (0x0201, false), (0x0201, true), (0x0201, true)
            ],
            bus.Addresses());
        Assert.Equal([(0x0201, 0x43), (0x0201, 0x42)], bus.Writes());
    }

    [Fact]
    public void LAX_AbsoluteY_Cross_FiveCycles()
    {
        var cpu = CpuTestHarness.NewCpuWithProgram(out var bus, 0xBF, 0xFF, 0x02);
        cpu.Y = 0x01;
        bus.Ram.Load(0x0200, [0x99]);
        bus.Ram.Load(0x0300, [0x42]);
        Assert.Equal(5, cpu.RunInstruction());
        Assert.Equal(
            [(0x8000, false), (0x8001, false), (0x8002, false), (0x0200, false), (0x0300, false)],
            bus.Addresses());
        Assert.Equal((byte)0x42, cpu.A);
        Assert.Equal((byte)0x42, cpu.X);
    }

    public static TheoryData<byte> UnofficialModifies =>
        [.. Opcodes(InstructionKind.ReadModifyWrite, unofficial: true, _ => true)];

    [Theory]
    [MemberData(nameof(UnofficialModifies))]
    public void UnofficialModify_Cycles_ReadsThenWritesTwice(byte op)
    {
        var info = OpcodeTable.Entries[op];
        var cpu = CpuTestHarness.NewCpuWithProgram(out var bus, op, 0x10, 0x00);
        cpu.X = 0x00;
        cpu.Y = 0x00;
        Assert.Equal(ModifyCycles(info.Mode), cpu.RunInstruction());
        var target = bus.Log[^1].Address;
        Assert.Equal([(target, false), (target, true), (target, true)], bus.Addresses()[^3..]);
        Assert.Equal(2, bus.Writes().Length);
        Assert.Equal((ushort)(0x8000 + info.Length), cpu.PC);
    }

    // Completeness check: every opcode except JAM runs to its end without throwing.
    public static TheoryData<byte> AllExceptJam =>
    [
        .. Enumerable.Range(0, 256)
            .Select(op => (byte)op)
            .Where(op => OpcodeTable.Entries[op].Op != Operation.Jam)
    ];

    [Theory]
    [MemberData(nameof(AllExceptJam))]
    public void AllOpcodes_Execute(byte op)
    {
        var cpu = CpuTestHarness.NewCpuWithProgram(out var bus, op, 0x10, 0x00);
        bus.Ram.Load(0xFFFE, [0x00, 0x90]);
        cpu.X = 0x00;
        cpu.Y = 0x00;
        var cycles = cpu.RunInstruction();
        Assert.InRange(cycles, 2, 8);
    }
}
