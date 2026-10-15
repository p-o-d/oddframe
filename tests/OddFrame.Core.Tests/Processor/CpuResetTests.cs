using OddFrame.Core.Processor;
using OddFrame.Core.Tests.Fakes;

namespace OddFrame.Core.Tests.Processor;

public class CpuResetTests
{
    [Fact]
    public void PowerOn_AfterSevenCycles_RegistersMatchSpec()
    {
        var cpu = CpuTestHarness.NewCpu(out _);
        Assert.Equal((byte)0, cpu.A);
        Assert.Equal((byte)0, cpu.X);
        Assert.Equal((byte)0, cpu.Y);
        Assert.Equal((byte)0xFD, cpu.S);
        Assert.Equal(StatusFlags.I, cpu.P);
        Assert.Equal((ushort)0x8000, cpu.PC);
        Assert.Equal(7u, cpu.TotalCycles);
        Assert.True(cpu.IsInstructionBoundary);
    }

    [Fact]
    public void PowerOn_SequenceIsSevenReads()
    {
        var bus = new RecordingBus();
        var cpu = new Cpu(bus);
        cpu.RunCycles(7);
        Assert.Equal(
            [
                ((ushort)0x0000, false),
                ((ushort)0x0000, false),
                ((ushort)0x0100, false),
                ((ushort)0x01FF, false),
                ((ushort)0x01FE, false),
                ((ushort)0xFFFC, false),
                ((ushort)0xFFFD, false)
            ],
            bus.Log.Select(a => (a.Address, a.IsWrite))
        );
    }

    [Fact]
    public void PowerOn_NotAtBoundaryUntilSeventhCycle()
    {
        var bus = new RecordingBus();
        var cpu = new Cpu(bus);
        cpu.RunCycles(6);
        Assert.False(cpu.IsInstructionBoundary);
        cpu.Tick();
        Assert.True(cpu.IsInstructionBoundary);
    }

    [Fact]
    public void Reset_FromRunningState_KeepsRegisters_DecrementsS_SetsI()
    {
        var cpu = CpuTestHarness.NewCpu(out _, 0x9000);
        cpu.A = 0x11;
        cpu.X = 0x22;
        cpu.Y = 0x33;
        cpu.S = 0x80;
        cpu.P = StatusFlags.C;
        cpu.Reset();
        cpu.RunCycles(7);
        Assert.Equal((byte)0x11, cpu.A);
        Assert.Equal((byte)0x22, cpu.X);
        Assert.Equal((byte)0x33, cpu.Y);
        Assert.Equal((byte)0x7D, cpu.S);
        Assert.Equal(StatusFlags.C | StatusFlags.I, cpu.P);
        Assert.Equal((ushort)0x9000, cpu.PC);
        Assert.Equal(14u, cpu.TotalCycles);
    }

    [Fact]
    public void Reset_DummyPushesReadFromCurrentStack()
    {
        RecordingBus bus;
        var cpu = CpuTestHarness.NewCpu(out bus, 0x9000);
        cpu.A = 0x11;
        cpu.X = 0x22;
        cpu.Y = 0x33;
        cpu.S = 0x80;
        cpu.P = StatusFlags.C;
        cpu.Reset();
        cpu.RunCycles(7);
        Assert.Equal(
            [
                ((ushort)0x9000, false),
                ((ushort)0x9000, false),
                ((ushort)0x0180, false),
                ((ushort)0x017F, false),
                ((ushort)0x017E, false),
                ((ushort)0xFFFC, false),
                ((ushort)0xFFFD, false)
            ],
            bus.Log.Select(a => (a.Address, a.IsWrite))
        );
    }

    [Fact]
    public void PowerOn_AfterRunning_RestoresPowerUpState()
    {
        var cpu = CpuTestHarness.NewCpu(out _);
        cpu.A = 0x11;
        cpu.X = 0x22;
        cpu.Y = 0x33;
        cpu.S = 0x80;
        cpu.P = StatusFlags.C;
        cpu.PowerOn();
        cpu.RunCycles(7);
        Assert.Equal(0, cpu.A);
        Assert.Equal(0, cpu.X);
        Assert.Equal(0, cpu.Y);
        Assert.Equal((byte)0xFD, cpu.S);
        Assert.Equal(StatusFlags.I, cpu.P);
        Assert.Equal((ushort)0x8000, cpu.PC);
        Assert.Equal(7u, cpu.TotalCycles);
    }

    [Fact]
    public void P_Setter_MasksBreakAndUnused()
    {
        var cpu = CpuTestHarness.NewCpu(out _);
        cpu.P = (StatusFlags)0xFF;
        Assert.Equal((byte)0xCF, (byte)cpu.P);
    }
}