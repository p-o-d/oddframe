# Milestone 0: Skeleton Implementation Plan

> **Executor:** the owner writes all code by hand (see `AGENTS.md`). Claude may write the files marked *(Claude may write)*: `.gitignore`, `global.json`, `tests/roms/fetch.sh`, `tests/roms/checksums.txt`, CI config, README. Everything under `src/` and `tests/OddFrame.Core.Tests/` is owner-written; this plan describes shape and tests, not full code. Steps use `- [ ]` for tracking.

**Goal:** A buildable, testable solution with `Device` ticking a master clock, the fake-bus test fixture ready for milestone 1, and a working test-ROM fetch script. `dotnet build && dotnet test` green on a fresh clone.

**Architecture:** Three projects plus tests per spec section 1. `Device` owns only the master clock and a `TimingProfile` for now; no `Cpu`/`Ppu` stubs. `IBus` and `IStateful` are defined now because milestone 1 tests depend on them. ROM tests skip when ROMs are missing.

**Tech Stack:** .NET SDK 10.0.112, C#, xUnit v3 (`Assert.Skip` for missing ROMs), Avalonia 12 (App scaffold only), bash + `sha256sum` + `curl` for ROM fetch, GitHub Actions.

**Spec:** `docs/superpowers/specs/2026-10-10-nes-emulator-architecture-design.md`, sections 1, 2, 3, 5 (Snapshots), 7, 8.

## Deviations during implementation

The plan below is kept as written. The implementation differs in these points:

- **Solution file** is `oddframe.slnx`, not `oddframe.sln`; spec section 1 is updated. Every `oddframe.sln` below means `oddframe.slnx`. Only the test project sits in a solution folder (`/tests/`); the three `src/` projects are at the solution root (`dotnet sln add --in-root`).
- **Branch** is `milestone-0-implementation`, not `milestone-0-skeleton`. The PR was opened during Task 10, because the CI `push` trigger only fires on `master`.
- **`global.json`** also sets `"test": {"runner": "Microsoft.Testing.Platform"}`, added by the xUnit v3 template. `dotnet test` exits with code 8 when a filter matches no tests; a run where every test is skipped exits 0.
- **Task 8:** Avalonia templates 12.1.4 no longer generate `BuiltInComInteropSupport` or `AvaloniaUseCompiledBindingsByDefault` (compiled bindings are the default), so there is nothing to keep.
- **Task 9:** the repo-root marker for `TestRoms.RootDirectory` is `global.json`. Committed in two parts: fetch script, then the skip helper.
- **Task 10:** actions are `checkout@v7`, `setup-dotnet@v6`, `cache@v6`. The ROM cache key hashes `fetch.sh` as well as `checksums.txt`, because the cached `tests/roms` directory contains the script and a hit would restore a stale copy. The workflow also sets `permissions: contents: read`, a per-ref `concurrency` group, and 10-minute job timeouts.

---

## Decisions locked for this plan

Change any of these before starting; the tasks assume them.

| Question | Choice | Why |
|---|---|---|
| Solution format | `oddframe.sln` via `dotnet new sln --format sln` | Matches spec section 1. SDK 10 defaults to `.slnx`; either works, `.sln` avoids a spec edit. |
| Test framework | xUnit v3 (`xunit.v3` 4.0.2) | Native `Assert.Skip()` for missing ROMs. xUnit 2 has no runtime skip without a third-party package. |
| App project | Scaffold from Avalonia template now, leave untouched | Spec says three projects in M0. CI then builds App from day one. No Avalonia code until M4. |
| Packages in Core | None | Spec rule. |
| Packages in Cli | None yet | `System.CommandLine` arrives in M2 with the `trace` command. |
| `ImplicitUsings` | Enabled everywhere | BCL-only usings, less noise. |

## Known traps (verified 2026-10-10)

- `.gitignore` line 11 `[Dd]ebug/` ignores `src/OddFrame.Core/Debug/`. Confirmed with `git check-ignore`. Task 1 removes it.
- Two SDKs installed (9.0.121, 10.0.112). Task 1 adds `global.json`.
- Avalonia templates not installed. Task 8 installs them.
- `mmc3_test_2` ROMs in the upstream repo live under `mmc3_test_2/rom_singles/`, sprite hit tests under `sprite_hit_tests_2005.10.05/`. Task 9 uses these exact paths.
- Shared git stash across worktrees. Do not use bare `git stash` here.

## File structure

```
global.json                                   SDK pin
.gitignore                                    fixed Debug/ rule, ROM rules
oddframe.sln
Directory.Build.props                         shared: net10.0, nullable, implicit usings
src/OddFrame.Core/OddFrame.Core.csproj        warnings as errors, InternalsVisibleTo tests
src/OddFrame.Core/Device.cs                   master clock, Tick(), IStateful
src/OddFrame.Core/DeviceOptions.cs            Region, EnableDebug
src/OddFrame.Core/Region.cs                   enum Ntsc, Dendy, Pal
src/OddFrame.Core/TimingProfile.cs            record + Ntsc instance + For(Region)
src/OddFrame.Core/IStateful.cs                Save/Load
src/OddFrame.Core/Bus/IBus.cs                 Read/Peek/Write
src/OddFrame.Cli/OddFrame.Cli.csproj
src/OddFrame.Cli/Program.cs                   prints usage, exit code 1
src/OddFrame.App/                             Avalonia template output, untouched
tests/OddFrame.Core.Tests/OddFrame.Core.Tests.csproj
tests/OddFrame.Core.Tests/TimingProfileTests.cs
tests/OddFrame.Core.Tests/DeviceTests.cs
tests/OddFrame.Core.Tests/Fakes/RamBus.cs     flat 64 KB IBus for M1
tests/OddFrame.Core.Tests/RamBusTests.cs
tests/OddFrame.Core.Tests/Roms/TestRoms.cs    path resolution + skip helper
tests/OddFrame.Core.Tests/Roms/TestRomsTests.cs
tests/roms/fetch.sh                           (Claude may write)
tests/roms/checksums.txt                      (generated by fetch.sh, committed)
.github/workflows/ci.yml                      (Claude may write)
README.md                                     commands
```

---

### Task 1: Repo hygiene

**Files:**
- Modify: `.gitignore`
- Create: `global.json`

- [ ] **Step 1: Confirm branch**

Run: `git status --short --branch`
Expected: `## milestone-0-skeleton...origin/master`, clean.

- [ ] **Step 2: Fix `.gitignore`** *(Claude may write)*

Delete line 11 (`[Dd]ebug/`). `[Bb]in/` and `[Oo]bj/` already cover build output. Append:

```gitignore
# Test ROMs are fetched, never committed
tests/roms/*
!tests/roms/fetch.sh
!tests/roms/checksums.txt
```

- [ ] **Step 3: Verify the trap is gone**

Run: `git check-ignore -v src/OddFrame.Core/Debug/ObservableBus.cs tests/roms/nestest.nes tests/roms/fetch.sh`
Expected: only `tests/roms/nestest.nes` is listed as ignored. Exit code non-zero is fine because the other two are not ignored.

- [ ] **Step 4: Add `global.json`** *(Claude may write)*

```json
{
  "sdk": {
    "version": "10.0.100",
    "rollForward": "latestFeature"
  }
}
```

Run: `dotnet --version`
Expected: `10.0.112`

- [ ] **Step 5: Commit**

```bash
git add .gitignore global.json
git commit -m "chore: pin SDK 10 and unignore Core/Debug"
```

---

### Task 2: Solution and Core project

**Files:**
- Create: `oddframe.sln`, `Directory.Build.props`, `src/OddFrame.Core/OddFrame.Core.csproj`

- [ ] **Step 1: Scaffold**

```bash
dotnet new sln --name oddframe --format sln
dotnet new classlib -n OddFrame.Core -o src/OddFrame.Core
rm src/OddFrame.Core/Class1.cs
dotnet sln add src/OddFrame.Core
```

- [ ] **Step 2: Shared props**

`Directory.Build.props` at repo root:

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <LangVersion>latest</LangVersion>
  </PropertyGroup>
</Project>
```

- [ ] **Step 3: Core csproj**

Replace the generated `src/OddFrame.Core/OddFrame.Core.csproj` body so it contains only Core-specific settings:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <RootNamespace>OddFrame.Core</RootNamespace>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <AllowUnsafeBlocks>false</AllowUnsafeBlocks>
  </PropertyGroup>
  <ItemGroup>
    <InternalsVisibleTo Include="OddFrame.Core.Tests" />
  </ItemGroup>
</Project>
```

`InternalsVisibleTo` now so milestone 1 tests can read `cycle` and `opcode` without making them public.

- [ ] **Step 4: Build empty library**

Run: `dotnet build`
Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`

- [ ] **Step 5: Commit**

```bash
git add oddframe.sln Directory.Build.props src/OddFrame.Core
git commit -m "feat: add solution and empty Core library"
```

---

### Task 3: Test project and `TimingProfile`

**Files:**
- Create: `tests/OddFrame.Core.Tests/OddFrame.Core.Tests.csproj`
- Create: `tests/OddFrame.Core.Tests/TimingProfileTests.cs`
- Create: `src/OddFrame.Core/Region.cs`, `src/OddFrame.Core/TimingProfile.cs`

- [ ] **Step 1: Scaffold test project with xUnit v3**

```bash
dotnet new install xunit.v3.templates
dotnet new xunit3 -n OddFrame.Core.Tests -o tests/OddFrame.Core.Tests
rm tests/OddFrame.Core.Tests/UnitTest1.cs
dotnet sln add tests/OddFrame.Core.Tests
dotnet add tests/OddFrame.Core.Tests reference src/OddFrame.Core
```

If the template install fails, scaffold with `dotnet new xunit` and edit the csproj: set `<OutputType>Exe</OutputType>` (xUnit v3 test projects are executables), replace `xunit` 2.9.3 with `xunit.v3` 4.0.2, bump `xunit.runner.visualstudio` to 4.0.1 and `Microsoft.NET.Test.Sdk` to 18.10.1, drop `coverlet.collector`. Remove `TargetFramework`/`Nullable`/`ImplicitUsings` lines: `Directory.Build.props` supplies them.

- [ ] **Step 2: Write failing `TimingProfileTests`**

One test class, `[Fact]` per row of the spec section 2 region table, NTSC column only:

- `Ntsc_MasterClockHz_Is21477272`
- `Ntsc_CpuDivider_Is12`
- `Ntsc_PpuDivider_Is4`
- `Ntsc_ScanlinesPerFrame_Is262`
- `Ntsc_NmiScanline_Is241`
- `Ntsc_OddFrameDotSkip_IsTrue`
- `For_Ntsc_ReturnsNtscProfile` asserting `TimingProfile.For(Region.Ntsc)` is the same instance as `TimingProfile.Ntsc`
- `For_Dendy_Throws` and `For_Pal_Throws` asserting `NotSupportedException`

Shape of one test: `Assert.Equal(12, TimingProfile.Ntsc.CpuDivider);`

- [ ] **Step 3: Run, confirm compile failure**

Run: `dotnet test --filter "FullyQualifiedName~TimingProfileTests"`
Expected: build error `The name 'TimingProfile' does not exist`.

- [ ] **Step 4: Write `Region` and `TimingProfile`**

`Region`: `enum Region { Ntsc, Dendy, Pal }`.

`TimingProfile`: `sealed record` with positional members `uint MasterClockHz`, `int CpuDivider`, `int PpuDivider`, `int ScanlinesPerFrame`, `int NmiScanline`, `bool OddFrameDotSkip`. One `static readonly TimingProfile Ntsc` with the NTSC column. `static TimingProfile For(Region region)` switch: Ntsc returns `Ntsc`, anything else throws `NotSupportedException` naming the region. APU tables are not in this record yet; add them in milestone 7 when their shape is known.

- [ ] **Step 5: Run, confirm green**

Run: `dotnet test --filter "FullyQualifiedName~TimingProfileTests"`
Expected: `Passed! - Failed: 0, Passed: 9`

- [ ] **Step 6: Commit**

```bash
git add tests/OddFrame.Core.Tests src/OddFrame.Core/Region.cs src/OddFrame.Core/TimingProfile.cs
git commit -m "feat: add TimingProfile with NTSC constants"
```

---

### Task 4: `Device` master clock

**Files:**
- Create: `src/OddFrame.Core/DeviceOptions.cs`, `src/OddFrame.Core/Device.cs`
- Create: `tests/OddFrame.Core.Tests/DeviceTests.cs`

- [ ] **Step 1: Write failing `DeviceTests`**

- `NewDevice_MasterClockIsZero`: `new Device(new DeviceOptions())`, assert `MasterClock == 0UL`.
- `NewDevice_UsesProfileForRegion`: default options, assert `device.Timing` is `TimingProfile.Ntsc`.
- `Tick_IncrementsMasterClockByOne`: tick once, assert 1; tick two more, assert 3.
- `StepCpuCycle_AdvancesCpuDividerTicks`: call `StepCpuCycle()`, assert `MasterClock == (ulong)TimingProfile.Ntsc.CpuDivider`. Call again, assert 24.

- [ ] **Step 2: Run, confirm compile failure**

Run: `dotnet test --filter "FullyQualifiedName~DeviceTests"`
Expected: `The name 'Device' does not exist`.

- [ ] **Step 3: Write `DeviceOptions` and `Device`**

`DeviceOptions`: `sealed record` or class with `Region Region { get; init; } = Region.Ntsc;` and `bool EnableDebug { get; init; }`.

`Device` (namespace `OddFrame.Core`, `sealed`):
- Constructor takes `DeviceOptions`, stores `Timing = TimingProfile.For(options.Region)`.
- `ulong MasterClock { get; private set; }`. `ulong`, never `uint`: 21.5 M ticks per second wraps a `uint` in 200 seconds.
- `void Tick()`: increment `MasterClock`. Leave the two divider checks from spec section 2 as a comment, not code, until `Ppu`/`Cpu` exist. Keep PPU-before-CPU order in that comment; it matters for NMI edge timing.
- `void StepCpuCycle()`: tick until `MasterClock % CpuDivider == 0` has happened once. Simplest: loop `Tick()` `CpuDivider` times only works from an aligned clock; prefer `do Tick(); while (MasterClock % (ulong)Timing.CpuDivider != 0);` so it is correct from any phase.

- [ ] **Step 4: Run, confirm green**

Run: `dotnet test --filter "FullyQualifiedName~DeviceTests"`
Expected: `Passed: 4`

- [ ] **Step 5: Commit**

```bash
git add src/OddFrame.Core/Device.cs src/OddFrame.Core/DeviceOptions.cs tests/OddFrame.Core.Tests/DeviceTests.cs
git commit -m "feat: add Device with master clock"
```

---

### Task 5: `IStateful` and `Device` save/load

**Files:**
- Create: `src/OddFrame.Core/IStateful.cs`
- Modify: `src/OddFrame.Core/Device.cs`
- Modify: `tests/OddFrame.Core.Tests/DeviceTests.cs`

- [ ] **Step 1: Write failing tests**

- `SaveLoad_RoundTripsMasterClock`: tick device A 37 times, `var bytes = a.SaveState()`, `b.LoadState(bytes)` on a fresh device, assert `b.MasterClock == 37UL`.
- `LoadState_RejectsWrongMagic`: `Assert.Throws<InvalidDataException>(() => device.LoadState(new byte[16]))`.
- `SaveState_IsDeterministic`: two fresh devices ticked the same count produce byte-identical arrays. This is the seed of the spec section 7 layer 3 determinism tests.

- [ ] **Step 2: Run, confirm failure**

Run: `dotnet test --filter "FullyQualifiedName~DeviceTests"`
Expected: compile error on `SaveState`.

- [ ] **Step 3: Write `IStateful` and the Device methods**

`IStateful` exactly as spec section 5: `void Save(BinaryWriter writer); void Load(BinaryReader reader);`.

`Device : IStateful`:
- `Save`: write `MasterClock`. Nothing else yet.
- `Load`: read it back.
- `byte[] SaveState()`: `MemoryStream` + `BinaryWriter`. Header first: 4-byte ASCII magic (`ODDF`), `ushort` version `1`, then a 32-byte ROM hash slot. No cartridge yet, so write 32 zero bytes; milestone 2 fills it. Then `Save(writer)`.
- `void LoadState(byte[] state)`: verify magic and version, throw `InvalidDataException` on mismatch, skip the hash slot, then `Load(reader)`.

Fixing the header layout now means every later component just appends to `Save`/`Load`.

- [ ] **Step 4: Run, confirm green**

Run: `dotnet test`
Expected: all passing, 0 failed.

- [ ] **Step 5: Commit**

```bash
git add src/OddFrame.Core/IStateful.cs src/OddFrame.Core/Device.cs tests/OddFrame.Core.Tests/DeviceTests.cs
git commit -m "feat: add IStateful and Device save state"
```

---

### Task 6: `IBus` and the `RamBus` test fixture

**Files:**
- Create: `src/OddFrame.Core/Bus/IBus.cs`
- Create: `tests/OddFrame.Core.Tests/Fakes/RamBus.cs`
- Create: `tests/OddFrame.Core.Tests/RamBusTests.cs`

This fixture is the whole milestone 1 harness. Build and test it now.

- [ ] **Step 1: Write failing `RamBusTests`**

- `Write_ThenRead_ReturnsValue`: write `0xA5` at `0x1234`, read back `0xA5`.
- `Peek_MatchesRead`: write, then `Peek` equals `Read`.
- `NewBus_IsZeroed`: `Read(0xFFFF) == 0`.
- `Load_CopiesBytesAtOffset`: `bus.Load(0xC000, new byte[] { 0x4C, 0x00, 0xC0 })`, `Read(0xC002) == 0xC0`. Milestone 1 uses this to drop nestest PRG at `$C000`.

- [ ] **Step 2: Run, confirm compile failure**

Run: `dotnet test --filter "FullyQualifiedName~RamBusTests"`

- [ ] **Step 3: Write `IBus` and `RamBus`**

`IBus` in namespace `OddFrame.Core.Bus`, verbatim from spec section 3:

```csharp
public interface IBus
{
    byte Read(ushort address);
    byte Peek(ushort address);
    void Write(ushort address, byte value);
}
```

`RamBus` in the tests project, namespace `OddFrame.Core.Tests.Fakes`: `byte[65536]`, `Read` and `Peek` both index it, `Write` stores, `Load(ushort offset, ReadOnlySpan<byte> data)` copies. Expose the array as `Memory` for direct assertions later.

- [ ] **Step 4: Run, confirm green**

Run: `dotnet test`
Expected: all passing.

- [ ] **Step 5: Commit**

```bash
git add src/OddFrame.Core/Bus tests/OddFrame.Core.Tests/Fakes tests/OddFrame.Core.Tests/RamBusTests.cs
git commit -m "feat: add IBus and RamBus test fixture"
```

---

### Task 7: Cli stub

**Files:**
- Create: `src/OddFrame.Cli/OddFrame.Cli.csproj`, `src/OddFrame.Cli/Program.cs`

- [ ] **Step 1: Scaffold**

```bash
dotnet new console -n OddFrame.Cli -o src/OddFrame.Cli
dotnet sln add src/OddFrame.Cli
dotnet add src/OddFrame.Cli reference src/OddFrame.Core
```

Strip `TargetFramework`/`Nullable`/`ImplicitUsings` from the csproj; props supply them.

- [ ] **Step 2: `Program.cs`**

Top-level statements. Print one usage line listing the three future commands (`run`, `trace`, `test`) to stderr and return exit code `1`. Reference `Device` once (construct and discard) so the project reference is exercised by the build. No `System.CommandLine` until milestone 2.

- [ ] **Step 3: Verify**

Run: `dotnet run --project src/OddFrame.Cli; echo "exit=$?"`
Expected: usage line, `exit=1`.

- [ ] **Step 4: Commit**

```bash
git add src/OddFrame.Cli
git commit -m "feat: add Cli project stub"
```

---

### Task 8: App scaffold

**Files:**
- Create: `src/OddFrame.App/` from template

- [ ] **Step 1: Install templates and scaffold**

```bash
dotnet new install Avalonia.Templates
dotnet new avalonia.mvvm -n OddFrame.App -o src/OddFrame.App
dotnet sln add src/OddFrame.App
dotnet add src/OddFrame.App reference src/OddFrame.Core
```

`avalonia.mvvm` brings `CommunityToolkit.Mvvm`, which spec section 6 names. Do not edit the generated views. Strip duplicated `TargetFramework`/`Nullable` lines from the csproj if present; keep Avalonia-specific properties (`BuiltInComInteropSupport`, `ApplicationManifest`, `AvaloniaUseCompiledBindingsByDefault`).

- [ ] **Step 2: Verify**

Run: `dotnet build`
Expected: four projects build, 0 errors. Warnings from the Avalonia template are acceptable here; only Core has warnings-as-errors.

Run: `dotnet run --project src/OddFrame.App`
Expected: empty window opens. Close it.

- [ ] **Step 3: Commit**

```bash
git add src/OddFrame.App oddframe.sln
git commit -m "feat: add Avalonia App project scaffold"
```

---

### Task 9: Test-ROM fetch script and skip helper

**Files:**
- Create: `tests/roms/fetch.sh` *(Claude may write)*
- Create: `tests/roms/checksums.txt` (generated)
- Create: `tests/OddFrame.Core.Tests/Roms/TestRoms.cs`, `tests/OddFrame.Core.Tests/Roms/TestRomsTests.cs`

- [ ] **Step 1: `fetch.sh`** *(Claude may write)*

Source: `christopherpow/nes-test-roms` pinned at commit `95d8f621ae55cee0d09b91519a8989ae0e64753b`. Pinning keeps `checksums.txt` stable; bump the SHA deliberately.

```bash
#!/usr/bin/env bash
# Downloads the test ROMs used by OddFrame.Core.Tests and verifies SHA-256.
# Usage: tests/roms/fetch.sh            download missing files, verify all
#        tests/roms/fetch.sh --refresh  re-download everything and rewrite checksums.txt
set -euo pipefail

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
sha="95d8f621ae55cee0d09b91519a8989ae0e64753b"
base="https://raw.githubusercontent.com/christopherpow/nes-test-roms/${sha}"

files=(
  other/nestest.nes
  other/nestest.log
  instr_test-v5/all_instrs.nes
  instr_test-v5/official_only.nes
  cpu_timing_test6/cpu_timing_test.nes
  instr_misc/instr_misc.nes
  cpu_interrupts_v2/cpu_interrupts.nes
  ppu_vbl_nmi/ppu_vbl_nmi.nes
  sprite_hit_tests_2005.10.05/01.basics.nes
  sprite_hit_tests_2005.10.05/02.alignment.nes
  sprite_hit_tests_2005.10.05/03.corners.nes
  sprite_hit_tests_2005.10.05/04.flip.nes
  sprite_hit_tests_2005.10.05/05.left_clip.nes
  sprite_hit_tests_2005.10.05/06.right_edge.nes
  sprite_hit_tests_2005.10.05/07.screen_bottom.nes
  sprite_hit_tests_2005.10.05/08.double_height.nes
  sprite_hit_tests_2005.10.05/09.timing_basics.nes
  sprite_hit_tests_2005.10.05/10.timing_order.nes
  sprite_hit_tests_2005.10.05/11.edge_timing.nes
  sprite_overflow_tests/1.Basics.nes
  sprite_overflow_tests/2.Details.nes
  sprite_overflow_tests/3.Timing.nes
  sprite_overflow_tests/4.Obscure.nes
  sprite_overflow_tests/5.Emulator.nes
  oam_read/oam_read.nes
  ppu_open_bus/ppu_open_bus.nes
  apu_test/apu_test.nes
  apu_reset/4015_cleared.nes
  apu_reset/4017_timing.nes
  apu_reset/4017_written.nes
  apu_reset/irq_flag_cleared.nes
  apu_reset/len_ctrs_enabled.nes
  apu_reset/works_immediately.nes
  dmc_dma_during_read4/dma_2007_read.nes
  dmc_dma_during_read4/dma_2007_write.nes
  dmc_dma_during_read4/dma_4016_read.nes
  dmc_dma_during_read4/double_2007_read.nes
  dmc_dma_during_read4/read_write_2007.nes
  mmc3_test_2/rom_singles/1-clocking.nes
  mmc3_test_2/rom_singles/2-details.nes
  mmc3_test_2/rom_singles/3-A12_clocking.nes
  mmc3_test_2/rom_singles/4-scanline_timing.nes
  mmc3_test_2/rom_singles/5-MMC3.nes
  mmc3_test_2/rom_singles/6-MMC3_alt.nes
)

refresh=0
[[ "${1:-}" == "--refresh" ]] && refresh=1

cd "$here"
for f in "${files[@]}"; do
  if [[ $refresh -eq 1 || ! -f "$f" ]]; then
    mkdir -p "$(dirname "$f")"
    echo "fetch $f"
    curl -fsSL --retry 3 -o "$f" "${base}/${f}"
  fi
done

if [[ $refresh -eq 1 || ! -f checksums.txt ]]; then
  sha256sum "${files[@]}" > checksums.txt
  echo "wrote checksums.txt (${#files[@]} files)"
fi

sha256sum --check --quiet checksums.txt
echo "ok: ${#files[@]} ROM files verified"
```

- [ ] **Step 2: Run it, commit the checksums**

```bash
chmod +x tests/roms/fetch.sh
tests/roms/fetch.sh
git status --short tests/roms
```

Expected: last line `ok: 44 ROM files verified`. `git status` shows only `fetch.sh` and `checksums.txt`; no `.nes` files.

Run it a second time: no `fetch` lines, same `ok`.

- [ ] **Step 3: Write failing `TestRomsTests`**

- `Resolve_ReturnsPathUnderRepoRoot`: `TestRoms.Resolve("other/nestest.nes")` ends with `tests/roms/other/nestest.nes` and the parent chain contains `oddframe.sln`.
- `Require_MissingFile_Skips`: `[Fact]` that calls `TestRoms.Require("does/not/exist.nes")` and then `Assert.Fail("should have skipped")`. xUnit v3 reports it as skipped, not failed. Mark it `[Trait("Category", "Rom")]` so it demonstrates the filter too.
- `Require_PresentFile_ReturnsPath`: `[Trait("Category", "Rom")]`, calls `Require("other/nestest.nes")`, asserts `File.Exists`. Skips on a clone without ROMs.

- [ ] **Step 4: Write `TestRoms`**

Static class in `OddFrame.Core.Tests.Roms`:
- `RootDirectory`: walk up from `AppContext.BaseDirectory` until a directory containing `oddframe.sln` is found; throw `DirectoryNotFoundException` if the root of the filesystem is reached. Never resolve relative to `bin/`.
- `string Resolve(string relative)`: `System.IO.Path.Combine(RootDirectory, "tests", "roms", relative)`. Do not name this method `Path`: inside the class it would shadow `System.IO.Path`.
- `string Require(string relative)`: if the file is missing, `Assert.Skip($"test ROM missing: {relative}. Run tests/roms/fetch.sh")`. Else return the path.

- [ ] **Step 5: Run, confirm green and that filters work**

```bash
dotnet test
dotnet test --filter "Category!=Rom"
dotnet test --filter "Category=Rom"
```

Expected: first run shows 1 skipped (`Require_MissingFile_Skips`) and 0 failed. Second run excludes both Rom-tagged tests. Third runs only those two.

Then prove the fresh-clone promise: move `tests/roms/other` aside, run `dotnet test`, expect 2 skipped, 0 failed, move it back.

- [ ] **Step 6: Commit**

```bash
git add tests/roms/fetch.sh tests/roms/checksums.txt tests/OddFrame.Core.Tests/Roms
git commit -m "test: add ROM fetch script and skip helper"
```

---

### Task 10: CI

**Files:**
- Create: `.github/workflows/ci.yml` *(Claude may write)*

- [ ] **Step 1: Workflow**

```yaml
name: ci
on:
  push:
    branches: [master]
  pull_request:

jobs:
  fast:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          global-json-file: global.json
      - run: dotnet build --configuration Release
      - run: dotnet test --configuration Release --no-build --filter "Category!=Rom"

  roms:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          global-json-file: global.json
      - uses: actions/cache@v4
        with:
          path: tests/roms
          key: roms-${{ hashFiles('tests/roms/checksums.txt') }}
      - run: tests/roms/fetch.sh
      - run: dotnet build --configuration Release
      - run: dotnet test --configuration Release --no-build --filter "Category=Rom"
```

- [ ] **Step 2: Commit and push, watch the run**

```bash
git add .github/workflows/ci.yml
git commit -m "ci: build and test on push"
git push -u origin milestone-0-skeleton
gh run watch
```

Expected: both jobs green.

---

### Task 11: README and done check

**Files:**
- Modify: `README.md` *(Claude may write)*

- [ ] **Step 1: README**

One paragraph on what OddFrame is, the commands block from `AGENTS.md` trimmed to what exists now (`dotnet build`, `dotnet test`, the two filters, `tests/roms/fetch.sh`), link to the spec and `AGENTS.md`.

- [ ] **Step 2: Fresh-clone check**

```bash
git clone --branch milestone-0-skeleton . /tmp/oddframe-clonecheck
cd /tmp/oddframe-clonecheck
dotnet build && dotnet test
```

Expected: green with 2 skipped Rom tests. This is the spec's milestone 0 "done when" line.

- [ ] **Step 3: Commit, open PR**

```bash
git add README.md
git commit -m "docs: describe build and test commands"
git push
gh pr create --base master --title "Milestone 0: skeleton" --fill
```

---

## Self-review against spec section 8, milestone 0

| Requirement | Task |
|---|---|
| Solution | 2 |
| Three projects | 2, 7, 8 |
| Test project | 3 |
| `Device` with empty `Tick()` | 4 (ticks master clock only; divider dispatch commented until components exist) |
| `dotnet build && dotnet test` green | 11 |
| ROM fetch script works | 9 |

Extras beyond the letter of M0, each justified by M1 needing it on day one: `TimingProfile` (spec section 2 says no scattered constants), `IBus` + `RamBus` (section 7 layer 1), `IStateful` + state header (section 5), CI.

Not in this plan, deferred on purpose: any `Cpu`/`Ppu`/`Apu` type, `StepInstruction`/`StepScanline`/`StepFrame`, `ObservableBus`, `System.CommandLine`, PNG writer.
