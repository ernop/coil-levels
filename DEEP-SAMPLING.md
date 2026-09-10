# Deeper sampling: implementation, research, and measured limits

The new sampler replaces complete short path sections inside small regions,
in addition to the earlier endpoint and rectangular edits. This improves
exploration in the measured experiments and is now the `gen-any` default.
It preserves legal Mortal Coil movement and the ability to generate every
solvable board. **It still does not give uniform large-board samples or
certified equilibrium samples.**

The [experiment report](gallery/deep-v1/README.md) contains full boards, traces,
convergence diagnostics, exact small-state comparisons, uniform reference
draws, and the 1000-square comparison. All 46 path specimens are retained,
including pilot runs. There are also 102 accepted independent uniform board
draws at sizes 5x5 through 7x7; the 7x7 run reached its attempt limit.

## What changed

`BlockPathSampler.cs` adds an exact conditional route sampler:

1. Select a 2x2, 3x3, or 4x4 region and an entry edge, independently of the
   current board. An unusable entry is a stay step.
2. Find the contiguous visit to that region following the entry. Freeze the
   preceding and following path, its entry/exit, and any other visits to the
   same region.
3. Enumerate every simple route that can replace this visit, including
   routes with several turns and different occupancies. Reuse of the old
   section's cells is allowed. Every candidate must satisfy the forced-slide
   rules, including stopping constraints in neighboring retained cells.
4. Select a candidate with exact integer weight `activity^routeLength`.
   The current route is included. There is no enumeration cutoff, rejection
   of inconvenient valid candidates, or floating-point acceptance threshold.

This is a conditional-update, or heat-bath, method. The general idea is to
draw a group of variables from its conditional distribution with the
surrounding state fixed; see [Caputo's notes on block dynamics](https://www.mat.uniroma3.it/users/caputo/entropy.pdf)
and [Duke's blocked-sampling lecture](https://www2.stat.duke.edu/courses/Fall21/sta601.001/reading/10-reading.html).
Our partition into replaceable visits and the legality checks are specific
to Mortal Coil.

The mixture uses a region update on one eighth of steps and the existing
edit kernel on seven eighths. It keeps every stay step in the clock.
`gen-any --sampler edits` retains the earlier method, and
`--sampler backward` retains the single-pass growth experiment. The
construction-code format has not changed.

## Why this preserves the target and all-board coverage

For one fixed region/entry descriptor, every candidate has exactly the same
set of allowable reverse replacements. All unchanged path cells have the
same order relative to the replaced visit. The entry remains eligible after
replacement, including when the visit contains the finish. No empty route
that would remove the entry is admitted.

Let the outside contain c cells and a candidate visit contain m cells.
The intended solution-state weight is `activity^(c+m)`. Conditioning on the
outside cancels the common factor `activity^c`, leaving precisely the route
weights used by the implementation. The reverse conditional normalizer is
identical. The pairwise detailed-balance equality follows directly. Regions
and entries are selected independently of state, so their mixture preserves
the same target. Mixing this with the earlier reversible kernel does too.

The target on boards remains:

```text
weight(board) = numberOfOrderedSolutions(board) * activity^openCells(board)
```

At activity 2, density and solution count both affect probability. Denser
outputs are not by themselves evidence of more uniform board sampling.

All one-cell growth/deletion moves from the completeness proof still have
positive probability through the legacy branch. A sequence of at most
`4*area-4` such moves reaches any target solution from any start. Stay steps
pad the run. Thus the minimum `gen-any` budget of `4*area` still retains full
support under unrestricted random choices. The arbitrary-length
[construction-code interface](UNIVERSAL-GENERATOR.md) supplies the direct
all-board guarantee independently of fixed pseudorandom seed widths.

Every emitted board passes `Debug.DoDebug` and `CoilFormat.Validate`. Saved
path specimens also replay their persisted construction codes. No quality
filter, trimming, reverse-play requirement, or maximality condition was added.

## Results at equal step counts and similar runtime

The main 32x32 experiment uses four starts—singleton, backward growth,
horizontal corridors, and vertical corridors—and two independent streams
per start. It runs both methods for two million steps at activity 2. Stream
derivation is recorded in the plan; distinct initializations use distinct
streams. The methods use the same stream for each paired comparison.

| Method | Steps per chain | Chains | Final open-cell range | Mean seconds per chain |
|---|---:|---:|---:|---:|
| Earlier edits | 2 million | 8 | 440–686 | 0.615 |
| Region mixture | 2 million | 8 | 724–739 | 1.193 |
| Earlier edits, longer control | 4 million | 8 | 546–691 | 1.088 |
| Region mixture, long runs | 20 million | 4 | 747–784 | 9.087 |

The two-million-step region runs took about 1.1–1.3 seconds; the
four-million-step legacy controls took about 1.0–1.2 seconds. These are
recorded run times with other experiment work active, not isolated CPU
benchmarks. The occupancy improvement persists in this approximate runtime
comparison. All runs and actual timing values are in
[diagnostics.json](gallery/deep-v1/diagnostics.json).

![Independent starting boards](gallery/deep-v1/boards-32.png)

At 1000x1000, using the same saved seed, horizontal initialization,
activity 2, and 100 million steps:

| Method | Open cells | Solution slides | Recorded generation seconds |
|---|---:|---:|---:|
| Earlier edits | 568,668 | 127,173 | 23.1 |
| Region mixture | 650,155 | 272,737 | 54.2 |

The region sampler made 283,917 changes to local visits. Its occupancy is
still increasing late in the run: every million-step checkpoint from 91M
through 100M increased, from 647,335 to 650,155 open cells. The last pair of
checkpoints differ in about 0.66% of board cells.
Its path-direction imbalance remains 0.541, where +1 means all path edges
horizontal and -1 all vertical. This is one finite-run comparison, not a
multi-chain convergence assessment at a million cells.

![Million-cell comparison](gallery/deep-v1/large-comparison.png)

## Detecting the remaining bias

We record occupancy, signed and absolute board-adjacency imbalance,
solution-path directional imbalance, slide count, and the fraction of cells
that differ from the previous recorded state. Both signed and absolute
directional measures matter: averaging horizontal and vertical stripes can
give zero signed mean while every board is still striped.

`analysis/chain_diagnostics.py` computes rank-normalized split R-hat, folded
split R-hat, their maximum, and a rank-based bulk effective-sample-size
estimate. It discards the first half of recorded checkpoints, includes stay
states, and uses Geyer's initial-positive, monotone autocorrelation pairs.
ESS is conservatively capped at the number of retained checkpoints. Constant
or degenerate traces are marked uninformative rather than assigned R-hat 1.
This follows the methods of [Vehtari et al.](https://sites.stat.columbia.edu/gelman/research/published/rhat.pdf).

| Group | Occupancy R-hat | Path-direction R-hat |
|---|---:|---:|
| Earlier edits, 2M | 3.805 | 4.111 |
| Region mixture, 2M | 1.445 | 3.849 |
| Earlier edits, 4M | 3.373 | 4.186 |
| Region mixture, 20M | 1.488 | 2.766 |

These all flag disagreement. The paper's suggested first-level threshold is
1.01; passing it would still not prove exploration of unmeasured board
features. R-hat does not have to improve monotonically with longer runs, and
the long group has four chains instead of eight. The data specifically
reject the claim that these runs are established equilibrium samples.

![Occupancy and path-direction traces](gallery/deep-v1/traces-32.png)

The exact 3x3 study enumerates all 477 ordered solution states and every
production descriptor, then propagates the full probability vector. At
activity 2 after 16,384 steps, total-variation distance from the stated
weighted solution target changes:

- From a singleton: 0.00722 with earlier edits to 0.000356 with region updates.
- From a full-board solution: 0.26869 to 0.19233.

Detailed-balance residual is zero at floating-point precision for activities
1, 2, and 4. Separate integer tests check the conditional probability
identities exactly. The smaller distance is an improvement per step on this
finite space; it does not establish a million-cell mixing bound.

## Independent uniform board sampling beyond 4x4

`sample-uniform-rejection` generates independent fair wall/open bits, rejects
empty or disconnected boards, and uses the reference solver for the remaining
complete solvability decisions. A successful board has the same original
probability as every other layout. Conditioning on solvability therefore
gives uniform boards, without weighting them by their number of solutions.
This is exact under ideal independent fair bits; saved seeded runs are
reproducible realizations. Every emitted solution passes both validators.

The implementation supports up to 64 cells. A solver budget, timeout, or
depth failure aborts the run; it is never counted as an unsolvable proposal.
The attempt limit saves explicitly incomplete results and exits with an
error. Duplicate accepted boards are retained.

| Size | Accepted / requested | Proposals | Seconds | Sample mean open cells |
|---|---:|---:|---:|---:|
| 5x5 | 48 / 48 | 14,398 | 0.15 | 14.1875 |
| 6x6 | 48 / 48 | 463,325 | 1.66 | 20.1250 |
| 7x7 | 6 / 12 | 10,000,000 | 31.52 | 28.6667 |

These sample means have sampling error, especially the six-board 7x7 result.
They are not extrapolations to large dimensions. The sharply worsening
acceptance rate is direct evidence that naive independent-bit rejection is
not a practical million-cell uniform sampler.

[Every uniform 5x5 draw](gallery/deep-v1/uniform-5x5/boards.png),
[every uniform 6x6 draw](gallery/deep-v1/uniform-6x6/boards.png), and
[the partial 7x7 collection](gallery/deep-v1/uniform-7x7/boards.png) are saved.

## What the research suggests next

These are research conclusions and proposed experiments, not implemented
guarantees.

| Approach | What it could address | Mortal Coil-specific work still needed |
|---|---|---|
| Multiple-visit region reconnection | Change how long outside sections connect, addressing persistent global path direction | Resample several visits together; reject cycles; replay changed global visit order; prove reverse proposal probabilities |
| Configurational-bias regrowth | Replace much longer sections without enumerating all routes | Compute both old and new Rosenbluth proposal factors; measure trapping and acceptance under forced slides |
| Replica exchange / parallel tempering | Let dense states traverse lower-density configurations | Use an activity ladder with overlapping occupancy distributions; measure accepted swaps and round trips; include swap acceptance ratios |
| Projected SAT sampling | Target distinct boards while treating solution paths as existential witnesses | Encode physics, project onto wall/open bits, and verify sampler guarantees and cost on progressively larger boards |
| Pivot or backbite moves | Change large-scale path shape and connectivity | A self-avoiding path can still violate forced slides; every affected stopping dependency needs validation |

The immediate geometric limitation is that our region sampler freezes other
visits to the same region and the order of the outside path. The persistent
path-direction diagnostic makes multi-visit reconnection or long-section
regrowth a more direct next experiment than simply increasing the existing
step count again. This prioritization is an inference from our measurements.

[Siepmann and Frenkel's configurational-bias method](https://siepmann.chem.umn.edu/publications/3)
introduces large chain changes with Rosenbluth-based construction. Its
polymer results motivate an adaptation; they do not prove effectiveness
under Mortal Coil's stronger constraints. For fixed-length prefix regrowth
using uniform legal choices, proposal probability is the reciprocal of the
product of legal-choice counts. The Metropolis correction is the new product
divided by the old product, capped at one. Trapped proposals remain stays.

[Earl and Deem review replica exchange](https://arxiv.org/abs/physics/0508111).
For our weight `a^n`, swapping paths of sizes n_i and n_j at activities a_i
and a_j has acceptance `min(1, (a_i/a_j)^(n_j-n_i))`. A ladder with negligible
swap acceptance would provide little exploration benefit. A pilot must
measure overlap and round trips before committing large-board resources.

[UniGen supports a sampling/projection set](https://github.com/meelgroup/unigen/),
and its authors give [near-uniform SAT sampling guarantees](https://www.cs.toronto.edu/~meel/Papers/DAC2014.pdf).
The relevant formulation would sample only the wall/open variables while
allowing any legal solution witness. Sampling all SAT variables without
projection would recreate the solution-count bias. A possible encoding uses
open-cell bits, predecessor/successor edges, and visit-order variables; turn
constraints require the cell ahead to be a wall, boundary, or earlier visit.
This is a proposed encoding, not an implemented SAT exporter or a performance
claim for a million-cell grid.

[Clisby's pivot work](https://arxiv.org/abs/1005.1444) demonstrates efficient
large self-avoiding-walk simulation. Its collision test alone is insufficient
here: rotating or reversing a subpath can change which visited cells block
turns. Applying those techniques requires additional physics checks.

## Commands and validation

```sh
# The improved default; codes still replay with no random source.
dotnet run -c Release -- gen-any 1000 1000 --out output/deeper-1000

# Images, exact geometry, traces, and a saved seed.
dotnet run -c Release -- deep-specimen NEW-DIRECTORY 1000 --steps 100000000

# Uniform board reference, with explicit attempt limit.
dotnet run -c Release -- sample-uniform-rejection 6 6 NEW-DIRECTORY --count 48 --max-attempts 1000000

# Exact production transition probabilities and reproducible comparisons.
dotnet run -c Release -- deep-distribution-study NEW.json
python3 scripts/build_deep_collection.py NEW-DIRECTORY --plan gallery/deep-v1/plan.json
python3 scripts/report_deep_sampling.py
```

Regression tests check every 3x3 board decision, all 4,256 enumerated
conditional routes and identical inverse supports on the 477 solution
states, exact integer detailed-balance identities, and 45,000 mixed steps
with repeated full validation on larger boards. Earlier exhaustive 4x4
coverage and regression checks remain enabled. Diagnostic tests cover
independent draws, location and scale disagreement, ties, and constant traces.
The exported path collection is checked with `verify-collection`.
