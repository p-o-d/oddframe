namespace OddFrame.Core.Processor;

partial class Cpu
{
    public void Save(BinaryWriter writer)
    {
        writer.Write(A);
        writer.Write(X);
        writer.Write(Y);
        writer.Write(S);
        writer.Write(PC);
        writer.Write((byte)P);
        writer.Write(TotalCycles);
        writer.Write(Halted);
        writer.Write((byte)_cycle);
        writer.Write(_opcode);
        writer.Write(_address);
        writer.Write(_pointer);
        writer.Write(_data);
        writer.Write(_pageCrossed);
        writer.Write((byte)_pendingInterrupt);
        writer.Write((byte)_currentInterrupt);
        writer.Write((byte)_interruptSample);
        writer.Write((byte)_branchSample);
        writer.Write(_nmiPrevious);
        writer.Write(_nmiLatched);
        writer.Write(Nmi);
        writer.Write(IrqLine);
    }

    public void Load(BinaryReader reader)
    {
        A = reader.ReadByte();
        X = reader.ReadByte();
        Y = reader.ReadByte();
        S = reader.ReadByte();
        PC = reader.ReadUInt16();
        P = (StatusFlags)reader.ReadByte();
        TotalCycles = reader.ReadUInt64();
        Halted = reader.ReadBoolean();
        _cycle = reader.ReadByte();
        _opcode = reader.ReadByte();
        _address = reader.ReadUInt16();
        _pointer = reader.ReadByte();
        _data = reader.ReadByte();
        _pageCrossed = reader.ReadBoolean();
        _pendingInterrupt = (InterruptKind)reader.ReadByte();
        _currentInterrupt = (InterruptKind)reader.ReadByte();
        _interruptSample = (InterruptKind)reader.ReadByte();
        _branchSample = (InterruptKind)reader.ReadByte();
        _nmiPrevious = reader.ReadBoolean();
        _nmiLatched = reader.ReadBoolean();
        Nmi = reader.ReadBoolean();
        IrqLine = reader.ReadBoolean();
    }
}