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
  instr_test-v5/rom_singles/01-basics.nes
  instr_test-v5/rom_singles/02-implied.nes
  instr_test-v5/rom_singles/03-immediate.nes
  instr_test-v5/rom_singles/04-zero_page.nes
  instr_test-v5/rom_singles/05-zp_xy.nes
  instr_test-v5/rom_singles/06-absolute.nes
  instr_test-v5/rom_singles/07-abs_xy.nes
  instr_test-v5/rom_singles/08-ind_x.nes
  instr_test-v5/rom_singles/09-ind_y.nes
  instr_test-v5/rom_singles/10-branches.nes
  instr_test-v5/rom_singles/11-stack.nes
  instr_test-v5/rom_singles/12-jmp_jsr.nes
  instr_test-v5/rom_singles/13-rts.nes
  instr_test-v5/rom_singles/14-rti.nes
  instr_test-v5/rom_singles/15-brk.nes
  instr_test-v5/rom_singles/16-special.nes
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
if [[ "${1:-}" == "--refresh" ]]; then
  refresh=1
fi

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
