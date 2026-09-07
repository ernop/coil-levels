# Real Coil evaluation and meta-solver

The Coil bridge runs the repository's C# reference solver. It reads actual
boards, enforces fixed evaluation limits, and independently replays reported
solutions in Python. It does not simulate results or read saved answer files.

## Run without an LLM or API keys

From the repository root:

```sh
dotnet build -c Release
python3 -m meta_solver.examples.coil_integration --budget 100000 --timeout 3 --compare --output output/meta-solver/reference-comparison.json
python3 -m unittest meta_solver.test_coil_bridge -v
python3 -m meta_solver.test_framework
```

The default dataset is `levels/hard/`. Pass another directory as the positional
argument. Discovery is recursive and supports `.board`, `.board.gz`, and
rectangular `.coil` files. Malformed input fails with its path. Boards above
`--max-cells` (default 10,000 cells, not side length) are explicitly listed as
skipped; an empty usable dataset is an error. The 500-square gallery therefore
belongs in the geometry tools, rather than the default bounded DFS benchmark.

The command prints a JSON report; `--output` also saves it. `--compare` tries
three additional direction/start orderings against the same boards and limits.
A comparison can reject every candidate; no improvement is fabricated.

## Contract and validity

`dotnet .../coil-levels-csharp.dll evaluate` accepts one coilbench board on
stdin and emits one JSON object. Successful execution exits zero even when a
search is unsolved; invalid arguments/input exit two with `status: error`.
Statuses distinguish `solved`, `unsolvable` (exhausted search), `node_budget`,
`time_limit`, and `depth_limit`. The Python process deadline additionally
reports `process_timeout` with unavailable search telemetry. Crashes, invalid
JSON, inconsistent metadata, and invalid solutions raise bridge errors and
cannot be scored as ordinary puzzle failures.

Search results contain the board SHA-256, dimensions, node and branch counts,
maximum visited-cell count, elapsed search time, limit flags, and the first
solution. Every success must pass `CoilFormat.Validate` and independent Python
slide replay. A legal start may be any open cell, including an interior cell.

The evaluator fixes node, time, cell, and depth limits. The depth cap prevents
recursive DFS from overflowing the process stack and does not claim that a
bounded-out board is unsolvable. Defaults: 100,000 nodes, 5 seconds, 10,000
cells, and 2,048 slides. A Python deadline adds one second for process startup
and result transfer. `elapsedSeconds` measures search; `wallSeconds` includes
the process and validation overhead.

Candidate controls are real search settings:

```json
{"directions":"DRUL","starts":"low-degree","pruning":true}
```

- `directions`: any permutation of `URDL`.
- `starts`: `natural`, `reverse`, `low-degree`, or `high-degree`.
- `pruning`: enable or disable the reference feasibility check.

They change search effort without changing legal moves. Budget changes,
unknown settings, vague prose without a concrete JSON configuration, and
no-op modifications are rejected. Generator tweak/segment pickers are not
solver controls. Arbitrary code generation/application is outside this
configuration evaluator.

A validated solve scores `0.5 + 0.5 / (1 + nodes / 1000)`; all unsolved outcomes
score zero. The fixed node scale avoids wall-clock noise or changing the
budget denominator to inflate a score. The comparator matches board IDs,
rejects empty/duplicate/mismatched results and non-finite scores, protects
previously solved cases, and applies its documented score tolerance. This is
a deterministic acceptance rule, not a statistical significance test.

## Use with the generic meta-solver

```python
from meta_solver import MetaSolver, MetaSolverConfig
from meta_solver.examples.coil_integration import CoilProblem, CoilSolution

problem = CoilProblem("levels/hard")
solver = MetaSolver(problem, CoilSolution(), llm_ensemble=[],
                    config=MetaSolverConfig(max_iterations=0))
baseline = solver.run()
```

Zero iterations performs real baseline evaluation. Supply implementations of
`LLMInterface` to propose JSON configuration changes for further iterations.
No external model calls are required by the bridge or regression suite.

`coil_meta_solver.py` retains the Coil-specific idea-memory and tier interface
and now calls the same bridge. Default nonempty tiers are strata by open-cell
count, not measured difficulty classes. Explicit tiers may impose pass-rate
thresholds. Baselines must cover every configured case; regressions are checked
in every tier. Idea success, measured failure analysis, and checkpoints now
reflect actual runs. Its checkpoints are written atomically under
`output/meta-solver/`, which is ignored by Git. Concrete JSON configurations
are deduplicated by their contents; a keyword hash no longer collapses distinct
parameter settings.

## Validation and observed comparison

Regression checks include successful and impossible boards, interior starts,
node/depth/time limits, process failures, malformed results, wrong hashes,
false solution claims, input formats, candidate mutation, and both evaluation
flows. Native tests exhaust all 512 binary 3×3 boards across 16 search
configurations against an independent tiny-board enumerator.

The initial real run at 100,000 nodes and 3 seconds solved 2/12 saved hard
boards with default ordering; reversed starts solved 1/12 and triggered
regression rejection. Low-degree starts improved the sample score by about
0.0089, below the default 0.01 acceptance threshold. These observations are
bounded measurements, not claims that the other boards are impossible.

The older `COIL_DESIGN.md` and `DESIGN_DISCUSSION.md` are design notes. Wider
room separators, compositional proofs, motif spectra, and learned candidate
strategies remain separate research work; the functioning bridge does not
claim to implement those algorithms.
