namespace OddFrame.Core.Tests;

public class TimingProfileTests
{
    [Fact]
    public void Ntsc_MasterClockHz_Is21477272()
    {
        Assert.Equal(21_477_272u, TimingProfile.Ntsc.MasterClockHz);
    }

    [Fact]
    public void Ntsc_CpuDivider_Is12()
    {
        Assert.Equal(12, TimingProfile.Ntsc.CpuDivider);
    }

    [Fact]
    public void Ntsc_PpuDivider_Is4()
    {
        Assert.Equal(4, TimingProfile.Ntsc.PpuDivider);
    }

    [Fact]
    public void Ntsc_ScanlinesPerFrame_Is262()
    {
        Assert.Equal(262, TimingProfile.Ntsc.ScanlinesPerFrame);
    }

    [Fact]
    public void Ntsc_NmiScanline_Is241()
    {
        Assert.Equal(241, TimingProfile.Ntsc.NmiScanline);
    }

    [Fact]
    public void Ntsc_OddFrameDotSkip_IsTrue()
    {
        Assert.True(TimingProfile.Ntsc.OddFrameDotSkip);
    }
    
    [Fact]
    public void For_Ntsc_ReturnsNtscProfile()
    {
        Assert.Same(TimingProfile.Ntsc, TimingProfile.For(Region.Ntsc));
    }
    
    [Fact]
    public void For_Dendy_Throws()
    {
        Assert.Throws<NotSupportedException>(() => TimingProfile.For(Region.Dendy));
    }
    
    [Fact]
    public void For_Pal_Throws()
    {
        Assert.Throws<NotSupportedException>(() => TimingProfile.For(Region.Pal));
    }
}