namespace OddFrame.Core.Processor;

[Flags]
public enum StatusFlags : byte
{
    None = 0x00,
    C = 0x01,
    Z = 0x02,
    I = 0x04,
    D = 0x08,
    B = 0x10,
    U = 0x20,
    V = 0x40,
    N = 0x80,
}