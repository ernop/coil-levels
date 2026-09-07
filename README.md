# coil-levels

Level generator for Mortal Coil (the puzzle: one path that visits every open
square, turning only at walls). Generates a level by random walk plus repeated
"tweaks", then writes the board as text (`levels/*.coil`), a PNG of the upper
corner (`output/<w>x<h>/*.png`), a stats row (`output/<w>x<h>/results.csv`),
and a log (`logs/`).

The current analysis goal is to describe visual style and the deductions a
board permits. See [`STYLE.md`](STYLE.md) for parameter definitions, the
board-only prototype, validation, and the next step toward compositional
room solving. The historical DFS experiments remain in `HARDNESS.md`.

## Build and run

Requires the .NET 10 SDK (`dotnet --list-sdks`).

```sh
dotnet build -c Release
dotnet run --project tests/RegressionTests.csproj -c Release
dotnet run -c Release -- gen 100 100            # width height, seed 0 -> output/100x100/
dotnet run -c Release -- gen 120 80 3 --path    # seed 3, also save the solution png
dotnet run -c Release -- gen 5000 5000          # about 60 s
dotnet run -c Release -- hardest 30 30 1 300    # 300 seeds, keep the 10 hardest -> output/hardest/30x30/
dotnet run -c Release -- solve some.board --all 1000000000
dotnet run -c Release -- bench 24 24 1 30 --picker last --lim none --all
dotnet run -c Release -- pickers
dotnet run -c Release -- help                  # prints usage; no args at all runs the legacy 5000x5000 gen
```

Subcommands: `gen` (one level: `.board`/`.solution` in coilbench format,
`.coil` text, corner png, `results.csv` row), `solve` (reference solver,
prints effort), `stats` (structural facts of boards), `bench` (generate +
solve many seeds, CSV to stdout), `hardest` (generate many, keep the hardest;
see `HARDNESS.md`), `pickers`. Options: `--picker NAME --segpicker NAME
--lim N|none --loops N --keep-deadends --quiet`; `hardest` adds `--keep N
--score exact|proxy --budget NODES --threads N`.

Runs from any working directory: all file IO is resolved from the repo root,
found by walking up from the executable to `coil-levels-csharp.csproj`
(`Paths.cs`). If that marker is not found the program throws.

The regression runner needs no additional test packages. It checks solver
solution limits, hardness ordering, both index assignment strategies before
and after dead-end trimming, full reindexing, and every saved hard-level
solution. Generated test levels stay in memory.

Coilbench interchange: `x=W&y=H&board=<.X...>` for a board and
`x=SX&y=SY&path=<URDL...>` for a solution (`CoilFormat.cs`). Every generated
level is validated against the segment model (`Debug.DoDebug`) and by an
independent replay of the game rules (`CoilFormat.Validate`) before it is
written; a failure throws.

## Layout

| Path | What |
|---|---|
| `Program.cs` | Entry point: subcommands, `GenerateLevel` |
| `Level.cs`, `BaseLevel.cs`, `Seg.cs`, `Navigation.cs`, `HitManager.cs` | Board model, segments, wander + tweak loop, dead-end trimming |
| `SegPicker.cs`, `TweakPicker*.cs` | Strategies for which segment to tweak and how |
| `Solver.cs`, `Hardness.cs`, `CoilFormat.cs` | Reference DFS solver (the hardness oracle), hardness scores, coilbench format + independent validator |
| `HARDNESS.md` | What makes a level hard, measured; how to produce hard levels |
| `ImageUtil.cs`, `Coilutil.cs`, `Reportutil.cs` | PNG rendering, `.coil` text output, stats line |
| `Paths.cs` | Repo-root resolution for all file IO |
| `tiles/` | PNG tiles composited into the level image |
| `levels/` | Checked-in generated levels (`.coil` text) |
| `levels/hard/` | Hardest picks at coilbench sizes (`.board` + `.solution`, coilbench format); see `HARDNESS.md` |
| `meta_solver/` | Separate Python project: LLM-driven solver framework for coilbench. See its `README.md` |
| `web/index.html` | Select2 picker demo for the picker names |
| `todo.txt` | Working notes and timings |

## Decisions (2026-09-04, Linux port)

- **Target `net10.0`** (was `netcoreapp3.0`, end of life and not installable
  alongside the current SDK). No code change was required for the runtime.
- **ImageSharp 3.1.12 + ImageSharp.Drawing 2.1.7.** The dependabot bump to
  ImageSharp 2.1.8 had left Drawing at `1.0.0-beta0007` (compiled against
  ImageSharp 1.0), which broke the build. ImageSharp 4 / Drawing 3 were
  considered and rejected: they require a per-developer Six Labors license
  key at build time (free tier exists, but it means a registration step on
  every machine and a key file that must stay out of this public repo).
  3.1 / 2.1 is the newest line without that requirement. API changes applied
  in `ImageUtil.cs`: `SixLabors.Primitives` / `SixLabors.Shapes` types moved
  into `SixLabors.ImageSharp[.Drawing.Processing]`, `SystemFonts.Find` is now
  `SystemFonts.Get`, `DrawLines` is `DrawLine`.
- **Font: `DejaVu Sans`** (`ImageUtil.FontFamilyName`), one name and no
  fallback list. The original was Comic Sans MS, which is not present on
  Linux; `SystemFonts.Get` throws if the family is missing, which is the
  intended behavior.
- **Removed `Spark.NET`** (only a `using`, never called; .NET Framework-only
  package) and **`Microsoft.CodeAnalysis.FxCopAnalyzers`** (deprecated,
  produced 170+ style warnings and nothing else). **C5 to 3.0.0** (same
  `IntervalHeap` API, netstandard2.0 build).
- **File IO is repo-root relative via `Paths.cs`**, replacing hardcoded
  `../../..` strings that only worked when the cwd was `bin/Debug/<tfm>`
  (Visual Studio's default); `dotnet run` from the project dir used to try to
  create `/home/output`.
- **`Main` takes `[width height [seed]]`** so a smoke run does not require
  editing source.

## Decisions (2026-09-04, correctness and performance)

- **Validation throws.** `Debug.DoDebug` used to print `Bad!` and continue;
  it now throws `InvalidOperationException`, and every level is additionally
  replayed under the coilbench rules (`CoilFormat.Validate`) before output.
  `SaveLevelAsText` overwrites instead of appending (each run used to append a
  second copy to an existing `.coil`).
- **Index caches are checked by validation.** Both space-filled reindexing
  and sequential index adjustment refresh cached owners and minimum hits;
  disabling `UseSpaceFillingIndexes` previously left stale comparisons and
  could produce an invalid path.
- **Solver solution limits apply across all starts.** `solve --all MAX`
  stops once MAX solutions have been found, including when MAX is above one.
- **Hardness ranking uses separate comparison fields.** Budget-exceeded
  searches rank first and are ordered by proxy; completed searches are
  ordered by node count. Adding the proxy to `1e18` previously rounded away
  the proxy and left exceeded searches in seed order.
- **Path ends are trimmed of dead ends by default** (`--keep-deadends` to
  keep). Reason in `HARDNESS.md` § 1: a dead end cuts solver effort about 5x.
- **C5 `IntervalHeap` replaced by `System.Collections.Generic.PriorityQueue`**
  with lazy deletion (`Seg.HeapStamp`); C5 is no longer a dependency. The
  stale-handle check was `handle.ToString().Contains("-1")` inside a
  try/catch; it is now a stamp comparison.
- **Segment indexes are `ulong`** (were `uint`): the space-filling reindex
  (`RedoAllIndexesSpaceFilled`, an O(cells) pass) runs 2 times on a
  5000x5000 run; with 32-bit indexes the gaps between neighbouring indexes
  were exhausted far more often.
- **Hot-path allocations removed.** `Tweak` is a struct; `GetTweaks` appends
  into a reused buffer; the seg picker's `OrderByDescending().First()` is an
  `ArgMax` loop; `ApplyTweak` reuses its node lists; the vertical-cache
  dictionary is a pooled `int[][]`; owner index and min-hit index live in one
  `BaseLevel.Cell[]` so `GetSafeLength`/`GetReturnable` read one cache line
  per square. Tweak phase for 1000x1000: 8.1 s at the start of this work,
  1.7 s now (`todo.txt` records ~15 min for the original); 5000x5000 whole
  run about 60 s, 3.6 GB allocated total.
- **No fallbacks.** Unknown picker names, missing fonts, missing repo root,
  and failed validation all throw with the name of what failed.

## Board style atlas (2026-09-06)

Open [the HTML atlas](web/gallery-guide.html) for the measured picker/seed
survey and selected large square boards. [Collection documentation](gallery/README.md)
links the reproduction commands, exact metric definitions, selection record,
and [measured findings](gallery/FINDINGS.md). The atlas compares geometry and
seed variation; it does not use a single hardness score.

`specimen <new-dir> <side> <seed> [gen options]` exports gzip board/solution
pairs, a whole map, overview/detail images, and geometry JSON.
`verify-collection <dir>` replays saved solutions, checks board hashes,
recomputes geometry, and compares every full-map cell. Initial-walk options
are `--wander-full`, `--wander-max N`, and `--wander-steps N`.

`BoardCharacterization.cs` implements exact compact board-only geometry for
large boards. It complements the Python proof prototype; room decomposition,
proof metrics, and motif analysis are not included in this compact pass.

## Real evaluation and visual inspection (2026-09-06)

The [Python evaluation bridge](meta_solver/README.md) now runs bounded C#
search, independently replays solutions, and records measured results. Run
`python3 -m meta_solver.examples.coil_integration --compare` after building.
The native `evaluate` command accepts a board on stdin and returns JSON.

The [board wall](web/board-wall.html) shows all 32 saved 500-square boards at
once and supports every size in `gallery/boards`. The [visual stats lab](web/stats-lab.html)
compares exact 500-square cell masks with measurement overlays, labeled cell
details, close numerical matches, and within-recipe seed variation.

The central gallery crop PNGs contain a visible CROP label, coordinates, and frame. The full
maps retain exact cells. A crop boundary must never be interpreted as a board
wall; click a whole map in the lab for cell-level context.

The tweak-based generator limits full-slide initial walks to eight segments
by default and rejects all-open squares exceeding one fifth of the board side
(with an eight-cell allowance on small boards). This is a collection quality
policy in addition to game-rule validity. See [the repair record](gallery/REPAIRS.md) for the affected specimens'
before/after hashes and geometry.
