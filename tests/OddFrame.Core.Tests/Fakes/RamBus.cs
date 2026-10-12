using OddFrame.Core.Bus;

namespace OddFrame.Core.Tests.Fakes;

public sealed class RamBus : IBus
{
    private readonly byte[] _memory = new byte[0x10000];
    
    public byte Read(ushort address)
    {
        return _memory[address];
    }

    public byte Peek(ushort address)
    {
        return _memory[address];
    }

    public void Write(ushort address, byte value)
    {
        _memory[address] = value;
    }
    
    public void Load(ushort offset, ReadOnlySpan<byte> data)
    {
        data.CopyTo(_memory.AsSpan(offset));
    }
}