# Generation completeness

## Scope and conclusion

A valid board here is a nonempty open-cell subset of a fixed finite W by H
rectangle with at least one Mortal Coil solution: visit every open cell once,
sliding until a boundary, wall, or visited cell blocks movement. A singleton
has an empty solution, as accepted by `CoilFormat.Validate`. There are no
uniqueness, density, difficulty, or endpoint restrictions.

The original `gen` pipeline is **not complete under its defaults**.
Backward growth from any singleton (or any oriented adjacent pair for boards
with at least two cells) **is complete**, under the rule below. The C#
`gen-backward` command now implements this construction. The existing `gen`
pipeline retains its previous behavior. The accompanying Python program is
an independent exhaustive analysis reference. [`gen-any`](UNIVERSAL-GENERATOR.md)
now exposes this through arbitrary-length numeric construction codes: every
integer produces a legal board, and every legal solution has an integer that
reconstructs it. Its default dense sampler uses undoable solution edits.

Here "mask" means the board's wall/open layout. "Reversible edits" means
generator changes with an inverse, not solutions that can be played backward.
There is no reverse-play requirement in our generator or in the
[game author's rules](https://github.com/adum/coilbench#3-rules).

## Current generator audit

`BaseLevel.InitialWander` digs successive perpendicular segments and records
their stopping cells. `Level.GetTweaks` and `ApplyTweak` replace straight
portions with rectangular detours, including variants that lengthen neighboring
segments. Thus the existing generator already adds twists to a solution.
The CLI limits candidate tweaks, runs configured passes, and normally trims
dead ends before output.

A counterexample to default completeness is the three-cell L:

```text
..
.X
```

It is solvable, but both endpoints have degree one and its solution needs two
segments. `TrimDeadEnds` removes its first endpoint while there is more than
one segment. The default pipeline therefore cannot emit this board. Embedding
the three cells in a larger wall-filled rectangle gives the same obstruction.

Disabling trimming removes this obstruction; it does not prove completeness.
A proof would also need to address the restricted start range on large boards,
`MakeRandomSegFrom` excluding the maximum available length when that length
exceeds one unless `GoMax` is enabled, tweak candidate limits, and emission
only after the tweak phase. These observations do not individually prove
that every possible configuration is incomplete.

## Exact backward-growth rule

Fill the rectangle with walls and open one cell. Keep an ordered solution
P, start first and finish last. Its cells are exactly the board's open cells.
Prepend a wall cell v adjacent to the current start s.

Let d = s - v and a = s + d, the cell straight ahead of s when arriving from
v. The addition is legal exactly when:

1. v is inside the rectangle and is not already open; and
2. P is a singleton, **or** its next cell is a, **or** a is not open.

The last alternative includes a outside the rectangle. Continuing straight
through s extends the first slide. Turning at s requires a blocker at a;
an open a would still be unvisited and force continued straight movement.

Once we reach s, v is visited. Every subsequent slide sees v as blocked,
just as it saw the wall there before the addition. Nothing else about the
suffix changes. This proves soundness of the local rule. Membership checks
and the old first step suffice; no scan of later segments is needed.

We construct a forward-playable solution backward. We do not assume that
reversing the traversal of a solution on its original board is legal.

## Completeness proof

Take any valid board B and any solution expressed as individual cells
P = (p0, p1, ..., pn).

**Suffix lemma.** Delete p0 and make its cell a wall. The remaining path
(p1, ..., pn) is a valid solution of the remaining board. Throughout that
suffix in the original play, p0 was visited and blocked. Replacing it with
a wall has the same effect. Starting inside the first slide shortens that
slide; removing the whole first slide starts play with the next one.

Repeat until only (pn) remains, or stop at the adjacent pair (p[n-1], pn)
when singletons are outside the desired scope. Every suffix is valid.
Reverse the deletions: each is a legal prepend under the exact rule above.
They reconstruct B and its chosen solution.

**The construction reaches every valid board, and every ordered valid
solution, provided every seed position and legal prepend are allowed and
every intermediate state can be emitted.** Interior detours and forward-end
growth are unnecessary for this theorem.

For randomized generation, every seed and legal choice must have positive
probability, and every construction length must be eligible for output.
The implementation chooses a target size uniformly from 1 through W*H before
growing, then chooses uniformly among legal additions until that size or until
trapped. For any target solution with n cells, choose size n, its finish cell,
and its reverse construction sequence. Each choice has positive probability,
so their finite product is positive. Exhaustively branching instead gives
deterministic enumeration. The recipe interface directly exposes all finite
legal sequences, independent of assumptions about physical random sources.

This assumes a random source capable of arbitrary finite choice sequences.
A deterministic generator with only a 32-bit seed has at most 2^32 outputs
for fixed other parameters. It cannot cover a board class larger than that.
An implementation claiming full support must expose arbitrary choice
sequences or sufficiently general random input, not identify abstract
randomness with `Random(int)`.

The command now defaults to a saved 512-bit seed and a versioned SHA-256
counter stream, with `--seed-hex` accepting longer seeds. This improves
reproducible variety but does not turn a fixed-width seed into a proof of
universal coverage. The arbitrary-length recipe remains a direct interface
to every legal construction sequence. `gen-any` also supplies a numeric
interface without hashing: `code = (n-1) + A*(finish + A*choices)`, where
choices are base-4 legal-branch indices. Its encoder and decoder implement a
constructive inverse for every valid solution. See
[the code specification](UNIVERSAL-GENERATOR.md#why-every-board-is-reachable).

## Both ends and interior twists

Forward-end growth can open a wall that stopped an earlier slide. It must
preserve earlier stopping constraints or replay the entire proposed path.
This asymmetry explains the smaller legality test for backward growth.

Interior rerouting can improve density and visual variety. It must validate
the resulting solution, including later slides that stop on earlier trail.
The existing rectangular tweaks are candidates. They are unnecessary for
coverage; a completeness claim must retain the option to emit the backward
construction unchanged.

To let a mutation process escape any existing board, allow start deletion
as well as prepend: it can reduce to a singleton and regrow. To move the
singleton, also allow finish deletion: prepend a neighboring cell to make
a pair, then delete the old finish. Finish deletion leaves a legal prefix,
because future cells could never have supplied visited-trail blockers for
earlier turns. Pair moves connect all singleton positions in the rectangle.
Together these operations connect all solution states. Their scheduling
must permit temporary decreases in size; growth-only greedy mutations do
not inherit this connectivity argument.

An adjacent-pair seed means two open cells in an otherwise wall-filled board.
If "most occupied" means already-open path cells, growth alone cannot reach
smaller boards. A fixed pair position does not suffice for backward-only
growth; the theorem allows every oriented pair position.

## Exhaustive check

`analysis/check_backward_generation.py` enumerates backward constructions
and independently solves every nonempty board mask by maximal forward
slides. It compares complete sets of ordered cell paths per board, checking
for both missing solutions and invalid constructions.

```sh
python3 analysis/check_backward_generation.py 4 4
```

Result: **3,503 solvable boards, 9,432 ordered solutions, exact agreement**
over all 65,535 nonempty masks. The finite check supports implementation
correctness; the suffix lemma proves coverage for arbitrary finite dimensions.
The script does not emit production levels. `tests/BackwardGenerationTests.cs`
also exhaustively enumerates the C# implementation and compares solution
counts per mask with the existing C# solver over all 4x4 masks: the same
3,503 boards and 9,432 distinct validated solutions. It checks singleton and
one-dimensional boards, rejection of illegal extensions, recipe replay, and
stopping on an extensible board.

Production output passes both `Debug.DoDebug` and `CoilFormat.Validate`.
`BackwardDebug.cs` adds a cell-path overload of the structural validator:
it independently reconstructs visitation order, checks occupancy, adjacency,
uniqueness, and blockers at every turn. It handles singleton paths without
zero-length segments. The existing segment-model validator is unchanged.

## Production implications

The universal construction method is backward growth. Forward-end growth,
both-end deletion, and undoable rectangular detours are implemented in the
large solution-edit sampler. Preserve positive probabilities for every legal construction
choice and every stopping length. Do not unconditionally trim or transform outputs in a mode claiming
all-board coverage. Explicit size and style filters define narrower target
classes and must be named as such.

Completeness does not establish optimal speed, density, style, or uniform
sampling. Multiple solutions and construction histories can make some boards
much more likely than others. Compare these practical outcomes against the
existing generator before changing its default behavior.

The [first production survey](gallery/backward-v1/README.md) retains all 30
runs at ten sizes through 1000 square. With uniform legal-cell selection,
24 runs trapped before their target size. The three 1000-square specimens
have 12, 16, and 53 open cells. This is evidence that this sampler does not
reliably produce large occupied puzzles, despite the construction's
completeness. Full maps and labeled occupied-region crops make the distinction
visible. No outputs were filtered or replaced with denser selections.

## Sampling uniformly rather than merely reaching every board

The target in this section is uniform over distinct solvable wall/open masks
on a fixed W by H rectangle, with positions and orientations counted
separately. Uniform occupancy counts, uniform solutions, and uniform legal
choices give different distributions.

Enumerating the current sampler's branching probabilities on 4x4 gives
mean occupancy 6.4470463505 cells and probability 0.3914930556 of at least
eight open cells. Uniform selection among the 3,503 solvable masks instead
gives mean occupancy 8.8552669141 and probability 0.7199543249 of at least
eight open cells. These are small-board results, not extrapolations to 1000.

An exact independent reference sampler is fair independent wall/open bits
followed by an exact solvability test: reject unsolvable masks and repeat.
Every accepted mask has the same original probability 2^(-W*H). Enumeration
and uniform selection from the resulting mask list is another exact method
for small boards. Neither has demonstrated practical performance here for
large boards.

A board-state Markov chain has a uniform stationary distribution:

1. With probability one half, stay at the current board.
2. Otherwise select one of the W*H cells uniformly and flip wall/open.
3. Accept exactly when the resulting nonempty board is solvable; otherwise
   stay at the old board. Retain stays in the sampling clock.

Each distinct accepted transition and its reverse have probability 1/(2WH),
so detailed balance holds for uniform board probabilities. The graph is
connected: suffix deletion reduces any solvable board to a singleton;
opening a neighboring singleton cell then closing the old one moves it
anywhere; reverse suffix deletion constructs any target. The explicit stay
makes the chain aperiodic. These facts prove convergence to uniform on the
finite state space, not a useful finite running time. Exhaustive checking
also found the single-cell-flip graph connected on all 3,503 solvable 4x4
masks. The general stationary/convergence results are described in
[Levin and Peres, Chapters 3–4](https://pages.uoregon.edu/dlevin/MARKOV/markovmixing.pdf).

Two practical costs remain: exact solvability checks after proposals, and
unknown mixing time. A solver timeout cannot be treated as proof that a board
is unsolvable in a claim covering all boards. Recording only accepted moves
would also change the stationary sampling distribution.

For large boards, reversible edits to a stored valid path can avoid solving
each candidate from scratch: add/delete at either end, remove/regrow a
section, and replace a section with a validated detour. Include the inverse
operations and account for proposal probabilities in acceptance. Such a
sampler must be labeled by its actual target: uniform solution states weight
a board in proportion to its number of solutions. To induce uniform boards
from solution states requires total weight one per board, for example weight
1/S(B) for each of its S(B) solutions. Counting S(B) is an additional obstacle.

Occupancy preferences can be explicit rather than accidental: board weights
proportional to exp(lambda * openCells) retain positive weight for every
board for finite lambda, with lambda > 0 favoring larger occupancy. This is
a different target from uniform boards. A path-state version also retains
solution-count bias unless corrected. Deletion and rerouting address trapping;
they alone prove neither uniformity nor a practical mixing time.

These methods are now implemented: see [SAMPLING.md](SAMPLING.md) for the
proposal rules, target distributions, validation, and finite-budget coverage
proof; [the measured study](gallery/sampling-v1/README.md) compares the results.
Exact uniform sampling is available for small boards. Large-board weighted
path samples are generated and measured, with remaining initialization bias
explicitly reported.
