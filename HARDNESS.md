# What makes a coil level hard

Findings from 2026-09-04, measured with the reference solver in `Solver.cs`.
Everything here is reproducible with the `bench`, `solve` and `hardest`
subcommands (`README.md`).

## Scope of these experiments

The project now targets visual style and structural solving descriptors;
see [`STYLE.md`](STYLE.md). The measurements below are historical experiments
against one reference DFS implementation. They do not define the project's
current objective or measure how fully a board can be solved by room,
parity, or other explicit mathematical analysis.

`nodes` is the reference solver's move count before its first solution;
`nodesAll` is its full search-tree move count across starts and solutions.
The latter removes first-solution start-order luck when the search completes,
but still depends on this algorithm, its representation, and its rules.
Budget-limited counts are censored, not exact full-tree measurements.

The solver uses degree, forced-move, and reachability checks. With two dead
ends it tries those as starts; with one it tries that first and retains all
other open cells as candidates. Its statistics remain useful for reproducing
these experiments, separately from the structural analysis.

## Findings

### 1. Dead ends give the level away (5x)

A cell with one open neighbour must be the start or the end. With two, the
path is pinned at both ends; with one, half the start candidates vanish and
the feasibility check gets a free constraint on every branch. Same seed,
24x24, `--picker last --lim none`, 12 of 40 seeds had a dead end:

| seed | dead end kept | dead end trimmed | ratio |
|---|---|---|---|
| 5 | 168,514 | 1,064,795 | 6.3x |
| 18 | 96,975 | 587,967 | 6.1x |
| 4 | 58,212 | 302,275 | 5.2x |
| 29 | 14,849 | 77,124 | 5.2x |
| 40 | 37,495 | 188,819 | 5.0x |

The generator's path ends were one-exit pockets in about a third of levels. So
`GenerateLevel` now trims the path ends until neither end is a dead end
(`Level.TrimDeadEnds`, off with `--keep-deadends`). The trimmed level is
still a valid coil level with the shortened solution. Trimming the ends
also means the two path ends are ordinary-looking cells: a bot has to try
every cell as a start (about 400 candidates at 24x24 instead of 1 or 2).

### 2. Hard decisions, pillars, and open area are what the effort tracks

960 levels (24x24, 32 generator configs x 30 seeds, dead ends trimmed).
Correlation of each structural feature with ln(`nodesAll`):

| feature | corr | meaning |
|---|---|---|
| hard decisions (`hardDec`) | +0.54 | segment ends where both turns are open and neither leads into a one-exit pocket, so the wrong branch is not refuted immediately |
| isolated walls % | +0.53 | share of wall cells touching no other wall: single-cell pillars rather than wall blobs |
| deg3 % | +0.42 | open cells with three open neighbours |
| open % | +0.40 | fraction of the board that is path |
| candidate starts | +0.40 | same thing as open %, seen from the solver |
| deg2 % | -0.32 | corridor cells (two open neighbours); corridors are forced moves |
| easy decisions | +0.19 | segment ends where one turn is an obvious pocket |
| solutions | +0.14 | more solutions did not make it easier at this size |
| avg segment length | 0.00 | |

Concretely: a hard level is **dense** (72-75% open), made of **isolated
pillars rather than wall runs**, and has **many places where the path
turns and the other direction would also have worked for a while**.
Corridors, wall blobs and pockets are what let the pruning bite early.

Multivariate fit on these features (see `Hardness.Proxy`): each additional
hard decision per 100 open cells multiplies the effort by about 1.27; each
percentage point of isolated walls by 1.04. R^2 0.38, Spearman 0.61 with
`nodesAll` at 24x24; Spearman 0.49 on 300 levels at 30x30. So the
structure explains a lot but not most of the variance; the rest may include arrangement and structural relationships not represented
by those features. These experiments did not test an analytic room-based solver.

### 3. Generator settings matter about 3x; the seed matters more

Geometric mean `nodesAll` at 24x24, 30 seeds each (full table in the
session sweep; `bench` reproduces any row):

| tweak picker | seg picker | tweak limit | gm nodesAll | open % |
|---|---|---|---|---|
| `last` | Weighted4 | none | 191,275 | 70.7 |
| `2lim10` | First | none | 191,259 | 72.1 |
| `1stlast2` | Weighted4 | none | 146,468 | 71.6 |
| `2lim10` | Weighted4 | 20 | 138,135 | 71.3 |
| `2lim10` | Longest | none | 133,805 | 72.0 |
| `2lim10` | Weighted4 | none | 116,008 | 72.8 |
| `rnd99` (old default) | Weighted4 | 20 | 80,040 | 65.6 |
| `rnd3` | Weighted4 | none | 69,896 | 65.3 |
| `len23mid` | Weighted4 | 20 | 68,198 | 64.6 |
| `2lim10` | Last | none | 67,863 | 74.1 |

- `last` (always take the last enumerated tweak, i.e. the largest one) and
  `First` seg ordering come out on top; the old default `rnd99` is in the
  bottom third. The `hardest` command therefore defaults to
  `--picker last --lim none`.
- The per-segment tweak limit (`--lim 20` vs none) changes little either way.
- `--loops`: 1 pass gives 103 K, 2 or more 121 K; passes beyond 2 change
  nothing at this size.
- Within one config the seed spread is larger than the config spread:
  p10 48 K, p50 99 K, p90 244 K, p99 477 K, max 882 K over all 960 levels.
  So **generate many, keep the hardest** is the strongest lever available
  without changing the algorithm.

### 4. Against hacker.org's own levels

Full-tree effort of the public coilbench levels (hacker.org's), same solver:

| level | size | nodesAll |
|---|---|---|
| 47 | 19x18 | 17,450 |
| 49 | 19x19 | 91,264 |
| 61 | 23x23 | 55,636 |
| 67 | 25x25 | 100,368 |
| 73 | 27x27 | 262,982 |
| 83 | 30x30 | 292,613 |
| 87 | 32x31 | 4,040,927 |
| 95 | 34x34 | 469,015 |
| 99 | 36x35 | 87,216 |

Generated with the defaults of `hardest` (`last`, no limit, trimmed):

| size | seeds | median | p90 | best | best vs hacker.org same size |
|---|---|---|---|---|---|
| 19x19 | 200 | 47 K | 102 K | 217 K | 2.4x level 49 |
| 24x24 | 960 | 99 K | 244 K | 882 K | (no public level at 24x24; 61 and 67 bracket it at 56 K / 100 K) |
| 30x30 | 300 | 538 K | 1.5 M | 3.3 M | 11x level 83 |

| 34x34 | 300 | 1.07 M | 3.6 M | 40.7 M (seed 109; 2 seeds exceeded the 20 M selection budget, exhausted afterwards with `solve --all`) | 87x level 95 |

An unselected generated level is about as hard as hacker.org's at the same
size; the top one percent is 2-90x harder. Hacker.org's are not uniformly
hard either (level 87 is 14x level 89).

The top three at each of these sizes are checked in under `levels/hard/`
as `<size>-rankNN-seedS.board` + `.solution` (all pass coilbench's `check`).
The `.solution` files are in the same public repo, so they are for checking
a bot's answers, not for a blind test.

### 5. The coilbench baseline solver

`coil_solver.py` in coilbench is a DFS with no pruning that tries starts in
row-major order. On hacker.org 19x19 (level 49) it takes 187 s; on 19x18
(level 47) 0.7 s. On four unselected generated 19x19 levels (`--picker last
--lim none`, seeds 1-4) it took 664, 1429, 623 and 898 s. The three
`hardest` 19x19 picks (`levels/hard/19x19-*`) had not finished after 60 min
each and were killed. For a bot without dead-end or connectivity pruning
the cost is dominated by exhausting wrong starts, so a level with no dead
ends and a late (row-major) true start is worst; the picks' starts are at
rows 2, 4 and 9 of 19, so this is not what makes them slow. If the target
bot is known to scan starts in an order, flipping the board so both path
ends are late in that order is a free multiplier, but it does nothing
against a bot that starts from dead ends or tries starts in another order.

## Producing hard levels

```sh
dotnet run -c Release -- hardest 30 30 1 300 --keep 10
```

generates 300 seeds at 30x30 in parallel, scores each with the exact
full-tree effort (budget 20 M nodes; exceeded budgets rank first, ordered
by descending proxy among themselves), writes
the 10 hardest to `output/hardest/30x30/rankNN-seedS.board` + `.solution`
and a `summary.csv` with the batch distribution; `scores.csv` has every seed.
On 32 threads: 200 seeds at 19x19 in 3 s, 300 at 30x30 in 2 min, 300 at
34x34 in 5.5 min.

Above 34x34 the exact score is too slow (it grows about 10x per +6 cells of
side) and `hardest` switches to the structural proxy (`--score proxy`), which
is O(cells): 2000 seeds at 60x60 in 8 s. The proxy's top decile is about
2x the batch average in exact effort (the exact top decile is 3-4x), so at
large sizes expect a smaller but still positive gain from selection.

Every emitted `.board`/`.solution` pair is validated twice before being
written: `Debug.DoDebug` against the segment model and `CoilFormat.Validate`,
an independent replay of the coilbench rules. The coilbench `check` binary
accepts them.

## Not done / ideas

- **Hardness-aware tweak picking.** The tweak picker only sees candidate
  tweaks for one segment and cannot know which will end up as hard
  decisions, because whether a side square is "open later" is decided by
  later tweaks. A hill-climb on the finished level would need an undo or a
  level copy per step; not built.
- **Deliberately incomplete tweaking.** `todo.txt` notes that a level
  tweaked to exhaustion leaks information (a solver that knows the
  generator can infer where a tweak was impossible). Randomly skipping some
  available tweaks would hide this against a generator-aware bot, at some
  cost in density. Not measured.
- **Different solver classes.** Everything above is against DFS + pruning.
  A bot that encodes the level as a constraint problem or exploits the
  path-alternation structure would rank levels differently.
