using OddFrame.Core.Processor;

namespace OddFrame.Core.Tests.Processor;

public class CpuInstructionTests
{
    [Theory]
    [InlineData(0xA9, CpuTestHarness.Register.A)]
    [InlineData(0xA2, CpuTestHarness.Register.X)]
    [InlineData(0xA0, CpuTestHarness.Register.Y)]
    public void Load_SetsZ(byte op, CpuTestHarness.Register register)
    {
        var cpu = CpuTestHarness.NewCpuWithProgram(out _, op, 0x00);
        cpu.P = StatusFlags.N;
        Assert.Equal(2, cpu.RunInstruction());
        Assert.Equal(StatusFlags.Z, cpu.P);
        Assert.Equal((byte)0x00, cpu.Get(register));
    }
    
    [Theory]
    [InlineData(0xA9, CpuTestHarness.Register.A)]
    [InlineData(0xA2, CpuTestHarness.Register.X)]
    [InlineData(0xA0, CpuTestHarness.Register.Y)]
    public void Load_SetsN(byte op, CpuTestHarness.Register register)
    {
        var cpu = CpuTestHarness.NewCpuWithProgram(out _, op, 0x80);
        cpu.P = StatusFlags.Z;
        Assert.Equal(2, cpu.RunInstruction());
        Assert.Equal(StatusFlags.N, cpu.P);
        Assert.Equal((byte)0x80, cpu.Get(register));
    }
    
    [Theory]
    [InlineData(0xA9, CpuTestHarness.Register.A)]
    [InlineData(0xA2, CpuTestHarness.Register.X)]
    [InlineData(0xA0, CpuTestHarness.Register.Y)]
    public void Load_ClearsStaleZN(byte op, CpuTestHarness.Register register)
    {
        var cpu = CpuTestHarness.NewCpuWithProgram(out _, op, 0x01);
        cpu.P = StatusFlags.Z |  StatusFlags.N;
        Assert.Equal(2, cpu.RunInstruction());
        Assert.Equal(StatusFlags.None, cpu.P);
        Assert.Equal((byte)0x01, cpu.Get(register));
    }

    [Theory]
    [InlineData(0xAA, CpuTestHarness.Register.A, CpuTestHarness.Register.X, 0x00, StatusFlags.Z)]
    [InlineData(0xA8, CpuTestHarness.Register.A, CpuTestHarness.Register.Y, 0x90, StatusFlags.N)]
    [InlineData(0x8A, CpuTestHarness.Register.X, CpuTestHarness.Register.A, 0x00, StatusFlags.Z)]
    [InlineData(0x98, CpuTestHarness.Register.Y, CpuTestHarness.Register.A, 0x90, StatusFlags.N)]
    [InlineData(0xBA, CpuTestHarness.Register.S, CpuTestHarness.Register.X, 0x80, StatusFlags.N)]
    [InlineData(0xAA, CpuTestHarness.Register.A, CpuTestHarness.Register.X, 0x01, StatusFlags.None)]
    public void Transfer(byte op, 
        CpuTestHarness.Register src, 
        CpuTestHarness.Register dst, 
        byte value,
        StatusFlags expectedP)
    {
        var cpu = CpuTestHarness.NewCpuWithProgram(out _, op);
        cpu.P = StatusFlags.None;
        cpu.Set(src, value);
        cpu.Set(dst, 0x55); //Destination register is guarded by setting a value
        Assert.Equal(2, cpu.RunInstruction());
        Assert.Equal(expectedP, cpu.P);
        Assert.Equal(value, cpu.Get(src));
        Assert.Equal(value, cpu.Get(dst));
    }

    [Fact]
    public void TXS_SetsNoFlags()
    {
        var cpu = CpuTestHarness.NewCpuWithProgram(out _, 0x9A);
        cpu.P = StatusFlags.None;
        cpu.X = 0x00;
        Assert.Equal(2, cpu.RunInstruction());
        Assert.Equal(StatusFlags.None, cpu.P);
        Assert.Equal((byte)0x00, cpu.S);
    }
    
    [Fact]
    public void INX_WrapsFFToZero_SetsZ()
    {
        var cpu = CpuTestHarness.NewCpuWithProgram(out _, 0xE8);
        cpu.P = StatusFlags.None;
        cpu.X = 0xFF;
        Assert.Equal(2, cpu.RunInstruction());
        Assert.Equal(StatusFlags.Z, cpu.P);
        Assert.Equal((byte)0x00, cpu.X);
    }
    
    [Fact]
    public void INY_SetsN()
    {
        var cpu = CpuTestHarness.NewCpuWithProgram(out _, 0xC8);
        cpu.P = StatusFlags.None;
        cpu.Y = 0x7F;
        Assert.Equal(2, cpu.RunInstruction());
        Assert.Equal(StatusFlags.N, cpu.P);
        Assert.Equal((byte)0x80, cpu.Y);
    }
    
    [Fact]
    public void DEX_ToZero_SetsZ()
    {
        var cpu = CpuTestHarness.NewCpuWithProgram(out _, 0xCA);
        cpu.P = StatusFlags.None;
        cpu.X = 0x01;
        Assert.Equal(2, cpu.RunInstruction());
        Assert.Equal(StatusFlags.Z, cpu.P);
        Assert.Equal((byte)0x00, cpu.X);
    }
    
    [Fact]
    public void DEY_WrapsZeroToFF_SetsN()
    {
        var cpu = CpuTestHarness.NewCpuWithProgram(out _, 0x88);
        cpu.P = StatusFlags.None;
        cpu.Y = 0x00;
        Assert.Equal(2, cpu.RunInstruction());
        Assert.Equal(StatusFlags.N, cpu.P);
        Assert.Equal((byte)0xFF, cpu.Y);
    }

    private const StatusFlags All =
        StatusFlags.C | StatusFlags.Z | StatusFlags.I | StatusFlags.D | StatusFlags.V | StatusFlags.N;

    // Clears start from All and sets from None, so a stray change to any other flag fails.
    [Theory]
    [InlineData(0x18, All, All & ~StatusFlags.C)]
    [InlineData(0x38, StatusFlags.None, StatusFlags.C)]
    [InlineData(0x58, All, All & ~StatusFlags.I)]
    [InlineData(0x78, StatusFlags.None, StatusFlags.I)]
    [InlineData(0xD8, All, All & ~StatusFlags.D)]
    [InlineData(0xF8, StatusFlags.None, StatusFlags.D)]
    [InlineData(0xB8, All, All & ~StatusFlags.V)]
    public void FlagOp_ChangesOnlyItsFlag(byte op, StatusFlags before, StatusFlags after)
    {
        var cpu = CpuTestHarness.NewCpuWithProgram(out _, op);
        cpu.P = before;
        Assert.Equal(2, cpu.RunInstruction());
        Assert.Equal(after, cpu.P);
    }

    [Theory]
    [InlineData(0x50, 0x10, false, 0x60, StatusFlags.None)]
    [InlineData(0x50, 0x50, false, 0xA0, StatusFlags.V | StatusFlags.N)]
    [InlineData(0xD0, 0x90, false, 0x60, StatusFlags.C | StatusFlags.V)]
    [InlineData(0xFF, 0x01, false, 0x00, StatusFlags.C | StatusFlags.Z)]
    [InlineData(0x00, 0x00, true, 0x01, StatusFlags.None)]
    [InlineData(0x7F, 0x00, true, 0x80, StatusFlags.V | StatusFlags.N)]
    public void ADC_Immediate(byte a, byte m, bool carryIn, byte expectedA, StatusFlags expectedP)
    {
        var cpu = CpuTestHarness.NewCpuWithProgram(out _, 0x69, m);
        cpu.A = a;
        cpu.P = carryIn ? StatusFlags.C : StatusFlags.None;
        Assert.Equal(2, cpu.RunInstruction());
        Assert.Equal(expectedP, cpu.P);
        Assert.Equal(expectedA, cpu.A);
    }

    // C = 1 means no borrow. $EB is the unofficial copy of $E9.
    [Theory]
    [InlineData(0xE9, 0x50, 0xF0, true, 0x60, StatusFlags.None)]
    [InlineData(0xE9, 0x50, 0xB0, true, 0xA0, StatusFlags.V | StatusFlags.N)]
    [InlineData(0xE9, 0x00, 0x01, true, 0xFF, StatusFlags.N)]
    [InlineData(0xE9, 0x50, 0x30, false, 0x1F, StatusFlags.C)]
    [InlineData(0xEB, 0x50, 0x30, false, 0x1F, StatusFlags.C)]
    public void SBC_Immediate(byte op, byte a, byte m, bool carryIn, byte expectedA, StatusFlags expectedP)
    {
        var cpu = CpuTestHarness.NewCpuWithProgram(out _, op, m);
        cpu.A = a;
        cpu.P = carryIn ? StatusFlags.C : StatusFlags.None;
        Assert.Equal(2, cpu.RunInstruction());
        Assert.Equal(expectedP, cpu.P);
        Assert.Equal(expectedA, cpu.A);
    }

    // The 2A03 has no BCD: D is a plain flag.
    [Fact]
    public void ADC_InDecimalMode_IsBinary()
    {
        var cpu = CpuTestHarness.NewCpuWithProgram(out _, 0x69, 0x01);
        cpu.A = 0x09;
        cpu.P = StatusFlags.D;
        Assert.Equal(2, cpu.RunInstruction());
        Assert.Equal(StatusFlags.D, cpu.P);
        Assert.Equal((byte)0x0A, cpu.A);
    }

    // N is bit 7 of (reg - m), not "less than": see the 00/81 and FF/01 rows.
    [Theory]
    [InlineData(0xC9, 0x10, 0x10, StatusFlags.C | StatusFlags.Z)]
    [InlineData(0xC9, 0x10, 0x20, StatusFlags.N)]
    [InlineData(0xC9, 0x20, 0x10, StatusFlags.C)]
    [InlineData(0xC9, 0x00, 0x81, StatusFlags.None)]
    [InlineData(0xC9, 0xFF, 0x01, StatusFlags.C | StatusFlags.N)]
    [InlineData(0xE0, 0x10, 0x10, StatusFlags.C | StatusFlags.Z)]
    [InlineData(0xE0, 0x10, 0x20, StatusFlags.N)]
    [InlineData(0xE0, 0x20, 0x10, StatusFlags.C)]
    [InlineData(0xE0, 0x00, 0x81, StatusFlags.None)]
    [InlineData(0xE0, 0xFF, 0x01, StatusFlags.C | StatusFlags.N)]
    [InlineData(0xC0, 0x10, 0x10, StatusFlags.C | StatusFlags.Z)]
    [InlineData(0xC0, 0x10, 0x20, StatusFlags.N)]
    [InlineData(0xC0, 0x20, 0x10, StatusFlags.C)]
    [InlineData(0xC0, 0x00, 0x81, StatusFlags.None)]
    [InlineData(0xC0, 0xFF, 0x01, StatusFlags.C | StatusFlags.N)]
    public void Compare_Immediate(byte op, byte register, byte m, StatusFlags expectedP)
    {
        var cpu = CpuTestHarness.NewCpuWithProgram(out _, op, m);
        cpu.A = cpu.X = cpu.Y = register;
        cpu.P = StatusFlags.None;
        Assert.Equal(2, cpu.RunInstruction());
        Assert.Equal(expectedP, cpu.P);
        Assert.Equal(register, cpu.A);
        Assert.Equal(register, cpu.X);
        Assert.Equal(register, cpu.Y);
    }

    [Theory]
    [InlineData(0x29, 0xF0, 0x0F, 0x00, StatusFlags.Z)]
    [InlineData(0x29, 0xF0, 0x90, 0x90, StatusFlags.N)]
    [InlineData(0x09, 0xF0, 0x0F, 0xFF, StatusFlags.N)]
    [InlineData(0x09, 0x00, 0x00, 0x00, StatusFlags.Z)]
    [InlineData(0x49, 0xF0, 0xF0, 0x00, StatusFlags.Z)]
    [InlineData(0x49, 0xF0, 0x0F, 0xFF, StatusFlags.N)]
    public void Logic_Immediate(byte op, byte a, byte m, byte expectedA, StatusFlags expectedP)
    {
        var cpu = CpuTestHarness.NewCpuWithProgram(out _, op, m);
        cpu.A = a;
        cpu.P = StatusFlags.None;
        Assert.Equal(2, cpu.RunInstruction());
        Assert.Equal(expectedP, cpu.P);
        Assert.Equal(expectedA, cpu.A);
    }

    [Theory]
    [InlineData(0x0A, 0x81, false, 0x02, StatusFlags.C)]
    [InlineData(0x0A, 0x40, true, 0x80, StatusFlags.N)]
    [InlineData(0x4A, 0x01, false, 0x00, StatusFlags.C | StatusFlags.Z)]
    [InlineData(0x4A, 0xFF, true, 0x7F, StatusFlags.C)]
    [InlineData(0x2A, 0x80, true, 0x01, StatusFlags.C)]
    [InlineData(0x2A, 0x40, false, 0x80, StatusFlags.N)]
    // Carry-in differs from the bit shifted out, so a mixed-up old/new carry fails.
    [InlineData(0x2A, 0x00, true, 0x01, StatusFlags.None)]
    [InlineData(0x2A, 0x80, false, 0x00, StatusFlags.C | StatusFlags.Z)]
    [InlineData(0x6A, 0x01, true, 0x80, StatusFlags.C | StatusFlags.N)]
    [InlineData(0x6A, 0x02, false, 0x01, StatusFlags.None)]
    [InlineData(0x6A, 0x00, true, 0x80, StatusFlags.N)]
    [InlineData(0x6A, 0x01, false, 0x00, StatusFlags.C | StatusFlags.Z)]
    public void Shift_Accumulator(byte op, byte a, bool carryIn, byte expectedA, StatusFlags expectedP)
    {
        var cpu = CpuTestHarness.NewCpuWithProgram(out _, op);
        cpu.A = a;
        cpu.P = carryIn ? StatusFlags.C : StatusFlags.None;
        Assert.Equal(2, cpu.RunInstruction());
        Assert.Equal(expectedP, cpu.P);
        Assert.Equal(expectedA, cpu.A);
    }

    [Theory]
    [InlineData(0x1A, 0x8001)]
    [InlineData(0xFA, 0x8001)]
    [InlineData(0x80, 0x8002)]
    [InlineData(0xE2, 0x8002)]
    public void UnofficialNop_TwoCycles_ChangesOnlyPc(byte op, ushort expectedPc)
    {
        var cpu = CpuTestHarness.NewCpuWithProgram(out _, op, 0x89);
        cpu.P = StatusFlags.None;
        Assert.Equal(2, cpu.RunInstruction());
        Assert.Equal(expectedPc, cpu.PC);
        Assert.Equal(StatusFlags.None, cpu.P);
        Assert.Equal((byte)0x00, cpu.A);
        Assert.Equal((byte)0x00, cpu.X);
        Assert.Equal((byte)0x00, cpu.Y);
    }

    // A & m = 0 gives Z, while N and V come from m alone.
    [Fact]
    public void BIT_SetsNVFromMemory_ZFromAnd()
    {
        var cpu = CpuTestHarness.NewCpuWithProgram(out var bus, 0x24, 0x10);
        bus.Ram.Load(0x0010, [0xC0]);
        cpu.A = 0x3F;
        cpu.P = StatusFlags.None;
        Assert.Equal(3, cpu.RunInstruction());
        Assert.Equal(StatusFlags.Z | StatusFlags.V | StatusFlags.N, cpu.P);
        Assert.Equal((byte)0x3F, cpu.A);
    }

    [Fact]
    public void BIT_ClearsStaleNVZ()
    {
        var cpu = CpuTestHarness.NewCpuWithProgram(out var bus, 0x24, 0x10);
        bus.Ram.Load(0x0010, [0x01]);
        cpu.A = 0x01;
        cpu.P = StatusFlags.Z | StatusFlags.V | StatusFlags.N;
        Assert.Equal(3, cpu.RunInstruction());
        Assert.Equal(StatusFlags.None, cpu.P);
        Assert.Equal((byte)0x01, cpu.A);
    }

    [Fact]
    public void BIT_Absolute()
    {
        var cpu = CpuTestHarness.NewCpuWithProgram(out var bus, 0x2C, 0x34, 0x12);
        bus.Ram.Load(0x1234, [0x40]);
        cpu.A = 0xFF;
        cpu.P = StatusFlags.None;
        Assert.Equal(4, cpu.RunInstruction());
        Assert.Equal(StatusFlags.V, cpu.P);
        Assert.Equal((byte)0xFF, cpu.A);
    }

    // A SetZN copied from the loads would set Z on the 00 rows and clear Z|N on the 01 rows.
    [Theory]
    [InlineData(0x85, CpuTestHarness.Register.A, 0x00, StatusFlags.None)]
    [InlineData(0x85, CpuTestHarness.Register.A, 0x01, StatusFlags.Z | StatusFlags.N)]
    [InlineData(0x86, CpuTestHarness.Register.X, 0x00, StatusFlags.None)]
    [InlineData(0x86, CpuTestHarness.Register.X, 0x01, StatusFlags.Z | StatusFlags.N)]
    [InlineData(0x84, CpuTestHarness.Register.Y, 0x00, StatusFlags.None)]
    [InlineData(0x84, CpuTestHarness.Register.Y, 0x01, StatusFlags.Z | StatusFlags.N)]
    public void Store_SetsNoFlags(byte op, CpuTestHarness.Register register, byte value, StatusFlags p)
    {
        var cpu = CpuTestHarness.NewCpuWithProgram(out var bus, op, 0x10);
        bus.Ram.Load(0x0010, [0x99]);
        cpu.Set(register, value);
        cpu.P = p;
        Assert.Equal(3, cpu.RunInstruction());
        Assert.Equal(p, cpu.P);
        Assert.Equal(value, bus.Ram.Peek(0x0010));
    }

    [Theory]
    [InlineData(0xE6, 0x7F, StatusFlags.None, 0x80, StatusFlags.N)]
    [InlineData(0xE6, 0xFF, StatusFlags.None, 0x00, StatusFlags.Z)]
    [InlineData(0xE6, 0x00, StatusFlags.Z | StatusFlags.N, 0x01, StatusFlags.None)]
    [InlineData(0xC6, 0x01, StatusFlags.None, 0x00, StatusFlags.Z)]
    [InlineData(0xC6, 0x00, StatusFlags.None, 0xFF, StatusFlags.N)]
    [InlineData(0xC6, 0x80, StatusFlags.Z | StatusFlags.N, 0x7F, StatusFlags.None)]
    // C and V are untouched.
    [InlineData(0xE6, 0x00, StatusFlags.C | StatusFlags.V, 0x01, StatusFlags.C | StatusFlags.V)]
    [InlineData(0xC6, 0x00, StatusFlags.C | StatusFlags.V, 0xFF, StatusFlags.C | StatusFlags.V | StatusFlags.N)]
    public void IncDec_Memory(byte op, byte m, StatusFlags before, byte expectedM, StatusFlags expectedP)
    {
        var cpu = CpuTestHarness.NewCpuWithProgram(out var bus, op, 0x10);
        bus.Ram.Load(0x0010, [m]);
        cpu.P = before;
        Assert.Equal(5, cpu.RunInstruction());
        Assert.Equal(expectedP, cpu.P);
        Assert.Equal(expectedM, bus.Ram.Peek(0x0010));
        Assert.Equal((byte)0x00, cpu.A);
    }

    // Same Modify as the accumulator forms; these check memory gets the result, not A.
    [Theory]
    [InlineData(0x06, 0x81, false, 0x02, StatusFlags.C)]
    [InlineData(0x46, 0x01, false, 0x00, StatusFlags.C | StatusFlags.Z)]
    [InlineData(0x26, 0x40, true, 0x81, StatusFlags.N)]
    [InlineData(0x66, 0x02, true, 0x81, StatusFlags.N)]
    [InlineData(0x66, 0x01, false, 0x00, StatusFlags.C | StatusFlags.Z)]
    public void Shift_Memory(byte op, byte m, bool carryIn, byte expectedM, StatusFlags expectedP)
    {
        var cpu = CpuTestHarness.NewCpuWithProgram(out var bus, op, 0x10);
        bus.Ram.Load(0x0010, [m]);
        cpu.A = 0x55;
        cpu.P = carryIn ? StatusFlags.C : StatusFlags.None;
        Assert.Equal(5, cpu.RunInstruction());
        Assert.Equal(expectedP, cpu.P);
        Assert.Equal(expectedM, bus.Ram.Peek(0x0010));
        Assert.Equal((byte)0x55, cpu.A);
    }
}