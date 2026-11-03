namespace OddFrame.Core.Debug;

public interface ITraceSink
{
    void OnInstruction(in CpuTraceEntry entry);
}