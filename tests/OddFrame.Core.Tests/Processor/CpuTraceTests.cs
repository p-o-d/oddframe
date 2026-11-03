using OddFrame.Core.Debug;
using OddFrame.Core.Processor;
using OddFrame.Core.Tests.Fakes;

namespace OddFrame.Core.Tests.Processor;

public class CpuTraceTests
{
    [Fact]
    public void TraceSink_FiresOnFetchTick_WithCyclesBeforeInstruction()
    {
        var cpu = CpuTestHarness.NewCpu(out var bus);
        bus.Ram.Load(0x8000, [0x4C, 0x34, 0x12]);
        var sink = new ListTraceSink();
        cpu.TraceSink = sink;
        cpu.Tick();
        Assert.Equal(new CpuTraceEntry(0x8000, 0, 0, 0, 0xFD, StatusFlags.I, 0x4C, 0x34, 0x12, 7), sink.Entries[0]);
    }

    [Fact]
    public void TraceSink_NotCalledDuringResetSequence()
    {
        var bus = new RecordingBus();
        bus.Ram.Load(0xFFFC, [0x00, 0x80]);
        var cpu = new Cpu(bus);
        var sink = new ListTraceSink();
        cpu.TraceSink = sink;
        cpu.RunCycles(7);
        Assert.Empty(sink.Entries);
        cpu.Tick();
        Assert.Single(sink.Entries);
        Assert.Equal((ushort)0x8000, Assert.Single(sink.Entries).PC);
    }

    [Fact]
    public void TraceSink_UsesPeekOnly()
    {
        var cpu = CpuTestHarness.NewCpu(out var bus);
        bus.Ram.Load(0x8000, [0x4C, 0x34, 0x12]);
        var sink = new ListTraceSink();
        cpu.TraceSink = sink;
        cpu.Tick();
        Assert.Equal([new Access(0x8000, 0x4C, false)], bus.Log);
    }
}