"""Exact small examples and exhaustive soundness checks for deductions."""
from pathlib import Path
import unittest

from describe import Board, describe, geometry, patterns, replay, topology


def tiny_solutions(board):
    # Independent exhaustive oracle used ONLY in tests, never for descriptors.
    # Enumerate legal slide paths, keeping their adjacency edges and endpoints.
    result = []

    def visit(start, head, visited, edges):
        if len(visited) == len(board.open):
            result.append((edges, {start, head}))
            return
        for dx, dy in ((0, -1), (1, 0), (0, 1), (-1, 0)):
            x, y = head % board.w, head // board.w
            following, made, p = [], set(), head
            while 0 <= x + dx < board.w and 0 <= y + dy < board.h:
                x, y = x + dx, y + dy
                q = y * board.w + x
                if q in visited or board.cells[q] == "X":
                    break
                following.append(q)
                made.add((min(p, q), max(p, q)))
                p = q
            if following:
                visit(start, following[-1], visited | set(following), edges | made)

    for start in board.open:
        visit(start, start, {start}, set())
    return result


class Descriptors(unittest.TestCase):
    def test_corridor_certificate(self):
        board = Board(7, 5, "......." "XXXXXX." "......." ".XXXXXX" ".......")
        report = describe(board)
        self.assertEqual(report["deductions"]["status"], "solved_by_rules")
        self.assertEqual(len(report["deductions"]["forced_edges"]), len(board.open) - 1)
        self.assertEqual(report["geometry"]["chamber_cores"][0]["core_area"]["count"], 0)
        for solution in report["deductions"]["certified_solutions"]:
            replay(board, solution)

    def test_room_definitions_are_distinct(self):
        board = Board(7, 3, "...X..." "......." "...X...")
        topo, geom = topology(board), geometry(board)
        self.assertEqual(sorted((r["kind"], r["area"]) for r in topo["bridge_rooms"]),
                         [("terminal", 9), ("terminal", 9), ("transit", 1)])
        self.assertEqual(len(topo["bridge_edges"]), 2)
        self.assertEqual(geom["chamber_cores"][0]["core_area"]["count"], 2)

    def test_square_counts_and_no_fake_symmetry(self):
        report = describe(Board(5, 5, "." * 25))
        self.assertEqual(report["geometry"]["open_squares"]["placement_count_by_side"],
                         {"1": 25, "2": 16, "3": 9, "4": 4, "5": 1})
        self.assertEqual(report["geometry"]["chamber_cores"][0]["core_area"]["max"], 9)
        self.assertEqual(report["geometry"]["wall_squares"]["largest_side"], 0)
        self.assertIsNone(report["patterns"]["patches"][0]["local_exact_symmetry_fraction"])
        self.assertIsNone(report["patterns"]["global_symmetry"]["left_right"]["excess_agreement"])

    def test_rotation_preserves_unoriented_features(self):
        board = Board(5, 5, "..X.." "X...." "..X.X" "....." ".XX..")
        rotated = Board(5, 5, "".join(board.cells[(4 - x) * 5 + y] for y in range(5) for x in range(5)))
        a, b = patterns(board), patterns(rotated)
        for p, q in zip(a["patches"], b["patches"]):
            for key in ("repeat_pair_fraction", "unoriented_entropy_bits", "local_exact_symmetry_fraction", "density_variance"):
                self.assertAlmostEqual(p[key], q[key])
        self.assertEqual(geometry(board)["open_squares"]["placement_count_by_side"],
                         geometry(rotated)["open_squares"]["placement_count_by_side"])

    def test_every_3x3_board_against_all_slide_solutions(self):
        solvable = 0
        for mask in range(1, 1 << 9):
            board = Board(3, 3, "".join("." if mask & (1 << v) else "X" for v in range(9)))
            solutions = tiny_solutions(board)
            report = describe(board)
            deduction = report["deductions"]
            if solutions:
                solvable += 1
                self.assertNotEqual(deduction["status"], "contradiction", board.text())
                for edges, ends in solutions:
                    self.assertTrue({board.edges[e] for e in deduction["forced_edges"]} <= edges, board.text())
                    self.assertFalse({board.edges[e] for e in deduction["excluded_edges"]} & edges, board.text())
                    self.assertTrue(set(deduction["endpoint_cells"]) <= ends, board.text())
                    self.assertTrue(ends <= set(deduction["endpoint_candidates"]), board.text())
            for certificate in deduction["certified_solutions"]:
                replay(board, certificate)
        self.assertGreater(solvable, 100)

    def test_saved_solutions_obey_all_deductions(self):
        root = Path(__file__).resolve().parents[1]
        paths = sorted((root / "levels/hard").glob("*.board"))
        self.assertEqual(len(paths), 12)
        for path in paths:
            board = Board.parse(path.read_text())
            selected, endpoints = replay(board, path.with_suffix(".solution").read_text())
            deduction = describe(board)["deductions"]
            self.assertNotEqual(deduction["status"], "contradiction", path.name)
            self.assertTrue({board.edges[e] for e in deduction["forced_edges"]} <= selected, path.name)
            self.assertFalse({board.edges[e] for e in deduction["excluded_edges"]} & selected, path.name)
            self.assertTrue(set(deduction["endpoint_cells"]) <= endpoints, path.name)

    def test_connected_components_and_articulations(self):
        for mask in range(1, 1 << 6):
            board = Board(3, 2, "".join("." if mask & (1 << v) else "X" for v in range(6)))
            topo = topology(board)

            def count(removed_vertices=set(), removed_edges=set()):
                unseen, result = set(board.open) - removed_vertices, 0
                while unseen:
                    pending = [unseen.pop()]
                    result += 1
                    for v in pending:
                        for u, e in board.adj[v]:
                            if u in unseen and e not in removed_edges:
                                unseen.remove(u)
                                pending.append(u)
                return result

            base = count()
            self.assertEqual(topo["components"], base)
            self.assertEqual(set(topo["articulation_cells"]), {v for v in board.open if count({v}) > base})
            self.assertEqual(set(topo["bridge_edges"]), {e for e in range(len(board.edges)) if count(removed_edges={e}) > base})


if __name__ == "__main__":
    unittest.main()
