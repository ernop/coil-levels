# Board gallery and sampling collections

[Open the live Board explorer](https://ernop.github.io/coil-levels/).
Select a board, then use **Notes & files → Board** to download its level file.
The **Legal solution** link appears when a saved solution exists. Gzip files
can be decompressed for tools expecting plain board/solution text.
[Publishing instructions](../HOSTING.md) cover GitHub Pages updates.

Open [the Board explorer](../web/gallery.html) directly in a
browser. The explorer uses the checked-in board index and local assets, with
no remote libraries, API, or server requirement. A local server also works:
`python3 -m http.server 8764 --bind 127.0.0.1` from the repository root.

This collection investigates styles and variation within a recipe. It is
not a DFS-hardness leaderboard. Read [FINDINGS.md](FINDINGS.md) for measured
observations and [../STYLE.md](../STYLE.md) for the broader structural/proof
analysis goal and its implementation boundaries.

## New sampling collections in the standard gallery

The shared catalog contains **4,864 board entries**: 1,208 original-game levels,
2,924 distinct historical generated layouts, 12 earlier solver-selected boards,
and the 720 previously collected study specimens and sampling draws.
[Archive coverage and provenance](ARCHIVES.md) explains each source, repeated
historical copies, and missing solution certificates.

The compact browser keeps source, search, **Has solution**, and sorting visible
above the thumbnail list. Has solution selects the 732 boards with saved solutions;
the checkbox does not classify boards without certificates as unsolvable. Method,
configuration, dimensions, and collection filters expand under Filters. View selects
the board or a measurement overlay; the separate solution toggle controls the route.

Selecting a thumbnail updates the inspection panel in place. The chooser keeps
its existing cards, order, expanded results, filters, focus, and scroll position;
only its selected-card highlight changes. The previous board remains visible
while the next record loads, and selection does not scroll or reload the page.
The selected board has a cyan border; a separate dashed white outline appears
only during keyboard navigation. Arrow keys select adjacent thumbnails in the
visible grid and append more results as needed. Scrolling near the bottom loads
another 48 thumbnails automatically, using an observer rooted in the bounded list.
Existing thumbnail nodes and focus remain intact. Choose size, board name, or any
geometry measurement to sort; the direction button says Small/Large first, Low/High
first, or A–Z/Z–A. Undefined measurements sort last. Filters and order are bookmarked.

The explorer fits the viewport, including the left chooser's internal scrolling
area. Its controls stay fixed above the list. Both board views and all 11 headline
measurements plus wall percentage stay visible while changing the lower detail tab.
The compact layout removes decorative panel padding and puts explanations and files
under **Notes & files**. Stats use at most two decimal places, with two significant
digits in scientific notation for nonzero magnitudes below 0.01. Exact integers,
hover values, data attributes, and the complete saved record retain their precision.
Neighbor counts are percentages of open cells. Run-length distributions are
percentages of runs within each orientation; horizontal/vertical shares divide by
the combined run count. Square placements divide by all possible placements of
that size, including overlaps. Both distribution charts share a fixed 0–100% scale.
Largest-square sides divide by the smaller board dimension in the headline stats,
sorting, and comparisons. Dimensions, mean run lengths, and generation parameters
retain their meaningful units. Each section's Notes explain the denominators.

Close-up sizing uses a logarithmic slider for the whole range, an exact cell-count
input, and −/+ buttons for one-cell adjustments. The window stays within the smaller
board dimension, and sizing or panning does not change whole-board stats. Position
controls open over the board; long seeds expand within their table row.

**Show solution (S)** toggles the saved solution when available. **Every cell**
colors the complete visit order on boards through 1000×1000. **Sampled path** joins
every nth visited cell, always keeping both endpoints, with blue-to-orange color
and direction arrows. These straight connecting lines summarize the route and may
cross walls; they are not playable moves. The initial interval produces about 600
points; the exact number input and logarithmic slider allow up to 50,001 points.
The slider gives more room to small intervals, and both controls stay synchronized.
Paths redraw after a brief pause or on release to avoid replaying large solutions
for every pointer movement. Larger boards use sampled
paths, replaying the saved solution with the same legality checks and compact memory.
The sample interval and display choice are preserved in bookmarked URLs.

The 296 experimental boards and draws comprise:

| Collection | Saved specimens |
|---|---:|
| Backward growth (`backward-v1`) | 30 |
| Earlier solution edits (`reversible-v1`) | 22 |
| Deeper sampling and controls (`deep-v1`) | 46 |
| Exact uniform 4×4 draws | 48 |
| 4×4 board-chain states | 48 |
| Uniform rejection 5×5 draws | 48 |
| Uniform rejection 6×6 draws | 48 |
| Uniform rejection 7×7 draws | 6 of 12 requested; incomplete |

The [standard board viewer](../web/gallery.html) supports all **4,864 boards,
through 10000×10000**, including every new specimen. It loads one board record
at a time from `viewer-data/`. Large boards use a reduced overview and exact
cell access for the movable close-up; the full-resolution map is also linked.

The viewer opens original-game level 1 by default. Filters, selected
board, detail tab, display, zoom and position persist in its URL. Both canvases
can show walls and measurement overlays. Forward solution visit-order colors
are available through 1000×1000 when a saved solution exists. Larger boards
with certificates retain downloads and start/finish navigation. Imported layouts
without a certificate say No saved solution; no solution is fabricated. Singleton solutions and wholly open tiny boards
are supported. The old `stats-lab.html`, `board-wall.html`, and
`gallery-guide.html` entry points redirect here, preserving bookmarked selections.

[The integrated research section](../web/gallery.html#sampling-research)
contains the construction proof, terminology, distribution targets, sampling
figures, independent-chain diagnostics, large-board comparisons, partial-batch
status, source links, and direct viewer links to the experimental boards.
Finite solution-edit runs remain explicitly labeled as nonuniform with
unresolved mixing. Undoable edits do not require backward-playable solutions.

`prepare-sampling-gallery` rereads the 198 existing small-board draws, checks
both mandatory physics validators, and writes derived geometry and labeled
images to `sampling-viewer/`. It never samples new boards or edits the source
draws. Rerunning it refreshes these derived records. Other experiments retain
their original exact stats and assets. Backward-growth static details show an
occupied-region crop, labeled as such; the movable viewer can inspect any area.

## Original style study

The original completed collection contains 424 specimens. Sizes are side lengths:
10,000 square means 10,000 × 10,000 cells (100 million cells).

| Board side | Saved specimens |
|---:|---:|
| 300 | 346 |
| 500 | 32 |
| 1,000 | 18 |
| 2,000 | 16 |
| 5,000 | 8 |
| 10,000 | 4 |

- `survey/`: 330 boards, 300 square, across 110 configurations and three
  shared seeds (101, 202, 303).
- 53 distinct selectable tweak names at Weighted4/lim20.
- Six tweak choices crossed with the six other segment pickers (36 extra
  configurations).
- Three tweak choices × seven generation controls (21 extra configurations).
- `boards/`: eight measured style representatives, with two contrasting seeds
  at 500, 1000, and 2000 square; one seed per retained recipe at 5000; four boards across three
  configurations at 10000 (including a full-walk seed pair). Completed preliminary recipe samples are also retained and labeled
  `initial sample` in the catalog. They are outside the controlled survey.
- Duplicate outcomes remain recorded: a distinct recipe is not automatically
  a distinct board. Board SHA-256 hashes identify coincidences.

`selection.json` records geometric representative selection and the scaling
substitution. The unlimited `last` recipe was expensive at 500 square, and
its 1000-square attempt was stopped without an exported specimen. From 1000
upwards, a finite-candidate alternative is used. The equal23short/First export took 241.7 seconds at 2000 square; a Longest-order
variant was then tried at 5000. Four ordering-heavy 5000 probes reached their
recorded whole-command budgets (600 or 1200 seconds, including export) and are excluded from the saved collection.
Additional Weighted4/lim20 recipes provide measured geometry coverage instead.
See `attempts/`, `excludedAtScale`, and the extra selection decisions for outcomes.
No incomplete or timed-out
attempt is represented as a valid board.

All generated specimens pass `Debug.DoDebug` and independent game-rule
replay. The gzip files are read back and replayed before `stats.json` is
written as the completion marker. The final `verify-collection` command
replays every saved pair again, recomputes all geometry, verifies hashes,
and compares every full-map pixel to the board.

Final verification passed for all 424 specimens on 2026-09-06:
330 survey boards, 82 preliminary/selected boards through 2000 square,
and all 12 large boards. [verification.json](verification.json) records the
checks and verifier source hashes. C# regressions, the seven Python analysis
tests, JavaScript syntax, and browser interactions also passed. The current Release build has no warnings. The real evaluator has additional exhaustive tiny-board and subprocess-contract regressions; see [the bridge documentation](../meta_solver/README.md).

The [Board explorer](../web/gallery.html) shows one full board beside an adjustable zoom, with the same overlay in both views. Click or drag the full map to position the zoom; drag the zoom to pan, or use its coordinates and arrow keys. The CROP label and coordinates are printed on its canvas. Parameters & method shows the selected configuration and saved run; All stats contains the complete measurement ledger; Compare boards uses the same filtered specimens; Research & guide explains the generators and measurements.

All eleven measurements and wall percentage appear below the viewer; all eleven overlays are available in View. The complete measurement ledger includes every saved field: degrees, both run histograms, all square placement counts and witnesses, tile summaries, six symmetry values, every edge layer, and generation metadata. Exact-value tables and the full-precision JSON remain expandable. Buttons jump to square witnesses or specific edge layers. The board, overlay, zoom size, and position are retained in the URL; older `?left=ID` links still work. No geometry is recomputed from the crop.

`node scripts/check_stats_lab.js` checks every packed board against its source cells and saved metadata, independently replays all 732 supplied solutions, compares overlay geometry against exact stats, checks each displayed field, rejects omitted fields, verifies all 296 source draws are present, and checks catalog assets and research links. Large-board accessor checks include runs crossing crop boundaries. `node scripts/check_gallery.js` checks configuration grouping, combined filters, bookmark redirects, and unified-page dependencies.

The original full-slide walk produced oversized open regions. All 18 affected specimens were regenerated using eight initial segments; before/after hashes and measurements are in [REPAIRS.md](REPAIRS.md). The tweak-based generator now rejects oversized all-open squares before export. This is a collection quality policy, separate from Coil validity. Selection choices and their original means remain frozen; the catalog, paired effects, and findings use repaired boards. The survey covers the original 53 picker names; the current command builder also offers the separately named `len23-10th-exact`.

## Files in an original specimen

| File | Meaning |
|---|---|
| `level.board.gz` | Gzip-compressed coilbench board; decompress before using a text-only solver |
| `level.solution.gz` | Gzip-compressed coilbench solution; validation certificate, not a blind-test secret |
| `map.png` | Whole board, exactly one pixel per cell, white open and black wall |
| `preview.png` | 480-square full-board preview using box resampling; gray means mixed occupancy |
| `detail.png` | Central 128-square crop enlarged to 384 square inside a 512-square frame; CROP and zero-based coordinates are printed on the PNG |
| `stats.json` | Dimensions, seed, explicit options, generation time, board hash, validation statement, exact geometry |

The compact geometry pass counts occupancy, degrees, isolated walls, open
runs, all-open/all-wall squares and witnesses, layer occupancy, and symmetry.
Tile density variance uses nonoverlapping 32-square tiles; partial edge tiles
have equal weight. It does **not** run the Python room/proof engine, calculate
motif spectra, or solve the board by search. See the HTML guide for formulas,
denominators, and visual examples.

The metric input is the board alone. Solution segment counts and generation
time are not substituted for geometry. Generation time includes generator
validation and excludes map/stats export; it depends on hardware and load.

## Reproduction

Requires the .NET 10 SDK and the project packages. The viewer packing script
also requires Python's Pillow package. Scripts resolve the repository root
from their own path.

```sh
dotnet build -c Release
# Recreate one specimen in a NEW output directory, using its recorded options:
dotnet run -c Release -- specimen output/example 300 101 --picker rnd99 --segpicker Weighted4 --lim 20
# Initial-walk controls also supported:
# --wander-full  --wander-max 5  --wander-steps 8

# Replay and verify the saved collection (includes the 100-million-cell boards):
dotnet run -c Release -- verify-collection gallery/boards
dotnet run -c Release -- verify-collection gallery/survey

# Reuse complete survey specimens; new ones run in three bounded subprocesses:
python3 scripts/explore_styles.py
python3 scripts/select_collection.py  # reuses the frozen selection; --reselect deliberately recalculates it
# Up to two jobs through 5000 square, then serial 10000 jobs; complete specimens reused:
python3 scripts/generate_selected.py
python3 scripts/report_survey.py
dotnet run -c Release -- prepare-sampling-gallery  # refresh derived assets for saved small-board draws
python3 scripts/build_catalog.py --require-complete
python3 scripts/build_stats_lab.py
node scripts/check_stats_lab.js
node scripts/check_gallery.js
node scripts/check_stat_proportions.js
node scripts/check_solution_sampling.js
# Reimport original-game and historical boards: see ARCHIVES.md.
python3 scripts/build_picker_catalog.py --check

# Extract a board without changing the checked-in compressed artifact:
gzip -dc gallery/boards/10000-tweak-rnd99-s101/level.board.gz > /tmp/coil-10000.board
```

Existing specimen directories are never silently overwritten. Survey runs
have a 120-second per-job generation/export budget and record failures in
`survey-results.json`. A failed/incomplete directory must be inspected before
an intentional new run. Generation uses at most two jobs through 5000 square and serial 10000 jobs
because the generator holds several per-cell arrays. Do not launch the original preliminary batch
alongside the selected large collection.

For byte-identical board reproduction, use the same generator source,
.NET random implementation, seed, dimensions, and options. Preview PNG bytes
may vary with image-library versions. Timings are observations, not guarantees.
