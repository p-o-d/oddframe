namespace OddFrame.Core.Bus;

public interface IBus
{
    byte Read(ushort address);
    byte Peek(ushort address);
    void Write(ushort address, byte value);
}