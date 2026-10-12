# OddFrame

[![ci](https://github.com/p-o-d/oddframe/actions/workflows/ci.yml/badge.svg)](https://github.com/p-o-d/oddframe/actions/workflows/ci.yml)

OddFrame is a cycle-stepped NES emulator written in C# on .NET 10, with an Avalonia desktop app and a headless command-line tool. It is a personal coding exercise: the emulation code is written by hand.

Status: milestone 0 (skeleton). The solution builds, `Device` ticks a master clock, and the test-ROM tooling is in place. No CPU, PPU, or APU yet.

## Requirements

- .NET SDK 10. `global.json` pins 10.0.100 and rolls forward to the newest installed 10.0.x.
- For test ROMs: `bash`, `curl`, and `sha256sum` (GNU coreutils).

## Build and test

```bash
dotnet build
dotnet test                                   # everything
dotnet test --filter "Category!=Rom"          # fast unit tests only
dotnet test --filter "Category=Rom"           # test-ROM suite
tests/roms/fetch.sh                           # download test ROMs and verify checksums
dotnet run --project src/OddFrame.App         # open the (empty) desktop window
```

## Test ROMs

ROMs are not committed. `tests/roms/fetch.sh` downloads 44 files from [christopherpow/nes-test-roms](https://github.com/christopherpow/nes-test-roms) at a pinned commit and verifies them against `tests/roms/checksums.txt`. A second run downloads nothing and only verifies.

Without the ROMs, tests tagged `Category=Rom` are skipped, not failed, so `dotnet test` is green on a fresh clone.

`tests/roms/fetch.sh --refresh` re-downloads everything and rewrites `checksums.txt`. Use it only when bumping the pinned commit.

## Layout

```
src/OddFrame.Core/          emulation core, class library, no dependencies outside the BCL
src/OddFrame.App/           Avalonia desktop app
src/OddFrame.Cli/           headless console app
tests/OddFrame.Core.Tests/  xUnit v3 tests
tests/roms/                 test-ROM fetch script and checksums
```

## Docs

- [Architecture design](docs/superpowers/specs/2026-10-10-nes-emulator-architecture-design.md): components, timing model, interfaces, and the milestone plan (section 8).
- [AGENTS.md](AGENTS.md): rules for AI assistants working in this repository.

## License

[MIT](LICENSE)
