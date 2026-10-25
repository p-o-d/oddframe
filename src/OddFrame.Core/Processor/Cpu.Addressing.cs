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
        switch (info.Mode, _cycle)
        {
            case (AddressingMode.Immediate, 2):
                ExecuteRead(info.Op, _bus.Read(PC++));
                EndInstruction();
                break;
            case (AddressingMode.ZeroPage, 2): FetchAddressLow(); break;
            case (AddressingMode.ZeroPage, 3): FinishRead(info); break;
            case (AddressingMode.ZeroPageX, 2): FetchAddressLow(); break;
            case (AddressingMode.ZeroPageX, 3): IndexZeroPage(X); break;
            case (AddressingMode.ZeroPageX, 4): FinishRead(info); break;
            case (AddressingMode.ZeroPageY, 2): FetchAddressLow(); break;
            case (AddressingMode.ZeroPageY, 3): IndexZeroPage(Y); break;
            case (AddressingMode.ZeroPageY, 4): FinishRead(info); break;
            case (AddressingMode.Absolute, 2): FetchAddressLow(); break;
            case (AddressingMode.Absolute, 3): FetchAddressHigh(); break;
            case (AddressingMode.Absolute, 4): FinishRead(info); break;
            case (AddressingMode.AbsoluteX, 2): FetchAddressLow(); break;
            case (AddressingMode.AbsoluteX, 3): FetchAddressHighIndexed(X); break;
            case (AddressingMode.AbsoluteX, 4): ReadUnfixedOrFix(info); break;
            case (AddressingMode.AbsoluteX, 5): FinishRead(info); break;
            case (AddressingMode.AbsoluteY, 2): FetchAddressLow(); break;
            case (AddressingMode.AbsoluteY, 3): FetchAddressHighIndexed(Y); break;
            case (AddressingMode.AbsoluteY, 4): ReadUnfixedOrFix(info); break;
            case (AddressingMode.AbsoluteY, 5): FinishRead(info); break;
            case (AddressingMode.IndirectX, 2): FetchPointer(); break;
            case (AddressingMode.IndirectX, 3): IndexPointer(); break;
            case (AddressingMode.IndirectX, 4): ReadPointerLow(); break;
            case (AddressingMode.IndirectX, 5): ReadPointerHigh(); break;
            case (AddressingMode.IndirectX, 6): FinishRead(info); break;
            case (AddressingMode.IndirectY, 2): FetchPointer(); break;
            case (AddressingMode.IndirectY, 3): ReadPointerLow(); break;
            case (AddressingMode.IndirectY, 4): ReadPointerHighIndexed(Y); break;
            case (AddressingMode.IndirectY, 5): ReadUnfixedOrFix(info); break;
            case (AddressingMode.IndirectY, 6): FinishRead(info); break;
            default:
                throw new NotImplementedException($"op {info.Op} mode {info.Mode} cycle {_cycle}");
        }
    }
}