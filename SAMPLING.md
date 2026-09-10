# Implemented sampling methods

The implementation separates exact uniform board sampling from scalable
sampling of weighted solution paths. The [measured study](gallery/sampling-v1/README.md)
and [large-board collection](gallery/reversible-v1/README.md) include images,
statistics, and the limitations observed in the experiments.

For the direct command that can construct every board, use
[`gen-any`](UNIVERSAL-GENERATOR.md). "Mask" below means the wall/open board
layout. "Reversible" refers to undoable generator edits; the solution does
not need to be playable backward. Existing class names and dataset identifiers
retain that word for compatibility.

[Deeper sampling](DEEP-SAMPLING.md) adds exact conditional route replacements,
independent uniform board rejection sampling beyond 4x4, and multi-start
diagnostics. `gen-any` now defaults to the block mixture; `--sampler edits`
selects the earlier kernel documented below.

## Commands

```sh
dotnet run -c Release -- sample-uniform 4 NEW-DIRECTORY --count 48
dotnet run -c Release -- sample-uniform 4 NEW-DIRECTORY --count 48 --method chain --burn-in 4096 --stride 128
dotnet run -c Release -- sampling-study NEW.json
dotnet run -c Release -- reversible-specimen NEW-DIRECTORY 1000 --activity 2 --steps 100000000
dotnet run -c Release -- reversible-specimen NEW-DIRECTORY 64 --init singleton --activity 2 --steps 5000000
```

Generation commands accept `--seed-hex HEX` and otherwise save a freshly
generated 512-bit seed. Output directories must be new. Every exported board
has a solution that passes both the cell-path structural validator and the
independent Mortal Coil replay. Reversible specimens include full maps,
solution colors, visibly labeled crops, exact board geometry, proposal counts,
acceptance counts, and occupancy histories. Their final backward-construction
recipes remain usable with `gen-backward --recipe FILE --out NEW-STEM`.

## Exact uniform boards

`UniformBoardSampler` enumerates every nonempty wall/open mask of a fixed
rectangle, with a maximum of 16 cells. It asks the existing solver for a
witness and throws if any decision was cut short. Each solvable mask appears
once in the catalogue, irrespective of its number of solutions. Sampling one
uniform catalogue index therefore gives a uniform board under ideal random
choices. Repeated draws are retained; filtering duplicates would change the
experiment. The CLI currently exposes square sides 1 through 4.

The optional chain method uses the exact catalogue as its solvability oracle:
half the proposals stay, the other half flip a uniformly selected cell, and
unsolvable proposals stay. All steps, including stays, count toward burn-in
and sample spacing. Its stationary distribution is uniform over boards;
finite-run samples are correlated and can retain initialization bias.

`sampling-study` propagates the full board-chain probability vector on 2x2,
3x3, and 4x4 from singleton and full-board starts. This uses floating-point
arithmetic with rounding error, but no Monte Carlo estimation error. It also
writes a companion `.paths.json` with the full 3x3 path-state experiment.
No efficient exact-uniform sampler or mixing bound for million-cell boards
is claimed.

## Undoable solution edits

The state is an ordered valid solution whose cells define the entire open
board. `ReversiblePathSampler` stores predecessor/successor arrays and ordered
cell indices. It supports six paired operations:

- Add a straight ray at the start; delete that ray from the start.
- Add a straight ray at the finish; delete that ray from the finish.
- Replace a straight section with a rectangular detour; replace that detour
  with the straight section.

Each step stays with probability 1/2. Otherwise it chooses one of the six
operations uniformly. Endpoint descriptors choose a direction uniformly
from four and length uniformly from 1..8. Rectangle descriptors choose an
anchor uniformly from **all board cells**, a direction from four, a baseline
length from 2..12, a height from 1..3, and one of the two sides uniformly.
Invalid descriptors stay. Choosing only successful edits would change this
kernel and would require a different acceptance calculation.

Each descriptor and its inverse have equal proposal probability. An integer
`activity >= 1` defines target weight `activity^n` per ordered solution of n
cells. Growth is accepted when legal. A net deletion of k cells is accepted
with probability `activity^-k`, implemented exactly by k independent bounded
choices equaling zero. At activity 1 every legal proposal is accepted. There
are no rounded floating-point acceptance thresholds.

This satisfies detailed balance for the stated solution distribution. On
boards, the induced weight is `S(B) * activity^openCells(B)`, where S(B) counts
ordered solutions. Activity 1 therefore does **not** mean uniform boards.
Higher activity favors occupancy and can slow exploration substantially.

## Local legality and output validation

A replacement preserves the order of every retained cell. The only path
turns whose incoming/outgoing directions change are the two retained ends
and new cells. Opening a former wall can also invalidate a neighboring turn
that stopped there. Check these cells and all neighbors of inserted cells:
at a turn, the cell straight ahead must be outside, closed, or visited earlier.
Removed cells become walls and cannot invalidate another retained turn's
existing blocker. Proposed cells must all be unused and the constructed
rectangle/ray must stay inside the board.

Invalid proposals and probabilistically rejected proposals restore the old
links and indices. Index gaps are replenished by reindexing when exhausted.
Snapshots check linked-list consistency, increasing indices, occupancy count,
and reconstruct a backward recipe. Replaying that recipe checks every prepend,
then `Debug.DoDebug` and `CoilFormat.Validate` check the final result again.
No validator or movement rule is weakened.

## Coverage and finite runs

Single-cell start deletions reduce any solution to a singleton. A start
addition followed by finish deletion moves a singleton to an adjacent cell.
Repeating this moves it anywhere. Backward growth then reconstructs any
target solution, by the suffix proof in [GENERATION-COMPLETENESS.md](GENERATION-COMPLETENESS.md).
All these moves occur with positive probability at every finite activity.

For N = W*H, the sequence requires at most
`(N-1) + 2*(W+H-2) + (N-1) <= 4*N-4` proposals from any starting state.
Lazy steps can pad this to any longer prescribed budget. Thus, with ideal
arbitrary random choices, a budget of at least 4*N suffices for **positive
support on every valid solution**, including non-maximal boards. The default
20*N and the collection's 100*N exceed that bound. This is a reachability
bound, not a useful probability or convergence bound. Short custom budgets
need not have full support.

A fixed-width seed still represents finitely many runs. The arbitrary-choice
API and final construction recipes express the general coverage argument;
a 512-bit seed must not be used as a substitute for that argument.

## Tests and observed limits

The regression suite checks all 171 solvable 3x3 masks, their uniform indices,
symmetric board transitions and connectivity. It enumerates all 477 3x3
solution states and checks 2,560 accepted endpoint/rectangle edits and their
inverses, validating restored states as well as accepted ones. It also checks
180,000 random mutation steps across activities 1, 2, and 4. The existing
exhaustive 4x4 backward test and all earlier regressions remain enabled.

The path-distribution study enumerates **all production descriptors** on
3x3 and checks detailed balance for activities 1, 2, and 4. It propagates
probabilities from singleton and full-board solution starts for 16,384 steps.
At activity 2 the full-start total-variation distance is still about 0.269;
at activity 4 it is about 0.542. Occupancy means alone would conceal that bias.

The large collection has 20 main specimens at 16, 64, 256, 512, and 1000
square, using two seeds at activities 1 and 2. Its initialization is a
validated serpentine corridor board, not the earlier wander/tweak generator.
The four 1000-square outputs have 565,190, 565,555, 568,668, and 568,471 open
cells. They retain directional structure from their initialization.

Two controlled 64-square, activity-2 runs use the same seed and five million
steps. The singleton start yields 398 open cells; the stripe start yields
2,594. This is direct evidence of remaining initialization dependence. Large
outputs are usable validated puzzles and measured samples of this finite-run
procedure, not certified equilibrium samples. The newer block mixture resamples
multi-turn sections in regions through 4x4. Larger section regrowth,
solution-count correction, and a large-board mixing guarantee remain unresolved.
