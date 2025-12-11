"""
Coil Problem Integration

This shows how the meta-solver framework could be applied to the coil puzzle.
The actual implementation would need to:
1. Interface with the C# solver
2. Parse .coil level files
3. Extract meaningful features from attempts

This is a STUB showing the integration pattern.
"""

import subprocess
import os
import time
import json
from dataclasses import dataclass
from typing import Optional, Any
from pathlib import Path
import sys

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

from core import Problem, Solution, TestCase, EvalResult, Difficulty


@dataclass
class CoilLevel:
    """A coil puzzle level."""
    id: str
    width: int
    height: int
    grid: str  # The raw .coil file content
    blocked_cells: int
    total_cells: int
    known_solvable: bool = True
    best_time: Optional[float] = None


class CoilProblem(Problem):
    """
    The Coil Puzzle Problem.
    
    Find a Hamiltonian path through a grid where:
    - Start from any edge cell
    - Move in straight lines until hitting wall/edge/visited
    - Visit all non-blocked cells exactly once
    """
    
    def __init__(self, levels_dir: str = "/workspace/levels"):
        self.levels_dir = Path(levels_dir)
        self._load_levels()
    
    def _load_levels(self):
        """Load and categorize levels by difficulty."""
        self.test_cases = {
            Difficulty.EASY: [],
            Difficulty.MEDIUM: [],
            Difficulty.HARD: []
        }
        
        if not self.levels_dir.exists():
            print(f"Warning: Levels directory {self.levels_dir} not found")
            return
        
        # Load .coil files
        coil_files = list(self.levels_dir.glob("*.coil"))
        
        for path in coil_files[:100]:  # Limit for now
            try:
                content = path.read_text()
                lines = content.strip().split('\n')
                
                # Parse dimensions from first line (format: "1000x1000 - description")
                first_line = lines[0]
                dims_part = first_line.split()[0]  # Get "1000x1000"
                
                # Parse WxH format
                import re
                match = re.match(r'(\d+)x(\d+)', dims_part)
                if not match:
                    continue
                    
                width, height = int(match.group(1)), int(match.group(2))
                
                # Count blocked cells (X = blocked, . = empty)
                grid_lines = lines[1:]
                blocked = sum(1 for line in grid_lines for c in line.upper() if c == 'X')
                total = width * height
                
                level = CoilLevel(
                    id=path.stem,
                    width=width,
                    height=height,
                    grid=content,
                    blocked_cells=blocked,
                    total_cells=total
                )
                
                # Categorize by difficulty
                # Simple heuristic: smaller grids are easier
                cells_to_visit = total - blocked
                
                if cells_to_visit <= 100:
                    difficulty = Difficulty.EASY
                elif cells_to_visit <= 1000:
                    difficulty = Difficulty.MEDIUM
                else:
                    difficulty = Difficulty.HARD
                
                self.test_cases[difficulty].append(
                    TestCase(
                        id=level.id,
                        data=level,
                        difficulty=difficulty,
                        expected=None,  # We don't know optimal time
                        metadata={'path': str(path)}
                    )
                )
            except Exception as e:
                # Silently skip malformed files
                pass
        
        for d in Difficulty:
            print(f"Loaded {len(self.test_cases[d])} {d.value} levels")
    
    def describe(self) -> str:
        return """
# Coil Puzzle (Hamiltonian Path on Grid)

## Problem Statement
Given a rectangular grid with some blocked cells:
1. Start from any cell on the edge
2. Move in a straight line (up/down/left/right)
3. You MUST continue until you hit a wall, edge, or already-visited cell
4. Goal: Visit every non-blocked cell exactly once

## Characteristics
- NP-complete in general
- Requires careful path planning to avoid "trapping" yourself
- Dead ends can occur when you've visited cells that prevent completing the path
- Some configurations are provably unsolvable

## Current Solver Approach
The C# solver uses:
- Segment-based exploration (picking directions intelligently)
- Tweaking/backtracking when stuck
- Various heuristics for direction selection

## Challenges
- Some hard levels take very long to solve
- Easy to get trapped in dead ends
- Need to balance exploration vs exploitation

## Evaluation
- Primary: Did we solve it? (binary)
- Secondary: How long did it take?
- Tertiary: How many backtracks needed?
"""
    
    def describe_current_approach(self) -> str:
        return """
Current Solver Strategy (from C# codebase):

1. **Segment-Based Movement**: Rather than moving cell-by-cell, treat each
   straight-line movement as a "segment".

2. **Direction Selection**: Multiple heuristics compete:
   - Prefer directions that don't create dead ends
   - Consider "must visit" cells and ensure they're reachable
   - Weight by how many cells will be visited

3. **Backtracking**: When stuck, undo recent segments and try alternatives.
   - TweakSection system for organized backtracking
   - TweakPickers for selecting which segments to modify

4. **Performance**: Solves most levels quickly, but some hard levels
   remain unsolved or take excessive time.

Areas for potential improvement:
- Dead-end detection earlier
- Better global planning (not just greedy local decisions)
- Symmetry detection and exploitation
- Pattern recognition from solved levels
"""
    
    def get_test_cases(self, difficulty: Difficulty) -> list[TestCase]:
        return self.test_cases.get(difficulty, [])
    
    def evaluate(self, solution: 'CoilSolution', test_case: TestCase) -> EvalResult:
        """
        Evaluate by running the C# solver with the given configuration.
        
        In a real implementation, this would:
        1. Write the configuration to a file or pass as args
        2. Run the C# solver
        3. Parse the output
        4. Return structured results
        """
        level = test_case.data
        
        start = time.time()
        
        # STUB: In real implementation, would run:
        # result = subprocess.run(
        #     ["dotnet", "run", "--", level.id, solution.config_json],
        #     capture_output=True, timeout=30
        # )
        
        # For now, simulate based on solution strategy
        solved, simulated_time = self._simulate_solve(level, solution)
        
        elapsed = time.time() - start
        
        if solved:
            # Score based on time (faster is better)
            # Use inverse of time, capped
            time_score = 1.0 / (1.0 + simulated_time)
            score = 0.5 + 0.5 * time_score  # 0.5 for solving, 0.5 for speed
        else:
            score = 0.0
        
        return EvalResult(
            test_case_id=test_case.id,
            score=score,
            time_taken=elapsed,
            success=solved,
            details={
                'solved': solved,
                'simulated_time': simulated_time,
                'level_size': level.total_cells - level.blocked_cells
            }
        )
    
    def _simulate_solve(self, level: CoilLevel, solution: 'CoilSolution') -> tuple[bool, float]:
        """
        Simulate solving based on strategy.
        In real implementation, this would run actual solver.
        """
        import random
        
        cells = level.total_cells - level.blocked_cells
        
        # Base solve probability depends on difficulty
        base_prob = {
            'greedy': 0.7,
            'careful': 0.8,
            'exhaustive': 0.95,
            'hybrid': 0.85
        }.get(solution.strategy, 0.75)
        
        # Harder levels are harder to solve
        difficulty_factor = 1.0 - (cells / 200)
        solve_prob = base_prob * difficulty_factor
        
        solved = random.random() < solve_prob
        
        # Simulate time
        base_time = cells * 0.01  # 10ms per cell base
        strategy_factor = {
            'greedy': 0.5,
            'careful': 1.0,
            'exhaustive': 3.0,
            'hybrid': 1.5
        }.get(solution.strategy, 1.0)
        
        time = base_time * strategy_factor * (1 + random.random())
        
        return solved, time


class CoilSolution(Solution):
    """
    Configuration for the coil solver.
    
    In the real implementation, this would hold parameters for:
    - SegPicker weights
    - TweakSection configurations
    - Timeout settings
    - Heuristic choices
    """
    
    def __init__(self, strategy: str = "greedy", params: dict = None):
        self.strategy = strategy
        self.params = params or {}
    
    def clone(self) -> 'CoilSolution':
        return CoilSolution(self.strategy, self.params.copy())
    
    def apply_modification(self, modification: dict) -> bool:
        """Apply an idea from the LLM."""
        approach = modification.get('approach', '').lower()
        raw = modification.get('raw_text', '').lower()
        
        # Parse different strategies
        if 'exhaustive' in approach or 'complete' in approach:
            self.strategy = 'exhaustive'
            return True
        elif 'careful' in approach or 'dead.?end' in raw:
            self.strategy = 'careful'
            return True
        elif 'hybrid' in approach or 'combination' in approach:
            self.strategy = 'hybrid'
            return True
        elif 'greedy' in approach or 'fast' in approach:
            self.strategy = 'greedy'
            return True
        
        # Look for parameter suggestions
        import re
        
        # e.g., "set weight to 0.8"
        weight_match = re.search(r'weight[:\s]*(\d+\.?\d*)', raw)
        if weight_match:
            self.params['weight'] = float(weight_match.group(1))
        
        # e.g., "timeout 5 seconds"
        timeout_match = re.search(r'timeout[:\s]*(\d+)', raw)
        if timeout_match:
            self.params['timeout'] = int(timeout_match.group(1))
        
        return True
    
    def execute(self, test_case: TestCase) -> Any:
        """In real impl, this would run the solver."""
        return {'strategy': self.strategy, 'params': self.params}
    
    def serialize(self) -> str:
        return json.dumps({'strategy': self.strategy, 'params': self.params})
    
    @property
    def config_json(self) -> str:
        """Generate config for C# solver."""
        return self.serialize()


def demo():
    """Demo the coil problem integration."""
    print("="*60)
    print("Coil Problem Integration Demo")
    print("="*60)
    
    problem = CoilProblem()
    
    # Test different strategies
    strategies = ['greedy', 'careful', 'exhaustive', 'hybrid']
    
    for strat in strategies:
        print(f"\n--- Strategy: {strat} ---")
        solution = CoilSolution(strategy=strat)
        
        total_score = 0
        total_solved = 0
        count = 0
        
        for difficulty in [Difficulty.EASY, Difficulty.MEDIUM]:
            cases = problem.get_test_cases(difficulty)
            for tc in cases[:10]:  # Test on subset
                result = problem.evaluate(solution, tc)
                total_score += result.score
                if result.details.get('solved'):
                    total_solved += 1
                count += 1
        
        if count > 0:
            avg_score = total_score / count
            solve_rate = total_solved / count
            print(f"Average score: {avg_score:.4f}")
            print(f"Solve rate: {solve_rate:.1%}")


if __name__ == "__main__":
    demo()
