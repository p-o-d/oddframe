using OddFrame.Core.Bus;

namespace OddFrame.Core.Tests.Fakes;

public sealed class BlarggStubBus : IBus
{
    private const ushort PpuStatus = 0x2002;
    private const byte VblankFlag = 0x80;
    public RamBus Ram { get; } = new();

    public byte Read(ushort address)
    {
        return address == PpuStatus ? VblankFlag : Ram.Read(address);
    }

    public byte Peek(ushort address)
    {
        return address == PpuStatus ? VblankFlag : Ram.Peek(address);
    }

    public void Write(ushort address, byte value)
    {
        Ram.Write(address, value);
    }
}