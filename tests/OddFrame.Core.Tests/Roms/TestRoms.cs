namespace OddFrame.Core.Tests.Roms;

public static class TestRoms
{
    private static readonly Lazy<string> _root = new(() =>
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "global.json")))
                return dir.FullName;
        throw new DirectoryNotFoundException($"global.json not found above {AppContext.BaseDirectory}");
    });
    public static string RootDirectory => _root.Value;

    public static string Resolve(string relative)
    {
        return Path.Combine(RootDirectory, "tests", "roms", relative);
    }

    public static string Require(string relative)
    {
        var path = Resolve(relative);
        Assert.SkipUnless(File.Exists(path), $"Test ROM missing: {relative}. Run tests/roms/fetch.sh.");
        return path;
    }
}