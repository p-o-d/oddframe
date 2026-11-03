namespace OddFrame.Core.Processor;

internal enum InterruptKind : byte
{
    None,
    Reset,
    Nmi,
    Irq,
    Brk
}