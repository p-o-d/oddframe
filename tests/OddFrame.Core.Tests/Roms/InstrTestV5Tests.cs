namespace OddFrame.Core.Tests.Roms;

// The singles are NROM; all_instrs.nes and official_only.nes are MMC1 and wait for M6.
public class InstrTestV5Tests
{
    // The slowest single, 07-abs_xy, finishes in about 10.6 M cycles.
    private const ulong MaxCycles = 50_000_000;

    [Theory]
    [Trait("Category", "Rom")]
    [InlineData("01-basics")]
    [InlineData("02-implied")]
    [InlineData("03-immediate")]
    [InlineData("04-zero_page")]
    [InlineData("05-zp_xy")]
    [InlineData("06-absolute")]
    [InlineData("07-abs_xy")]
    [InlineData("08-ind_x")]
    [InlineData("09-ind_y")]
    [InlineData("10-branches")]
    [InlineData("11-stack")]
    [InlineData("12-jmp_jsr")]
    [InlineData("13-rts")]
    [InlineData("14-rti")]
    [InlineData("15-brk")]
    [InlineData("16-special")]
    public void Single_Passes(string name)
    {
        var result = BlarggRunner.Run($"instr_test-v5/rom_singles/{name}.nes", MaxCycles);
        Assert.True(result is { Outcome: BlarggOutcome.Finished, Status: 0 }, result.ToString());
    }
}
