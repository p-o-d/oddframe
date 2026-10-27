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

    private void StepWrite(OpcodeInfo info)
    {
        switch (info.Mode, _cycle)
        {
            case (AddressingMode.ZeroPage, 2): FetchAddressLow(); break;
            case (AddressingMode.ZeroPage, 3): FinishWrite(info); break;
            case (AddressingMode.ZeroPageX, 2): FetchAddressLow(); break;
            case (AddressingMode.ZeroPageX, 3): IndexZeroPage(X); break;
            case (AddressingMode.ZeroPageX, 4): FinishWrite(info); break;
            case (AddressingMode.ZeroPageY, 2): FetchAddressLow(); break;
            case (AddressingMode.ZeroPageY, 3): IndexZeroPage(Y); break;
            case (AddressingMode.ZeroPageY, 4): FinishWrite(info); break;
            case (AddressingMode.Absolute, 2): FetchAddressLow(); break;
            case (AddressingMode.Absolute, 3): FetchAddressHigh(); break;
            case (AddressingMode.Absolute, 4): FinishWrite(info); break;
            case (AddressingMode.AbsoluteX, 2): FetchAddressLow(); break;
            case (AddressingMode.AbsoluteX, 3): FetchAddressHighIndexed(X); break;
            case (AddressingMode.AbsoluteX, 4): ReadUnfixedAndFix(); break;
            case (AddressingMode.AbsoluteX, 5): FinishWrite(info); break;
            case (AddressingMode.AbsoluteY, 2): FetchAddressLow(); break;
            case (AddressingMode.AbsoluteY, 3): FetchAddressHighIndexed(Y); break;
            case (AddressingMode.AbsoluteY, 4): ReadUnfixedAndFix(); break;
            case (AddressingMode.AbsoluteY, 5): FinishWrite(info); break;
            case (AddressingMode.IndirectX, 2): FetchPointer(); break;
            case (AddressingMode.IndirectX, 3): IndexPointer(); break;
            case (AddressingMode.IndirectX, 4): ReadPointerLow(); break;
            case (AddressingMode.IndirectX, 5): ReadPointerHigh(); break;
            case (AddressingMode.IndirectX, 6): FinishWrite(info); break;
            case (AddressingMode.IndirectY, 2): FetchPointer(); break;
            case (AddressingMode.IndirectY, 3): ReadPointerLow(); break;
            case (AddressingMode.IndirectY, 4): ReadPointerHighIndexed(Y); break;
            case (AddressingMode.IndirectY, 5): ReadUnfixedAndFix(); break;
            case (AddressingMode.IndirectY, 6): FinishWrite(info); break;
            default:
                throw new NotImplementedException($"op {info.Op} mode {info.Mode} cycle {_cycle}");
        }
    }
    
    private void StepModify(OpcodeInfo info)
    {
        switch (info.Mode, _cycle)
        {
            case (AddressingMode.ZeroPage, 2): FetchAddressLow(); break;
            case (AddressingMode.ZeroPage, 3): ReadData(); break;
            case (AddressingMode.ZeroPage, 4): WriteOldAndModify(info); break;
            case (AddressingMode.ZeroPage, 5): FinishModify(); break;
            case (AddressingMode.ZeroPageX, 2): FetchAddressLow(); break;
            case (AddressingMode.ZeroPageX, 3): IndexZeroPage(X); break;
            case (AddressingMode.ZeroPageX, 4): ReadData(); break;
            case (AddressingMode.ZeroPageX, 5): WriteOldAndModify(info); break;
            case (AddressingMode.ZeroPageX, 6): FinishModify(); break;
            case (AddressingMode.Absolute, 2): FetchAddressLow(); break;
            case (AddressingMode.Absolute, 3): FetchAddressHigh(); break;
            case (AddressingMode.Absolute, 4): ReadData(); break;
            case (AddressingMode.Absolute, 5): WriteOldAndModify(info); break;
            case (AddressingMode.Absolute, 6): FinishModify(); break;
            case (AddressingMode.AbsoluteX, 2): FetchAddressLow(); break;
            case (AddressingMode.AbsoluteX, 3): FetchAddressHighIndexed(X); break;
            case (AddressingMode.AbsoluteX, 4): ReadUnfixedAndFix(); break;
            case (AddressingMode.AbsoluteX, 5): ReadData(); break;
            case (AddressingMode.AbsoluteX, 6): WriteOldAndModify(info); break;
            case (AddressingMode.AbsoluteX, 7): FinishModify(); break;
            default:
                throw new NotImplementedException($"op {info.Op} mode {info.Mode} cycle {_cycle}");
        }
    }

    private void StepBranch(OpcodeInfo info) // 2 cycles not taken, 3 taken, 4 taken across a page
    {
        switch (_cycle)
        {
            case 2: FetchBranchOffset(info); break;
            case 3: AddBranchOffset(); break;
            case 4: FixBranchHigh(); break;
            default:
                throw new NotImplementedException($"op {info.Op} cycle {_cycle}");
        }
    }

    private void StepJump(OpcodeInfo info)
    {
        switch (_cycle)
        {
            case 2: FetchAddressLow(); break;
            case 3: FetchHighAndJump(); break;
            default:
                throw new NotImplementedException($"op {info.Op} cycle {_cycle}");
        }
    }

    private void StepJumpIndirect(OpcodeInfo info)
    {
        switch (_cycle)
        {
            case 2: FetchAddressLow(); break;
            case 3: FetchAddressHigh(); break;
            case 4: ReadData(); break;
            case 5: ReadIndirectHighAndJump(); break;
            default:
                throw new NotImplementedException($"op {info.Op} cycle {_cycle}");
        }
    }

    private void StepJsr(OpcodeInfo info) // pushes the address of its own last byte
    {
        switch (_cycle)
        {
            case 2: FetchAddressLow(); break;
            case 3: _ = _bus.Read(StackAddress); break;
            case 4: Push((byte)(PC >> 8)); break;
            case 5: Push((byte)PC); break;
            case 6: FetchHighAndJump(); break;
            default:
                throw new NotImplementedException($"op {info.Op} cycle {_cycle}");
        }
    }

    private void StepRts(OpcodeInfo info) // pulls PC, then adds 1 to undo JSR's "minus one"
    {
        switch (_cycle)
        {
            case 2: _ = _bus.Read(PC); break;
            case 3: _ = ReadStackAndIncrement(); break;
            case 4: _address = ReadStackAndIncrement(); break;
            case 5: PC = (ushort)(ReadStack() << 8 | _address); break;
            case 6: IncrementPcAfterReturn(); break;
            default:
                throw new NotImplementedException($"op {info.Op} cycle {_cycle}");
        }
    }

    private void StepRti(OpcodeInfo info) // pulls P, then PC; no +1
    {
        switch (_cycle)
        {
            case 2: _ = _bus.Read(PC); break;
            case 3: _ = ReadStackAndIncrement(); break;
            case 4: P = (StatusFlags)ReadStackAndIncrement(); break; // the setter drops B and U
            case 5: _address = ReadStackAndIncrement(); break;
            case 6: PullHighAndJump(); break;
            default:
                throw new NotImplementedException($"op {info.Op} cycle {_cycle}");
        }
    }

    private void StepPush(OpcodeInfo info)
    {
        switch (_cycle)
        {
            case 2: _ = _bus.Read(PC); break;
            case 3: FinishPush(info); break;
            default:
                throw new NotImplementedException($"op {info.Op} cycle {_cycle}");
        }
    }

    private void StepPull(OpcodeInfo info)
    {
        switch (_cycle)
        {
            case 2: _ = _bus.Read(PC); break;
            case 3: _ = ReadStackAndIncrement(); break;
            case 4: FinishPull(info); break;
            default:
                throw new NotImplementedException($"op {info.Op} cycle {_cycle}");
        }
    }

    private void StepBrk(OpcodeInfo info) // cycles 3-7 run in StepInterrupt
    {
        switch (_cycle)
        {
            case 2: StartBrk(); break;
            default:
                throw new NotImplementedException($"op {info.Op} cycle {_cycle}");
        }
    }

    private void FetchAddressLow()
    {
        _address = _bus.Read(PC++);
    }

    private void FetchAddressHigh()
    {
        _address |= (ushort)(_bus.Read(PC++) << 8);
    }

    private void FetchAddressHighIndexed(byte index)
    {
        var low = (byte)_address;
        var hi = _bus.Read(PC++);
        var sum = low + index;
        _pageCrossed = sum > 0xFF;
        _address = (ushort)(hi << 8 | (byte)sum); // carry unfixed
    }

    private void IndexZeroPage(byte index)
    {
        _ = _bus.Read(_address);
        _address = (byte)(_address + index); // ignores carry, wraps within page zero
    }

    private void FetchPointer()
    {
        _pointer = _bus.Read(PC++);
    }

    private void IndexPointer()
    {
        _ = _bus.Read(_pointer);
        _pointer = (byte)(_pointer + X);
    }

    private void ReadPointerLow()
    {
        _address = _bus.Read(_pointer);
    }

    private void ReadPointerHigh()
    {
        var value = _bus.Read((byte)(_pointer + 1));
        _address |= (ushort)(value << 8);
    }

    private void ReadPointerHighIndexed(byte index)
    {
        var low = (byte)_address;
        var hi = _bus.Read((byte)(_pointer + 1));
        var sum = low + index;
        _pageCrossed = sum > 0xFF;
        _address = (ushort)(hi << 8 | (byte)sum); // carry unfixed
    }

    private void ReadUnfixedOrFix(OpcodeInfo opcode)
    {
        var value = _bus.Read(_address);
        if (_pageCrossed)
        {
            _address = (ushort)(_address + 0x0100);
        }
        else
        {
            ExecuteRead(opcode.Op, value);
            EndInstruction();
        }
    }

    private void FinishRead(OpcodeInfo opcode)
    {
        var value = _bus.Read(_address);
        ExecuteRead(opcode.Op, value);
        EndInstruction();
    }
    
    private void ReadUnfixedAndFix()
    {
        _ = _bus.Read(_address);
        if (_pageCrossed)
            _address = (ushort)(_address + 0x100);
    }

    private void FinishWrite(OpcodeInfo opcode)
    {
        _bus.Write(_address, StoreValue(opcode.Op));
        EndInstruction();
    }

    private void ReadData()
    {
        _data = _bus.Read(_address);
    }

    private void WriteOldAndModify(OpcodeInfo opcode)
    {
        _bus.Write(_address, _data);
        _data = Modify(opcode.Op, _data);
    }

    private void FinishModify()
    {
        _bus.Write(_address, _data);
        EndInstruction();
    }

    private void FetchBranchOffset(OpcodeInfo opcode)
    {
        _data = _bus.Read(PC++);
        if (!BranchTaken(opcode.Op))
            EndInstruction();
    }

    private void AddBranchOffset()
    {
        _ = _bus.Read(PC);
        _address = (ushort)(PC + (sbyte)_data); // PC is already the next instruction
        _pageCrossed = (_address & 0xFF00) != (PC & 0xFF00);
        PC = (ushort)((PC & 0xFF00) | (_address & 0x00FF)); // high byte unfixed
        if (!_pageCrossed)
            EndInstruction();
    }

    private void FixBranchHigh()
    {
        _ = _bus.Read(PC);
        PC = _address;
        EndInstruction();
    }

    private void FetchHighAndJump()
    {
        PC = (ushort)(_bus.Read(PC) << 8 | _address);
        EndInstruction();
    }

    private void ReadIndirectHighAndJump()
    {
        // 6502 bug: the pointer's low byte wraps without carrying, so ($02FF) reads $02FF and $0200
        var hi = _bus.Read((ushort)((_address & 0xFF00) | (byte)(_address + 1)));
        PC = (ushort)(hi << 8 | _data);
        EndInstruction();
    }

    private void IncrementPcAfterReturn()
    {
        _ = _bus.Read(PC);
        PC++;
        EndInstruction();
    }

    private void PullHighAndJump()
    {
        PC = (ushort)(ReadStack() << 8 | _address);
        EndInstruction();
    }

    private void FinishPush(OpcodeInfo opcode)
    {
        Push(PushValue(opcode.Op));
        EndInstruction();
    }

    private void FinishPull(OpcodeInfo opcode)
    {
        ApplyPull(opcode.Op, ReadStack());
        EndInstruction();
    }

    private void StartBrk()
    {
        _ = _bus.Read(PC++); // skips the padding byte, so BRK pushes PC + 2
        _currentInterrupt = InterruptKind.Brk;
    }

    private void Push(byte value)
    {
        _bus.Write(StackAddress, value);
        S--;
    }

    // A pull is "S++, then read". The first call is the dummy read that does the
    // pre-increment; each later call reads the slot the previous one stepped onto.
    private byte ReadStackAndIncrement()
    {
        var value = _bus.Read(StackAddress);
        S++;
        return value;
    }

    private byte ReadStack()
    {
        return _bus.Read(StackAddress);
    }
}