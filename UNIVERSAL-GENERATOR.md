# Generating every legal Mortal Coil board

`gen-any` is the unrestricted generation command. For any positive rectangle
that fits memory, **every nonempty solvable wall/open board has a finite
construction code that generates it**. Every accepted code produces a legal
board and a forward solution. It includes singletons, one-dimensional boards,
boards whose solutions cannot be played backward, and boards that could still
be dug into further. There is no trimming, minimum occupancy, endpoint,
uniqueness, or difficulty filter.

## The terms

A **mask** is just an encoding of a board's wall/open layout: one bit per
cell. A **solvable mask** means a board with at least one legal solution.
Neither term defines a special class of puzzle.

**Undoable solution edits** is the term used here for what earlier reports
called "reversible path edits." An addition can be undone by a deletion; a
detour can be replaced by the straight section it replaced. This lets random
generation shrink, escape a trapped state, and grow elsewhere. It does **not**
require the player's solution to work backward. The class name
`ReversiblePathSampler`, command `reversible-specimen`, and saved identifier
`reversible-path-v1` remain stable for existing scripts and collections.

The original `gen` implementation uses forward wandering and rectangular
tweaks. It did not use this new sampler or require reverse-playable solutions.
For example, the saved [500-random-s202 board](gallery/boards/500-random-s202/preview.png)
has 164,927 open cells, and reversing its saved solution already fails at the
third visited cell: it would turn at (409,172) while (408,172) remains unblocked.
The [game author's published rules](https://github.com/adum/coilbench#3-rules)
require sliding until blocked by a wall, boundary, or visited square and
covering every open cell. They impose no reverse-play requirement. Those rules
do not establish which internal generation method the official game uses.

For example, numbers below show a legal visit order; `X` is a wall:

```text
5 X X
4 3 X
1 2 X
```

Start at 1 and play right, up, left, up. Starting at 5 and trying the reverse
order fails: the first downward slide passes 4 and continues to 1. Our
generator includes this board and the forward solution. Its 3x3 construction
code is `1EB5`; the regression suite checks that the reverse traversal fails.

## Use it

```sh
# New dense sample, 100 million edit steps; save the full construction code.
dotnet run -c Release -- gen-any 1000 1000 --out output/any-1000

# Replay exactly, with no random source or known solution needed.
dotnet run -c Release -- gen-any --code-file output/any-1000.code.json --out output/any-1000-replay

# The forward-only example above.
dotnet run -c Release -- gen-any 3 3 --code 1EB5 --out output/forward-only

# Compute a code for an existing board and any validated solution.
dotnet run -c Release -- encode-board existing.board existing.solution --out output/existing.code.json
```

`encode-board` also reads gzip files. Each `gen-any` output includes `.board`,
`.solution`, `.recipe.json`, `.code.json`, and `.generation.json`; existing
files are rejected. Every output passes `Debug.DoDebug` and
`CoilFormat.Validate`, and its code is decoded again to check exact equality
of both board and solution. The code JSON includes its dimensions and format
version. It can be arbitrarily long; it is not hashed into a fixed-size seed.

Without a supplied code, the default `--sampler deep` starts with a serpentine
corridor board. Seven eighths of its steps use endpoint/rectangle edits;
one eighth exactly resamples a path section inside a 2x2, 3x3, or 4x4 region.
[Research, experiments, and remaining bias](DEEP-SAMPLING.md) describe why this
replaced the earlier `--sampler edits` default, which remains available.
It uses fresh blocks of system-random bytes, with exact bounded
integer rejection sampling. `--activity N` defaults to 2 and favors occupancy.
`--steps N` defaults to 100 times the rectangle area and must be at least four
times that area, preserving the reachability bound below. The saved code
reproduces the final board and solution, not the whole random edit history.

`--sampler backward` instead chooses a target size and legally grows from
the finish. That method also has full support under ideal random choices,
but it commonly traps early and produces small occupied regions.

**The default dense samples are not uniform random boards.** Their stationary
board weight is `solutionCount * activity^openCells`, and finite runs retain
initialization bias. For exact uniform small-board sampling and measured
large-board limitations, see [SAMPLING.md](SAMPLING.md). Universal coverage
does not settle the probability of any particular board.

## Why every board is reachable

Take any legal solution, written as individual visited cells. Remove its
first cell and make that cell a wall. The remaining solution still works:
during its original play, the removed cell was already visited and therefore
blocked. Replacing that visited cell with a wall has the same effect.

Repeat until only the finish remains. Reverse these deletions, opening one
cell at the start at each step. These are precisely the legal additions in
`BackwardGenerator`. This reconstructs the original board and chosen forward
solution. Stop at that length even when another legal addition is possible.
The [full proof](GENERATION-COMPLETENESS.md) establishes the exact local rule.

The direct integer interface makes this constructive. Let `A = width*height`,
`n` be the number of open cells, and `f` the row-major index of the finish.
At each construction step, list legal growth directions in U, R, D, L order.
Record the chosen index (0 through 3) as a base-4 digit, first choice least
significant. Let `C` be the resulting integer. Encode:

```text
code = (n - 1) + A * (f + A * C)
```

Decoding takes the remainder modulo A as `n-1`, then the next remainder as f.
The remaining integer supplies base-4 choices. A digit is reduced modulo the
current number of legal additions; if there are none, construction stops.
Missing high digits are zero. Every integer therefore gives legal choices
and a permitted stopping point. For an encoded legal solution, every digit
is already a legal index, no early trap occurs, and the result is exact.

Thus `Decode(Encode(solution)) = solution` for **every** legal solution,
which proves that every solvable board occurs among the decoder's outputs.
Codes are not unique, and uniformly chosen integers would not yield uniformly
chosen boards. Every solution on area A has a code below `A^2 * 4^(A-1)`;
roughly two bits per visited cell plus the two location/length fields suffice.
The implementation extracts all choice bytes once, then uses constant work
per growth step, so million-cell codes do not require quadratic replay.

For the default random edit sampler, any starting solution of size s can
reach any target solution of size n in at most
`(s-1) + 2*(width+height-2) + (n-1) <= 4*A-4` steps: delete to a singleton,
move it by adjacent add/delete pairs, then prepend the target construction.
Every required one-cell edit and acceptance has positive probability for
finite activity. In the deep mixture, choosing the legacy edit branch at
each of these steps also has positive probability. Stay steps pad this to any larger budget. This proves full
support under unrestricted random choices; it does not prove fast mixing.
The direct code guarantee does not rely on a claim about physical RNG entropy
or the coverage of a finite pseudorandom seed space.

## Checks

The regression suite compares all nonempty 4x4 wall/open layouts with an
independent forward-slide solver: **3,503 solvable boards, 9,432 ordered
solutions**. Every generated solution is encoded, decoded, and checked for
exact board/solution equality and stable canonical code. It also checks
singletons, one-dimensional boards, the forward-only example, extensible
boards, arbitrary large numeric inputs, and a 1000x1000 construction.

The finite enumeration checks the implementation; the deletion argument
proves coverage beyond those tested sizes. Both validators remain mandatory.

All 52 specimens in the [backward collection](gallery/backward-v1/README.md)
and [solution-edit collection](gallery/reversible-v1/README.md) now include
`construction.code.json` files linked in their manifests. The production
encoder and decoder reproduced every board and solution, including all
1000-square specimens; [verification record](gallery/construction-verification.json).
