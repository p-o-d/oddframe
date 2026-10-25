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

    // Every official memory-mode read opcode from the table. The cycle counts live here,
    // independent of the CPU code.
    public static TheoryData<byte> OfficialReads => [.. OfficialReadOpcodes(_ => true)];

    public static TheoryData<byte> OfficialIndexedReads =>
    [
        .. OfficialReadOpcodes(mode =>
            mode is AddressingMode.AbsoluteX or AddressingMode.AbsoluteY or AddressingMode.IndirectY)
    ];

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
        var cpu = info.Mode == AddressingMode.IndirectY
            ? CpuTestHarness.NewCpuWithProgram(out var bus, op, 0x10)
            : CpuTestHarness.NewCpuWithProgram(out bus, op, 0xFF, 0x02);
        bus.Ram.Load(0x0010, [0xFF, 0x02]);
        cpu.X = 0x01;
        cpu.Y = 0x01;
        cpu.P = StatusFlags.None;
        Assert.Equal(BaseCycles(info.Mode) + 1, cpu.RunInstruction());
        Assert.Equal((ushort)(0x8000 + info.Length), cpu.PC);
    }

    private static IEnumerable<byte> OfficialReadOpcodes(Func<AddressingMode, bool> modeFilter) =>
        Enumerable.Range(0, 256)
            .Select(op => (byte)op)
            .Where(op => OpcodeTable.Entries[op] is { Kind: InstructionKind.Read, Unofficial: false } info
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
}