using OddFrame.Core.Debug;
using OddFrame.Core.Processor;
using OddFrame.Core.Tests.Fakes;

namespace OddFrame.Core.Tests.Debug;

public class NestestFormatterTests
{
    public static IEnumerable<object[]> SampleData =>
        new List<object[]>
        {
            // Expected lines are copied from nestest.log (line numbers 1-based) so the
            // formatter is tested without the ROM files.
            // Line 1. Absolute jump prints no "= vv"; PPU fields pad to 3.
            new object[] {
                "C000  4C F5 C5  JMP $C5F5                       A:00 X:00 Y:00 P:24 SP:FD PPU:  0, 21 CYC:7",
                0xC000, 0x4C, 0xF5, 0xC5, 0x00, 0x00, 0x00, 0x24, 0xFD, 0, 21, 7 },
            // Line 9. Relative prints the target, PC + 2 + offset.
            new object[] {
                "C72F  B0 04     BCS $C735                       A:00 X:00 Y:00 P:27 SP:FB PPU:  0, 93 CYC:31",
                0xC72F, 0xB0, 0x04, 0x00, 0x00, 0x00, 0x00, 0x27, 0xFB, 0, 93, 31 },
            // Line 37. Zero page with value.
            new object[] {
                "C780  85 01     STA $01 = 00                    A:FF X:00 Y:00 P:A4 SP:FB PPU:  0,285 CYC:95",
                0xC780, 0x85, 0x01, 0x00, 0xFF, 0x00, 0x00, 0xA4, 0xFB, 0, 285, 95 },
            // Line 802. Absolute write still shows the old value.
            new object[] {
                "CDAE  8E FF 07  STX $07FF = 00                  A:00 X:FB Y:99 P:A5 SP:FB PPU: 16, 85 CYC:1847",
                0xCDAE, 0x8E, 0xFF, 0x07, 0x00, 0xFB, 0x99, 0xA5, 0xFB, 16, 85, 1847 },
            // Line 976. Accumulator.
            new object[] {
                "CEFC  4A        LSR A                           A:01 X:55 Y:69 P:65 SP:FB PPU: 20, 53 CYC:2291",
                0xCEFC, 0x4A, 0x00, 0x00, 0x01, 0x55, 0x69, 0x65, 0xFB, 20, 53, 2291 },
            // Line 1104. (ind,X): $FF + X wraps to pointer $80 inside page 0.
            new object[] {
                "CFFA  A1 FF     LDA ($FF,X) @ 80 = 0200 = 5A    A:5D X:81 Y:69 P:A5 SP:FB PPU: 22,289 CYC:2597",
                0xCFFA, 0xA1, 0xFF, 0x00, 0x5D, 0x81, 0x69, 0xA5, 0xFB, 22, 289, 2597,
                new (ushort, byte)[] { (0x0080, 0x00), (0x0081, 0x02), (0x0200, 0x5A) } },
            // Line 3065. (ind),Y: pointer high byte comes from $0000, not $0100; base + Y crosses a page.
            new object[] {
                "D959  B1 FF     LDA ($FF),Y = 0146 @ 0245 = 12  A:01 X:65 Y:FF P:E5 SP:FA PPU: 77,215 CYC:8824",
                0xD959, 0xB1, 0xFF, 0x00, 0x01, 0x65, 0xFF, 0xE5, 0xFA, 77, 215, 8824,
                new (ushort, byte)[] { (0x00FF, 0x46), (0x0000, 0x01), (0x0100, 0x99), (0x0245, 0x12) } },
            // Line 3348. JMP ($02FF): high byte comes from $0200, not $0300.
            new object[] {
                "DBB5  6C FF 02  JMP ($02FF) = 0300              A:60 X:07 Y:00 P:65 SP:F9 PPU: 84,201 CYC:9615",
                0xDBB5, 0x6C, 0xFF, 0x02, 0x60, 0x07, 0x00, 0x65, 0xF9, 84, 201, 9615,
                new (ushort, byte)[] { (0x02FF, 0x00), (0x0200, 0x03), (0x0300, 0x99) } },
            // Line 3639. zp,X.
            new object[] {
                "DBCD  B4 33     LDY $33,X @ 33 = AA             A:66 X:00 Y:00 P:67 SP:FB PPU: 91,148 CYC:10393",
                0xDBCD, 0xB4, 0x33, 0x00, 0x66, 0x00, 0x00, 0x67, 0xFB, 91, 148, 10393,
                new (ushort, byte)[] { (0x0033, 0xAA) } },
            // Line 4306. zp,Y: $80 + $FF wraps to $7F.
            new object[] {
                "DEFE  96 80     STX $80,Y @ 7F = 00             A:47 X:69 Y:FF P:24 SP:FB PPU:109, 61 CYC:12410",
                0xDEFE, 0x96, 0x80, 0x00, 0x47, 0x69, 0xFF, 0x24, 0xFB, 109, 61, 12410,
                new (ushort, byte)[] { (0x017F, 0x99) } },
            // Line 4369. abs,X crossing a page.
            new object[] {
                "E1E4  BC FF 05  LDY $05FF,X @ 0689 = BB         A:66 X:8A Y:00 P:26 SP:FB PPU:110,179 CYC:12563",
                0xE1E4, 0xBC, 0xFF, 0x05, 0x66, 0x8A, 0x00, 0x26, 0xFB, 110, 179, 12563,
                new (ushort, byte)[] { (0x0689, 0xBB) } },
            // Line 5004. Unofficial marker, zero page.
            new object[] {
                "C6BD  04 A9    *NOP $A9 = 00                    A:AA X:97 Y:4E P:EF SP:F9 PPU:128, 89 CYC:14579",
                0xC6BD, 0x04, 0xA9, 0x00, 0xAA, 0x97, 0x4E, 0xEF, 0xF9, 128, 89, 14579 },
            // Line 5038. Unofficial immediate.
            new object[] {
                "C6EA  80 89    *NOP #$89                        A:AA X:97 Y:4E P:EF SP:F3 PPU:129, 21 CYC:14670",
                0xC6EA, 0x80, 0x89, 0x00, 0xAA, 0x97, 0x4E, 0xEF, 0xF3, 129, 21, 14670 },
        };
    
    [Theory]
    [MemberData(nameof(SampleData))]
    public void Nestest_Selected_Lines_Equal(
        string expected,
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
        var bus = new RamBus();
        bus.Load(PC, [opcode, operand1, operand2]);
        foreach (var (address, value) in memory ?? [])
            bus.Write(address, value);
        var entry = new CpuTraceEntry(PC, A, X, Y, S, P, opcode, operand1, operand2, totalCycles);
        var actual = NestestFormatter.Format(entry, bus, scanline, dot);
        Assert.Equal(expected, actual);
    }
}