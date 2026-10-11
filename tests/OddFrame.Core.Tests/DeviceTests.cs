namespace OddFrame.Core.Tests;

public class DeviceTests
{
    [Fact]
    public void NewDevice_MasterClockIsZero()
    {
        var device = new Device(new DeviceOptions());
        Assert.Equal(0UL, device.MasterClock);
    }
    
    [Fact]
    public void NewDevice_UsesProfileForRegion()
    {
        var device = new Device(new DeviceOptions());
        Assert.Same(TimingProfile.Ntsc, device.Timing);
    }
    
    [Fact]
    public void Tick_IncrementsMasterClockByOne()
    {
        var device = new Device(new DeviceOptions());
        device.Tick();
        Assert.Equal(1UL, device.MasterClock);
        device.Tick();
        device.Tick();
        Assert.Equal(3UL, device.MasterClock);
    }
    
    [Fact]
    public void StepCpuCycle_AdvancesCpuDividerTicks()
    {
        var device = new Device(new DeviceOptions());
        device.StepCpuCycle();
        Assert.Equal((ulong)TimingProfile.Ntsc.CpuDivider, device.MasterClock);
        device.StepCpuCycle();
        Assert.Equal((ulong)TimingProfile.Ntsc.CpuDivider * 2, device.MasterClock);
    }
    
    [Fact]
    public void StepCpuCycle_FromUnalignedClock_StopsAtNextBoundary()
    {
        var device = new Device(new DeviceOptions());
        device.Tick();
        device.Tick();
        device.Tick();
        device.Tick();
        device.Tick();
        device.StepCpuCycle();
        Assert.Equal((ulong)TimingProfile.Ntsc.CpuDivider, device.MasterClock);
    }
}