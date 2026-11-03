namespace OddFrame.Core.Processor;

partial class Cpu
{
    // LDA LDX LDY ADC SBC AND ORA EOR CMP CPX CPY BIT NOP LAX LAS ANC ALR ARR AXS LXA XAA
    private void ExecuteRead(Operation op, byte m)
    {
        byte r;
        switch (op)
        {
            case Operation.Lda:
                A = m;
                SetZN(A);
                break;
            case Operation.Ldx:
                X = m;
                SetZN(X);
                break;
            case Operation.Ldy:
                Y = m;
                SetZN(Y);
                break;
            case Operation.And:
                A &= m;
                SetZN(A);
                break;
            case Operation.Ora:
                A |= m;
                SetZN(A);
                break;
            case Operation.Eor:
                A ^= m;
                SetZN(A);
                break;
            case Operation.Adc:
                Adc(m);
                break;
            case Operation.Sbc:
                Adc((byte)~m);
                break;
            case Operation.Cmp:
                Compare(A, m);
                break;
            case Operation.Cpx:
                Compare(X, m);
                break;
            case Operation.Cpy:
                Compare(Y, m);
                break;
            case Operation.Nop:
                break;
            case Operation.Bit:
                SetFlag(StatusFlags.Z, (A & m) == 0);
                SetFlag(StatusFlags.N, (m & 0x80) != 0);
                SetFlag(StatusFlags.V, (m & 0x40) != 0);
                break;
            case Operation.Lax:
                A = X = m;
                SetZN(A);
                break;
            case Operation.Las:
                A = X = S = (byte)(m & S);
                SetZN(A);
                break;
            case Operation.Anc:
                A &= m;
                SetZN(A);
                SetFlag(StatusFlags.C, (A & 0x80) != 0);
                break;
            case Operation.Alr:
                A &= m;
                A = Modify(Operation.Lsr, A);
                break;
            case Operation.Arr:
                // C and V come from the result, not from the bit ROR shifts out
                r = (byte)(((A & m) >> 1) | (Carry ? 0x80 : 0));
                A = r;
                SetZN(A);
                SetFlag(StatusFlags.C, (r & 0x40) != 0);
                SetFlag(StatusFlags.V, (((r >> 6) ^ (r >> 5)) & 0x01) != 0);
                break;
            case Operation.Axs:
                // CMP that also stores: no carry-in, V unchanged
                r = (byte)(A & X);
                Compare(r, m);
                X = (byte)(r - m);
                break;
            case Operation.Lxa:
                // Unstable on real chips; instr_test-v5 expects the operand to pass through
                A = X = m;
                SetZN(A);
                break;
            case Operation.Xaa:
                // Unstable on real chips; untested by nestest and instr_test-v5
                A = (byte)((A | 0xEE) & X & m);
                SetZN(A);
                break;
            default:
                throw new NotImplementedException(op.ToString());
        }
    }

    private void ExecuteImplied(Operation op) // transfers, INC/DEC reg, flag ops, NOP, JAM
    {
        switch (op)
        {
            case Operation.Tax:
                X = A;
                SetZN(X);
                break;
            case Operation.Tay:
                Y = A;
                SetZN(Y);
                break;
            case Operation.Txa:
                A = X;
                SetZN(A);
                break;
            case Operation.Tya:
                A = Y;
                SetZN(A);
                break;
            case Operation.Tsx:
                X = S;
                SetZN(X);
                break;
            case Operation.Txs:
                S = X;
                break;
            case Operation.Inx:
                SetZN(++X);
                break;
            case Operation.Iny:
                SetZN(++Y);
                break;
            case Operation.Dex:
                SetZN(--X);
                break;
            case Operation.Dey:
                SetZN(--Y);
                break;
            case Operation.Clc:
                SetFlag(StatusFlags.C, false);
                break;
            case Operation.Sec:
                SetFlag(StatusFlags.C, true);
                break;
            case Operation.Cli:
                SetFlag(StatusFlags.I, false);
                break;
            case Operation.Sei:
                SetFlag(StatusFlags.I, true);
                break;
            case Operation.Cld:
                SetFlag(StatusFlags.D, false);
                break;
            case Operation.Sed:
                SetFlag(StatusFlags.D, true);
                break;
            case Operation.Clv:
                SetFlag(StatusFlags.V, false);
                break;
            case Operation.Nop:
                break;
            case Operation.Jam:
                Halted = true;
                break;
            default:
                throw new NotImplementedException(op.ToString());
        }
    }

    // ASL LSR ROL ROR INC DEC SLO RLA SRE RRA DCP ISB; returns result.
    // The unofficial ones run the RMW half, then feed its result to a read operation on A.
    private byte Modify(Operation op, byte v)
    {
        byte r;
        bool oldCarry;
        switch (op)
        {
            case Operation.Asl:
                r = (byte)(v << 1);
                SetFlag(StatusFlags.C, (v & 0x80) != 0);
                SetZN(r);
                return r;
            case Operation.Lsr:
                r = (byte)(v >> 1);
                SetFlag(StatusFlags.C, (v & 0x01) != 0);
                SetZN(r);
                return r;
            case Operation.Rol:
                oldCarry = Carry;
                r = (byte)((v << 1) | (oldCarry ? 0x01 : 0));
                SetFlag(StatusFlags.C, (v & 0x80) != 0);
                SetZN(r);
                return r;
            case Operation.Ror:
                oldCarry = Carry;
                r = (byte)((v >> 1) | (oldCarry ? 0x80 : 0));
                SetFlag(StatusFlags.C, (v & 0x01) != 0);
                SetZN(r);
                return r;
            case Operation.Inc:
                r = (byte)(v + 1);
                SetZN(r);
                return r;
            case Operation.Dec:
                r = (byte)(v - 1);
                SetZN(r);
                return r;
            case Operation.Slo:
                r = Modify(Operation.Asl, v);
                ExecuteRead(Operation.Ora, r);
                return r;
            case Operation.Rla:
                r = Modify(Operation.Rol, v);
                ExecuteRead(Operation.And, r);
                return r;
            case Operation.Sre:
                r = Modify(Operation.Lsr, v);
                ExecuteRead(Operation.Eor, r);
                return r;
            case Operation.Rra:
                r = Modify(Operation.Ror, v); // ADC uses the carry ROR just produced
                ExecuteRead(Operation.Adc, r);
                return r;
            case Operation.Dcp:
                r = Modify(Operation.Dec, v);
                ExecuteRead(Operation.Cmp, r);
                return r;
            case Operation.Isb:
                r = Modify(Operation.Inc, v);
                ExecuteRead(Operation.Sbc, r);
                return r;
            default:
                throw new NotImplementedException(op.ToString());
        }
    }

    private byte StoreValue(Operation op) // STA STX STY SAX; returns the byte to write, sets no flags
    {
        switch (op)
        {
            case Operation.Sta:
                return A;
            case Operation.Stx:
                return X;
            case Operation.Sty:
                return Y;
            case Operation.Sax:
                return (byte)(A & X);
            default:
                throw new NotImplementedException(op.ToString());
        }
    }

    private bool BranchTaken(Operation op) // BPL BMI BVC BVS BCC BCS BNE BEQ
    {
        switch (op)
        {
            case Operation.Bpl:
                return !IsSet(StatusFlags.N);
            case Operation.Bmi:
                return IsSet(StatusFlags.N);
            case Operation.Bvc:
                return !IsSet(StatusFlags.V);
            case Operation.Bvs:
                return IsSet(StatusFlags.V);
            case Operation.Bcc:
                return !IsSet(StatusFlags.C);
            case Operation.Bcs:
                return IsSet(StatusFlags.C);
            case Operation.Bne:
                return !IsSet(StatusFlags.Z);
            case Operation.Beq:
                return IsSet(StatusFlags.Z);
            default:
                throw new NotImplementedException(op.ToString());
        }
    }

    private byte PushValue(Operation op) // PHA PHP; returns the byte to push
    {
        switch (op)
        {
            case Operation.Pha:
                return A;
            case Operation.Php:
                return PushedStatus(true);
            default:
                throw new NotImplementedException(op.ToString());
        }
    }

    private void ApplyPull(Operation op, byte v) // PLA PLP
    {
        switch (op)
        {
            case Operation.Pla:
                A = v;
                SetZN(A);
                break;
            case Operation.Plp:
                P = (StatusFlags)v; // the setter drops B and U
                break;
            default:
                throw new NotImplementedException(op.ToString());
        }
    }

    // B exists only in the pushed copy of P: set by PHP and BRK, clear for IRQ and NMI. U is always 1.
    private byte PushedStatus(bool breakFlag)
    {
        return (byte)(P | StatusFlags.U | (breakFlag ? StatusFlags.B : StatusFlags.None));
    }

    private void Adc(byte m)
    {
        var sum = A + m + (Carry ? 1 : 0);
        SetFlag(StatusFlags.V, ((A ^ sum) & (m ^ sum) & 0x80) != 0);
        SetFlag(StatusFlags.C, sum > 0xFF);
        A = (byte)sum;
        SetZN(A);
    }

    private void Compare(byte register, byte m)
    {
        SetFlag(StatusFlags.C, register >= m);
        SetZN((byte)(register - m));
    }

    private void SetZN(byte value)
    {
        SetFlag(StatusFlags.Z, value == 0);
        SetFlag(StatusFlags.N, (value & 0x80) != 0);
    }

    private void SetFlag(StatusFlags flag, bool on)
    {
        if (on) P |= flag;
        else P &= ~flag;
    }
    
    private bool Carry => (P & StatusFlags.C) != 0;

    private bool IsSet(StatusFlags flag) => (P & flag) != 0;
}