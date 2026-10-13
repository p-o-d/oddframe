using OddFrame.Core.Bus;

namespace OddFrame.Core.Tests.Fakes;

public readonly record struct Access(ushort Address, byte Value, bool IsWrite);

public sealed class RecordingBus : IBus
{
    public RamBus Ram { get; } = new();

    public List<Access> Log { get; } = [];

    public byte Read(ushort address)
    {
        var value = Ram.Read(address);
        Log.Add(new Access(address, value, false));
        return value;
    }

    public byte Peek(ushort address)
    {
        return Ram.Peek(address);
    }

    public void Write(ushort address, byte value)
    {
        Log.Add(new Access(address, value, true));
        Ram.Write(address, value);
    }
}