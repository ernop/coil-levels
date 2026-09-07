# Measured style survey — 2026-09-06

All 330 generated 300-square boards passed the internal validator and independent slide-rule replay, including replay of the persisted gzip pairs. 110 configurations × seeds 101, 202, 303. No path search or DFS difficulty ranking was used for selection.

## Observations

- `tweak-rnd99`: open area 65.72–66.11%; mean open run 3.16–3.18 cells. These ranges describe only the three observed seeds.
- `tweak-first`: open area 63.73–64.03%; mean open run 2.95–2.98 cells. These ranges describe only the three observed seeds.
- `tweak-last`: open area 67.98–68.76%; mean open run 3.35–3.43 cells. These ranges describe only the three observed seeds.
- `seg-2lim10-Last`: open area 75.31–75.84%; mean open run 4.52–4.61 cells. These ranges describe only the three observed seeds.
- `control-rnd99-wanderfull`: open area 65.81–66.20%; mean open run 3.17–3.19 cells. These ranges describe only the three observed seeds.
- `control-last-unlimited`: open area 70.35–72.48%; mean open run 3.53–3.84 cells. These ranges describe only the three observed seeds.

## Paired changes

Changes below hold the seed and tweak picker fixed and compare to Weighted4 / lim20 / default initial walk and trimming. Values are the mean of three seed-wise differences, not confidence intervals. The shared seed does not guarantee the same random-call sequence after a method changes.

| Variant | Open area change (percentage points) | Mean run change (cells) |
|---|---:|---:|
| `seg-rnd99-First` | +4.457 | +0.572 |
| `seg-rnd99-Last` | +5.816 | +0.741 |
| `seg-last-First` | +4.514 | +0.607 |
| `seg-2lim10-Last` | +4.483 | +0.725 |
| `control-rnd99-wanderfull` | +0.088 | +0.011 |
| `control-rnd99-onepass` | -0.259 | -0.019 |
| `control-rnd99-keepends` | +0.001 | -0.000 |
| `control-last-unlimited` | +2.893 | +0.248 |

Full per-seed changes: [paired-effects.csv](paired-effects.csv).

## Identical outcomes

There are 320 distinct board hashes across 330 runs. The following recipe/seed sets coincide exactly; parameter names are not evidence of distinct output:

- `300-control-2lim10-keepends-s101`, `300-tweak-2lim10-s101`
- `300-control-rnd99-keepends-s101`, `300-tweak-rnd99-s101`
- `300-control-last-keepends-s101`, `300-tweak-last-s101`
- `300-control-rnd99-keepends-s202`, `300-tweak-rnd99-s202`
- `300-tweak-len23rnd3-s303`, `300-tweak-sz23rnd3-s303`
- `300-tweak-3lim50-s101`, `300-tweak-len23-s101`
- `300-control-2lim10-keepends-s202`, `300-tweak-2lim10-s202`
- `300-tweak-len23rnd3-s101`, `300-tweak-sz23rnd3-s101`
- `300-control-last-keepends-s202`, `300-tweak-last-s202`
- `300-tweak-len23rnd3-s202`, `300-tweak-sz23rnd3-s202`

## Coverage and limitations

The first survey holds the candidate limit at 20 for all 53 originally selectable tweak names. Six tweak families are crossed with the other six segment pickers. Three tweak families receive seven extra control settings. This is not all 53 × 7 × all limits × all seed values.

The original unbounded full-length initial walk could spiral across most of the board before tweaking. All nine affected survey boards now use eight full slides; the measurements here describe the repaired outputs. Run distributions, pillars, axis bias, and spatial density still expose differences among the remaining styles. Use the visual stats lab to inspect what each statistic counts and what it misses. Crop images carry their labels and coordinates on the image itself. See REPAIRS.md for the before/after audit.

The 500-square unlimited-last exports took 51.6 and 122.2 seconds. The 1000-square attempt was stopped before export after several minutes; no board is claimed for it. Larger selected boards replace that recipe with `seg-2lim10-Last` (lim20), chosen by the same geometric coverage rule among finite-candidate alternatives.

Next investigations: more seeds for high-spread configurations; more initial-walk lengths and starting positions; controlled spatial maps and motif spectra; bridge/room/proof analysis on selected manageable boards. These remain distinct from a ranking by search effort.

## Recorded scale limits

| Attempt | Outcome | Budget (seconds) |
|---|---|---:|
| `5000-seg-2lim10-Last-s101` | generation_budget_exceeded | 1200 |
| `5000-seg-equal23short-Longest-s101` | generation_budget_exceeded | 600 |
| `5000-seg-first-Last-s101` | generation_budget_exceeded | 600 |
| `5000-seg-last-First-s202` | generation_budget_exceeded | 1200 |

Budget-exceeded attempts have no claimed specimen. The retained larger collection uses additional Weighted4/lim20 representatives selected for measured geometry coverage. The 10000-square series uses rnd99, eight initial full slides at two seeds, and len23. Generation budgets measure practicality in these runs, not mathematical impossibility or solver difficulty.

## Saved large-board measurements

Sizes below are side lengths. Generation time includes generator validation but excludes artifact export. These are individual observations on this machine, not complexity estimates.

| Specimen | Open area | Mean open run (cells) | Largest open square (side) | Generation seconds |
|---|---:|---:|---:|---:|
| `10000-control-rnd99-wanderfull-s101` | 66.018% | 3.200 | 6 | 280.8 |
| `10000-control-rnd99-wanderfull-s303` | 66.041% | 3.203 | 6 | 242.4 |
| `10000-tweak-len23-s101` | 71.483% | 3.936 | 7 | 214.9 |
| `10000-tweak-rnd99-s101` | 66.010% | 3.200 | 6 | 287.5 |
| `5000-control-rnd99-wanderfull-s101` | 66.111% | 3.210 | 6 | 59.1 |
| `5000-seg-first-Longest-s101` | 63.865% | 2.985 | 6 | 65.5 |
| `5000-tweak-len1-s101` | 65.228% | 3.088 | 5 | 73.0 |
| `5000-tweak-len23-s101` | 71.464% | 3.931 | 7 | 51.6 |
| `5000-tweak-rnd99-s101` | 65.994% | 3.197 | 6 | 55.9 |
| `5000-tweak-sz1-23-s101` | 63.638% | 2.925 | 5 | 126.0 |
| `5000-tweak-sz23rnd2-s101` | 70.371% | 3.749 | 7 | 56.1 |
| `5000-tweak-sz3-2-s101` | 66.503% | 3.119 | 5 | 146.0 |

Compare the repaired eight-slide seed pair to inspect variation within one method at 10000 square. Whole-board overviews and explicitly labeled cell crops serve different scales of inspection. The current 54th picker, len23-10th-exact, is not retroactively added to the frozen 110-configuration survey.
