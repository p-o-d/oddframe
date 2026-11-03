using OddFrame.Core.Tests.Fakes;

namespace OddFrame.Core.Tests;

public class RecordingBusTests
{
    [Fact]
    public void Read_IsRecordedWithValue()
    {
        var bus = new RecordingBus();
        bus.Ram.Load(0x1234, [0x5A]);
        var v = bus.Read(0x1234);
        Assert.Equal((byte)0x5A, v);
        Assert.Equal<Access>([new Access(0x1234, 0x5A, false)], bus.Log);
    }

    [Fact]
    public void Write_IsRecorded()
    {
        var bus = new RecordingBus();
        bus.Write(0x0200, 0x01);
        Assert.Equal((byte)0x01, bus.Ram.Peek(0x0200));
        Assert.Equal<Access>([new Access(0x0200, 0x01, true)], bus.Log);
    }

    [Fact]
    public void Peek_IsNotRecorded()
    {
        var bus = new RecordingBus();
        bus.Ram.Load(0x0300, [0x77]);
        _ = bus.Peek(0x0300);
        Assert.Empty(bus.Log);
    }

    [Fact]
    public void Log_PreservesOrder()
    {
        var bus = new RecordingBus();
        bus.Ram.Load(0x0010, [0xAA]);
        _ = bus.Read(0x0010);
        bus.Write(0x0010, 0xBB);
        _ = bus.Read(0x0010);
        Assert.Equal<Access>([
            new Access(0x0010, 0xAA, false),
            new Access(0x0010, 0xBB, true),
            new Access(0x0010, 0xBB, false),
        ], bus.Log);
    }

    [Fact]
    public void RamLoad_IsNotRecorded()
    {
        var bus = new RecordingBus();
        bus.Ram.Load(0x0010, [0xAA]);
        Assert.Empty(bus.Log);
    }
}