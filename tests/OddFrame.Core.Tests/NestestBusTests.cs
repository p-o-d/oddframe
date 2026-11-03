using OddFrame.Core.Tests.Fakes;

namespace OddFrame.Core.Tests;

public class NestestBusTests
{
    [Fact]
    public void PeekApuRange_ReturnsFF()
    {
        var bus = new NestestBus();
        Assert.Equal((byte)0xFF, bus.Peek(0x4000));
        Assert.Equal((byte)0xFF, bus.Peek(0x4015));
        Assert.Equal((byte)0xFF, bus.Peek(0x4017));
    }
    
    [Fact]
    public void ReadApuRange_ReturnsFF()
    {
        var bus = new NestestBus();
        Assert.Equal((byte)0xFF, bus.Read(0x4015));
    }
    
    [Fact]
    public void PeekApuRange_IgnoresWrites()
    {
        var bus = new NestestBus();
        bus.Write(0x4015, 0x02);
        Assert.Equal((byte)0xFF, bus.Peek(0x4015));
    }
    
    [Fact]
    public void OutsideApuRange_IsRam()
    {
        var bus = new NestestBus();
        bus.Ram.Load(0x3FFF, [0x11]);
        bus.Ram.Load(0x4018, [0x22]);
        Assert.Equal((byte)0x11, bus.Peek(0x3FFF));
        Assert.Equal((byte)0x22, bus.Peek(0x4018));
    }
    
    [Fact]
    public void Write_OutsideRange_RoundTrips()
    {
        var bus = new NestestBus();
        bus.Write(0x0300, 0x89);
        Assert.Equal((byte)0x89, bus.Peek(0x0300));
    }
}