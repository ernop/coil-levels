"""Real Coil evaluation. Run from the repository root with python -m meta_solver.examples.coil_integration."""
import argparse
from dataclasses import asdict
import json
from pathlib import Path
import re

from meta_solver.core import Problem, Solution, TestCase, EvalResult, Difficulty, Comparator
from meta_solver.coil_bridge import ROOT, DEFAULT_SOLVER, BoardTooLarge, CoilBoard, CoilBridge, SearchConfig, load_board, search_score


class CoilProblem(Problem):
    def __init__(self, levels_dir: str | Path = ROOT / 'levels/hard', *, bridge: CoilBridge | None = None):
        self.levels_dir = Path(levels_dir).resolve()
        if not self.levels_dir.is_dir():
            raise FileNotFoundError(self.levels_dir)
        self.bridge = bridge or CoilBridge()
        self.test_cases = {difficulty: [] for difficulty in Difficulty}
        self.skipped = []
        files = sorted(p for p in self.levels_dir.rglob('*') if p.is_file() and
                       (p.name.endswith('.board') or p.name.endswith('.board.gz') or p.name.endswith('.coil')))
        for path in files:
            try:
                board = load_board(path, self.bridge.max_cells)
            except BoardTooLarge as error:
                self.skipped.append({'path': str(path.relative_to(self.levels_dir)), 'reason': str(error)})
                continue
            except ValueError as error:
                raise ValueError(f'{path}: {error}') from error
            # These are size strata for staged evaluation, not measured solving difficulty.
            difficulty = Difficulty.EASY if board.open_cells <= 100 else Difficulty.MEDIUM if board.open_cells <= 1000 else Difficulty.HARD
            case_id = path.relative_to(self.levels_dir).as_posix()
            self.test_cases[difficulty].append(TestCase(case_id, board, difficulty,
                metadata={'path': str(path), 'boardSha256': board.sha256, 'stratumBasis': 'open-cell count'}))
        if not any(self.test_cases.values()):
            raise ValueError(f'No evaluable boards in {self.levels_dir}; skipped {len(self.skipped)} oversized boards')

    def describe(self) -> str:
        return ('Mortal Coil: start at any open cell, including interior cells. Slide in each chosen '
                'orthogonal direction until stopped by a wall, boundary, or visited cell; visit every open '
                'cell exactly once. Candidate controls are directions (a permutation of URDL), starts '
                '(natural, reverse, low-degree, high-degree), and pruning (boolean). Supply changes as '
                'JSON, for example {"directions":"DRUL","starts":"reverse","pruning":true}. '
                'Node, wall-time, depth, and cell limits are fixed by the evaluator. Scores reward '
                'validated solutions and lower node counts; bounded-out searches score zero.')

    def describe_current_approach(self) -> str:
        return ('The C# reference solver performs deterministic DFS over complete slide moves. '
                'It narrows candidate starts using dead ends and optionally prunes unreachable or '
                'dead-end-constrained residual states. TweakPickers and SegPickers generate boards; '
                'they are not search heuristics. No generated answer file is read during evaluation.')

    def get_test_cases(self, difficulty: Difficulty) -> list[TestCase]:
        return self.test_cases[difficulty]

    def evaluate(self, solution: 'CoilSolution', test_case: TestCase) -> EvalResult:
        result = self.bridge.evaluate(test_case.data, solution.config)
        return EvalResult(test_case.id, search_score(result), result['wallSeconds'], result['solved'], result)


class CoilSolution(Solution):
    def __init__(self, config: dict | None = None, *, bridge: CoilBridge | None = None):
        self.config = SearchConfig.from_dict({} if config is None else config)
        self.bridge = bridge

    def clone(self) -> 'CoilSolution':
        return CoilSolution(asdict(self.config), bridge=self.bridge)

    def apply_modification(self, modification: dict) -> bool:
        candidate = modification.get('config')
        if candidate is None:
            raw = modification.get('raw_text', '')
            snippets = re.findall(r'\{[^{}]*\}', raw)
            if len(snippets) != 1:
                return False
            try:
                candidate = json.loads(snippets[0])
            except json.JSONDecodeError:
                return False
        try:
            SearchConfig.from_dict(candidate)  # Reject unsupported controls before merging.
            config = SearchConfig.from_dict(asdict(self.config) | candidate)
        except (ValueError, TypeError):
            return False
        if config == self.config:
            return False
        self.config = config
        return True

    def execute(self, test_case: TestCase) -> dict:
        return (self.bridge or CoilBridge()).evaluate(test_case.data, self.config)

    def serialize(self) -> str:
        return json.dumps(asdict(self.config), sort_keys=True)

    @property
    def config_json(self) -> str:
        return self.serialize()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('levels_dir', nargs='?', default=str(ROOT / 'levels/hard'))
    parser.add_argument('--solver', default=str(DEFAULT_SOLVER))
    parser.add_argument('--budget', type=int, default=100000)
    parser.add_argument('--timeout', type=float, default=5)
    parser.add_argument('--max-cells', type=int, default=10000)
    parser.add_argument('--depth-limit', type=int, default=2048)
    parser.add_argument('--config', default='{}', help='JSON with directions, starts, pruning')
    parser.add_argument('--compare', action='store_true', help='Evaluate three additional real search orderings with the same bounds')
    parser.add_argument('--output', type=Path, help='Write the full JSON report')
    args = parser.parse_args()
    bridge = CoilBridge(args.solver, node_budget=args.budget, timeout=args.timeout, max_cells=args.max_cells, depth_limit=args.depth_limit)
    problem = CoilProblem(args.levels_dir, bridge=bridge)
    cases = [case for difficulty in Difficulty for case in problem.get_test_cases(difficulty)]
    configs = [json.loads(args.config)]
    if args.compare:
        configs += [configs[0] | change for change in ({'directions': 'DRUL'}, {'starts': 'reverse'}, {'starts': 'low-degree'})]
    reports = []
    baseline = None
    for config in configs:
        solution = CoilSolution(config)
        results = [problem.evaluate(solution, case) for case in cases]
        comparison = None if baseline is None else Comparator().compare(baseline, results)
        if baseline is None:
            baseline = results
        reports.append({'config': asdict(solution.config), 'solved': sum(r.success for r in results),
                        'cases': len(results), 'score': sum(r.score for r in results) / len(results),
                        'comparisonToBaseline': comparison, 'results': [asdict(r) for r in results]})
    report = {'limits': {'nodeBudget': args.budget, 'timeoutSeconds': args.timeout, 'maxCells': args.max_cells, 'depthLimit': args.depth_limit},
              'skipped': problem.skipped, 'evaluations': reports}
    text = json.dumps(report, indent=2, allow_nan=False) + '\n'
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(text)
    print(text, end='')


if __name__ == '__main__':
    main()
