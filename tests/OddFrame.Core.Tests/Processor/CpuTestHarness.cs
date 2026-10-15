using OddFrame.Core.Processor;
using OddFrame.Core.Tests.Fakes;

namespace OddFrame.Core.Tests.Processor;

public static class CpuTestHarness
{
    public static Cpu NewCpu(out RecordingBus bus, ushort resetVector = 0x8000)
    {
        bus = new RecordingBus();
        bus.Ram.Load(0xFFFC, [(byte)(resetVector & 0xFF), (byte)((resetVector >> 8) & 0xFF)]);
        var cpu = new Cpu(bus);
        cpu.RunCycles(7);
        bus.Log.Clear();
        return cpu;
    }

    public static void RunCycles(this Cpu cpu, int n)
    {
        for (var i = 0; i < n; i++)
            cpu.Tick();
    }

    public static int RunInstruction(this Cpu cpu)
    {
        var count = 0;
        do
        {
            if (count >= 10)
                Assert.Fail($"instruction did not finish in 10 cycles, PC=${cpu.PC:X4}");
            cpu.Tick();
            count++;
        } 
        while (!cpu.IsInstructionBoundary);
        return count;
    }
}