namespace OddFrame.Core.Tests.Roms;

// Synthetic images, so these run without the ROM files.
public class BlarggRunnerTests
{
    private const ulong Cap = 10_000;
    private const int StatusOperand = 0x1F;

    // Writes $80 first, like the real shell, then the signature, "OK", and the final status.
    private static readonly byte[] FinishingProgram =
    [
        0xA9, 0x80,       // $8000 LDA #$80
        0x8D, 0x00, 0x60, // $8002 STA $6000
        0xA9, 0xDE,       // $8005 LDA #$DE
        0x8D, 0x01, 0x60, // $8007 STA $6001
        0xA9, 0xB0,       // $800A LDA #$B0
        0x8D, 0x02, 0x60, // $800C STA $6002
        0xA9, 0x61,       // $800F LDA #$61
        0x8D, 0x03, 0x60, // $8011 STA $6003
        0xA9, 0x4F,       // $8014 LDA #'O'
        0x8D, 0x04, 0x60, // $8016 STA $6004
        0xA9, 0x4B,       // $8019 LDA #'K'
        0x8D, 0x05, 0x60, // $801B STA $6005
        0xA9, 0x03,       // $801E LDA #$03 (status)
        0x8D, 0x00, 0x60, // $8020 STA $6000
        0x4C, 0x23, 0x80, // $8023 JMP $8023
    ];

    // The program sits at $8000 and the reset vector points there. With one bank,
    // $FFFC reads the mirror at $C000, so the vector is at the end of either size.
    private static byte[] Image(byte[] program, byte prgBanks = 2, byte flags6 = 0)
    {
        var prgSize = prgBanks * 0x4000;
        var image = new byte[16 + prgSize];
        "NES\x1A"u8.CopyTo(image);
        image[4] = prgBanks;
        image[6] = flags6;
        program.CopyTo(image, 16);
        image[16 + prgSize - 4] = 0x00;
        image[16 + prgSize - 3] = 0x80;
        return image;
    }

    private static byte[] WithStatus(byte status)
    {
        var program = FinishingProgram.ToArray();
        program[StatusOperand] = status;
        return program;
    }

    [Fact]
    public void Run_Finished_ReturnsStatusAndText()
    {
        var result = BlarggRunner.Run(Image(FinishingProgram), Cap);
        Assert.Equal(BlarggOutcome.Finished, result.Outcome);
        Assert.Equal((byte)0x03, result.Status);
        Assert.Equal("OK", result.Text);
        Assert.True(result.Cycles < Cap);
    }

    [Fact]
    public void Run_StatusZero_IsFinished()
    {
        var result = BlarggRunner.Run(Image(WithStatus(0x00)), Cap);
        Assert.Equal(BlarggOutcome.Finished, result.Outcome);
        Assert.Equal((byte)0x00, result.Status);
    }

    [Fact]
    public void Run_StatusEightyOne_IsResetRequest()
    {
        var result = BlarggRunner.Run(Image(WithStatus(0x81)), Cap);
        Assert.Equal(BlarggOutcome.ResetRequested, result.Outcome);
        Assert.Equal((byte)0x81, result.Status);
    }

    // Signature written, status still $80: the test is running, not finished.
    [Fact]
    public void Run_StillRunning_TimesOut()
    {
        byte[] program = [.. FinishingProgram[..0x1E], 0x4C, 0x1E, 0x80]; // $801E JMP $801E
        var result = BlarggRunner.Run(Image(program), Cap);
        Assert.Equal(BlarggOutcome.Timeout, result.Outcome);
        Assert.Equal((byte)0x80, result.Status);
        Assert.Equal("OK", result.Text);
        Assert.True(result.Cycles >= Cap);
    }

    // RAM starts at zero; a zero status without the signature is not a pass.
    [Fact]
    public void Run_NoSignature_TimesOut()
    {
        var result = BlarggRunner.Run(Image([0x4C, 0x00, 0x80]), Cap);
        Assert.Equal(BlarggOutcome.Timeout, result.Outcome);
        Assert.Equal("", result.Text);
    }

    [Fact]
    public void Run_Jam_ReportsHaltedImmediately()
    {
        var result = BlarggRunner.Run(Image([0x02]), Cap);
        Assert.Equal(BlarggOutcome.Halted, result.Outcome);
        Assert.True(result.Cycles < 20);
    }

    [Fact]
    public void Run_SixteenKbPrg_MirroredAtC000()
    {
        var result = BlarggRunner.Run(Image(FinishingProgram, prgBanks: 1), Cap);
        Assert.Equal(BlarggOutcome.Finished, result.Outcome);
        Assert.Equal("OK", result.Text);
    }

    [Fact]
    public void Run_Mapper1_Throws()
    {
        var e = Assert.Throws<InvalidDataException>(() => BlarggRunner.Run(Image(FinishingProgram, flags6: 0x10), Cap));
        Assert.Contains("mapper 1", e.Message);
    }

    [Fact]
    public void Run_Trainer_Throws()
    {
        Assert.Throws<InvalidDataException>(() => BlarggRunner.Run(Image(FinishingProgram, flags6: 0x04), Cap));
    }

    [Fact]
    public void Run_BadMagic_Throws()
    {
        var image = Image(FinishingProgram);
        image[3] = 0x00;
        Assert.Throws<InvalidDataException>(() => BlarggRunner.Run(image, Cap));
    }
}
