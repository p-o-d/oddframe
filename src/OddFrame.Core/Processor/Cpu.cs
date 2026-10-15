using System.Diagnostics;
using OddFrame.Core.Bus;

namespace OddFrame.Core.Processor;

public sealed partial class Cpu
{
    private readonly IBus _bus;
    private int _cycle;

    private byte _opcode;

    private ushort _address;
    private InterruptKind _pendingInterrupt;
    private InterruptKind _currentInterrupt;

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

    public void PowerOn()
    {
        A = X = Y = S = 0;
        PC = 0;
        P = StatusFlags.I;
        TotalCycles = 0;
        _cycle = 0;
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
        throw new NotImplementedException($"opcode ${_opcode:X2}");
    }

    private void StepInterrupt()
    {
        switch (_cycle)
        {
            case 2:
                _ = _bus.Read(PC);
                break;
            case 3:
            case 4:
            case 5:
                PushOrDummy(0);
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

    private void PushOrDummy(byte value)
    {
        if (_currentInterrupt == InterruptKind.Reset)
        {
            _ = _bus.Read(StackAddress);
            S--;
            return;
        }

        throw new NotImplementedException("IRQ/NMI/BRK interrupts not implemented yet");
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