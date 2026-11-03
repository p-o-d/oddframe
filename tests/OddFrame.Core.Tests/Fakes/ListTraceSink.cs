using OddFrame.Core.Debug;

namespace OddFrame.Core.Tests.Fakes;

public sealed class ListTraceSink : ITraceSink
{
    public List<CpuTraceEntry> Entries { get; } = [];

    public void OnInstruction(in CpuTraceEntry entry)
    {
        Entries.Add(entry);
    }
}