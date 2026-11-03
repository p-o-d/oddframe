using System.Diagnostics;

namespace OddFrame.Core.Processor;

public readonly record struct OpcodeInfo(
    Operation Op,
    AddressingMode Mode,
    InstructionKind Kind,
    bool Unofficial
)
{
    private static readonly string[] Mnemonics = [.. Enum.GetNames<Operation>().Select(x => x.ToUpperInvariant())];
    public int Length =>
        Mode switch
        {
            AddressingMode.Implied
                or AddressingMode.Accumulator => 1,
            AddressingMode.Immediate
                or AddressingMode.ZeroPage
                or AddressingMode.ZeroPageX
                or AddressingMode.ZeroPageY
                or AddressingMode.IndirectX
                or AddressingMode.IndirectY
                or AddressingMode.Relative => 2,
            AddressingMode.Absolute
                or AddressingMode.AbsoluteX
                or AddressingMode.AbsoluteY
                or AddressingMode.Indirect => 3,
            _ => throw new UnreachableException(nameof(Mode))
        };
    public string Mnemonic => Mnemonics[(int)Op];
}