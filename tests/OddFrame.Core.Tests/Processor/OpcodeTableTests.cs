using OddFrame.Core.Processor;

namespace OddFrame.Core.Tests.Processor;

public class OpcodeTableTests
{
    [Fact]
    public void Table_Has256Entries()
    {
        Assert.Equal(256, OpcodeTable.Entries.Length);
    }

    [Fact]
    public void NoEntry_IsDefault()
    {
        Assert.DoesNotContain([.. OpcodeTable.Entries], e => e.Op == Operation.None);
    }

    [Fact]
    public void OfficialCount_Is151()
    {
        Assert.Equal(151, OpcodeTable.Entries.ToArray().Count(e => !e.Unofficial));
    }

    [Fact]
    public void UnofficialCount_Is105()
    {
        Assert.Equal(105, OpcodeTable.Entries.ToArray().Count(e => e.Unofficial));
    }
    
    [Theory]
    [InlineData(AddressingMode.Implied, 1)]
    [InlineData(AddressingMode.Accumulator, 1)]
    [InlineData(AddressingMode.Immediate, 2)]
    [InlineData(AddressingMode.ZeroPage, 2)]
    [InlineData(AddressingMode.ZeroPageX, 2)]
    [InlineData(AddressingMode.ZeroPageY, 2)]
    [InlineData(AddressingMode.IndirectX, 2)]
    [InlineData(AddressingMode.IndirectY, 2)]
    [InlineData(AddressingMode.Relative, 2)]
    [InlineData(AddressingMode.Absolute, 3)]
    [InlineData(AddressingMode.AbsoluteX, 3)]
    [InlineData(AddressingMode.AbsoluteY, 3)]
    [InlineData(AddressingMode.Indirect, 3)]
    public void Length_FollowsMode(AddressingMode mode, int length)
    {
        Assert.Equal(length, new OpcodeInfo(Operation.Adc, mode, InstructionKind.Branch, false).Length);
    }

    [Theory]
    [InlineData(0x4C, Operation.Jmp, AddressingMode.Absolute, InstructionKind.Jump )]
    [InlineData(0xA1, Operation.Lda, AddressingMode.IndirectX, InstructionKind.Read )]
    [InlineData(0x9D, Operation.Sta, AddressingMode.AbsoluteX, InstructionKind.Write )]
    [InlineData(0xFE, Operation.Inc, AddressingMode.AbsoluteX, InstructionKind.ReadModifyWrite )]
    [InlineData(0xEB, Operation.Sbc, AddressingMode.Immediate, InstructionKind.Read, true )]
    [InlineData(0x04, Operation.Nop, AddressingMode.ZeroPage, InstructionKind.Read, true )]
    [InlineData(0x02, Operation.Jam, AddressingMode.Implied, InstructionKind.Implied, true )]
    [InlineData(0x6C, Operation.Jmp, AddressingMode.Indirect, InstructionKind.JumpIndirect )]
    [InlineData(0x0A, Operation.Asl, AddressingMode.Accumulator, InstructionKind.Implied )]
    [InlineData(0x20, Operation.Jsr, AddressingMode.Absolute, InstructionKind.Jsr )]
    public void SpotChecks(byte opcode, Operation op, AddressingMode mode, InstructionKind kind, bool unofficial = false)
    {
        Assert.Equal(new OpcodeInfo(op, mode, kind, unofficial), OpcodeTable.Entries[opcode]);
    }
    
    [Theory]
    [InlineData(0x1A)]
    [InlineData(0x80)]
    [InlineData(0x0C)]
    [InlineData(0x1C)]
    public void UnofficialNops_AreNop(byte opcode)
    {
        Assert.Equal("NOP", OpcodeTable.Entries[opcode].Mnemonic);
    }

    [Fact]
    public void Jams_AreExactlyTwelve()
    {
        Assert.Equal(
            ["$02", "$12", "$22", "$32", "$42", "$52", "$62", "$72", "$92", "$B2", "$D2", "$F2"],
            OpcodesWhere(e => e.Op == Operation.Jam));
    }

    [Fact]
    public void KindAndMode_AreConsistent()
    {
        Assert.Empty(OpcodesWhere(e =>
            (e.Kind == InstructionKind.Branch) != (e.Mode == AddressingMode.Relative)));

        Assert.Empty(OpcodesWhere(e =>
            e.Kind == InstructionKind.Implied
            && e.Mode is not (AddressingMode.Implied or AddressingMode.Accumulator)));

        Assert.Empty(OpcodesWhere(e =>
            (e.Kind is InstructionKind.Read or InstructionKind.Write or InstructionKind.ReadModifyWrite)
            && (e.Mode is AddressingMode.Implied or AddressingMode.Accumulator
                or AddressingMode.Relative or AddressingMode.Indirect)));
    }

    // Opcode bits are aaabbbcc. In groups cc=01 and cc=11, bbb alone selects the mode.
    [Fact]
    public void Column01And11_FollowMatrixModes()
    {
        AddressingMode[] modeByBbb =
        [
            AddressingMode.IndirectX,   // 000
            AddressingMode.ZeroPage,    // 001
            AddressingMode.Immediate,   // 010
            AddressingMode.Absolute,    // 011
            AddressingMode.IndirectY,   // 100
            AddressingMode.ZeroPageX,   // 101
            AddressingMode.AbsoluteY,   // 110
            AddressingMode.AbsoluteX,   // 111
        ];
        // SAX, LAX, AHX move X, so they index by Y instead.
        var exceptions = new Dictionary<int, AddressingMode>
        {
            [0x97] = AddressingMode.ZeroPageY,
            [0xB7] = AddressingMode.ZeroPageY,
            [0x9F] = AddressingMode.AbsoluteY,
            [0xBF] = AddressingMode.AbsoluteY,
        };

        var checkedCount = 0;
        var mismatches = new List<string>();
        for (var opcode = 0; opcode < 256; opcode++)
        {
            var cc = opcode & 0b11;
            if (cc is not (0b01 or 0b11))
                continue;

            var bbb = (opcode >> 2) & 0b111;
            var expected = exceptions.GetValueOrDefault(opcode, modeByBbb[bbb]);
            var actual = OpcodeTable.Entries[opcode].Mode;
            if (actual != expected)
                mismatches.Add($"${opcode:X2}: expected {expected}, got {actual}");
            checkedCount++;
        }

        Assert.Equal(128, checkedCount);
        Assert.Empty(mismatches);
    }

    // Hex strings so a failure reads "$10", not "16".
    private static List<string> OpcodesWhere(Func<OpcodeInfo, bool> predicate)
    {
        var result = new List<string>();
        for (var opcode = 0; opcode < 256; opcode++)
            if (predicate(OpcodeTable.Entries[opcode]))
                result.Add($"${opcode:X2}");
        return result;
    }
}