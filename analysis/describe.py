"""Board-only style and proof descriptors. Standard library; no path search.

Run: python3 analysis/describe.py levels/hard/*.board --output output/style.json
All coordinates and edge endpoints in JSON are row-major cell indexes.
"""
from __future__ import annotations

import argparse
from collections import Counter, deque
import json
import math
from pathlib import Path
import statistics


class Board:
    def __init__(self, width: int, height: int, cells: str):
        if width < 1 or height < 1 or len(cells) != width * height:
            raise ValueError("positive dimensions and exactly width*height cells required")
        if set(cells) - {".", "X"}:
            raise ValueError("board cells must be '.' or 'X'")
        if "." not in cells:
            raise ValueError("board must contain at least one open cell")
        self.w, self.h, self.cells = width, height, cells
        self.open = [i for i, c in enumerate(cells) if c == "."]
        self.adj = {v: [] for v in self.open}
        self.edges = []
        for v in self.open:
            for u in self.neighbors(v):
                if u > v and cells[u] == ".":
                    e = len(self.edges)
                    self.edges.append((v, u))
                    self.adj[v].append((u, e))
                    self.adj[u].append((v, e))

    @classmethod
    def parse(cls, text: str):
        fields = dict(part.split("=", 1) for part in text.strip().split("&"))
        return cls(int(fields["x"]), int(fields["y"]), fields["board"])

    def neighbors(self, v):
        x, y = v % self.w, v // self.w
        if x > 0:
            yield v - 1
        if x + 1 < self.w:
            yield v + 1
        if y > 0:
            yield v - self.w
        if y + 1 < self.h:
            yield v + self.w

    def text(self):
        return f"x={self.w}&y={self.h}&board={self.cells}"


def distribution(values):
    counts = Counter(values)
    ordered = sorted(values)
    return {"count": len(values), "mean": statistics.mean(values) if values else None,
            "median": statistics.median(values) if values else None,
            "max": ordered[-1] if values else None,
            "histogram": {str(k): v for k, v in sorted(counts.items())}}


def components(board, vertices, blocked_edges=frozenset()):
    unseen = set(vertices)
    groups = []
    for root in sorted(vertices):
        if root not in unseen:
            continue
        unseen.remove(root)
        group, queue = [], [root]
        for v in queue:
            group.append(v)
            for u in board.neighbors(v):
                if u in unseen:
                    if blocked_edges and (min(v, u), max(v, u)) in blocked_edges:
                        continue
                    unseen.remove(u)
                    queue.append(u)
        groups.append(group)
    return groups


def squares(board, target):
    # DP value = largest all-target square ending at this cell. This counts
    # every k-square exactly once, without enumerating all rectangles.
    prev = [0] * (board.w + 1)
    histogram, largest, witness = Counter(), 0, None
    for y in range(board.h):
        row = [0] * (board.w + 1)
        for x in range(board.w):
            if board.cells[y * board.w + x] == target:
                row[x + 1] = 1 + min(row[x], prev[x], prev[x + 1])
                histogram[row[x + 1]] += 1
                if row[x + 1] > largest:
                    largest = row[x + 1]
                    witness = [x - largest + 1, y - largest + 1, largest]
        prev = row
    cumulative = 0
    counts = {}
    for size in range(largest, 0, -1):
        cumulative += histogram[size]
        counts[str(size)] = cumulative
    return {"largest_side": largest, "largest_witness_xy_side": witness,
            "placement_count_by_side": counts,
            "placement_fraction_by_side": {
                k: n / ((board.w - int(k) + 1) * (board.h - int(k) + 1))
                for k, n in counts.items()}}


def clearance(board):
    # Chebyshev distance to wall or exterior: d>=r means a full open
    # (2*r-1)-square centered here. Exterior is a wall, not a wrapped edge.
    n = len(board.cells)
    d = [0] * n
    for v in board.open:
        x, y = v % board.w, v // board.w
        d[v] = min(x + 1, y + 1, board.w - x, board.h - y)
    for y in range(board.h):
        for x in range(board.w):
            v = y * board.w + x
            if d[v]:
                for dx, dy in ((-1, 0), (-1, -1), (0, -1), (1, -1)):
                    xx, yy = x + dx, y + dy
                    if 0 <= xx < board.w and 0 <= yy < board.h:
                        d[v] = min(d[v], d[yy * board.w + xx] + 1)
    for y in range(board.h - 1, -1, -1):
        for x in range(board.w - 1, -1, -1):
            v = y * board.w + x
            if d[v]:
                for dx, dy in ((1, 0), (1, 1), (0, 1), (-1, 1)):
                    xx, yy = x + dx, y + dy
                    if 0 <= xx < board.w and 0 <= yy < board.h:
                        d[v] = min(d[v], d[yy * board.w + xx] + 1)
    return d


def geometry(board):
    n, area = len(board.open), len(board.cells)
    horizontal, vertical = [], []
    for horizontal_scan, runs in ((True, horizontal), (False, vertical)):
        outer, inner = (board.h, board.w) if horizontal_scan else (board.w, board.h)
        for a in range(outer):
            run = 0
            for b in range(inner + 1):
                v = a * board.w + b if horizontal_scan else b * board.w + a
                if b < inner and board.cells[v] == ".":
                    run += 1
                elif run:
                    runs.append(run)
                    run = 0
    walls = [v for v, c in enumerate(board.cells) if c == "X"]
    wall_groups = components(board, walls)
    wall_shapes = []
    for group in wall_groups:
        xs, ys = [v % board.w for v in group], [v // board.w for v in group]
        width, height = max(xs) - min(xs) + 1, max(ys) - min(ys) + 1
        wall_shapes.append({"area": len(group), "bbox_xywh": [min(xs), min(ys), width, height],
                            "bbox_fill": len(group) / (width * height),
                            "bbox_squareness": min(width, height) / max(width, height)})
    layers = {}
    for v, cell in enumerate(board.cells):
        x, y = v % board.w, v // board.w
        layer = min(x, y, board.w - 1 - x, board.h - 1 - y)
        counts = layers.setdefault(layer, [0, 0])
        counts[0] += cell == "."
        counts[1] += 1
    open_distances = [min(v % board.w, v // board.w, board.w - 1 - v % board.w,
                          board.h - 1 - v // board.w) for v in board.open]
    distances = clearance(board)
    cores = []
    for r in (2, 3, 4):
        groups = components(board, [v for v in board.open if distances[v] >= r])
        cores.append({"radius": r, "required_square_side": 2 * r - 1,
                      "count_per_1000_open": len(groups) * 1000 / n,
                      "core_area": distribution([len(g) for g in groups]), "cells": groups})
    h_edges = sum(v // board.w == u // board.w for v, u in board.edges)
    return {"open_fraction": n / area, "open_cells": n, "wall_cells": area - n,
            "interface_sides_per_open": (4 * n - 2 * len(board.edges)) / n,
            "horizontal_open_runs": distribution(horizontal), "vertical_open_runs": distribution(vertical),
            "axis_bias": (2 * h_edges - len(board.edges)) / len(board.edges) if board.edges else None,
            "open_squares": squares(board, "."), "wall_squares": squares(board, "X"),
            "wall_components": wall_shapes,
            "edge_layers": [{"distance": d, "open_fraction": a / total, "cells": total}
                            for d, (a, total) in sorted(layers.items())],
            "mean_edge_distance_open": statistics.mean(open_distances),
            "square_clearance": distribution([distances[v] for v in board.open]),
            "chamber_cores": cores}


def topology(board):
    # Iterative low-link traversal: each edge is inspected O(1) times.
    # This is graph decomposition, not a search over puzzle solutions.
    tin, low, parent, parent_edge, children = {}, {}, {}, {}, Counter()
    bridges, cuts, blocks, edge_stack = [], set(), [], []
    clock = 0
    component_count = 0
    for root in board.open:
        if root in tin:
            continue
        component_count += 1
        parent[root] = None
        tin[root] = low[root] = clock
        clock += 1
        stack = [(root, iter(board.adj[root]))]
        while stack:
            v, iterator = stack[-1]
            step = next(iterator, None)
            if step is None:
                stack.pop()
                p = parent[v]
                if p is None:
                    if children[v] > 1:
                        cuts.add(v)
                    if not board.adj[v]:
                        blocks.append([v])
                else:
                    low[p] = min(low[p], low[v])
                    if low[v] > tin[p]:
                        bridges.append(parent_edge[v])
                    if low[v] >= tin[p]:
                        if parent[p] is not None:
                            cuts.add(p)
                        block = set()
                        while True:
                            e = edge_stack.pop()
                            block.update(board.edges[e])
                            if e == parent_edge[v]:
                                break
                        blocks.append(sorted(block))
                continue
            u, e = step
            if e == parent_edge.get(v):
                continue
            if u not in tin:
                children[v] += 1
                parent[u], parent_edge[u] = v, e
                edge_stack.append(e)
                tin[u] = low[u] = clock
                clock += 1
                stack.append((u, iter(board.adj[u])))
            elif tin[u] < tin[v]:
                low[v] = min(low[v], tin[u])
                edge_stack.append(e)
    bridge_set = {board.edges[e] for e in bridges}
    regions = components(board, board.open, bridge_set)
    region_of = {v: r for r, cells in enumerate(regions) for v in cells}
    gates = [[] for _ in regions]
    for e in bridges:
        v, u = board.edges[e]
        gates[region_of[v]].append(e)
        gates[region_of[u]].append(e)
    rooms = []
    for r, cells in enumerate(regions):
        gate_count = len(gates[r])
        kind = "unseparated" if not gate_count else "terminal" if gate_count == 1 else "transit" if gate_count == 2 else "branched"
        rooms.append({"id": r, "cells": cells, "area": len(cells), "gate_edges": gates[r],
                      "kind": kind, "checkerboard_imbalance": sum(1 if (v % board.w + v // board.w) % 2 == 0 else -1 for v in cells)})
    return {"components": component_count, "cycle_rank": len(board.edges) - len(board.open) + component_count,
            "degree_histogram": dict(sorted(Counter(len(board.adj[v]) for v in board.open).items())),
            "bridge_edges": sorted(bridges), "articulation_cells": sorted(cuts),
            "biconnected_blocks": blocks, "block_area": distribution([len(b) for b in blocks]),
            "bridge_rooms": rooms, "bridge_room_area": distribution([len(r) for r in regions]),
            "bridge_rooms_per_1000_open": len(regions) * 1000 / len(board.open)}


def patterns(board):
    def agreement(pairs):
        pairs = [(a, b) for a, b in pairs if a != b]
        if not pairs:
            return {"pairs": 0, "agreement": None, "excess_agreement": None}
        p = sum(board.cells[a] == "." for a, _ in pairs) / len(pairs)
        q = sum(board.cells[b] == "." for _, b in pairs) / len(pairs)
        observed = sum(board.cells[a] == board.cells[b] for a, b in pairs) / len(pairs)
        expected = p * q + (1 - p) * (1 - q)
        return {"pairs": len(pairs), "agreement": observed,
                "excess_agreement": (observed - expected) / (1 - expected) if expected < 1 else None}
    transforms = {"left_right": lambda x, y: (board.w - 1 - x, y),
                  "top_bottom": lambda x, y: (x, board.h - 1 - y),
                  "half_turn": lambda x, y: (board.w - 1 - x, board.h - 1 - y)}
    if board.w == board.h:
        transforms.update({"quarter_turn": lambda x, y: (board.w - 1 - y, x),
                           "main_diagonal": lambda x, y: (y, x),
                           "anti_diagonal": lambda x, y: (board.w - 1 - y, board.h - 1 - x)})
    symmetries = {}
    for name, transform in transforms.items():
        pairs = []
        for v in range(len(board.cells)):
            x, y = transform(v % board.w, v // board.w)
            pairs.append((v, y * board.w + x))
        symmetries[name] = agreement(pairs)
    translations = []
    for dx, dy in [(d, 0) for d in (1, 2, 3, 4, 8)] + [(0, d) for d in (1, 2, 3, 4, 8)]:
        pairs = [(y * board.w + x, (y + dy) * board.w + x + dx)
                 for y in range(board.h - dy) for x in range(board.w - dx)]
        translations.append({"dx": dx, "dy": dy, **agreement(pairs)})
    patches = []
    for k in (3, 5):
        counts, canonical_counts = Counter(), Counter()
        symmetric, nonuniform, windows = 0, 0, 0
        densities = []
        for y in range(board.h - k + 1):
            for x in range(board.w - k + 1):
                bits = tuple(board.cells[(y + j) * board.w + x + i] == "." for j in range(k) for i in range(k))
                windows += 1
                densities.append(sum(bits) / (k * k))
                if all(bits) or not any(bits):
                    continue
                nonuniform += 1
                variants = []
                current = bits
                for _ in range(4):
                    variants.append(current)
                    variants.append(tuple(current[j * k + k - 1 - i] for j in range(k) for i in range(k)))
                    current = tuple(current[(k - 1 - i) * k + j] for j in range(k) for i in range(k))
                symmetric += len(set(variants)) < 8
                encode = lambda b: sum(int(value) << i for i, value in enumerate(b))
                counts[encode(bits)] += 1
                canonical_counts[min(map(encode, variants))] += 1
        def entropy(counter):
            return -sum((n / nonuniform) * math.log2(n / nonuniform) for n in counter.values()) if nonuniform else None
        patches.append({"side": k, "windows": windows, "nonuniform_windows": nonuniform,
                        "homogeneous_fraction": 1 - nonuniform / windows if windows else None,
                        "density_variance": statistics.pvariance(densities) if densities else None,
                        "local_exact_symmetry_fraction": symmetric / nonuniform if nonuniform else None,
                        "oriented_entropy_bits": entropy(counts), "unoriented_entropy_bits": entropy(canonical_counts),
                        "repeat_pair_fraction": sum(n * (n - 1) for n in canonical_counts.values()) / (nonuniform * (nonuniform - 1)) if nonuniform > 1 else None,
                        "top_unoriented_motifs": [{"bits": bits, "count": count} for bits, count in canonical_counts.most_common(8)]})
    return {"global_symmetry": symmetries, "translation_agreement": translations, "patches": patches}


def replay(board, solution):
    fields = dict(part.split("=", 1) for part in solution.strip().split("&"))
    x, y = int(fields["x"]), int(fields["y"])
    if not (0 <= x < board.w and 0 <= y < board.h) or board.cells[y * board.w + x] != ".":
        raise ValueError("solution starts outside open cells")
    visited, traversed = {y * board.w + x}, set()
    directions = {"U": (0, -1), "R": (1, 0), "D": (0, 1), "L": (-1, 0)}
    for letter in fields["path"]:
        dx, dy = directions[letter]
        steps = 0
        while 0 <= x + dx < board.w and 0 <= y + dy < board.h:
            v, u = y * board.w + x, (y + dy) * board.w + x + dx
            if board.cells[u] == "X" or u in visited:
                break
            traversed.add((min(v, u), max(v, u)))
            visited.add(u)
            x, y = x + dx, y + dy
            steps += 1
        if not steps:
            raise ValueError("solution contains an immediately blocked move")
    if len(visited) != len(board.open):
        raise ValueError(f"solution covers {len(visited)} of {len(board.open)} cells")
    return traversed, {int(fields["y"]) * board.w + int(fields["x"]), y * board.w + x}


def forced_path_solutions(board, selected):
    if len(board.open) == 1:
        v = board.open[0]
        return [f"x={v % board.w}&y={v // board.w}&path="]
    adj = {v: [] for v in board.open}
    for e in selected:
        v, u = board.edges[e]
        adj[v].append(u)
        adj[u].append(v)
    ends = [v for v in board.open if len(adj[v]) == 1]
    if len(selected) != len(board.open) - 1 or len(ends) != 2 or any(len(a) not in (1, 2) for a in adj.values()):
        return None
    solutions = []
    for start in ends:
        route, seen, prev, v = [], {start}, None, start
        while True:
            following = [u for u in adj[v] if u != prev]
            if not following:
                break
            u = following[0]
            if u in seen:
                return None
            seen.add(u)
            dx, dy = u % board.w - v % board.w, u // board.w - v // board.w
            letter = {(0, -1): "U", (1, 0): "R", (0, 1): "D", (-1, 0): "L"}[dx, dy]
            if not route or route[-1] != letter:
                route.append(letter)
            prev, v = v, u
        if len(seen) != len(board.open):
            return None
        solution = f"x={start % board.w}&y={start // board.w}&path={''.join(route)}"
        try:
            replay(board, solution)
        except ValueError:
            # Both orientations of an already-proved path are checked; no
            # alternate paths or starts are searched.
            continue
        solutions.append(solution)
    return solutions


def deductions(board, topo, geom):
    n, m = len(board.open), len(board.edges)
    if n == 1:
        return {"status": "solved_by_rules", "forced_edges": [], "excluded_edges": [],
                "unknown_edges": [], "endpoint_cells": board.open, "endpoint_candidates": board.open,
                "edge_decided_fraction": 1.0, "forced_path_fraction": 1.0,
                "residual_components": [], "proof": [], "max_derivation_depth": 0,
                "certified_solutions": forced_path_solutions(board, []), "contradiction": None}
    endpoints = {v: m + i for i, v in enumerate(board.open)}
    values = [-1] * (m + n)
    depths = [0] * len(values)
    reasons = [None] * len(values)
    watches = [[] for _ in values]
    constraints, lower, upper, constraint_depth = [], [], [], []
    queue, queued = deque(), set()
    proof = []
    contradiction = None

    def add_constraint(terms, target, name):
        idx = len(constraints)
        constraints.append((terms, target, name))
        lower.append(sum(min(0, coefficient) for _, coefficient in terms))
        upper.append(sum(max(0, coefficient) for _, coefficient in terms))
        constraint_depth.append(0)
        for var, coefficient in terms:
            watches[var].append((idx, coefficient))
        queue.append(idx)
        queued.add(idx)

    def assign(var, value, rule, depth, constraint=None):
        nonlocal contradiction
        if values[var] != -1:
            if values[var] != value:
                contradiction = f"conflicting assignment: {rule}"
            return
        values[var], depths[var], reasons[var] = value, depth, rule
        proof.append({"variable": var, "value": value, "rule": rule, "depth": depth,
                      "constraint": constraint})
        for idx, coefficient in watches[var]:
            lower[idx] += coefficient * value - min(0, coefficient)
            upper[idx] += coefficient * value - max(0, coefficient)
            constraint_depth[idx] = max(constraint_depth[idx], depth)
            if idx not in queued:
                queue.append(idx)
                queued.add(idx)

    if topo["components"] != 1:
        contradiction = "open graph is disconnected"
    colors = [[v for v in board.open if (v % board.w + v // board.w) % 2 == c] for c in (0, 1)]
    imbalance = len(colors[0]) - len(colors[1])
    if abs(imbalance) > 1:
        contradiction = "checkerboard cell counts differ by more than one"
    for v in board.open:
        add_constraint([(e, 1) for _, e in board.adj[v]] + [(endpoints[v], 1)], 2, f"degree at cell {v}")
    add_constraint([(endpoints[v], 1) for v in board.open], 2, "exactly two endpoints")
    for c in (0, 1):
        target = 1 if imbalance == 0 else 2 if (imbalance > 0) == (c == 0) else 0
        add_constraint([(endpoints[v], 1) for v in colors[c]], target, f"checkerboard endpoint color {c}")
    for room in topo["bridge_rooms"]:
        if room["kind"] in ("terminal", "transit"):
            target = 1 if room["kind"] == "terminal" else 0
            add_constraint([(endpoints[v], 1) for v in room["cells"]], target, f"{room['kind']} bridge room {room['id']}")
    cuts = set(topo["articulation_cells"])
    for idx, block in enumerate(topo["biconnected_blocks"]):
        gate_cells = set(block) & cuts
        if len(gate_cells) == 1:
            add_constraint([(endpoints[v], 1) for v in block if v not in cuts], 1, f"leaf block {idx} contains one endpoint")
    # Signed degree sums over a region cancel every internal edge. The
    # remaining equation constrains its boundary and endpoints together.
    regions = [(f"bridge room {r['id']}", r["cells"]) for r in topo["bridge_rooms"]]
    regions += [(f"chamber radius {core['radius']} region {i}", cells)
                for core in geom["chamber_cores"] for i, cells in enumerate(core["cells"])]
    for name, cells in regions:
        members = set(cells)
        terms, delta = [], 0
        for v in cells:
            sign = 1 if (v % board.w + v // board.w) % 2 == 0 else -1
            delta += sign
            terms.append((endpoints[v], sign))
            terms.extend((e, sign) for u, e in board.adj[v] if u not in members)
        add_constraint(terms, 2 * delta, f"signed boundary balance: {name}")
    if not contradiction:
        for e in topo["bridge_edges"]:
            assign(e, 1, "bridge must be crossed", 0)
        for v in cuts:
            assign(endpoints[v], 0, "articulation cannot be an endpoint", 0)
    while queue and not contradiction:
        idx = queue.popleft()
        queued.remove(idx)
        terms, target, name = constraints[idx]
        lo, hi = lower[idx], upper[idx]
        if target < lo or target > hi:
            contradiction = f"{name}: target {target} outside [{lo}, {hi}]"
            break
        if target not in (lo, hi):
            continue
        depth = constraint_depth[idx] + 1
        for var, coefficient in terms:
            if values[var] == -1:
                value = int(coefficient > 0) if target == hi else int(coefficient < 0)
                assign(var, value, name, depth, idx)
    selected = [e for e in range(m) if values[e] == 1]
    excluded = [e for e in range(m) if values[e] == 0]
    unknown = [e for e in range(m) if values[e] == -1]
    residual_vertices = set(v for e in unknown for v in board.edges[e])
    # Residual components include logical coupling through endpoint/region
    # constraints, not just spatial adjacency of undecided edges.
    unseen_vars = set(var for var, value in enumerate(values) if value == -1)
    residual = []
    while unseen_vars:
        root = min(unseen_vars)
        unseen_vars.remove(root)
        group, pending, seen_constraints = [], [root], set()
        for var in pending:
            group.append(var)
            for idx, _ in watches[var]:
                if idx in seen_constraints:
                    continue
                seen_constraints.add(idx)
                for other, _ in constraints[idx][0]:
                    if other in unseen_vars:
                        unseen_vars.remove(other)
                        pending.append(other)
        residual.append({"variables": len(group), "edge_variables": sum(v < m for v in group),
                         "endpoint_variables": sum(v >= m for v in group)})
    certificates = forced_path_solutions(board, selected) if not contradiction else None
    status = "contradiction" if contradiction else "unresolved"
    if certificates is not None:
        if certificates:
            status = "solved_by_rules"
        else:
            status = "contradiction"
            contradiction = "forced spanning path violates slide rules in both orientations"
    return {"status": status, "forced_edges": selected, "excluded_edges": excluded, "unknown_edges": unknown,
            "endpoint_cells": [v for v in board.open if values[endpoints[v]] == 1],
            "endpoint_candidates": [v for v in board.open if values[endpoints[v]] != 0],
            "edge_decided_fraction": (len(selected) + len(excluded)) / m if m else None,
            "forced_path_fraction": len(selected) / (n - 1),
            "residual_cell_fraction": len(residual_vertices) / n,
            "residual_components": residual, "max_derivation_depth": max(depths),
            "rule_assignment_counts": dict(Counter(p["rule"].split(":")[0].split(" at ")[0] for p in proof)),
            "proof": proof, "constraints": [{"terms": terms, "target": target, "rule": name} for terms, target, name in constraints],
            "endpoint_variable_cells": board.open, "certified_solutions": certificates or [],
            "contradiction": contradiction}


def describe(board, name="board"):
    geom = geometry(board)
    topo = topology(board)
    return {"schema_version": 1, "name": name, "width": board.w, "height": board.h,
            "board": board.cells, "edges": board.edges,
            "geometry": geom, "patterns": patterns(board), "topology": topo,
            "deductions": deductions(board, topo, geom)}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("boards", nargs="+", type=Path)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--max-cells", type=int, default=100_000,
                        help="fail above this prototype memory bound; default 100000")
    args = parser.parse_args()
    reports = []
    for path in args.boards:
        # Read dimensions and enforce the memory bound before graph allocation.
        fields = dict(part.split("=", 1) for part in path.read_text().strip().split("&"))
        area = int(fields["x"]) * int(fields["y"])
        if area > args.max_cells:
            raise ValueError(f"{path}: {area} cells exceeds --max-cells {args.max_cells}")
        board = Board(int(fields["x"]), int(fields["y"]), fields["board"])
        report = describe(board, path.stem)
        reports.append(report)
        d = report["deductions"]
        print(f"{path.name}: open={len(board.open)/area:.3f} bridges={len(report['topology']['bridge_edges'])} "
              f"forced={len(d['forced_edges'])}/{len(board.open)-1} {d['status']}")
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(reports, separators=(",", ":"), allow_nan=False) + "\n")


if __name__ == "__main__":
    main()
