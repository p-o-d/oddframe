using System.Text;
using OddFrame.Core.Processor;
using OddFrame.Core.Tests.Fakes;

namespace OddFrame.Core.Tests.Roms;

public enum BlarggOutcome
{
    Finished,
    Timeout,
    Halted,
    ResetRequested,
}

public readonly record struct BlarggResult(BlarggOutcome Outcome, byte Status, string Text, ulong Cycles)
{
    public override string ToString()
    {
        return $"{Outcome}, status ${Status:X2} after {Cycles} cycles\n{Text}";
    }
}

// Runs an NROM blargg test on a RAM bus and reads the result protocol:
// $6000 status ($80 running, $81 reset request, else the result code),
// $6001-$6003 signature DE B0 61, zero-terminated text from $6004.
public static class BlarggRunner
{
    private const int HeaderSize = 16;
    private const int PrgBankSize = 0x4000;
    private const byte TrainerFlag = 0x04;
    private const ushort StatusAddress = 0x6000;
    private const ushort TextAddress = 0x6004;
    private const ushort TextEnd = 0x7FFF;
    private const byte Running = 0x80;
    private const byte ResetRequest = 0x81;

    public static BlarggResult Run(string romRelativePath, ulong maxCycles)
    {
        return Run(File.ReadAllBytes(TestRoms.Require(romRelativePath)), maxCycles);
    }

    public static BlarggResult Run(byte[] image, ulong maxCycles)
    {
        var prg = ParsePrg(image);
        var bus = new BlarggStubBus();
        bus.Ram.Load(0x8000, prg);
        if (prg.Length == PrgBankSize)
            bus.Ram.Load(0xC000, prg);

        var cpu = new Cpu(bus);
        while (cpu.TotalCycles < maxCycles)
        {
            cpu.Tick();
            if (cpu.Halted)
                return Result(BlarggOutcome.Halted, bus, cpu);
            if (!HasSignature(bus))
                continue;

            var status = bus.Peek(StatusAddress);
            if (status == Running)
                continue;
            return Result(status == ResetRequest ? BlarggOutcome.ResetRequested : BlarggOutcome.Finished, bus, cpu);
        }

        return Result(BlarggOutcome.Timeout, bus, cpu);
    }

    // iNES header: "NES\x1A", PRG size in 16 KB units at byte 4, mapper in the high nibbles of bytes 6 and 7.
    private static ReadOnlySpan<byte> ParsePrg(byte[] image)
    {
        if (image.Length < HeaderSize || !image.AsSpan(0, 4).SequenceEqual("NES\x1A"u8))
            throw new InvalidDataException("not an iNES image");
        var mapper = (image[6] >> 4) | (image[7] & 0xF0);
        if (mapper != 0)
            throw new InvalidDataException($"mapper {mapper} is not supported, only NROM (0)");
        if ((image[6] & TrainerFlag) != 0)
            throw new InvalidDataException("images with a trainer are not supported");
        var prgSize = image[4] * PrgBankSize;
        if (prgSize is not (PrgBankSize or 2 * PrgBankSize))
            throw new InvalidDataException($"PRG size {prgSize} is not 16 or 32 KB");
        if (image.Length < HeaderSize + prgSize)
            throw new InvalidDataException("image is shorter than its PRG size");
        return image.AsSpan(HeaderSize, prgSize);
    }

    private static bool HasSignature(BlarggStubBus bus)
    {
        return bus.Peek(0x6001) == 0xDE && bus.Peek(0x6002) == 0xB0 && bus.Peek(0x6003) == 0x61;
    }

    // Status and text mean nothing until the signature is written.
    private static BlarggResult Result(BlarggOutcome outcome, BlarggStubBus bus, Cpu cpu)
    {
        var valid = HasSignature(bus);
        var status = valid ? bus.Peek(StatusAddress) : (byte)0;
        var text = valid ? ReadText(bus) : "";
        return new BlarggResult(outcome, status, text, cpu.TotalCycles);
    }

    // Stops at $7FFF, so a missing terminator cannot run into PRG.
    private static string ReadText(BlarggStubBus bus)
    {
        var text = new StringBuilder();
        for (var address = TextAddress; address <= TextEnd; address++)
        {
            var b = bus.Peek(address);
            if (b == 0)
                break;
            text.Append((char)b);
        }

        return text.ToString();
    }
}
