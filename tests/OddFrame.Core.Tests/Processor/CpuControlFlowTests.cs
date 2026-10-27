using OddFrame.Core.Processor;
using OddFrame.Core.Tests.Fakes;

namespace OddFrame.Core.Tests.Processor;

public class CpuControlFlowTests
{
    private const StatusFlags All =
        StatusFlags.C | StatusFlags.Z | StatusFlags.I | StatusFlags.D | StatusFlags.V | StatusFlags.N;

    private static Cpu NewCpuAt(out RecordingBus bus, ushort origin, params byte[] program)
    {
        var cpu = CpuTestHarness.NewCpu(out bus, origin);
        bus.Ram.Load(origin, program);
        return cpu;
    }

    [Fact]
    public void Branch_NotTaken_TwoCycles()
    {
        var cpu = CpuTestHarness.NewCpuWithProgram(out var bus, 0xB0, 0x05);
        cpu.P = StatusFlags.None;
        Assert.Equal(2, cpu.RunInstruction());
        Assert.Equal([(0x8000, false), (0x8001, false)], bus.Addresses());
        Assert.Equal((ushort)0x8002, cpu.PC);
    }

    [Fact]
    public void Branch_Taken_ThreeCycles_DummyReadsNextOpcode()
    {
        var cpu = CpuTestHarness.NewCpuWithProgram(out var bus, 0xB0, 0x05);
        cpu.P = StatusFlags.C;
        Assert.Equal(3, cpu.RunInstruction());
        Assert.Equal([(0x8000, false), (0x8001, false), (0x8002, false)], bus.Addresses());
        Assert.Equal((ushort)0x8007, cpu.PC);
    }

    // The unfixed PC is old high byte : new low byte, $80:04.
    [Fact]
    public void Branch_TakenForwardCross_FourCycles_DummyReadsUnfixedPc()
    {
        var cpu = NewCpuAt(out var bus, 0x80FD, 0xB0, 0x05);
        cpu.P = StatusFlags.C;
        Assert.Equal(4, cpu.RunInstruction());
        Assert.Equal([(0x80FD, false), (0x80FE, false), (0x80FF, false), (0x8004, false)], bus.Addresses());
        Assert.Equal((ushort)0x8104, cpu.PC);
    }

    // An unsigned offset would land on $81F2 in three cycles.
    [Fact]
    public void Branch_TakenBackwardCross_FourCycles()
    {
        var cpu = NewCpuAt(out var bus, 0x8100, 0x90, 0xF0);
        cpu.P = StatusFlags.None;
        Assert.Equal(4, cpu.RunInstruction());
        Assert.Equal([(0x8100, false), (0x8101, false), (0x8102, false), (0x81F2, false)], bus.Addresses());
        Assert.Equal((ushort)0x80F2, cpu.PC);
    }

    [Fact]
    public void Branch_TakenBackward_NoCross_ThreeCycles()
    {
        var cpu = NewCpuAt(out var bus, 0x8010, 0xD0, 0xFC);
        cpu.P = StatusFlags.None;
        Assert.Equal(3, cpu.RunInstruction());
        Assert.Equal([(0x8010, false), (0x8011, false), (0x8012, false)], bus.Addresses());
        Assert.Equal((ushort)0x800E, cpu.PC);
    }

    // Each branch twice: its flag in the "taken" state with every other flag set as a decoy,
    // then only its own flag flipped.
    [Theory]
    [InlineData(0x10, All & ~StatusFlags.N, true)]
    [InlineData(0x10, StatusFlags.N, false)]
    [InlineData(0x30, StatusFlags.N, true)]
    [InlineData(0x30, All & ~StatusFlags.N, false)]
    [InlineData(0x50, All & ~StatusFlags.V, true)]
    [InlineData(0x50, StatusFlags.V, false)]
    [InlineData(0x70, StatusFlags.V, true)]
    [InlineData(0x70, All & ~StatusFlags.V, false)]
    [InlineData(0x90, All & ~StatusFlags.C, true)]
    [InlineData(0x90, StatusFlags.C, false)]
    [InlineData(0xB0, StatusFlags.C, true)]
    [InlineData(0xB0, All & ~StatusFlags.C, false)]
    [InlineData(0xD0, All & ~StatusFlags.Z, true)]
    [InlineData(0xD0, StatusFlags.Z, false)]
    [InlineData(0xF0, StatusFlags.Z, true)]
    [InlineData(0xF0, All & ~StatusFlags.Z, false)]
    public void Branch_Conditions(byte op, StatusFlags p, bool taken)
    {
        var cpu = CpuTestHarness.NewCpuWithProgram(out _, op, 0x10);
        cpu.P = p;
        Assert.Equal(taken ? 3 : 2, cpu.RunInstruction());
        Assert.Equal(taken ? (ushort)0x8012 : (ushort)0x8002, cpu.PC);
        Assert.Equal(p, cpu.P);
    }

    [Fact]
    public void JMP_Absolute_ThreeCycles()
    {
        var cpu = CpuTestHarness.NewCpuWithProgram(out var bus, 0x4C, 0x34, 0x12);
        Assert.Equal(3, cpu.RunInstruction());
        Assert.Equal([(0x8000, false), (0x8001, false), (0x8002, false)], bus.Addresses());
        Assert.Equal((ushort)0x1234, cpu.PC);
    }

    [Fact]
    public void JMP_Indirect_FiveCycles()
    {
        var cpu = CpuTestHarness.NewCpuWithProgram(out var bus, 0x6C, 0x00, 0x02);
        bus.Ram.Load(0x0200, [0x34, 0x12]);
        Assert.Equal(5, cpu.RunInstruction());
        Assert.Equal(
            [(0x8000, false), (0x8001, false), (0x8002, false), (0x0200, false), (0x0201, false)],
            bus.Addresses());
        Assert.Equal((ushort)0x1234, cpu.PC);
    }

    // nestest line 3348: JMP ($02FF) = 0300
    [Fact]
    public void JMP_Indirect_PageWrapBug()
    {
        var cpu = CpuTestHarness.NewCpuWithProgram(out var bus, 0x6C, 0xFF, 0x02);
        bus.Ram.Load(0x02FF, [0x00]);
        bus.Ram.Load(0x0200, [0x03]);
        bus.Ram.Load(0x0300, [0x04]);
        Assert.Equal(5, cpu.RunInstruction());
        Assert.Equal(
            [(0x8000, false), (0x8001, false), (0x8002, false), (0x02FF, false), (0x0200, false)],
            bus.Addresses());
        Assert.Equal((ushort)0x0300, cpu.PC);
    }

    // The pushed address is $8002, the JSR's own last byte. The target's high byte is fetched last.
    [Fact]
    public void JSR_PushesReturnMinusOne_SixCycles()
    {
        var cpu = CpuTestHarness.NewCpuWithProgram(out var bus, 0x20, 0x00, 0x90);
        cpu.S = 0xFD;
        Assert.Equal(6, cpu.RunInstruction());
        Assert.Equal(
            [(0x8000, false), (0x8001, false), (0x01FD, false), (0x01FD, true), (0x01FC, true), (0x8002, false)],
            bus.Addresses());
        Assert.Equal([(0x01FD, 0x80), (0x01FC, 0x02)], bus.Writes());
        Assert.Equal((ushort)0x9000, cpu.PC);
        Assert.Equal((byte)0xFB, cpu.S);
    }

    [Fact]
    public void RTS_PullsAndIncrements_SixCycles()
    {
        var cpu = CpuTestHarness.NewCpuWithProgram(out var bus, 0x60);
        cpu.S = 0xFB;
        bus.Ram.Load(0x01FC, [0x02, 0x80]);
        Assert.Equal(6, cpu.RunInstruction());
        Assert.Equal(
            [(0x8000, false), (0x8001, false), (0x01FB, false), (0x01FC, false), (0x01FD, false), (0x8002, false)],
            bus.Addresses());
        Assert.Equal((ushort)0x8003, cpu.PC);
        Assert.Equal((byte)0xFD, cpu.S);
    }

    [Fact]
    public void JSR_RTS_RoundTrip()
    {
        var cpu = CpuTestHarness.NewCpuWithProgram(out var bus, 0x20, 0x00, 0x90);
        bus.Ram.Load(0x9000, [0x60]);
        cpu.RunInstruction();
        cpu.RunInstruction();
        Assert.Equal((ushort)0x8003, cpu.PC);
        Assert.Equal((byte)0xFD, cpu.S);
    }

    [Fact]
    public void RTI_PullsPThenPc_SixCycles()
    {
        var cpu = CpuTestHarness.NewCpuWithProgram(out var bus, 0x40);
        cpu.S = 0xFA;
        bus.Ram.Load(0x01FB, [0xC3, 0x34, 0x12]);
        Assert.Equal(6, cpu.RunInstruction());
        Assert.Equal(
            [(0x8000, false), (0x8001, false), (0x01FA, false), (0x01FB, false), (0x01FC, false), (0x01FD, false)],
            bus.Addresses());
        Assert.Equal((ushort)0x1234, cpu.PC);
        Assert.Equal((byte)0xFD, cpu.S);
        Assert.Equal((StatusFlags)0xC3, cpu.P);
    }

    [Fact]
    public void RTI_IgnoresBitsFourAndFive()
    {
        var cpu = CpuTestHarness.NewCpuWithProgram(out var bus, 0x40);
        cpu.S = 0xFA;
        bus.Ram.Load(0x01FB, [0xFF, 0x34, 0x12]);
        cpu.RunInstruction();
        Assert.Equal((StatusFlags)0xCF, cpu.P);
    }

    [Fact]
    public void PHA_ThreeCycles()
    {
        var cpu = CpuTestHarness.NewCpuWithProgram(out var bus, 0x48);
        cpu.A = 0x42;
        cpu.S = 0xFD;
        Assert.Equal(3, cpu.RunInstruction());
        Assert.Equal([(0x8000, false), (0x8001, false), (0x01FD, true)], bus.Addresses());
        Assert.Equal([(0x01FD, 0x42)], bus.Writes());
        Assert.Equal((byte)0xFC, cpu.S);
    }

    [Fact]
    public void PHP_PushesWithBAndUSet()
    {
        var cpu = CpuTestHarness.NewCpuWithProgram(out var bus, 0x08);
        cpu.P = StatusFlags.C;
        cpu.S = 0xFD;
        Assert.Equal(3, cpu.RunInstruction());
        Assert.Equal([(0x01FD, 0x31)], bus.Writes());
        Assert.Equal(StatusFlags.C, cpu.P);
        Assert.Equal((byte)0xFC, cpu.S);
    }

    [Theory]
    [InlineData(0x00, StatusFlags.None, StatusFlags.Z)]
    [InlineData(0x80, StatusFlags.None, StatusFlags.N)]
    [InlineData(0x01, StatusFlags.Z | StatusFlags.N, StatusFlags.None)]
    public void PLA_FourCycles_SetsZN(byte value, StatusFlags before, StatusFlags expectedP)
    {
        var cpu = CpuTestHarness.NewCpuWithProgram(out var bus, 0x68);
        cpu.S = 0xFC;
        cpu.P = before;
        bus.Ram.Load(0x01FD, [value]);
        Assert.Equal(4, cpu.RunInstruction());
        Assert.Equal([(0x8000, false), (0x8001, false), (0x01FC, false), (0x01FD, false)], bus.Addresses());
        Assert.Equal(value, cpu.A);
        Assert.Equal(expectedP, cpu.P);
        Assert.Equal((byte)0xFD, cpu.S);
    }

    [Fact]
    public void PLP_IgnoresBitsFourAndFive()
    {
        var cpu = CpuTestHarness.NewCpuWithProgram(out var bus, 0x28);
        cpu.S = 0xFC;
        bus.Ram.Load(0x01FD, [0xFF]);
        Assert.Equal(4, cpu.RunInstruction());
        Assert.Equal((StatusFlags)0xCF, cpu.P);
        Assert.Equal((byte)0xFD, cpu.S);
    }

    [Fact]
    public void Stack_PushWrapsInPageOne()
    {
        var cpu = CpuTestHarness.NewCpuWithProgram(out var bus, 0x48);
        cpu.A = 0x42;
        cpu.S = 0x00;
        cpu.RunInstruction();
        Assert.Equal([(0x0100, 0x42)], bus.Writes());
        Assert.Equal((byte)0xFF, cpu.S);
    }

    [Fact]
    public void Stack_PullWrapsInPageOne()
    {
        var cpu = CpuTestHarness.NewCpuWithProgram(out var bus, 0x68);
        cpu.S = 0xFF;
        bus.Ram.Load(0x0100, [0x42]);
        cpu.RunInstruction();
        Assert.Equal([(0x8000, false), (0x8001, false), (0x01FF, false), (0x0100, false)], bus.Addresses());
        Assert.Equal((byte)0x42, cpu.A);
        Assert.Equal((byte)0x00, cpu.S);
    }

    [Fact]
    public void BRK_SevenCycles_PushesPcPlusTwoAndPWithB()
    {
        var cpu = CpuTestHarness.NewCpuWithProgram(out var bus, 0x00, 0x55);
        cpu.P = StatusFlags.C;
        cpu.S = 0xFD;
        bus.Ram.Load(0xFFFE, [0x00, 0x90]);
        Assert.Equal(7, cpu.RunInstruction());
        Assert.Equal(
            [
                (0x8000, false), (0x8001, false),
                (0x01FD, true), (0x01FC, true), (0x01FB, true),
                (0xFFFE, false), (0xFFFF, false)
            ],
            bus.Addresses());
        Assert.Equal([(0x01FD, 0x80), (0x01FC, 0x02), (0x01FB, 0x31)], bus.Writes());
        Assert.Equal((ushort)0x9000, cpu.PC);
        Assert.Equal(StatusFlags.C | StatusFlags.I, cpu.P);
        Assert.Equal((byte)0xFA, cpu.S);
    }

    [Fact]
    public void BRK_RTI_RoundTrip()
    {
        var cpu = CpuTestHarness.NewCpuWithProgram(out var bus, 0x00, 0x55);
        cpu.P = StatusFlags.C;
        cpu.S = 0xFD;
        bus.Ram.Load(0xFFFE, [0x00, 0x90]);
        bus.Ram.Load(0x9000, [0x40]);
        cpu.RunInstruction();
        cpu.RunInstruction();
        Assert.Equal((ushort)0x8002, cpu.PC);
        Assert.Equal(StatusFlags.C, cpu.P);
        Assert.Equal((byte)0xFD, cpu.S);
    }
}
