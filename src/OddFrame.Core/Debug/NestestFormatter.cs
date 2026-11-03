using System.Diagnostics;
using System.Text;
using OddFrame.Core.Bus;
using OddFrame.Core.Processor;

namespace OddFrame.Core.Debug;

public static class NestestFormatter
{
    public static string Format(in CpuTraceEntry e, IBus bus, int scanline, int dot)
    {
        var sb = new StringBuilder();
        var info = OpcodeTable.Entries[e.Opcode];
        sb.Append($"{e.PC:X4}  ");
        var op = $"{e.Opcode:X2} {e.Operand1:X2} {e.Operand2:X2} ";
        sb.Append(op[..(3 * info.Length)].PadRight(9));
        sb.Append(info.Unofficial ? '*' : ' ');
        var disasm = $"{info.Mnemonic} {FormatOperand(e, info, bus)}";
        sb.Append(disasm.PadRight(32));
        var registers =
            $"A:{e.A:X2} X:{e.X:X2} Y:{e.Y:X2} P:{(byte)(e.P | StatusFlags.U):X2} SP:{e.S:X2} PPU:{scanline,3},{dot,3} CYC:{e.TotalCycles}";
        sb.Append(registers);
        return sb.ToString();
    }

    private static string FormatOperand(in CpuTraceEntry e, OpcodeInfo info, IBus bus)
    {
        var zp = e.Operand1;
        var abs = (ushort)(e.Operand1 | e.Operand2 << 8);

        switch (info.Mode)
        {
            case AddressingMode.Implied:
                return "";

            case AddressingMode.Accumulator:
                return "A";

            case AddressingMode.Immediate:
                return $"#${zp:X2}";

            case AddressingMode.ZeroPage:
                return $"${zp:X2} = {bus.Peek(zp):X2}";

            case AddressingMode.ZeroPageX:
            {
                var address = (byte)(zp + e.X);
                return $"${zp:X2},X @ {address:X2} = {bus.Peek(address):X2}";
            }

            case AddressingMode.ZeroPageY:
            {
                var address = (byte)(zp + e.Y);
                return $"${zp:X2},Y @ {address:X2} = {bus.Peek(address):X2}";
            }

            case AddressingMode.Absolute:
                return info.Kind is InstructionKind.Jump or InstructionKind.Jsr
                    ? $"${abs:X4}"
                    : $"${abs:X4} = {bus.Peek(abs):X2}";

            case AddressingMode.AbsoluteX:
            {
                var address = (ushort)(abs + e.X);
                return $"${abs:X4},X @ {address:X4} = {bus.Peek(address):X2}";
            }

            case AddressingMode.AbsoluteY:
            {
                var address = (ushort)(abs + e.Y);
                return $"${abs:X4},Y @ {address:X4} = {bus.Peek(address):X2}";
            }

            case AddressingMode.IndirectX:
            {
                var pointer = (byte)(zp + e.X);
                var address = PeekWord(bus, pointer, (byte)(pointer + 1));
                return $"(${zp:X2},X) @ {pointer:X2} = {address:X4} = {bus.Peek(address):X2}";
            }

            case AddressingMode.IndirectY:
            {
                var baseAddress = PeekWord(bus, zp, (byte)(zp + 1));
                var address = (ushort)(baseAddress + e.Y);
                return $"(${zp:X2}),Y = {baseAddress:X4} @ {address:X4} = {bus.Peek(address):X2}";
            }

            case AddressingMode.Indirect:
            {
                var highAddress = (ushort)((abs & 0xFF00) | (byte)(abs + 1));
                var target = PeekWord(bus, abs, highAddress);
                return $"(${abs:X4}) = {target:X4}";
            }

            case AddressingMode.Relative:
            {
                var target = (ushort)(e.PC + 2 + (sbyte)zp);
                return $"${target:X4}";
            }

            default:
                throw new UnreachableException(nameof(info.Mode));
        }
    }

    private static ushort PeekWord(IBus bus, ushort lowAddress, ushort highAddress) =>
        (ushort)(bus.Peek(lowAddress) | bus.Peek(highAddress) << 8);
}