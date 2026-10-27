using System.Diagnostics;
using OddFrame.Core.Bus;
using OddFrame.Core.Debug;

namespace OddFrame.Core.Processor;

public sealed partial class Cpu
{
    private readonly IBus _bus;
    private InterruptKind _pendingInterrupt;
    private InterruptKind _currentInterrupt;
    private int _cycle;
    private ushort _address;
    private byte _opcode;
    private byte _pointer;
    private byte _data;
    private bool _pageCrossed;

    public Cpu(IBus bus)
    {
        ArgumentNullException.ThrowIfNull(bus);
        _bus = bus;
        PowerOn();
    }

    public byte A { get; set; }
    public byte X { get; set; }
    public byte Y { get; set; }
    public byte S { get; set; }
    public ushort PC { get; set; }

    public StatusFlags P
    {
        get;
        set => field = value & ~(StatusFlags.B | StatusFlags.U);
    }

    public ulong TotalCycles { get; private set; }
    public bool IsInstructionBoundary => _cycle == 0;
    public ITraceSink? TraceSink { get; set; }

    public void PowerOn()
    {
        A = X = Y = S = 0;
        PC = 0;
        P = StatusFlags.I;
        TotalCycles = 0;
        _cycle = 0;
        _opcode = 0;
        _address = 0;
        _pointer = 0;
        _data = 0;
        _pageCrossed = false;
        _currentInterrupt = InterruptKind.None;
        _pendingInterrupt = InterruptKind.Reset;
    }

    public void Reset()
    {
        _pendingInterrupt = InterruptKind.Reset;
        _cycle = 0;
    }

    public void Tick()
    {
        if (_cycle == 0 && TraceSink is not null && _pendingInterrupt == InterruptKind.None)
        {
            var entry = new CpuTraceEntry(
                PC,
                A,
                X,
                Y,
                S,
                P,
                _bus.Peek(PC),
                _bus.Peek((ushort)(PC + 1)),
                _bus.Peek((ushort)(PC + 2)),
                TotalCycles
            );
            TraceSink.OnInstruction(entry);
        }

        TotalCycles++;
        if (_cycle == 0)
        {
            if (_pendingInterrupt != InterruptKind.None)
            {
                _currentInterrupt = _pendingInterrupt;
                _pendingInterrupt = InterruptKind.None;
                _ = _bus.Read(PC);
            }
            else
            {
                _opcode = _bus.Read(PC++);
            }

            _cycle = 1;
        }
        else
        {
            _cycle++;
            if (_currentInterrupt != InterruptKind.None)
                StepInterrupt();
            else
                StepInstruction();
        }
    }

    private void StepInstruction()
    {
        var info = OpcodeTable.Entries[_opcode];
        switch (info.Kind)
        {
           case InstructionKind.Implied:
               StepImplied(info);
               break;
           case InstructionKind.Read:
               StepRead(info);
               break;
           case InstructionKind.Write:
               StepWrite(info);
               break;
           case InstructionKind.ReadModifyWrite:
               StepModify(info);
               break;
           case InstructionKind.Branch:
               StepBranch(info);
               break;
           case InstructionKind.Jump:
               StepJump(info);
               break;
           case InstructionKind.JumpIndirect:
               StepJumpIndirect(info);
               break;
           case InstructionKind.Jsr:
               StepJsr(info);
               break;
           case InstructionKind.Rts:
               StepRts(info);
               break;
           case InstructionKind.Rti:
               StepRti(info);
               break;
           case InstructionKind.Push:
               StepPush(info);
               break;
           case InstructionKind.Pull:
               StepPull(info);
               break;
           case InstructionKind.Brk:
               StepBrk(info);
               break;
           default:
               throw new NotImplementedException($"opcode ${_opcode:X2} {info.Mnemonic} {info.Mode}");
        }
    }

    private void StepInterrupt()
    {
        switch (_cycle)
        {
            case 2:
                _ = _bus.Read(PC);
                break;
            case 3:
                PushOrDummy((byte)(PC >> 8));
                break;
            case 4:
                PushOrDummy((byte)PC);
                break;
            case 5:
                PushOrDummy(PushedStatus(_currentInterrupt == InterruptKind.Brk));
                break;
            case 6:
                _address = _bus.Read(Vector);
                P |= StatusFlags.I;
                break;
            case 7:
                PC = (ushort)((_bus.Read((ushort)(Vector + 1)) << 8) | _address);
                _currentInterrupt = InterruptKind.None;
                _cycle = 0;
                break;
            default:
                throw new UnreachableException($"interrupt cycle {_cycle}");
        }
    }

    private void EndInstruction()
    {
        _cycle = 0;
    }

    // Reset runs the push cycles as reads: it never writes to the stack.
    private void PushOrDummy(byte value)
    {
        if (_currentInterrupt == InterruptKind.Reset)
        {
            _ = _bus.Read(StackAddress);
            S--;
            return;
        }

        Push(value);
    }

    private ushort StackAddress => (ushort)(0x0100 | S);

    private ushort Vector =>
        _currentInterrupt switch
        {
            InterruptKind.Reset => 0xFFFC,
            InterruptKind.Nmi => 0xFFFA,
            InterruptKind.Irq or InterruptKind.Brk => 0xFFFE,
            _ => throw new UnreachableException($"Unsupported interrupt value {_currentInterrupt}")
        };
}