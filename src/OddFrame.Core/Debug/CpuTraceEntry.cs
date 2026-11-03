using OddFrame.Core.Processor;

namespace OddFrame.Core.Debug;

public readonly record struct CpuTraceEntry(
    ushort PC,
    byte A,
    byte X,
    byte Y,
    byte S,
    StatusFlags P,
    byte Opcode,
    byte Operand1,
    byte Operand2,
    ulong TotalCycles
);