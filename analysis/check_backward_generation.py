"""Exhaustive small-board check of GENERATION-COMPLETENESS.md.

Compare backward construction with an independent board-first slide solver.
This is a proof reference, not the production generator or a sampling benchmark.
"""

import argparse
from collections.abc import Iterator

Cell = tuple[int, int]
Path = tuple[Cell, ...]
DIRECTIONS = ((0, -1), (1, 0), (0, 1), (-1, 0))


def backward_paths(width: int, height: int) -> Iterator[Path]:
    def extend(path: Path, opened: set[Cell]) -> Iterator[Path]:
        yield path  # Every intermediate state must be eligible for output.
        sx, sy = path[0]
        for dx, dy in DIRECTIONS:
            added = (sx - dx, sy - dy)
            if not (0 <= added[0] < width and 0 <= added[1] < height):
                continue
            if added in opened:
                continue
            ahead = (sx + dx, sy + dy)
            # A new turn at the old start needs a blocker. All subsequent
            # slides see the new square as visited trail, replacing a wall.
            if len(path) > 1 and path[1] != ahead and ahead in opened:
                continue
            yield from extend((added,) + path, opened | {added})

    for y in range(height):
        for x in range(width):
            yield from extend(((x, y),), {(x, y)})


def solve_board(width: int, height: int, mask: int) -> Iterator[Path]:
    """Enumerate solutions by maximal forward slides on a fixed board."""
    def search(path: Path, visited: int) -> Iterator[Path]:
        if visited == mask:
            yield path
            return
        for dx, dy in DIRECTIONS:
            x, y = path[-1]
            moved = []
            next_visited = visited
            while True:
                x, y = x + dx, y + dy
                if not (0 <= x < width and 0 <= y < height):
                    break
                bit = 1 << (y * width + x)
                if not mask & bit or next_visited & bit:
                    break
                next_visited |= bit
                moved.append((x, y))
            if moved:
                yield from search(path + tuple(moved), next_visited)

    for i in range(width * height):
        if mask & (1 << i):
            yield from search(((i % width, i // width),), 1 << i)


def check(width: int, height: int) -> None:
    generated: dict[int, set[Path]] = {}
    for path in backward_paths(width, height):
        mask = sum(1 << (y * width + x) for x, y in path)
        generated.setdefault(mask, set()).add(path)

    board_count = path_count = 0
    for mask in range(1, 1 << (width * height)):
        expected = set(solve_board(width, height, mask))
        actual = generated.get(mask, set())
        if actual != expected:
            raise AssertionError(
                f"{width}x{height} mask={mask}: "
                f"missing={next(iter(expected - actual), None)}, "
                f"invalid={next(iter(actual - expected), None)}"
            )
        board_count += bool(expected)
        path_count += len(expected)
    print(f"{width}x{height}: {board_count} solvable boards, "
          f"{path_count} ordered solutions; exact agreement")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("width", type=int)
    parser.add_argument("height", type=int)
    args = parser.parse_args()
    if args.width < 1 or args.height < 1 or args.width * args.height > 16:
        parser.error("positive dimensions and at most 16 cells required")
    check(args.width, args.height)
