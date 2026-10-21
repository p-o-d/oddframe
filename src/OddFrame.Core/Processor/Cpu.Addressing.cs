namespace OddFrame.Core.Processor;

partial class Cpu
{
    private void StepImplied(OpcodeInfo info)
    {
        switch (info.Mode)
        {
            case AddressingMode.Implied:
                _ = _bus.Read(PC);
                ExecuteImplied(info.Op);
                EndInstruction();
                break;
            case AddressingMode.Accumulator:
                _ = _bus.Read(PC);
                A = Modify(info.Op, A);
                EndInstruction();
                break;
            default:
                throw new NotImplementedException($"{info.Op} {info.Mode}");
        }
    }
    
    private void StepRead(OpcodeInfo info)
    {
        switch (info.Mode)
        {
            case AddressingMode.Immediate:
                ExecuteRead(info.Op, _bus.Read(PC++));
                EndInstruction();
                break;
            default:
                throw new NotImplementedException($"{info.Op} {info.Mode}");
        }
    }
}