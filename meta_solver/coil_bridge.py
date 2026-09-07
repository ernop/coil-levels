"""Bounded subprocess evaluation of actual Coil boards with independent replay."""
from dataclasses import dataclass
import gzip
import hashlib
import json
import math
from pathlib import Path
import re
import subprocess
import time

ROOT = Path(__file__).resolve().parents[1]
DEFAULT_SOLVER = ROOT / 'bin/Release/net10.0/coil-levels-csharp.dll'


class BoardTooLarge(ValueError):
    pass


class BridgeError(RuntimeError):
    pass


def query(text: str) -> dict[str, str]:
    fields = {}
    for part in text.strip().split('&'):
        key, separator, value = part.partition('=')
        if not separator or key in fields:
            raise ValueError('Malformed or duplicate query field')
        fields[key] = value
    return fields


@dataclass(frozen=True)
class CoilBoard:
    width: int
    height: int
    cells: str

    @property
    def text(self) -> str:
        return f'x={self.width}&y={self.height}&board={self.cells}'

    @property
    def open_cells(self) -> int:
        return self.cells.count('.')

    @property
    def sha256(self) -> str:
        return hashlib.sha256(self.text.encode()).hexdigest()

    @classmethod
    def parse(cls, text: str, max_cells: int = 10000) -> 'CoilBoard':
        text = text.strip()
        if text.startswith('x='):
            fields = query(text)
            if set(fields) != {'x', 'y', 'board'}:
                raise ValueError('Expected x, y, board fields')
            width, height = int(fields['x']), int(fields['y'])
            cells = fields['board']
        else:
            lines = text.splitlines()
            match = re.fullmatch(r'(\d+)x(\d+)(?:\s+-\s+.*)?', lines[0] if lines else '')
            if not match:
                raise ValueError('Expected coilbench query or WxH .coil header')
            width, height = map(int, match.groups())
            rows = lines[1:]
            if len(rows) != height or any(len(row) != width for row in rows):
                raise ValueError('Grid row count or width does not match header')
            cells = ''.join(rows)
        if width < 1 or height < 1:
            raise ValueError('Board dimensions must be positive')
        if width * height > max_cells:
            raise BoardTooLarge(f'{width}x{height} exceeds {max_cells} cells')
        if len(cells) != width * height or set(cells) - {'.', 'X'}:
            raise ValueError('Board length or cell alphabet is invalid')
        return cls(width, height, cells)


def load_board(path: str | Path, max_cells: int = 10000) -> CoilBoard:
    path = Path(path)
    opener = gzip.open if path.suffix == '.gz' else open
    with opener(path, 'rt', encoding='utf-8-sig') as stream:
        text = stream.read(2 * max_cells + 513)
    # A .coil grid needs at most one newline per cell plus its header.
    if len(text) > 2 * max_cells + 512:
        raise BoardTooLarge(f'{path.name} exceeds bounded input size for {max_cells} cells')
    return CoilBoard.parse(text, max_cells)


def replay(board: CoilBoard, solution: str) -> None:
    fields = query(solution)
    if set(fields) != {'x', 'y', 'path'}:
        raise ValueError('Expected x, y, path solution fields')
    x, y = int(fields['x']), int(fields['y'])
    if not (0 <= x < board.width and 0 <= y < board.height) or board.cells[y * board.width + x] != '.':
        raise ValueError('Invalid solution start')
    visited = {y * board.width + x}
    for move in fields['path']:
        if move not in 'URDL':
            raise ValueError('Invalid direction')
        dx, dy = {'U': (0, -1), 'R': (1, 0), 'D': (0, 1), 'L': (-1, 0)}[move]
        count = 0
        while 0 <= x + dx < board.width and 0 <= y + dy < board.height:
            nxt = (y + dy) * board.width + x + dx
            if board.cells[nxt] != '.' or nxt in visited:
                break
            x += dx
            y += dy
            visited.add(nxt)
            count += 1
        if not count:
            raise ValueError('Solution contains a blocked move')
    if len(visited) != board.open_cells:
        raise ValueError(f'Solution visits {len(visited)} of {board.open_cells} open cells')


@dataclass(frozen=True)
class SearchConfig:
    directions: str = 'URDL'
    starts: str = 'natural'
    pruning: bool = True

    def __post_init__(self):
        if not isinstance(self.directions, str) or sorted(self.directions) != sorted('URDL'):
            raise ValueError('directions must be a permutation of URDL')
        if self.starts not in ('natural', 'reverse', 'low-degree', 'high-degree'):
            raise ValueError('Unsupported start ordering')
        if type(self.pruning) is not bool:
            raise ValueError('pruning must be boolean')

    @classmethod
    def from_dict(cls, config: dict) -> 'SearchConfig':
        if not isinstance(config, dict) or set(config) - {'directions', 'starts', 'pruning'}:
            raise ValueError('Only directions, starts, and pruning are candidate controls')
        return cls(**config)


class CoilBridge:
    def __init__(self, solver_path: str | Path = DEFAULT_SOLVER, *, node_budget: int = 100000,
                 timeout: float = 5.0, max_cells: int = 10000, depth_limit: int = 2048):
        self.solver_path = Path(solver_path).resolve()
        if not self.solver_path.is_file():
            raise FileNotFoundError(f'Build the C# solver first: {self.solver_path}')
        if type(node_budget) is not int or not 0 <= node_budget <= 2**63 - 1:
            raise ValueError('node_budget must be a nonnegative Int64')
        if not math.isfinite(timeout) or not .001 <= timeout <= 3600:
            raise ValueError('timeout must be .001..3600 seconds')
        if type(max_cells) is not int or not 1 <= max_cells <= 100000:
            raise ValueError('max_cells must be 1..100000')
        if type(depth_limit) is not int or not 1 <= depth_limit <= 2048:
            raise ValueError('depth_limit must be 1..2048')
        self.node_budget, self.timeout, self.max_cells, self.depth_limit = node_budget, timeout, max_cells, depth_limit

    def evaluate(self, board: CoilBoard, config: SearchConfig) -> dict:
        if board.width * board.height > self.max_cells:
            raise BoardTooLarge('Board exceeds evaluation cell limit')
        command = (['dotnet', str(self.solver_path)] if self.solver_path.suffix == '.dll' else [str(self.solver_path)])
        command += ['evaluate', '--budget', str(self.node_budget), '--timeout-ms', str(int(self.timeout * 1000)),
                    '--max-cells', str(self.max_cells), '--depth-limit', str(self.depth_limit),
                    '--directions', config.directions, '--starts', config.starts, '--pruning', 'on' if config.pruning else 'off']
        start = time.monotonic()
        try:
            process = subprocess.run(command, input=board.text, capture_output=True, text=True, timeout=self.timeout + 1)
        except subprocess.TimeoutExpired:
            return {'status': 'process_timeout', 'solved': False, 'validated': False, 'nodes': None,
                    'coverage': None, 'bestVisited': None, 'elapsedSeconds': None, 'wallSeconds': time.monotonic() - start}
        try:
            result = json.loads(process.stdout)
        except (json.JSONDecodeError, TypeError) as error:
            raise BridgeError(f'Invalid solver JSON (exit {process.returncode}): {process.stderr[:500]}') from error
        if not isinstance(result, dict):
            raise BridgeError('Solver JSON must be an object')
        if process.returncode or result.get('status') == 'error':
            raise BridgeError(f'Solver error (exit {process.returncode}): {result.get("error", process.stderr[:500])}')
        try:
            status = result['status']
            if result['schemaVersion'] != 1 or status not in ('solved', 'unsolvable', 'node_budget', 'time_limit', 'depth_limit'):
                raise ValueError('Unsupported result schema or status')
            if (result['width'], result['height'], result['openCells'], result['boardSha256']) != (board.width, board.height, board.open_cells, board.sha256):
                raise ValueError('Result does not match the input board')
            if type(result['nodes']) is not int or not 0 <= result['nodes'] <= self.node_budget:
                raise ValueError('Invalid node count')
            if type(result['bestVisited']) is not int or not 0 <= result['bestVisited'] <= board.open_cells:
                raise ValueError('Invalid coverage count')
            if not math.isfinite(result['elapsedSeconds']) or result['elapsedSeconds'] < 0:
                raise ValueError('Invalid elapsed time')
            expected_coverage = result['bestVisited'] / board.open_cells if board.open_cells else 0
            if result['coverage'] != expected_coverage or result['solved'] is not (status == 'solved') or result['validated'] is not (status == 'solved'):
                raise ValueError('Inconsistent result flags or coverage')
            flags = {'node_budget': 'budgetExceeded', 'time_limit': 'timedOut', 'depth_limit': 'depthExceeded'}
            if any(type(result[key]) is not bool for key in flags.values()):
                raise ValueError('Limit flags must be boolean')
            if status in flags and not result[flags[status]]:
                raise ValueError('Status is inconsistent with limit flags')
            if status in ('solved', 'unsolvable') and any(result[key] for key in flags.values()):
                raise ValueError('Completed result has a limit flag')
            if status == 'solved':
                replay(board, result['solution'])
            elif result['solution'] is not None:
                raise ValueError('Unsolved result contains a solution')
        except (KeyError, TypeError, ValueError) as error:
            raise BridgeError(f'Invalid solver result: {error}') from error
        result['wallSeconds'] = time.monotonic() - start
        return result


def search_score(result: dict) -> float:
    # Fixed node scale makes comparisons deterministic and independent of the allotted budget.
    return .5 + .5 / (1 + result['nodes'] / 1000) if result['solved'] else 0.0
