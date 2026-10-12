namespace OddFrame.Core.Tests.Roms;

public class TestRomsTests
{
    [Fact]
    public void Resolve_ReturnsPathUnderRepoRoot()
    {
        Assert.EndsWith(Path.Combine("tests", "roms", "other", "nestest.nes"), TestRoms.Resolve("other/nestest.nes"));
        Assert.True(File.Exists(Path.Combine(TestRoms.RootDirectory, "global.json")));
    }

    [Fact]
    [Trait("Category", "Rom")]
    public void Require_MissingFile_Skips()
    {
        TestRoms.Require("does/not/exist.nes");
        Assert.Fail("Should have skipped");
    }

    [Fact]
    [Trait("Category", "Rom")]
    public void Require_PresentFile_ReturnsPath()
    {
        var path = TestRoms.Require("other/nestest.nes");
        Assert.True(File.Exists(path));
    }
}