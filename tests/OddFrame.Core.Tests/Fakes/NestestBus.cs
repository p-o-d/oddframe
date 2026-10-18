using OddFrame.Core.Bus;

namespace OddFrame.Core.Tests.Fakes;

public sealed class NestestBus : IBus
{
    private const ushort ApuIoStart = 0x4000;
    private const ushort ApuIoEnd = 0x4017;
    private const byte ApuIoValue = 0xFF;
    public RamBus Ram { get; } = new();

    public byte Read(ushort address)
    {
        return address is >= ApuIoStart and <= ApuIoEnd
            ? ApuIoValue
            : Ram.Read(address);
    }

    public byte Peek(ushort address)
    {
        return address is >= ApuIoStart and <= ApuIoEnd
            ? ApuIoValue
            : Ram.Peek(address);
    }

    public void Write(ushort address, byte value)
    {
        Ram.Write(address, value);
    }
}