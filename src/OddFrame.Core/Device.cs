namespace OddFrame.Core;

public sealed class Device : IStateful
{
    private static readonly byte[] Magic = "ODDF"u8.ToArray();
    private const ushort StateVersion = 1;
    private const int RomHashSize = 32;
    
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

    public void Save(BinaryWriter writer)
    {
        writer.Write(MasterClock);
    }

    public void Load(BinaryReader reader)
    {
        MasterClock = reader.ReadUInt64();
    }

    public byte[] SaveState()
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write(Magic);
        writer.Write(StateVersion);
        writer.Write(new byte[RomHashSize]);
        Save(writer);
        writer.Flush();
        return stream.ToArray();
    }

    public void LoadState(byte[] state)
    {
        ArgumentNullException.ThrowIfNull(state);
        using var stream = new MemoryStream(state, false);
        using var reader = new BinaryReader(stream);
        try
        {
            if (!reader.ReadBytes(Magic.Length).SequenceEqual(Magic))
                throw new InvalidDataException("not an OddFrame save state");
            if (reader.ReadUInt16() != StateVersion)
                throw new InvalidDataException("Not a valid OddFrame save state version");
            _ = reader.ReadBytes(RomHashSize);
            Load(reader);
        }
        catch (EndOfStreamException e)
        {
            throw new InvalidDataException("truncated save state", e);
        }
        
        if (stream.Position != stream.Length)
            throw new InvalidDataException("trailing bytes");
    }
}