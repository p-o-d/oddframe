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
}