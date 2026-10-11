namespace OddFrame.Core;

public sealed class Device
{
    public Device(DeviceOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        Timing = TimingProfile.For(options.Region);
        DebugEnabled = options.EnableDebug;
    }

    public bool DebugEnabled { get; }
    public TimingProfile Timing { get; }
    public ulong MasterClock { get; private set; }

    public void Tick()
    {
        ++MasterClock;
        // if (MasterClock % PpuDivider == 0) ppu.Tick();
        // if (MasterClock % CpuDivider == 0) cpu.Tick();
    }

    public void StepCpuCycle()
    {
        do Tick(); while (MasterClock % (ulong)Timing.CpuDivider != 0);
    }
}