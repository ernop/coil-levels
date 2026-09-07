# Board style collection

Open [`../web/gallery-guide.html`](../web/gallery-guide.html) directly in a
browser. The explorer uses the checked-in `catalog.js` and local images, with
no remote libraries, API, or server requirement. A local server also works:
`python3 -m http.server 8764 --bind 127.0.0.1` from the repository root.

This collection investigates styles and variation within a recipe. It is
not a DFS-hardness leaderboard. Read [FINDINGS.md](FINDINGS.md) for measured
observations and [../STYLE.md](../STYLE.md) for the broader structural/proof
analysis goal and its implementation boundaries.

## Study

The completed collection contains 424 specimens. Sizes are side lengths:
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

The [board wall](../web/board-wall.html) shows all 32 saved 500-square boards together. The [visual stats lab](../web/stats-lab.html) shows one full board beside an adjustable zoom, with the same overlay in both views. Click or drag the full map to position the zoom; drag the zoom to pan, or use its coordinates and arrow keys. The CROP label and coordinates are printed on its canvas.

Below the viewer, all eleven headline values appear together. The complete measurement ledger includes every saved field: degrees, both run histograms, all square placement counts and witnesses, tile summaries, six symmetry values, every edge layer, and generation metadata. Exact-value tables and the full-precision JSON remain expandable. Buttons jump to square witnesses or specific edge layers. The board, overlay, zoom size, and position are retained in the URL; older `?left=ID` links still work. No geometry is recomputed from the crop.

`node scripts/check_stats_lab.js` checks every packed board mask and saved metadata record, then checks each displayed field against the source JSON and rejects any omitted fields. This keeps newly added characterization fields from silently disappearing from the inspector.

The original full-slide walk produced oversized open regions. All 18 affected specimens were regenerated using eight initial segments; before/after hashes and measurements are in [REPAIRS.md](REPAIRS.md). The tweak-based generator now rejects oversized all-open squares before export. This is a collection quality policy, separate from Coil validity. Selection choices and their original means remain frozen; the catalog, paired effects, and findings use repaired boards. The survey covers the original 53 picker names; the current command builder also offers the separately named `len23-10th-exact`.

## Files in a specimen

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

Requires the .NET 10 SDK and the project packages. Python scripts require only
the standard library. Scripts resolve the repository root from their own path.

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
python3 scripts/build_catalog.py --require-complete
python3 scripts/build_stats_lab.py
node scripts/check_stats_lab.js
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
