using System.Text;
using OddFrame.Core.Bus;
using OddFrame.Core.Debug;
using OddFrame.Core.Processor;
using OddFrame.Core.Tests.Fakes;

namespace OddFrame.Core.Tests.Roms;

public class NestestTests
{
    private const int LogLines = 8991;
    private const ulong CycleCap = 100_000;
    private const int PrgSize = 0x4000;
    private const int HeaderSize = 16;
    private const ushort ResetVector = 0xFFFC;

    private sealed class FormattingSink(IBus bus) : ITraceSink
    {
        public List<string> Lines { get; } = [];

        public void OnInstruction(in CpuTraceEntry entry)
        {
            var scanline = (int)(entry.TotalCycles * 3 / 341 % 262);
            var dot = (int)(entry.TotalCycles * 3 % 341);
            Lines.Add(NestestFormatter.Format(entry, bus, scanline, dot));
        }
    }

    [Fact, Trait("Category", "Rom")]
    public void Trace_MatchesReferenceLog()
    {
        var rom = File.ReadAllBytes(TestRoms.Require("other/nestest.nes"));
        var expected = File.ReadAllLines(TestRoms.Require("other/nestest.log"));
        Assert.Equal("NES\x1A", Encoding.ASCII.GetString(rom, 0, 4));
        Assert.Equal((byte)1, rom[4]);
        var prg = rom.AsSpan(HeaderSize, PrgSize);
        var bus = new NestestBus();
        bus.Ram.Load(0x8000, prg);
        bus.Ram.Load(0xC000, prg);
        bus.Ram.Load(ResetVector, [0x00, 0xC0]);
        var cpu = new Cpu(bus);
        var sink = new FormattingSink(bus);
        cpu.TraceSink = sink;
        Exception? ex = null;
        while (sink.Lines.Count < LogLines && cpu.TotalCycles < CycleCap)
        {
            try
            {
                cpu.Tick();
            }
            catch (Exception e)
            {
                ex = e;
                break;
            }
        }

        var minLineCount = Math.Min(sink.Lines.Count, expected.Length);
        for (var i = 0; i < minLineCount; i++)
        {
            if (expected[i] == sink.Lines[i]) continue;
            var sb = new StringBuilder();
            sb.AppendLine($"nestest mismatch at line {i + 1}");
            sb.AppendLine($"prev: {(i > 0 ? sink.Lines[i - 1] : "none")}");
            sb.AppendLine($"want: {expected[i]}");
            sb.AppendLine($"got : {sink.Lines[i]}");
            Assert.Fail(sb.ToString());
        }

        if (sink.Lines.Count != expected.Length)
        {
            var sb = new StringBuilder();
            sb.Append($"trace stopped after {sink.Lines.Count} of {LogLines} lines, ");
            sb.AppendLine(ex is not null
                ? $"ex ({ex.GetType().Name}, {ex.Message}, PC=${cpu.PC:X4})"
                : "cycle cap hit");
            if (sink.Lines.Count > 0)
                sb.AppendLine($"last: {sink.Lines[^1]}");
            Assert.Fail(sb.ToString());
        }

        var code = bus.Peek(0x0002);
        if (code != 0)
            Assert.Fail($"$02 = ${code:X2}: official opcode failure (see nestest.txt)");
        code = bus.Peek(0x0003);
        if (code != 0)
            Assert.Fail($"$03 = ${code:X2}: unofficial opcode failure (see nestest.txt)");
    }
}