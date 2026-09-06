# coil-levels - agent index

Mortal Coil level generator (C#, .NET 10) plus a separate Python meta-solver.

Start with [`README.md`](README.md): build/run commands (subcommands `gen`,
`solve`, `stats`, `bench`, `hardest`), layout, and the settled decisions
(target framework, ImageSharp version and why not 4.x, font, repo-root path
resolution, validation-throws, dead-end trimming, performance work).
[`STYLE.md`](STYLE.md) defines the current goal: visual-style parameters and
structural solving through explicit deductions and room analysis.
`analysis/describe.py` is the first board-only prototype; it never searches
solution paths. [`HARDNESS.md`](HARDNESS.md) records historical experiments on
what makes a level hard for a DFS bot,
measured against the reference solver and hacker.org's levels, and how to
produce hard levels (`hardest`). `todo.txt` holds the author's working notes.

Every emitted level must pass `Debug.DoDebug` and `CoilFormat.Validate`;
never relax either to make output appear.

Rules and writing conventions come from the parent workspace:
`~/proj/mybrowser/AGENTS.md` and `~/proj/mybrowser/.cursor/rules/`.

Public repo: [ernop/coil-levels](https://github.com/ernop/coil-levels). Do not
commit license keys or generated `output/`, `logs/`, `tweaks/` (gitignored).
Generated `levels/*.coil` and the `levels/hard/` picks are tracked on
purpose; do not commit smoke-test levels (`gen` writes a `.coil` into
`levels/` on every run).

The [board style collection](gallery/README.md) and
[HTML atlas](web/gallery-guide.html) contain a controlled picker/seed survey
and selected large specimens. `specimen` exports validated gzip boards and
geometry; `verify-collection` replays and verifies the saved collection.
`gallery/` is deliberately versioned, unlike ordinary `output/` runs.
