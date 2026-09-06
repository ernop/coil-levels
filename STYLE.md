# Level style and structural solving

The current goal is a vocabulary of meaningful, independently inspectable
parameters for a level's visual style and the mathematical work needed to
solve it. A vector of measurements, distributions, spatial maps, and proof
objects is more useful than one weighted score. The DFS experiments in
`HARDNESS.md` describe that particular search procedure; they are not the
objective of this analysis.

`analysis/describe.py` is the first executable prototype. It takes only a
`.board`, requires Python 3.10+ and the standard library, and does no search
over paths or guessed starts. Known solutions are used only in tests.

```sh
python3 analysis/describe.py levels/hard/*.board --output output/style/hard-levels.json
python3 -m unittest discover -s analysis -p 'test_*.py' -v
```

The default limit is 100,000 cells, checked before graph allocation. This is
an explicit prototype memory bound, adjustable with `--max-cells`, not an
approximation. The Python graph representation and full proof output are
not intended for 5000x5000 boards. A compact C# implementation and streaming
geometry summaries are needed before applying this to the largest levels.

## Definitions that keep the parameters meaningful

- **Occupancy:** fraction of cells that are open. Its complement is wall
  occupancy. Report both labels explicitly when comparing other datasets.
- **Visual chamber:** a broad connected open area, defined at a stated
  spatial scale. The initial implementation finds components of cells with
  enough clearance to center a full open square. These are chamber *cores*,
  not a partition into entire rooms.
- **Structural room:** a region whose interface with the remaining board
  constrains traversal. Initially these are components after removing all
  bridges, plus vertex-biconnected blocks. A two-cell-wide doorway will
  usually have no bridge; detecting those requires wider separators.
- **Forced room:** the interface forces an endpoint or a traversal pattern.
  Every open cell in every room must be visited. What can be optional is the
  choice of ports, their pairing, direction, or traversal order—not coverage.
- **Difficulty:** specify which deductions are available, what they prove,
  their dependency depth, and the structure of what they leave unresolved.
  Failure of an incomplete rule set to deduce something is not proof that
  it requires guessing or is hard.

Degree two by itself does not make both incident edges mandatory: that cell
might be an endpoint. A 2x2 open square is a useful counterexample. Endpoint
variables must remain part of the reasoning.

## Parameter families

Implemented quantities are marked **v1**. The remaining quantities describe
concrete extensions, not already-computed measurements.

| Family | Parameters | Mathematical form and interpretation |
|---|---|---|
| Amount and local density | **v1:** open fraction, 3x3/5x5 density variance, open/wall interface per open cell | Scalar plus scale function; distinguish equal occupancy with clustered versus dispersed cells |
| Straightness and directional texture | **v1:** horizontal and vertical run-length distributions, signed horizontal/vertical edge bias | Distributions and orientation tensor; later include diagonal structure and multiscale directional correlations |
| Unused area shape | **v1:** largest all-wall square and its coordinates, number/fraction of all-wall squares at each size, wall-component area, bounding-box fill and squareness | Morphological size spectrum and shape distribution; a long thin wall should differ from a solid unused square |
| Open space and chambers | **v1:** all-open square spectrum, square-clearance distribution, connected core counts and areas at clearance radii 2, 3, 4 | Scale-indexed geometry; later track the birth, split, and disappearance of chamber cores across all radii |
| Relation to the board edge | **v1:** occupancy in every distance-to-edge layer and mean open-cell edge distance | Spatial profile; later measure boundary-parallel runs, side-specific imbalance, and the same statistics for room centers and gates |
| Symmetry | **v1:** global reflections/rotations, nonuniform local patch symmetry | Group action: exact invariance and density-adjusted agreement; report orientation-specific values rather than hiding all axes in a maximum |
| Repetition | **v1:** oriented and rotation/reflection-canonical 3x3/5x5 motif entropy, repeated-patch pair fraction, common motifs, translation agreement at offsets 1, 2, 3, 4, 8 | Information and correlation; separate a repeated motif from a repeated orientation of that motif |
| Topology | **v1:** connected components, bridges, articulation cells, vertex-biconnected blocks, graph cycle rank | Graph invariants and decomposition; cycle rank E−V+C counts independent graph cycles, not holes in the drawn open region |
| Room interfaces | **v1:** bridge-room areas and gate counts, terminal/transit labels, checkerboard imbalance | Region graph with explicit gates; next: narrow vertex/edge separators, contiguous doorway widths, port positions and permitted port combinations |
| Forced structure | **v1:** forced and excluded edges, endpoint candidates, rule assignment counts, dependency depth, full derivation records | A proof-producing constraint system; each parameter has an inspectable witness |
| Unresolved structure | **v1:** unknown edge count, affected cell fraction, components of the residual variable/constraint graph | Size and coupling of the remaining problem; later contract solved rooms and measure separator widths, branching structure, and boundary-state counts |
| Arrangement beyond local motifs | Pair correlation as a full displacement field, Fourier power by scale/direction, chamber-spacing distribution, graph spectral gaps | Functions and spectra; useful when two levels share local motif counts but differ in large-scale organization |
| Reusable room logic | Room shape class, port signature count, symmetry-equivalent signatures, orientation dependencies, number of times a proven room rule applies | Relations and compositional proofs; the main next step toward detailed solving through subrooms |

All fractions name their denominators in the schema or this document. Counts
of rooms/cores are also given per 1000 open cells. Keep absolute sizes and
sizes normalized by board dimensions; choosing only one loses information.
Bounding-box squareness alone does not establish a solid square, so it is
paired with fill and the exact square spectrum.

## What v1 proves

Let the open cells and their orthogonal adjacencies form a graph. Each
adjacency has a binary variable `x_e` indicating use by the final path; each
cell has an endpoint variable `e_v`. For boards with at least two open cells:

    sum(x_e incident to v) + e_v = 2
    sum(e_v) = 2

The implementation propagates bounds on these equalities through a work
queue. A variable is assigned at most once. A rule is revisited only when
one of its inputs changes. There is no candidate-path enumeration,
backtracking, or repeated flood-fill pruning.

Additional deductions:

1. **Checkerboard parity.** Equal color populations require one endpoint of
   each color. An excess of one cell requires both endpoints on that color.
   A larger imbalance rules out a covering path.
2. **Bridge crossings.** Every bridge must be used. A room at the leaf of the
   bridge decomposition contains one endpoint. A room between two bridges
   contains none: the path must traverse it between those bridges.
3. **Articulation structure.** An articulation cannot be an endpoint. A leaf
   biconnected block contains an endpoint away from its articulation.
4. **Signed room balance.** Give black cells sign +1 and white cells sign −1.
   Sum their signed degree equations over any region S. Internal edges
   cancel, leaving

       sum(sign(v) x_vu for v in S, u outside S)
         + sum(sign(v) e_v for v in S) = 2 (black(S) − white(S)).

   This constrains port choices jointly with endpoint placement. It applies
   to a geometric core too: it does not assume that the region is traversed
   in a single visit.

JSON includes every equality, every assignment, its rule, and the derivation
depth of the queue run. This depth is a reproducible proof statistic of this
implementation, not a demonstrated minimum proof depth. `forced_path_fraction`
is the number of selected edges divided by N−1; `edge_decided_fraction`
includes excluded edges and divides by all candidate adjacencies.

Residual components include remaining endpoint and region constraints. Two
geographically separate undecided areas are not called independent while a
shared endpoint or boundary equation still couples them.

These are necessary conditions for a covering grid path. Coil also requires
sliding to a blocker before turning. When deductions fix a spanning path,
v1 replays both orientations under the actual slide rules and emits only
validated solution certificates. Otherwise the result is `unresolved`.
It never reports an ordinary Hamiltonian path as a solved Coil board.

## Room signatures: the next substantial step

A useful room summary is a relation over its interface, not just area or a
cached solution for one start. Its signature needs:

- boundary crossing edges and their pairing;
- any endpoint inside the room;
- entry and exit directions;
- which outside cells must already have been visited to act as stoppers;
- which internal cells are covered by each traversal piece;
- allowed temporal order between crossings.

Two-port does not automatically mean one simple visit when a region is
selected geometrically. A signature must allow several disjoint path pieces
inside a region when the interface permits that. Likewise, treating the
whole outside of a room as wall can certify illegal Coil turns.

Start with proved families—straight corridors, bends, rectangles with
specified ports, and narrow strips. Derive their allowed signatures
algebraically or by a bounded-width transfer calculation. Canonicalize each
shape and its ports under rotations/reflections, compose compatible
signatures, and retain a proof/certificate of every elimination. If local
states are enumerated, expose that cost as *local state work*, separately
from deductions; do not rename enumeration "simple analysis."

That gives meaningful advanced parameters:

- fraction of cells discharged by known room rules;
- interface alternatives before and after composition;
- smallest rule family or strip width needed to discharge each room;
- dependency depth between rooms;
- size and separator width of the remaining coupled region;
- number of distinct room proofs versus repeated applications of one proof.

Do not assign a single difficulty weight to these yet. Calibrate them on
boards with explicit analyses of why their solutions follow.

## First measurements and validation

On the 12 saved hard levels, v1 proves some edges on five boards and leaves
all twelve unresolved. For example:

| Board | Open fraction | Bridges | Edges proved / N−1 |
|---|---:|---:|---:|
| 19x19-rank01-seed62 | 0.7285 | 0 | 94 / 262 |
| 19x19-rank02-seed141 | 0.7258 | 0 | 0 / 261 |

The first board has 263 open cells, so checkerboard imbalance rules out
endpoints on the minority color and forces some degree-two connections.
The second has 262 open cells and this rule set establishes no particular
endpoint or edge. This exposes a limitation of the current rules as well as
a structural distinction; it is not a measured difficulty ordering.

Tests cover all 511 nonempty 3x3 boards against **every** legal slide solution
from an independent exhaustive test oracle. Every selected edge, excluded
edge, and endpoint restriction must hold in every solution. Production
analysis never calls that oracle. Further tests cover all 3x2 bridge/cut
structures, rotation invariance, square counts, chamber definitions, a
corridor solved with certificates, and all 12 saved solutions.

For future parameter selection, use controlled pairs: same occupancy with
one large wall square versus fragmented walls; same room shapes with ports
moved; same motif histogram with motifs rearranged; same board reflected or
rotated; a narrow neck widened by one cell. Confirm the intended metric
changes and the unrelated metrics remain stable before fitting generator
controls. Solver runtime is not the target variable.

## Mathematical references and boundaries

- [Itai, Papadimitriou and Szwarcfiter, Hamilton Paths in Grid Graphs (1982)](https://csaws.cs.technion.ac.il/~itai/publications/Algorithms/Hamilton-paths.pdf)
  supplies the grid-graph and bipartite setting and results for rectangular
  grids. Its general-grid complexity result does not establish the complexity
  of Coil's additional slide rule without a separate reduction.
- [Tarjan, Depth-First Search and Linear Graph Algorithms (1972)](https://epubs.siam.org/doi/10.1137/0201010)
  gives linear-time graph decomposition. The traversal used to find cuts and
  blocks is distinct from enumerating puzzle solutions.
- [scikit-image morphology documentation](https://scikit-image.org/docs/stable/api/skimage.morphology)
  describes the distance-transform and morphology vocabulary. This prototype
  implements square/Chebyshev clearance itself and does not depend on that
  package. Its chamber cores should not be interpreted as complete rooms.

## Large-board geometry and style survey (2026-09-06)

[The collection](gallery/README.md) applies a compact C# geometry pass to a
330-board controlled survey and a selected scale series through 10000 square.
[Measured findings](gallery/FINDINGS.md) retain within-recipe seed ranges and
matched-seed method changes. [The HTML atlas](web/gallery-guide.html) exposes
scatter comparisons, distributions, fixed-scale crops, and same-recipe seeds.

This is a geometry implementation, not a port of the room/proof engine. Its
32-cell nonoverlapping tile variance is intentionally distinct from v1's
overlapping small-window variance. The full square spectra, run histograms,
and edge-layer profiles remain available in each specimen's JSON.
