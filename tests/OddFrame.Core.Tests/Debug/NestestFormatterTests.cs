using OddFrame.Core.Debug;
using OddFrame.Core.Processor;
using OddFrame.Core.Tests.Fakes;
using OddFrame.Core.Tests.Roms;

namespace OddFrame.Core.Tests.Debug;

public class NestestFormatterTests
{
    public static IEnumerable<object[]> SampleData =>
        new List<object[]>
        {
            // Absolute jump prints no "= vv"; PPU fields pad to 3.
            new object[] { 0, 0xC000, 0x4C, 0xF5, 0xC5, 0x00, 0x00, 0x00, 0x24, 0xFD, 0, 21, 7 },
            // Relative prints the target, PC + 2 + offset.
            new object[] { 8, 0xC72F, 0xB0, 0x04, 0x00, 0x00, 0x00, 0x00, 0x27, 0xFB, 0, 93, 31 },
            // Zero page with value.
            new object[] { 36, 0xC780, 0x85, 0x01, 0x00, 0xFF, 0x00, 0x00, 0xA4, 0xFB, 0, 285, 95 },
            // Absolute write still shows the old value.
            new object[] { 801, 0xCDAE, 0x8E, 0xFF, 0x07, 0x00, 0xFB, 0x99, 0xA5, 0xFB, 16, 85, 1847 },
            // Accumulator.
            new object[] { 975, 0xCEFC, 0x4A, 0x00, 0x00, 0x01, 0x55, 0x69, 0x65, 0xFB, 20, 53, 2291 },
            // (ind,X): $FF + X wraps to pointer $80 inside page 0.
            new object[] { 1103, 0xCFFA, 0xA1, 0xFF, 0x00, 0x5D, 0x81, 0x69, 0xA5, 0xFB, 22, 289, 2597,
                new (ushort, byte)[] { (0x0080, 0x00), (0x0081, 0x02), (0x0200, 0x5A) } },
            // (ind),Y: pointer high byte comes from $0000, not $0100; base + Y crosses a page.
            new object[] { 3064, 0xD959, 0xB1, 0xFF, 0x00, 0x01, 0x65, 0xFF, 0xE5, 0xFA, 77, 215, 8824,
                new (ushort, byte)[] { (0x00FF, 0x46), (0x0000, 0x01), (0x0100, 0x99), (0x0245, 0x12) } },
            // JMP ($02FF): high byte comes from $0200, not $0300.
            new object[] { 3347, 0xDBB5, 0x6C, 0xFF, 0x02, 0x60, 0x07, 0x00, 0x65, 0xF9, 84, 201, 9615,
                new (ushort, byte)[] { (0x02FF, 0x00), (0x0200, 0x03), (0x0300, 0x99) } },
            // zp,X.
            new object[] { 3638, 0xDBCD, 0xB4, 0x33, 0x00, 0x66, 0x00, 0x00, 0x67, 0xFB, 91, 148, 10393,
                new (ushort, byte)[] { (0x0033, 0xAA) } },
            // zp,Y: $80 + $FF wraps to $7F.
            new object[] { 4305, 0xDEFE, 0x96, 0x80, 0x00, 0x47, 0x69, 0xFF, 0x24, 0xFB, 109, 61, 12410,
                new (ushort, byte)[] { (0x017F, 0x99) } },
            // abs,X crossing a page.
            new object[] { 4368, 0xE1E4, 0xBC, 0xFF, 0x05, 0x66, 0x8A, 0x00, 0x26, 0xFB, 110, 179, 12563,
                new (ushort, byte)[] { (0x0689, 0xBB) } },
            // Unofficial marker, zero page.
            new object[] { 5003, 0xC6BD, 0x04, 0xA9, 0x00, 0xAA, 0x97, 0x4E, 0xEF, 0xF9, 128, 89, 14579 },
            // Unofficial immediate.
            new object[] { 5037, 0xC6EA, 0x80, 0x89, 0x00, 0xAA, 0x97, 0x4E, 0xEF, 0xF3, 129, 21, 14670 },
        };
    
    [Theory]
    [MemberData(nameof(SampleData))]
    public void Nestest_Selected_Lines_Equal(
        int lineNumber,
        ushort PC,
        byte opcode,
        byte operand1,
        byte operand2,
        byte A,
        byte X,
        byte Y,
        StatusFlags P,
        byte S,
        int scanline,
        int dot,
        ulong totalCycles,
        (ushort Address, byte Value)[]? memory = null
    )
    {
        var lines = File.ReadAllLines(TestRoms.Resolve("other/nestest.log"));
        var bus = new RamBus();
        bus.Load(PC, [opcode, operand1, operand2]);
        foreach (var (address, value) in memory ?? [])
            bus.Write(address, value);
        var entry = new CpuTraceEntry(PC, A, X, Y, S, P, opcode, operand1, operand2, totalCycles);
        var actual = NestestFormatter.Format(entry, bus, scanline, dot);
        Assert.Equal(lines[lineNumber],  actual);

    }
}