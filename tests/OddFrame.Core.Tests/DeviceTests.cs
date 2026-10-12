using System.Buffers.Binary;

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

    [Fact]
    public void SaveLoad_RoundTripsMasterClock()
    {
        var a = new Device(new DeviceOptions());
        for(var i =  0; i < 37; i++)
            a.Tick();
        var state = a.SaveState();
        var b = new Device(new DeviceOptions());
        b.LoadState(state);
        Assert.Equal(37UL, b.MasterClock);
    }

    [Fact]
    public void LoadState_RejectsWrongMagic()
    {
        var device = new Device(new DeviceOptions());
        Assert.Throws<InvalidDataException>(() => device.LoadState(new byte[46]));
    }

    [Fact]
    public void SaveState_IsDeterministic()
    {
        var a = new Device(new DeviceOptions());
        var b = new Device(new DeviceOptions());
        for (var i = 0; i < 14; i++)
        {
            a.Tick();
            b.Tick();
        }
        Assert.Equal(a.SaveState(), b.SaveState());    
    }

    [Fact]
    public void LoadState_RejectsWrongVersion()
    {
        var device = new Device(new DeviceOptions());
        var state = device.SaveState();
        state[4] = 2;
        Assert.Throws<InvalidDataException>(() => device.LoadState(state));
    }

    [Fact]
    public void LoadState_Truncated_Throws()
    {
        var device = new Device(new DeviceOptions());
        var state = device.SaveState();
        state = state[..40];
        Assert.Throws<InvalidDataException>(() => device.LoadState(state));
    }

    [Fact]
    public void LoadState_TrailingBytes_Throws()
    {
        var device = new Device(new DeviceOptions());
        var state = device.SaveState();
        state = [.. state, 0x00];
        Assert.Throws<InvalidDataException>(() => device.LoadState(state));
    }

    [Fact]
    public void SaveState_HeaderLayout()
    {
        var device = new Device(new DeviceOptions());
        var state = device.SaveState();
        Assert.Equal(state[..4], "ODDF"u8.ToArray());
        Assert.Equal(1, BinaryPrimitives.ReadUInt16LittleEndian(state.AsSpan(4)));
        Assert.Equal(46, state.Length);
    }
}