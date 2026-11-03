using OddFrame.Core.Tests.Fakes;

namespace OddFrame.Core.Tests;

public class BlarggStubBusTests
{
    [Fact]
    public void Read2002_ReturnsVblankFlag()
    {
        var bus = new BlarggStubBus();
        Assert.Equal((byte)0x80, bus.Read(0x2002));
    }

    [Fact]
    public void Peek2002_ReturnsVblankFlag()
    {
        var bus = new BlarggStubBus();
        Assert.Equal((byte)0x80, bus.Peek(0x2002));
    }

    [Fact]
    public void Write2002_DoesNotChangeRead()
    {
        var bus = new BlarggStubBus();
        bus.Write(0x2002, 0xAA);
        Assert.Equal((byte)0x80, bus.Read(0x2002));
    }

    [Fact]
    public void OtherAddresses_AreRam()
    {
        var bus = new BlarggStubBus();
        Assert.Equal((byte)0x00, bus.Read(0x2001));
        Assert.Equal((byte)0x00, bus.Read(0x2003));
        bus.Write(0x2001, 0xAA);
        bus.Write(0x2003, 0xBB);
        Assert.Equal((byte)0xAA, bus.Read(0x2001));
        Assert.Equal((byte)0xBB, bus.Read(0x2003));
        bus.Write(0x6000, 0x80);
        bus.Write(0x6001, 0xDE);
        bus.Write(0x6002, 0xB0);
        bus.Write(0x6003, 0x61);
        Assert.Equal((byte)0x80, bus.Peek(0x6000));
        Assert.Equal((byte)0xDE, bus.Peek(0x6001));
        Assert.Equal((byte)0xB0, bus.Peek(0x6002));
        Assert.Equal((byte)0x61, bus.Peek(0x6003));
    }
}