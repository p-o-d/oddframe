using OddFrame.Core.Tests.Fakes;

namespace OddFrame.Core.Tests;

public class RamBusTests
{
    [Fact]
    public void Write_ThenRead_ReturnsValue()
    {
        var bus = new RamBus();
        bus.Write(0x1234, 0xA5);
        Assert.Equal((byte)0xA5, bus.Read(0x1234));
    }

    [Fact]
    public void Peek_MatchesRead()
    {
        var bus = new RamBus();
        bus.Write(0x0200, 0x5A);
        Assert.Equal((byte)0x5A, bus.Peek(0x0200));
        Assert.Equal(bus.Peek(0x0200), bus.Read(0x0200));
    }

    [Fact]
    public void NewBus_IsZeroed()
    {
        var bus = new RamBus();
        Assert.Equal((byte)0x00, bus.Read(0x0000));
        Assert.Equal((byte)0x00, bus.Read(0xFFFF));
    }

    [Fact]
    public void Load_CopiesBytesAtOffset()
    {
        var bus = new RamBus();
        bus.Load(0xC000, [0x4C, 0x00, 0xC0]);
        Assert.Equal((byte)0x4C, bus.Read(0xC000));
        Assert.Equal((byte)0xC0, bus.Read(0xC002));
        Assert.Equal((byte)0x00, bus.Read(0xC003));
    }

    [Fact]
    public void Write_EdgeAddresses_DoNotAlias()
    {
        var bus = new RamBus();
        bus.Write(0x0000, 0x11);
        Assert.Equal((byte)0x11, bus.Read(0x0000));
        bus.Write(0xFFFF, 0x22);
        Assert.Equal((byte)0x22, bus.Read(0xFFFF));
    }

    [Fact]
    public void Load_PastEnd_Throws()
    {
        var bus = new RamBus();
        Assert.Throws<ArgumentException>(() => bus.Load(0xFFFF, [0x01, 0x02]));
    }
}