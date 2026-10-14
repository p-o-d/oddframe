namespace OddFrame.Core.Processor;

public enum InstructionKind : byte
{
    Read,
    ReadModifyWrite,
    Write,
    Implied,
    Branch,
    Jump,
    JumpIndirect,
    Jsr,
    Rts,
    Rti,
    Brk,
    Push,
    Pull
}