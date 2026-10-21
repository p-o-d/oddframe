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
}