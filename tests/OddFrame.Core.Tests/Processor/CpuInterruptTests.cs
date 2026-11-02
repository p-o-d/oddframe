using OddFrame.Core.Processor;
using OddFrame.Core.Tests.Fakes;

namespace OddFrame.Core.Tests.Processor;

// Lines are set between Tick() calls, the way Device will drive them. A line set before
// cycle n is seen by the sample at the end of cycle n. An instruction polls on its last
// cycle, using the sample from the end of the second-to-last one.
public class CpuInterruptTests
{
    private const ushort IrqHandler = 0x9000;
    private const ushort NmiHandler = 0xA000;

    private static Cpu NewCpu(out RecordingBus bus, params byte[] program)
    {
        return NewCpuAt(out bus, 0x8000, program);
    }

    // S is $FD after reset. Both handlers start with NOPs.
    private static Cpu NewCpuAt(out RecordingBus bus, ushort origin, params byte[] program)
    {
        var cpu = CpuTestHarness.NewCpu(out bus, origin);
        bus.Ram.Load(origin, program);
        bus.Ram.Load(0xFFFA, [0x00, 0xA0]);
        bus.Ram.Load(0xFFFE, [0x00, 0x90]);
        bus.Ram.Load(IrqHandler, [0xEA, 0xEA, 0xEA, 0xEA]);
        bus.Ram.Load(NmiHandler, [0xEA, 0xEA, 0xEA, 0xEA]);
        return cpu;
    }

    // The sequence starts at the NOP's boundary: the opcode fetch at $8001 is discarded and PC is not incremented.
    [Fact]
    public void Irq_WhenIClear_SevenCycleSequence_PushesPWithBClearUSet_SetsI_LoadsFFFE()
    {
        var cpu = NewCpu(out var bus, 0xEA, 0xEA);
        cpu.P = StatusFlags.None;
        cpu.IrqLine = true;
        Assert.Equal(2, cpu.RunInstruction());
        bus.Log.Clear();
        Assert.Equal(7, cpu.RunInstruction());
        Assert.Equal(
            [
                (0x8001, false), (0x8001, false),
                (0x01FD, true), (0x01FC, true), (0x01FB, true),
                (0xFFFE, false), (0xFFFF, false)
            ],
            bus.Addresses());
        Assert.Equal([(0x01FD, 0x80), (0x01FC, 0x01), (0x01FB, 0x20)], bus.Writes());
        Assert.Equal(IrqHandler, cpu.PC);
        Assert.Equal(StatusFlags.I, cpu.P);
        Assert.Equal((byte)0xFA, cpu.S);
    }

    [Fact]
    public void Irq_WhenISet_Ignored()
    {
        var cpu = NewCpu(out var bus, 0xEA, 0xEA);
        cpu.P = StatusFlags.I;
        cpu.IrqLine = true;
        Assert.Equal(2, cpu.RunInstruction());
        Assert.Equal(2, cpu.RunInstruction());
        Assert.Equal((ushort)0x8002, cpu.PC);
        Assert.Empty(bus.Writes());
    }

    // RTI restores I=0 while the line is still high, so the IRQ is taken again right away.
    [Fact]
    public void Irq_LevelSensitive_FiresAgainWhileHeld()
    {
        var cpu = NewCpu(out var bus, 0xEA);
        bus.Ram.Load(IrqHandler, [0x40]);
        cpu.P = StatusFlags.None;
        cpu.IrqLine = true;
        Assert.Equal(2, cpu.RunInstruction());
        Assert.Equal(7, cpu.RunInstruction());
        Assert.Equal(6, cpu.RunInstruction());
        Assert.Equal((ushort)0x8001, cpu.PC);
        bus.Log.Clear();
        Assert.Equal(7, cpu.RunInstruction());
        Assert.Equal(IrqHandler, cpu.PC);
        Assert.Equal([(0x01FD, 0x80), (0x01FC, 0x01), (0x01FB, 0x20)], bus.Writes());
    }

    // The first sample sees the line already low, so nothing was ever pending.
    [Fact]
    public void Irq_DroppedBeforeSecondToLastCycle_NotTaken()
    {
        var cpu = NewCpu(out _, 0xA5, 0x10, 0xEA);
        cpu.P = StatusFlags.None;
        cpu.IrqLine = true;
        cpu.Tick();
        cpu.IrqLine = false;
        cpu.RunCycles(2);
        Assert.True(cpu.IsInstructionBoundary);
        Assert.Equal(2, cpu.RunInstruction());
        Assert.Equal((ushort)0x8003, cpu.PC);
    }

    // The poll reads the stored sample from the end of cycle 2, not the live line on cycle 3.
    [Fact]
    public void Irq_SampledOnSecondToLastCycle_TakenEvenIfDroppedOnLastCycle()
    {
        var cpu = NewCpu(out _, 0xA5, 0x10, 0xEA);
        cpu.P = StatusFlags.None;
        cpu.Tick();
        cpu.IrqLine = true;
        cpu.Tick();
        cpu.IrqLine = false;
        cpu.Tick();
        Assert.True(cpu.IsInstructionBoundary);
        Assert.Equal(7, cpu.RunInstruction());
        Assert.Equal(IrqHandler, cpu.PC);
    }

    // Raised during LDA's last cycle: the poll already happened, so the next instruction runs first.
    [Fact]
    public void Irq_RaisedOnLastCycle_WaitsOneInstruction()
    {
        var cpu = NewCpu(out var bus, 0xA5, 0x10, 0xEA, 0xEA);
        cpu.P = StatusFlags.None;
        cpu.RunCycles(2);
        cpu.IrqLine = true;
        cpu.Tick();
        Assert.True(cpu.IsInstructionBoundary);
        Assert.Equal(2, cpu.RunInstruction());
        bus.Log.Clear();
        Assert.Equal(7, cpu.RunInstruction());
        Assert.Equal([(0x01FD, 0x80), (0x01FC, 0x03), (0x01FB, 0x22)], bus.Writes()); // LDA of $00 set Z
    }

    // Edge-triggered and not maskable: I is set, the line stays high, and the NMI fires once.
    [Fact]
    public void Nmi_EdgeTriggered_FiresOnce()
    {
        var cpu = NewCpu(out var bus, 0xEA, 0xEA);
        cpu.P = StatusFlags.I;
        cpu.Nmi = true;
        Assert.Equal(2, cpu.RunInstruction());
        bus.Log.Clear();
        Assert.Equal(7, cpu.RunInstruction());
        Assert.Equal((0xFFFA, false), bus.Addresses()[5]);
        Assert.Equal((0xFFFB, false), bus.Addresses()[6]);
        Assert.Equal([(0x01FD, 0x80), (0x01FC, 0x01), (0x01FB, 0x24)], bus.Writes());
        Assert.Equal(NmiHandler, cpu.PC);
        Assert.Equal(2, cpu.RunInstruction());
        Assert.Equal(2, cpu.RunInstruction());
        Assert.Equal((ushort)0xA002, cpu.PC);
    }

    [Fact]
    public void Nmi_RearmsAfterFallingAndRisingAgain()
    {
        var cpu = NewCpu(out _, 0xEA);
        cpu.P = StatusFlags.I;
        cpu.Nmi = true;
        cpu.RunInstruction();
        Assert.Equal(7, cpu.RunInstruction());
        cpu.Nmi = false;
        Assert.Equal(2, cpu.RunInstruction());
        cpu.Nmi = true;
        Assert.Equal(2, cpu.RunInstruction());
        Assert.Equal(7, cpu.RunInstruction());
        Assert.Equal(NmiHandler, cpu.PC);
    }

    // The pulse is high only during LDA's last cycle, so it is never polled while the line is high.
    // The latch must hold it.
    [Fact]
    public void Nmi_ShortPulse_StaysLatchedUntilServiced()
    {
        var cpu = NewCpu(out _, 0xA5, 0x10, 0xEA);
        cpu.P = StatusFlags.None;
        cpu.RunCycles(2);
        cpu.Nmi = true;
        cpu.Tick();
        cpu.Nmi = false;
        Assert.Equal(2, cpu.RunInstruction());
        Assert.Equal(7, cpu.RunInstruction());
        Assert.Equal(NmiHandler, cpu.PC);
    }

    [Fact]
    public void Nmi_HasPriorityOverIrq()
    {
        var cpu = NewCpu(out _, 0xEA);
        cpu.P = StatusFlags.None;
        cpu.Nmi = true;
        cpu.IrqLine = true;
        cpu.RunInstruction();
        Assert.Equal(7, cpu.RunInstruction());
        Assert.Equal(NmiHandler, cpu.PC);
    }

    // CLI clears I on its last cycle, after the poll used the sample from cycle 1 (I still set).
    [Fact]
    public void Cli_DelaysIrqByOneInstruction()
    {
        var cpu = NewCpu(out var bus, 0x58, 0xEA, 0xEA);
        cpu.P = StatusFlags.I;
        cpu.IrqLine = true;
        Assert.Equal(2, cpu.RunInstruction());
        Assert.Equal(2, cpu.RunInstruction());
        bus.Log.Clear();
        Assert.Equal(7, cpu.RunInstruction());
        Assert.Equal([(0x01FD, 0x80), (0x01FC, 0x02), (0x01FB, 0x20)], bus.Writes());
    }

    // SEI sets I on its last cycle, after the poll already saw I clear. The pushed P has I set.
    [Fact]
    public void Sei_DoesNotBlockIrqAlreadySampled()
    {
        var cpu = NewCpu(out var bus, 0x78, 0xEA);
        cpu.P = StatusFlags.None;
        cpu.IrqLine = true;
        Assert.Equal(2, cpu.RunInstruction());
        bus.Log.Clear();
        Assert.Equal(7, cpu.RunInstruction());
        Assert.Equal([(0x01FD, 0x80), (0x01FC, 0x01), (0x01FB, 0x24)], bus.Writes());
        Assert.Equal(IrqHandler, cpu.PC);
    }

    // PLP writes P on cycle 4 of 4, like CLI.
    [Fact]
    public void Plp_SameDelayAsCli()
    {
        var cpu = NewCpu(out var bus, 0x28, 0xEA, 0xEA);
        cpu.P = StatusFlags.I;
        cpu.S = 0xFC;
        bus.Ram.Load(0x01FD, [0x00]);
        cpu.IrqLine = true;
        Assert.Equal(4, cpu.RunInstruction());
        Assert.Equal(2, cpu.RunInstruction());
        bus.Log.Clear();
        Assert.Equal(7, cpu.RunInstruction());
        Assert.Equal([(0x01FD, 0x80), (0x01FC, 0x02), (0x01FB, 0x20)], bus.Writes());
    }

    // PLP setting I is the mirror case, like SEI.
    [Fact]
    public void Plp_SettingI_DoesNotBlockIrqAlreadySampled()
    {
        var cpu = NewCpu(out var bus, 0x28, 0xEA);
        cpu.P = StatusFlags.None;
        cpu.S = 0xFC;
        bus.Ram.Load(0x01FD, [0x04]);
        cpu.IrqLine = true;
        Assert.Equal(4, cpu.RunInstruction());
        bus.Log.Clear();
        Assert.Equal(7, cpu.RunInstruction());
        Assert.Equal([(0x01FD, 0x80), (0x01FC, 0x01), (0x01FB, 0x24)], bus.Writes());
    }

    // RTI writes P on cycle 4 of 6, so the poll on cycle 6 already sees I clear.
    [Fact]
    public void Rti_NoDelay()
    {
        var cpu = NewCpu(out var bus, 0x40);
        bus.Ram.Load(0x01FB, [0x00, 0x00, 0x81]);
        bus.Ram.Load(0x8100, [0xEA, 0xEA]);
        cpu.P = StatusFlags.I;
        cpu.S = 0xFA;
        cpu.IrqLine = true;
        Assert.Equal(6, cpu.RunInstruction());
        Assert.Equal((ushort)0x8100, cpu.PC);
        bus.Log.Clear();
        Assert.Equal(7, cpu.RunInstruction());
        Assert.Equal([(0x01FD, 0x81), (0x01FC, 0x00), (0x01FB, 0x20)], bus.Writes());
    }

    [Fact]
    public void NotTakenBranch_PollsNormally()
    {
        var cpu = NewCpu(out var bus, 0x90, 0x05, 0xEA);
        cpu.P = StatusFlags.C;
        cpu.IrqLine = true;
        Assert.Equal(2, cpu.RunInstruction());
        bus.Log.Clear();
        Assert.Equal(7, cpu.RunInstruction());
        Assert.Equal([(0x01FD, 0x80), (0x01FC, 0x02), (0x01FB, 0x21)], bus.Writes());
    }

    // A taken branch still polls, with the sample from the end of cycle 1.
    [Fact]
    public void TakenBranch_NoPageCross_IrqBeforeCycleTwo_FiresAfterBranch()
    {
        var cpu = NewCpu(out var bus, 0xB0, 0x00, 0xEA);
        cpu.P = StatusFlags.C;
        cpu.IrqLine = true;
        Assert.Equal(3, cpu.RunInstruction());
        bus.Log.Clear();
        Assert.Equal(7, cpu.RunInstruction());
        Assert.Equal([(0x01FD, 0x80), (0x01FC, 0x02), (0x01FB, 0x21)], bus.Writes());
    }

    // The 3-cycle taken branch polls as if it had 2 cycles: the sample from the end of cycle 2 is ignored.
    [Fact]
    public void TakenBranch_NoPageCross_DelaysIrqOneInstruction()
    {
        var cpu = NewCpu(out var bus, 0xB0, 0x00, 0xEA, 0xEA);
        cpu.P = StatusFlags.C;
        cpu.Tick();
        cpu.IrqLine = true;
        cpu.RunCycles(2);
        Assert.True(cpu.IsInstructionBoundary);
        Assert.Equal((ushort)0x8002, cpu.PC);
        Assert.Equal(2, cpu.RunInstruction());
        bus.Log.Clear();
        Assert.Equal(7, cpu.RunInstruction());
        Assert.Equal([(0x01FD, 0x80), (0x01FC, 0x03), (0x01FB, 0x21)], bus.Writes());
    }

    // $80FF + 1 = $8100 crosses a page. The 4-cycle branch polls normally, on cycle 4 with the cycle 3 sample.
    [Fact]
    public void TakenBranch_PageCross_PollsAtCycleFour()
    {
        var cpu = NewCpuAt(out var bus, 0x80FD, 0xB0, 0x01);
        bus.Ram.Load(0x8100, [0xEA]);
        cpu.P = StatusFlags.C;
        cpu.RunCycles(2);
        cpu.IrqLine = true;
        cpu.RunCycles(2);
        Assert.True(cpu.IsInstructionBoundary);
        Assert.Equal((ushort)0x8100, cpu.PC);
        bus.Log.Clear();
        Assert.Equal(7, cpu.RunInstruction());
        Assert.Equal([(0x01FD, 0x81), (0x01FC, 0x00), (0x01FB, 0x21)], bus.Writes());
    }

    // An NMI edge seen at the end of BRK's cycles 1-4 takes over the vector at cycle 5. The pushed P
    // keeps B set, and the latch is cleared, so no second NMI follows. A later edge is too late:
    // BRK goes to $9000 and the NMI runs after the handler's first instruction.
    [Theory]
    [InlineData(0, true)]
    [InlineData(2, true)]
    [InlineData(3, true)]
    [InlineData(4, false)]
    [InlineData(6, false)]
    public void Nmi_DuringBrk_HijacksOnlyEarly(int ticksBeforeNmi, bool hijacked)
    {
        var cpu = NewCpu(out var bus, 0x00, 0x55);
        cpu.P = StatusFlags.None;
        cpu.RunCycles(ticksBeforeNmi);
        cpu.Nmi = true;
        cpu.RunCycles(7 - ticksBeforeNmi);
        Assert.True(cpu.IsInstructionBoundary);
        Assert.Equal([(0x01FD, 0x80), (0x01FC, 0x02), (0x01FB, 0x30)], bus.Writes());
        Assert.Equal(hijacked ? NmiHandler : IrqHandler, cpu.PC);
        Assert.Equal(2, cpu.RunInstruction());
        Assert.Equal(hijacked ? 2 : 7, cpu.RunInstruction());
        Assert.Equal(hijacked ? (ushort)0xA002 : NmiHandler, cpu.PC);
    }

    // Plan name for the BRK case: an NMI during cycle 3.
    [Fact]
    public void Nmi_HijacksBrk()
    {
        var cpu = NewCpu(out var bus, 0x00, 0x55);
        cpu.P = StatusFlags.None;
        cpu.RunCycles(2);
        cpu.Nmi = true;
        cpu.RunCycles(5);
        Assert.Equal(NmiHandler, cpu.PC);
        Assert.Equal((0x01FB, (byte)0x30), bus.Writes()[2]);
    }

    // The same takeover for an IRQ sequence. The pushed P keeps B clear.
    [Fact]
    public void Nmi_HijacksIrq()
    {
        var cpu = NewCpu(out var bus, 0xEA);
        cpu.P = StatusFlags.None;
        cpu.IrqLine = true;
        cpu.RunInstruction();
        bus.Log.Clear();
        cpu.RunCycles(2);
        cpu.Nmi = true;
        cpu.RunCycles(5);
        Assert.True(cpu.IsInstructionBoundary);
        Assert.Equal(NmiHandler, cpu.PC);
        Assert.Equal([(0x01FD, 0x80), (0x01FC, 0x01), (0x01FB, 0x20)], bus.Writes());
        Assert.Equal(2, cpu.RunInstruction());
    }

    // Reset keeps its vector. The NMI stays latched and fires after the first instruction.
    [Fact]
    public void Nmi_DoesNotHijackReset()
    {
        var cpu = NewCpu(out _, 0xEA);
        cpu.Reset();
        cpu.RunCycles(2);
        cpu.Nmi = true;
        cpu.RunCycles(5);
        Assert.True(cpu.IsInstructionBoundary);
        Assert.Equal((ushort)0x8000, cpu.PC);
        Assert.Equal(2, cpu.RunInstruction());
        Assert.Equal(7, cpu.RunInstruction());
        Assert.Equal(NmiHandler, cpu.PC);
    }

    // A jammed CPU ignores both lines; only Reset recovers it.
    [Fact]
    public void Halted_IgnoresInterrupts()
    {
        var cpu = NewCpu(out var bus, 0x02);
        cpu.P = StatusFlags.None;
        cpu.IrqLine = true;
        cpu.Nmi = true;
        cpu.RunCycles(20);
        Assert.True(cpu.Halted);
        Assert.Equal((ushort)0x8001, cpu.PC);
        Assert.Empty(bus.Writes());
    }
}
