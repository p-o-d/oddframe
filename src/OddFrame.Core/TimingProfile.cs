namespace OddFrame.Core;

public sealed record TimingProfile(
    uint MasterClockHz,
    int CpuDivider,
    int PpuDivider,
    int ScanlinesPerFrame,
    int NmiScanline,
    bool OddFrameDotSkip)
{
    public static readonly TimingProfile Ntsc = new TimingProfile(
        21_477_272u,
        12,
        4,
        262,
        241,
        true);

    public static TimingProfile For(Region region)
    {
        return region switch
        {
            Region.Ntsc => Ntsc,
            Region.Pal or Region.Dendy => throw new NotSupportedException($"Region {region} is not supported"),
            _ => throw new ArgumentOutOfRangeException(nameof(region), region, null)
        };
    }
}