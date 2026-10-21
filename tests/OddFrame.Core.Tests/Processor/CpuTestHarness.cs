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

    public static Cpu NewCpuWithProgram(out RecordingBus bus, params byte[] program)
    {
        var cpu = NewCpu(out bus);
        bus.Ram.Load(0x8000, program);
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
    
    public enum Register { A, X, Y, S }

    extension(Cpu cpu)
    {
        public byte Get(Register r) => r switch
        {
            Register.A => cpu.A, Register.X => cpu.X, Register.Y => cpu.Y, Register.S => cpu.S,
            _ => throw new ArgumentOutOfRangeException(nameof(r)),
        };

        public void Set(Register r, byte value)
        {
            switch (r)
            {
                case Register.A:
                    cpu.A = value;
                    break;
                case Register.X:
                    cpu.X = value;
                    break;
                case Register.Y:
                    cpu.Y = value;
                    break;
                case Register.S:
                    cpu.S = value;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(r));
            }
        }
    }
}