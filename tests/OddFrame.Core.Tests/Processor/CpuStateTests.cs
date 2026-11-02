using OddFrame.Core.Bus;
using OddFrame.Core.Processor;
using OddFrame.Core.Tests.Fakes;
using OddFrame.Core.Tests.Roms;

namespace OddFrame.Core.Tests.Processor;

public class CpuStateTests
{
    private const ushort NmiHandler = 0x9000;
    private const ushort IrqHandler = 0x9100;
    private const int RamSize = 0x10000;

    // A loop that touches every latch: a page-crossing read once X >= 6, RMW, a pointer read,
    // the stack, JSR/RTS, BRK/RTI, a taken branch across a page and one within a page.
    // A and Y change every pass, so a lost register shows up in the reads and the PHA write.
    // DriveLines adds IRQ and NMI pulses on top.
    private static readonly byte[] SweepProgram =
    [
        0x58,             // $8000 CLI
        0xC8,             // $8001 INY
        0xBD, 0xFA, 0x02, // $8002 LDA $02FA,X
        0xE6, 0x10,       // $8005 INC $10
        0xB1, 0x20,       // $8007 LDA ($20),Y
        0x48,             // $8009 PHA
        0x68,             // $800A PLA
        0x20, 0xF0, 0x80, // $800B JSR $80F0
        0x00, 0xEA,       // $800E BRK
        0xB8,             // $8010 CLV
        0x50, 0xED,       // $8011 BVC $8000
    ];

    private static Cpu NewCpu(out RecordingBus bus, params byte[] program)
    {
        var cpu = CpuTestHarness.NewCpuWithProgram(out bus, program);
        bus.Ram.Load(0xFFFA, [0x00, 0x90]);
        bus.Ram.Load(0xFFFE, [0x00, 0x91]);
        bus.Ram.Load(NmiHandler, [0x40]);
        bus.Ram.Load(IrqHandler, [0x40]);
        return cpu;
    }

    private static Cpu NewSweepCpu(out RecordingBus bus)
    {
        var cpu = NewCpu(out bus, SweepProgram);
        bus.Ram.Load(0x80F0, [0xE8, 0xD0, 0x0E, 0x60]); // INX; BNE $8101; RTS
        bus.Ram.Load(0x8101, [0x60]);                   // RTS
        bus.Ram.Load(0x0020, [0x00, 0x03]);
        for (var i = 0x02FA; i < 0x0400; i++)
            bus.Ram.Write((ushort)i, (byte)(i * 37 + 11));
        return cpu;
    }

    // A pure function of saved state, so a clone sees the same lines on the same cycles.
    private static void DriveLines(Cpu cpu)
    {
        cpu.IrqLine = cpu.TotalCycles % 97 < 3;
        cpu.Nmi = cpu.TotalCycles % 61 < 2;
    }

    private static void RunDriven(Cpu cpu, int cycles)
    {
        for (var i = 0; i < cycles; i++)
        {
            DriveLines(cpu);
            cpu.Tick();
        }
    }

    private static byte[] SaveBytes(Cpu cpu)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        cpu.Save(writer);
        writer.Flush();
        return stream.ToArray();
    }

    private static byte[] CopyRam(RamBus ram)
    {
        var copy = new byte[RamSize];
        for (var i = 0; i < RamSize; i++)
            copy[i] = ram.Peek((ushort)i);
        return copy;
    }

    // The RAM is copied at the save point: it belongs to the bus, not to the Cpu.
    private static Cpu Clone(Cpu cpu, RamBus ram, IBus cloneBus, RamBus cloneRam)
    {
        cloneRam.Load(0, CopyRam(ram));
        var clone = new Cpu(cloneBus);
        using var reader = new BinaryReader(new MemoryStream(SaveBytes(cpu)));
        clone.Load(reader);
        return clone;
    }

    private static Cpu Clone(Cpu cpu, RecordingBus bus, out RecordingBus cloneBus)
    {
        cloneBus = new RecordingBus();
        return Clone(cpu, bus.Ram, cloneBus, cloneBus.Ram);
    }

    [Fact]
    public void SaveLoad_EveryCycle_ContinuesIdentically()
    {
        const int sweep = 800;
        const int after = 100;
        for (var saveAt = 0; saveAt < sweep; saveAt++)
        {
            var cpu = NewSweepCpu(out var bus);
            RunDriven(cpu, saveAt);
            var clone = Clone(cpu, bus, out var cloneBus);
            bus.Log.Clear();
            RunDriven(cpu, after);
            RunDriven(clone, after);

            for (var i = 0; i < Math.Min(bus.Log.Count, cloneBus.Log.Count); i++)
            {
                if (bus.Log[i] != cloneBus.Log[i])
                    Assert.Fail($"saved at cycle {saveAt}: access {i} was {bus.Log[i]}, clone {cloneBus.Log[i]}");
            }

            Assert.Equal(bus.Log.Count, cloneBus.Log.Count);
            Assert.Equal(SaveBytes(cpu), SaveBytes(clone));
        }
    }

    // Cycle 1 samples the IRQ, then the line drops. The taken branch polls that cycle 1
    // sample on cycle 3, so only a saved _branchSample keeps the IRQ.
    [Fact]
    public void SaveLoad_MidTakenBranch_KeepsBranchSample()
    {
        var cpu = NewCpu(out var bus, 0xB0, 0x00, 0xEA);
        cpu.P = StatusFlags.C;
        cpu.IrqLine = true;
        cpu.Tick();
        cpu.IrqLine = false;
        cpu.Tick();
        var clone = Clone(cpu, bus, out _);
        cpu.Tick();
        clone.Tick();
        Assert.Equal(7, cpu.RunInstruction());
        Assert.Equal(7, clone.RunInstruction());
        Assert.Equal(IrqHandler, clone.PC);
    }

    // The poll on LDA's last cycle reads the sample stored at the end of cycle 2.
    [Fact]
    public void SaveLoad_BeforeLastCycle_KeepsInterruptSample()
    {
        var cpu = NewCpu(out var bus, 0xA5, 0x10, 0xEA);
        cpu.P = StatusFlags.None;
        cpu.Tick();
        cpu.IrqLine = true;
        cpu.Tick();
        cpu.IrqLine = false;
        var clone = Clone(cpu, bus, out _);
        cpu.Tick();
        clone.Tick();
        Assert.Equal(7, cpu.RunInstruction());
        Assert.Equal(7, clone.RunInstruction());
        Assert.Equal(IrqHandler, clone.PC);
    }

    // Without _nmiPrevious the clone sees the held line as a fresh edge.
    [Fact]
    public void SaveLoad_NmiHeldHigh_NoSpuriousEdge()
    {
        var cpu = NewCpu(out var bus, 0xEA);
        bus.Ram.Load(NmiHandler, [0xEA, 0xEA, 0xEA]);
        cpu.P = StatusFlags.I;
        cpu.Nmi = true;
        cpu.RunInstruction();
        Assert.Equal(7, cpu.RunInstruction());
        var clone = Clone(cpu, bus, out _);
        Assert.True(clone.Nmi);
        Assert.Equal(2, cpu.RunInstruction());
        Assert.Equal(2, clone.RunInstruction());
        Assert.Equal(2, cpu.RunInstruction());
        Assert.Equal(2, clone.RunInstruction());
        Assert.Equal((ushort)0x9002, clone.PC);
    }

    // The pulse is seen only at the end of LDA's last cycle, and the line is low at the save.
    [Fact]
    public void SaveLoad_NmiLatchedNotPolled_StillFires()
    {
        var cpu = NewCpu(out var bus, 0xA5, 0x10, 0xEA, 0xEA);
        cpu.P = StatusFlags.None;
        cpu.RunCycles(2);
        cpu.Nmi = true;
        cpu.Tick();
        cpu.Nmi = false;
        var clone = Clone(cpu, bus, out _);
        Assert.Equal(2, clone.RunInstruction());
        Assert.Equal(7, clone.RunInstruction());
        Assert.Equal(NmiHandler, clone.PC);
        Assert.Equal(2, cpu.RunInstruction());
        Assert.Equal(7, cpu.RunInstruction());
        Assert.Equal(NmiHandler, cpu.PC);
    }

    // Saved after cycle 3 of an IRQ sequence: PCH is pushed, PCL and P are not.
    [Fact]
    public void SaveLoad_MidInterruptSequence_Continues()
    {
        var cpu = NewCpu(out var bus, 0xEA);
        cpu.P = StatusFlags.None;
        cpu.IrqLine = true;
        cpu.RunInstruction();
        cpu.RunCycles(3);
        var clone = Clone(cpu, bus, out var cloneBus);
        bus.Log.Clear();
        cpu.RunCycles(4);
        clone.RunCycles(4);
        Assert.True(clone.IsInstructionBoundary);
        Assert.Equal(IrqHandler, clone.PC);
        Assert.Equal(StatusFlags.I, clone.P);
        Assert.Equal([(0x01FC, 0x01), (0x01FB, 0x20)], cloneBus.Writes());
        Assert.Equal(bus.Log, cloneBus.Log);
    }

    // Device rewrites both lines before every tick, but a Cpu-only clone relies on the saved levels.
    [Fact]
    public void SaveLoad_KeepsInputLines()
    {
        var cpu = NewCpu(out var bus, 0xEA, 0xEA);
        cpu.P = StatusFlags.None;
        cpu.IrqLine = true;
        cpu.Nmi = true;
        var clone = Clone(cpu, bus, out _);
        Assert.True(clone.IrqLine);
        Assert.True(clone.Nmi);
    }

    [Fact]
    public void SaveLoad_Halted_StaysHalted()
    {
        var cpu = NewCpu(out var bus, 0x02);
        cpu.RunInstruction();
        var clone = Clone(cpu, bus, out var cloneBus);
        Assert.True(clone.Halted);
        clone.RunCycles(10);
        Assert.Equal((ushort)0x8001, clone.PC);
        Assert.Empty(cloneBus.Log);
    }

    // A swapped pair on Load (say A and X) round-trips to different bytes. The save point
    // is picked so the registers hold different values.
    [Fact]
    public void SaveLoadSave_RoundTripsBytes()
    {
        var cpu = NewSweepCpu(out var bus);
        RunDriven(cpu, 123);
        cpu.A = 0x11;
        cpu.X = 0x22;
        cpu.Y = 0x33;
        var clone = Clone(cpu, bus, out _);
        Assert.Equal(SaveBytes(cpu), SaveBytes(clone));
    }

    [Fact]
    public void Save_IsDeterministic()
    {
        var a = NewSweepCpu(out _);
        var b = NewSweepCpu(out _);
        RunDriven(a, 345);
        RunDriven(b, 345);
        Assert.Equal(SaveBytes(a), SaveBytes(b));
    }

    // Device.LoadState converts this to InvalidDataException; Cpu must let it through.
    [Fact]
    public void Load_Truncated_ThrowsEndOfStream()
    {
        var cpu = NewSweepCpu(out _);
        var state = SaveBytes(cpu);
        var target = new Cpu(new RecordingBus());
        using var reader = new BinaryReader(new MemoryStream(state[..^1]));
        Assert.Throws<EndOfStreamException>(() => target.Load(reader));
    }

    // Device rejects trailing bytes, so Load must consume exactly what Save wrote.
    [Fact]
    public void Load_ReadsExactlyWhatSaveWrote()
    {
        var cpu = NewSweepCpu(out _);
        var state = SaveBytes(cpu);
        using var stream = new MemoryStream([.. state, 0xAA]);
        using var reader = new BinaryReader(stream);
        new Cpu(new RecordingBus()).Load(reader);
        Assert.Equal(state.Length, stream.Position);
    }

    [Fact, Trait("Category", "Rom")]
    public void SaveLoad_MidInstruction_Nestest()
    {
        var rom = File.ReadAllBytes(TestRoms.Require("other/nestest.nes"));
        var prg = rom.AsSpan(16, 0x4000);
        var bus = new NestestBus();
        bus.Ram.Load(0x8000, prg);
        bus.Ram.Load(0xC000, prg);
        bus.Ram.Load(0xFFFC, [0x00, 0xC0]);
        var cpu = new Cpu(bus);
        cpu.RunCycles(1234);
        Assert.False(cpu.IsInstructionBoundary);

        var cloneBus = new NestestBus();
        var clone = Clone(cpu, bus.Ram, cloneBus, cloneBus.Ram);
        var traceA = new ListTraceSink();
        var traceB = new ListTraceSink();
        cpu.TraceSink = traceA;
        clone.TraceSink = traceB;
        cpu.RunCycles(500);
        clone.RunCycles(500);

        Assert.NotEmpty(traceA.Entries);
        Assert.Equal(traceA.Entries, traceB.Entries);
        Assert.Equal(SaveBytes(cpu), SaveBytes(clone));
        Assert.Equal(CopyRam(bus.Ram), CopyRam(cloneBus.Ram));
    }
}
