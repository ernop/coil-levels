# Gallery repairs — 2026-09-06

All 18 oversized-open-region specimens were regenerated and replayed. Their
IDs and seeds remain the same; their recorded options now explicitly include
`--wander-steps 8`. The current catalog, images, stats lab, and survey findings
use these replacements.

## Why the full-slide boards had huge open regions

The old `--wander-full` initial walk continued until it could make no further
moves. Maximal slides can spiral inward and sweep a broad region before any
tweaks are applied. At 500×500, seed 101 already contained an all-open 273×273
square before tweaking, and seed 303 contained a 487×487 square. Later tweaks
preserved most of these regions. They were legal, solution-covered cells,
but they made unsuitable specimens for this style collection.

Full-slide initial walks now default to eight segments. An explicit positive
`--wander-steps` still takes precedence. Before any tweak-generated level is
exported, `GenerationQuality.Validate` rejects an all-open square whose side
exceeds `max(8, min(width, height) / 5)` using integer division. Both existing
game-rule validators remain mandatory. A failed quality check emits no board.
This cutoff expresses the collection's quality policy; it is separate from
Coil solvability and from the generator's possible-board coverage.

The regression suite reproduces the historical seed-303 spiral and verifies
its rejection, the eight-segment default, explicit overrides, rectangular
boards, and the exact acceptance threshold.

## Before and after

All dimensions and square measurements below are side lengths. Each linked
record contains old/new board hashes, options, verification, and timing.

| Specimen | Open area before → after | Largest open square before → after |
|---|---:|---:|
| [300-control-2lim10-wanderfull-s101](repairs/300-control-2lim10-wanderfull-s101.json) | 87.043% → 71.377% | 161 → 6 |
| [300-control-2lim10-wanderfull-s202](repairs/300-control-2lim10-wanderfull-s202.json) | 97.318% → 71.494% | 231 → 6 |
| [300-control-2lim10-wanderfull-s303](repairs/300-control-2lim10-wanderfull-s303.json) | 99.221% → 71.432% | 244 → 5 |
| [300-control-last-wanderfull-s101](repairs/300-control-last-wanderfull-s101.json) | 85.446% → 68.468% | 164 → 5 |
| [300-control-last-wanderfull-s202](repairs/300-control-last-wanderfull-s202.json) | 97.107% → 68.334% | 267 → 5 |
| [300-control-last-wanderfull-s303](repairs/300-control-last-wanderfull-s303.json) | 99.059% → 68.701% | 285 → 6 |
| [300-control-rnd99-wanderfull-s101](repairs/300-control-rnd99-wanderfull-s101.json) | 84.597% → 66.050% | 163 → 4 |
| [300-control-rnd99-wanderfull-s202](repairs/300-control-rnd99-wanderfull-s202.json) | 96.824% → 65.810% | 263 → 5 |
| [300-control-rnd99-wanderfull-s303](repairs/300-control-rnd99-wanderfull-s303.json) | 99.037% → 66.199% | 279 → 5 |
| [500-control-rnd99-wanderfull-s101](repairs/500-control-rnd99-wanderfull-s101.json) | 84.458% → 66.117% | 272 → 5 |
| [500-control-rnd99-wanderfull-s303](repairs/500-control-rnd99-wanderfull-s303.json) | 99.129% → 66.002% | 467 → 5 |
| [1000-control-rnd99-wanderfull-s101](repairs/1000-control-rnd99-wanderfull-s101.json) | 84.536% → 66.085% | 543 → 5 |
| [1000-control-rnd99-wanderfull-s303](repairs/1000-control-rnd99-wanderfull-s303.json) | 99.197% → 66.100% | 934 → 5 |
| [2000-control-rnd99-wanderfull-s101](repairs/2000-control-rnd99-wanderfull-s101.json) | 84.594% → 65.987% | 1,087 → 5 |
| [2000-control-rnd99-wanderfull-s303](repairs/2000-control-rnd99-wanderfull-s303.json) | 99.243% → 66.010% | 1,875 → 6 |
| [5000-control-rnd99-wanderfull-s101](repairs/5000-control-rnd99-wanderfull-s101.json) | 85.342% → 66.111% | 2,721 → 6 |
| [10000-control-rnd99-wanderfull-s101](repairs/10000-control-rnd99-wanderfull-s101.json) | 84.658% → 66.018% | 5,442 → 6 |
| [10000-control-rnd99-wanderfull-s303](repairs/10000-control-rnd99-wanderfull-s303.json) | 99.302% → 66.041% | 9,396 → 6 |

The original artifacts remain available in Git history at `503c3d8`. The
selection recipe IDs and historical means remain frozen so regeneration does
not silently choose a different set. `survey-configs.json` records the fixed
110-configuration design, including the repaired full-slide options.
`select_collection.py --reselect` deliberately computes a fresh selection;
it is not needed to reproduce this collection.

## The apparent isolated area in `500-random-s101`

The reported image was a small part of the central crop, whose right boundary
is column 313 (zero-based). The supposed outer wall ends at this crop boundary;
the open corridor continues through column 314 in the complete board. The
reported patch matched columns 309–313 and rows 276–291 exactly.

Independent checks on the persisted board found one connected open component
containing all 165,092 open cells. Its saved solution visits all of them under
the slide rules. Every full-map pixel matched the board cell, and regenerating
the ordinary recipe after the fixes produced the same SHA-256:
`e1159eb33a9c0084c45cf283f0a7fecd2e8924dff2699b76919c673399b0c9cb`.
This specimen therefore retains its original board.

All 424 central detail PNGs now have **CROP**, source dimensions, zero-based
coordinates, and a visible frame baked into the image. The 128×128 cell window
is enlarged exactly threefold within a 512×512 labeled image. The wall starts
with whole-board views in square containers. The [stats lab](../web/stats-lab.html?left=500-random-s101)
shows a movable 22×22 cell detail, with its crop label printed on the canvas
and its position outlined in cyan on the whole-board map. Its initial detail
includes the corridor beside the reported crop boundary.

`verify-collection` now checks the detail's exact cell pixels against the full
map as well as its frame and caption regions. Full maps remain one pixel per
cell; reduced overviews explicitly describe gray as mixed occupancy.

## Older tile PNG renderer

The tile renderer used by ordinary `gen` exports assembled completed strips
in dictionary iteration order instead of their row coordinates. It also used
15-pixel strip heights when a different cell scale was requested. It now
assembles rows by index, resizes tiles to the requested scale, and disposes
intermediate images. Regression fixtures compare every pixel of distinct
row/column colors at cell scales 1, 3, and 15. Its corner output now also has
CROP printed in a footer when it truncates the board. Image-writing failures
propagate instead of being silently swallowed.

The gallery's direct one-pixel-per-cell map writer is a separate path and
passed its exact-cell checks. Repaired gallery image URLs include the board
hash and crop-format version so browser caches cannot mix old images with
replacement measurements.

## Understanding the measurements

The [visual stats lab](../web/stats-lab.html) includes all 32 saved 500-square
boards. Eleven overlays show the cells, runs, boundaries, tile densities,
reflection mismatches, or square witnesses behind the measurements. Each
explains its formula and what information it discards. Closest-number and
most-different-number comparisons make scalar limitations visible. Descriptive
ranges compare recipe means with observed variation among three seeds at 300
square, without claiming statistical significance or solving difficulty.

The [board wall](../web/board-wall.html) shows every matching board together.
The [atlas](../web/gallery-guide.html) retains all 424 specimens, distribution
charts, seed comparisons, formulas, and the updated development state.
