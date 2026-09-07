"""Contract and real-process regressions; requires a Release build, no API keys."""
from dataclasses import asdict
import gzip
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest

from meta_solver.coil_bridge import (BoardTooLarge, BridgeError, CoilBoard, CoilBridge, SearchConfig, load_board, replay)
from meta_solver.core import Comparator, Difficulty, EvalResult, Idea
from meta_solver.solver import MetaSolver, MetaSolverConfig
from meta_solver.examples.coil_integration import CoilProblem, CoilSolution
from meta_solver.coil_meta_solver import (MultiTierEvaluator, EvaluationTier, CoilMetaSolver, Idea as CoilIdea,
                                         ImprovementCategory, IdeaMemory, SolutionEnsemble, SolverVariant)


class BridgeTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name)
        self.bridge = CoilBridge(timeout=2)
        (self.root / 'tiny.board').write_text('x=2&y=2&board=....')

    def tearDown(self):
        self.temp.cleanup()

    def fake(self, source):
        path = self.root / 'fake-solver'
        path.write_text(f'#!{sys.executable}\n' + source)
        path.chmod(0o700)
        return path

    def test_formats_and_replay(self):
        text = '2x2 - fixture\n..\n.X\n'
        path = self.root / 'grid.coil'
        path.write_text(text)
        board = load_board(path)
        self.assertEqual(board.text, 'x=2&y=2&board=...X')
        with gzip.open(self.root / 'grid.board.gz', 'wt') as f:
            f.write(board.text)
        self.assertEqual(load_board(self.root / 'grid.board.gz'), board)
        replay(board, 'x=1&y=0&path=LD')
        for answer in ('x=1&y=1&path=', 'x=1&y=0&path=R', 'x=0&y=0&path=R', 'x=1&y=0&path=LZ'):
            with self.assertRaises(ValueError): replay(board, answer)

    def test_invalid_boards(self):
        for text in ('x=0&y=2&board=', 'x=1&x=2&y=1&board=..', 'x=2&y=1&board=.Q', '2x2\n..\n.', '2x2\n..\n..\n..'):
            with self.assertRaises(ValueError): CoilBoard.parse(text)
        with self.assertRaises(BoardTooLarge): CoilBoard.parse('x=2&y=2&board=....', 3)

    def test_real_success_unsolvable_and_limits(self):
        board = CoilBoard.parse('x=2&y=2&board=....')
        for cfg in (SearchConfig(), SearchConfig('DRUL', 'reverse', False)):
            result = self.bridge.evaluate(board, cfg)
            self.assertEqual(result['status'], 'solved')
            self.assertTrue(result['validated'])
            self.assertEqual(result['coverage'], 1)
            self.assertGreater(result['wallSeconds'], 0)
        self.assertEqual(self.bridge.evaluate(CoilBoard.parse('x=3&y=1&board=.X.'), SearchConfig())['status'], 'unsolvable')
        for budget in (0, 1):
            result = CoilBridge(node_budget=budget).evaluate(board, SearchConfig())
            self.assertEqual(result['status'], 'node_budget')
            self.assertLessEqual(result['nodes'], budget)
        self.assertEqual(CoilBridge(depth_limit=1).evaluate(board, SearchConfig())['status'], 'depth_limit')
        self.assertEqual(self.bridge.evaluate(CoilBoard.parse('x=3&y=3&board=XXXX.XXXX'), SearchConfig())['status'], 'solved')

    def test_real_time_limit(self):
        from meta_solver.coil_bridge import ROOT
        board = load_board(sorted((ROOT / 'levels/hard').glob('*.board'))[-1])
        result = CoilBridge(node_budget=1000000000, timeout=.001).evaluate(board, SearchConfig(pruning=False))
        self.assertEqual(result['status'], 'time_limit')

    def test_process_failures_are_not_puzzle_failures(self):
        board = CoilBoard.parse('x=1&y=1&board=.');cfg = SearchConfig()
        for source in ("print('bad json')", "print('[]')", "import sys; print('{}'); sys.exit(3)"):
            with self.assertRaises(BridgeError): CoilBridge(self.fake(source)).evaluate(board, cfg)
        result = CoilBridge(self.fake('import time; time.sleep(5)'), timeout=.001).evaluate(board, cfg)
        self.assertEqual(result['status'], 'process_timeout')
        self.assertIsNone(result['coverage'])

    def test_false_success_and_wrong_board_rejected(self):
        board = CoilBoard.parse('x=2&y=2&board=....')
        valid = self.bridge.evaluate(board, SearchConfig())
        for change in ({'solution': 'x=0&y=0&path=R'}, {'boardSha256': 'wrong'}, {'nodes': -1}, {'coverage': 0}, {'budgetExceeded': True}):
            bad = valid | change
            with self.assertRaises(BridgeError):
                CoilBridge(self.fake('print('+repr(json.dumps(bad))+')')).evaluate(board, SearchConfig())

    def test_discovery_and_explicit_skips(self):
        (self.root / 'large.board').write_text('x=4&y=4&board=' + '.' * 16)
        problem = CoilProblem(self.root, bridge=CoilBridge(max_cells=4))
        self.assertEqual(len(problem.skipped), 1)
        self.assertEqual(len(problem.get_test_cases(Difficulty.EASY)), 1)
        (self.root / 'bad.coil').write_text('garbage')
        with self.assertRaises(ValueError): CoilProblem(self.root)
        with tempfile.TemporaryDirectory() as empty:
            with self.assertRaises(ValueError): CoilProblem(empty)

    def test_candidate_controls_are_concrete_and_atomic(self):
        solution = CoilSolution()
        for modification in ({'raw_text': 'make it cleverer'}, {'config': {'budget': 1000000}}, {'config': {'directions': 'UUUU'}}, {'config': {'pruning': 'false'}}, {'config': {}}):
            self.assertFalse(solution.apply_modification(modification))
            self.assertEqual(solution.config, SearchConfig())
        self.assertTrue(solution.apply_modification({'raw_text': 'Use {"directions":"DRUL"} for search ordering.'}))
        clone = solution.clone()
        self.assertTrue(clone.apply_modification({'config': {'pruning': False}}))
        self.assertTrue(solution.config.pruning)
        case = CoilProblem(self.root).get_test_cases(Difficulty.EASY)[0]
        self.assertTrue(solution.execute(case)['validated'])

    def test_comparator_matches_identity_and_rejects_bad_sets(self):
        result = lambda name, score=1, success=True: EvalResult(name, score, 1, success)
        comparator = Comparator(min_improvement=0)
        self.assertTrue(comparator.compare([result('a'),result('b')], [result('b'),result('a')])[0])
        for base, candidate in (([],[]), ([result('a')],[result('b')]), ([result('a')]*2,[result('a')]*2),
                                ([result('a')],[result('a',float('nan'))]), ([result('a')],[result('a',1,False)])):
            self.assertFalse(comparator.compare(base,candidate)[0])

    def test_generic_meta_solver_runs_real_evaluation(self):
        problem = CoilProblem(self.root)
        solver = MetaSolver(problem, CoilSolution(), [], MetaSolverConfig(max_iterations=0, verbose=False))
        solver.run()
        self.assertGreater(solver.current_score, .5)
        baseline = solver.evaluator.full_eval(solver.current_solution)
        idea = Idea('reverse', 'test', '{"starts":"reverse"}', 'Reverse start order', 1)
        attempt = solver._test_idea(idea, baseline)
        self.assertEqual(len(attempt.results), 1)
        self.assertTrue(attempt.results[0].details['validated'])
        self.assertFalse(attempt.accepted)  # Equal work is not an improvement.

    def test_no_easy_cases_does_not_reject_by_empty_tier(self):
        problem = CoilProblem(self.root)
        cases = problem.test_cases[Difficulty.EASY]
        problem.test_cases[Difficulty.EASY] = []
        problem.test_cases[Difficulty.MEDIUM] = cases
        solver = MetaSolver(problem, CoilSolution(), [], MetaSolverConfig(max_iterations=0, verbose=False))
        baseline = solver.evaluator.full_eval(solver.current_solution)
        attempt = solver._test_idea(Idea('x','test','{"starts":"reverse"}','Reverse',1), baseline)
        self.assertEqual(len(attempt.results), 1)
        self.assertNotIn('Failed easy cases', attempt.rejection_reason)

    def test_legacy_tiers_and_idea_flow_use_real_results(self):
        evaluator = MultiTierEvaluator(str(self.bridge.solver_path), str(self.root))
        score, report = evaluator.evaluate({})
        self.assertGreater(score, .5)
        records = CoilMetaSolver._flatten(report)
        self.assertTrue(next(iter(records.values()))['validated'])
        evaluator.set_baseline(records)
        with self.assertRaises(ValueError): evaluator.set_baseline({})
        with self.assertRaises(ValueError): MultiTierEvaluator(str(self.bridge.solver_path), str(self.root), tiers=[])
        limited = MultiTierEvaluator(str(self.bridge.solver_path), str(self.root), node_budget=0)
        _, failure = limited.evaluate({})
        self.assertFalse(next(iter(CoilMetaSolver._flatten(failure).values()))['passed'])
        meta = CoilMetaSolver(str(self.bridge.solver_path), str(self.root))
        self.assertIsNotNone(meta.run(max_iterations=0))
        idea = CoilIdea('x', ImprovementCategory.SEARCH_ORDER, 'test', '{"starts":"reverse"}', 'Reverse')
        result = meta._evaluate_idea(idea)
        self.assertTrue(idea.attempted)
        self.assertEqual(idea.success, result['success'])
        self.assertTrue(next(iter(meta.last_results.values()))['validated'])

    def test_concrete_ideas_not_collapsed_by_keyword_hash(self):
        memory = IdeaMemory()
        for direction in ('URDL', 'DRUL'):
            raw = json.dumps({'directions': direction})
            idea = CoilIdea(direction, ImprovementCategory.SEARCH_ORDER, 'test', raw, 'Search order', CoilIdea.compute_semantic_hash(raw))
            self.assertTrue(memory.is_novel(idea))
            memory.add(idea)
            self.assertFalse(memory.is_novel(idea))


if __name__ == '__main__':
    unittest.main()
