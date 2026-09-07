"""
Proof-of-Concept: Meta-Solver applied to Knapsack Problem

This demonstrates the framework with a well-understood optimization problem.
The "ideas" from LLMs could be different heuristics:
- Greedy by value/weight ratio
- Dynamic programming
- Branch and bound variations
- Genetic algorithm approaches
- Simulated annealing parameters
"""

import random
import time
from dataclasses import dataclass, field
from typing import Any, Optional
import sys
import os

# Add parent to path
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parents[2]))

from meta_solver.core import (
    Problem, Solution, LLMInterface, TestCase, EvalResult, Difficulty
)


@dataclass
class KnapsackInstance:
    """A knapsack problem instance."""
    weights: list[int]
    values: list[int]
    capacity: int
    optimal_value: Optional[int] = None  # If known


@dataclass
class KnapsackTestCase(TestCase):
    """Test case wrapping a knapsack instance."""
    instance: KnapsackInstance = None


class KnapsackProblem(Problem):
    """The 0/1 Knapsack Problem as our demo problem."""
    
    def __init__(self, seed: int = 42):
        self.seed = seed
        random.seed(seed)
        self._generate_test_cases()
    
    def _generate_test_cases(self):
        """Generate test cases at different difficulty levels."""
        self.test_cases = {
            Difficulty.EASY: [],
            Difficulty.MEDIUM: [],
            Difficulty.HARD: []
        }
        
        # Easy: Small instances (5-10 items)
        for i in range(10):
            n = random.randint(5, 10)
            weights = [random.randint(1, 20) for _ in range(n)]
            values = [random.randint(1, 50) for _ in range(n)]
            capacity = sum(weights) // 2
            
            # Compute optimal via DP for ground truth
            optimal = self._compute_optimal_dp(weights, values, capacity)
            
            instance = KnapsackInstance(weights, values, capacity, optimal)
            self.test_cases[Difficulty.EASY].append(
                KnapsackTestCase(
                    id=f"easy_{i}",
                    data=instance,
                    difficulty=Difficulty.EASY,
                    expected=optimal,
                    instance=instance
                )
            )
        
        # Medium: Moderate instances (20-50 items)
        for i in range(10):
            n = random.randint(20, 50)
            weights = [random.randint(1, 50) for _ in range(n)]
            values = [random.randint(1, 100) for _ in range(n)]
            capacity = sum(weights) // 3
            
            optimal = self._compute_optimal_dp(weights, values, capacity)
            
            instance = KnapsackInstance(weights, values, capacity, optimal)
            self.test_cases[Difficulty.MEDIUM].append(
                KnapsackTestCase(
                    id=f"medium_{i}",
                    data=instance,
                    difficulty=Difficulty.MEDIUM,
                    expected=optimal,
                    instance=instance
                )
            )
        
        # Hard: Larger instances (100-200 items)
        for i in range(5):
            n = random.randint(100, 200)
            weights = [random.randint(1, 100) for _ in range(n)]
            values = [random.randint(1, 200) for _ in range(n)]
            capacity = sum(weights) // 4
            
            optimal = self._compute_optimal_dp(weights, values, capacity)
            
            instance = KnapsackInstance(weights, values, capacity, optimal)
            self.test_cases[Difficulty.HARD].append(
                KnapsackTestCase(
                    id=f"hard_{i}",
                    data=instance,
                    difficulty=Difficulty.HARD,
                    expected=optimal,
                    instance=instance
                )
            )
    
    def _compute_optimal_dp(self, weights, values, capacity) -> int:
        """Compute optimal solution via dynamic programming."""
        n = len(weights)
        # Use 1D DP to save memory
        dp = [0] * (capacity + 1)
        
        for i in range(n):
            for w in range(capacity, weights[i] - 1, -1):
                dp[w] = max(dp[w], dp[w - weights[i]] + values[i])
        
        return dp[capacity]
    
    def describe(self) -> str:
        return """
# 0/1 Knapsack Problem

Given n items, each with a weight w_i and value v_i, and a knapsack with 
capacity W, find the subset of items that:
1. Has total weight ≤ W
2. Maximizes total value

This is NP-hard, so for large instances we need heuristics.

## Instance Characteristics
- Easy: 5-10 items, small weights/values
- Medium: 20-50 items, moderate range
- Hard: 100-200 items, large range

## Evaluation
- Score = achieved_value / optimal_value
- Time is measured but not penalized (for now)
"""
    
    def describe_current_approach(self) -> str:
        return """
Current approach: Simple greedy by value/weight ratio.
- Sort items by value/weight ratio (descending)
- Greedily add items that fit
- Works well on easy cases but often suboptimal on harder instances
"""
    
    def get_test_cases(self, difficulty: Difficulty) -> list[TestCase]:
        return self.test_cases.get(difficulty, [])
    
    def evaluate(self, solution: 'KnapsackSolution', test_case: TestCase) -> EvalResult:
        """Evaluate a solution on a test case."""
        instance = test_case.data
        
        start_time = time.time()
        try:
            result = solution.execute(test_case)
            elapsed = time.time() - start_time
            
            # Validate: check weight constraint
            total_weight = sum(instance.weights[i] for i in result)
            total_value = sum(instance.values[i] for i in result)
            
            if total_weight > instance.capacity:
                return EvalResult(
                    test_case_id=test_case.id,
                    score=0.0,
                    time_taken=elapsed,
                    success=False,
                    details={'error': 'Weight constraint violated', 
                             'weight': total_weight, 
                             'capacity': instance.capacity}
                )
            
            # Score is ratio of achieved to optimal
            optimal = test_case.expected
            score = total_value / optimal if optimal > 0 else 1.0
            
            return EvalResult(
                test_case_id=test_case.id,
                score=score,
                time_taken=elapsed,
                success=True,
                details={'value': total_value, 'optimal': optimal, 'weight': total_weight}
            )
            
        except Exception as e:
            return EvalResult(
                test_case_id=test_case.id,
                score=0.0,
                time_taken=time.time() - start_time,
                success=False,
                details={'error': str(e)}
            )


class KnapsackSolution(Solution):
    """A solution strategy for the knapsack problem."""
    
    def __init__(self, strategy: str = "greedy_ratio"):
        self.strategy = strategy
        self.params: dict = {}
    
    def clone(self) -> 'KnapsackSolution':
        cloned = KnapsackSolution(self.strategy)
        cloned.params = self.params.copy()
        return cloned
    
    def apply_modification(self, modification: dict) -> bool:
        """Apply a modification from an idea."""
        approach = modification.get('approach', '').lower()
        
        # Parse different strategy names from ideas
        if 'greedy' in approach and 'ratio' in approach:
            self.strategy = 'greedy_ratio'
            return True
        elif 'greedy' in approach and 'value' in approach:
            self.strategy = 'greedy_value'
            return True
        elif 'greedy' in approach and 'weight' in approach:
            self.strategy = 'greedy_light'
            return True
        elif 'dp' in approach or 'dynamic' in approach:
            self.strategy = 'dp'
            return True
        elif 'branch' in approach or 'bound' in approach:
            self.strategy = 'branch_bound'
            return True
        elif 'random' in approach or 'sample' in approach:
            self.strategy = 'random_sampling'
            self.params['samples'] = 1000
            return True
        elif 'genetic' in approach or 'evolutionary' in approach:
            self.strategy = 'genetic'
            return True
        elif 'hybrid' in approach:
            self.strategy = 'hybrid'
            return True
        
        # Default: try to apply anyway
        return True
    
    def execute(self, test_case: TestCase) -> list[int]:
        """Run the solution strategy on a test case. Returns list of item indices."""
        instance = test_case.data
        
        if self.strategy == 'greedy_ratio':
            return self._greedy_ratio(instance)
        elif self.strategy == 'greedy_value':
            return self._greedy_value(instance)
        elif self.strategy == 'greedy_light':
            return self._greedy_light(instance)
        elif self.strategy == 'dp':
            return self._dp(instance)
        elif self.strategy == 'branch_bound':
            return self._branch_bound(instance)
        elif self.strategy == 'random_sampling':
            return self._random_sampling(instance)
        elif self.strategy == 'genetic':
            return self._genetic(instance)
        elif self.strategy == 'hybrid':
            return self._hybrid(instance)
        else:
            return self._greedy_ratio(instance)
    
    def _greedy_ratio(self, inst: KnapsackInstance) -> list[int]:
        """Greedy by value/weight ratio."""
        n = len(inst.weights)
        ratios = [(inst.values[i] / inst.weights[i], i) for i in range(n)]
        ratios.sort(reverse=True)
        
        selected = []
        remaining_capacity = inst.capacity
        
        for _, i in ratios:
            if inst.weights[i] <= remaining_capacity:
                selected.append(i)
                remaining_capacity -= inst.weights[i]
        
        return selected
    
    def _greedy_value(self, inst: KnapsackInstance) -> list[int]:
        """Greedy by value (take highest value items first)."""
        n = len(inst.weights)
        items = [(inst.values[i], i) for i in range(n)]
        items.sort(reverse=True)
        
        selected = []
        remaining_capacity = inst.capacity
        
        for _, i in items:
            if inst.weights[i] <= remaining_capacity:
                selected.append(i)
                remaining_capacity -= inst.weights[i]
        
        return selected
    
    def _greedy_light(self, inst: KnapsackInstance) -> list[int]:
        """Greedy by weight (take lightest items first)."""
        n = len(inst.weights)
        items = [(inst.weights[i], i) for i in range(n)]
        items.sort()
        
        selected = []
        remaining_capacity = inst.capacity
        
        for _, i in items:
            if inst.weights[i] <= remaining_capacity:
                selected.append(i)
                remaining_capacity -= inst.weights[i]
        
        return selected
    
    def _dp(self, inst: KnapsackInstance) -> list[int]:
        """Dynamic programming solution (optimal but may be slow)."""
        n = len(inst.weights)
        W = inst.capacity
        
        # DP table
        dp = [[0] * (W + 1) for _ in range(n + 1)]
        
        for i in range(1, n + 1):
            for w in range(W + 1):
                dp[i][w] = dp[i-1][w]
                if inst.weights[i-1] <= w:
                    dp[i][w] = max(dp[i][w], dp[i-1][w - inst.weights[i-1]] + inst.values[i-1])
        
        # Backtrack to find selected items
        selected = []
        w = W
        for i in range(n, 0, -1):
            if dp[i][w] != dp[i-1][w]:
                selected.append(i-1)
                w -= inst.weights[i-1]
        
        return selected
    
    def _branch_bound(self, inst: KnapsackInstance) -> list[int]:
        """Branch and bound with pruning."""
        n = len(inst.weights)
        
        # Sort by ratio for bound calculation
        items = [(inst.values[i] / inst.weights[i], inst.weights[i], inst.values[i], i) 
                 for i in range(n)]
        items.sort(reverse=True)
        
        best_value = 0
        best_selection = []
        
        def bound(level, weight, value):
            """Upper bound using fractional relaxation."""
            if weight > inst.capacity:
                return 0
            
            result = value
            total_weight = weight
            
            for i in range(level, n):
                if total_weight + items[i][1] <= inst.capacity:
                    total_weight += items[i][1]
                    result += items[i][2]
                else:
                    result += (inst.capacity - total_weight) * items[i][0]
                    break
            
            return result
        
        def search(level, weight, value, selection):
            nonlocal best_value, best_selection
            
            if level == n:
                if value > best_value:
                    best_value = value
                    best_selection = selection[:]
                return
            
            _, w, v, idx = items[level]
            
            # Include item
            if weight + w <= inst.capacity:
                selection.append(idx)
                search(level + 1, weight + w, value + v, selection)
                selection.pop()
            
            # Exclude item (with pruning)
            if bound(level + 1, weight, value) > best_value:
                search(level + 1, weight, value, selection)
        
        search(0, 0, 0, [])
        return best_selection
    
    def _random_sampling(self, inst: KnapsackInstance) -> list[int]:
        """Random sampling with local improvement."""
        n = len(inst.weights)
        samples = self.params.get('samples', 1000)
        
        best_value = 0
        best_selection = []
        
        for _ in range(samples):
            # Random selection
            selection = []
            weight = 0
            indices = list(range(n))
            random.shuffle(indices)
            
            for i in indices:
                if weight + inst.weights[i] <= inst.capacity:
                    selection.append(i)
                    weight += inst.weights[i]
            
            value = sum(inst.values[i] for i in selection)
            if value > best_value:
                best_value = value
                best_selection = selection
        
        return best_selection
    
    def _genetic(self, inst: KnapsackInstance) -> list[int]:
        """Simple genetic algorithm."""
        n = len(inst.weights)
        pop_size = 50
        generations = 100
        mutation_rate = 0.1
        
        def fitness(chromosome):
            weight = sum(inst.weights[i] for i, bit in enumerate(chromosome) if bit)
            if weight > inst.capacity:
                return 0
            return sum(inst.values[i] for i, bit in enumerate(chromosome) if bit)
        
        def crossover(p1, p2):
            point = random.randint(1, n-1)
            return p1[:point] + p2[point:]
        
        def mutate(chromosome):
            return [1 - bit if random.random() < mutation_rate else bit for bit in chromosome]
        
        # Initialize population
        population = [[random.randint(0, 1) for _ in range(n)] for _ in range(pop_size)]
        
        for _ in range(generations):
            # Evaluate
            scored = [(fitness(chrom), chrom) for chrom in population]
            scored.sort(reverse=True)
            
            # Select top half
            survivors = [chrom for _, chrom in scored[:pop_size//2]]
            
            # Create new generation
            new_pop = survivors[:]
            while len(new_pop) < pop_size:
                p1, p2 = random.sample(survivors, 2)
                child = mutate(crossover(p1, p2))
                new_pop.append(child)
            
            population = new_pop
        
        # Return best
        best = max(population, key=fitness)
        return [i for i, bit in enumerate(best) if bit]
    
    def _hybrid(self, inst: KnapsackInstance) -> list[int]:
        """Hybrid: try multiple strategies and pick best."""
        strategies = ['greedy_ratio', 'greedy_value', 'random_sampling']
        
        best_value = 0
        best_result = []
        
        for strat in strategies:
            self.strategy = strat
            if strat == 'random_sampling':
                self.params['samples'] = 500
            
            result = self.execute(
                type('TC', (), {'data': inst})()
            )
            value = sum(inst.values[i] for i in result)
            weight = sum(inst.weights[i] for i in result)
            
            if weight <= inst.capacity and value > best_value:
                best_value = value
                best_result = result
        
        self.strategy = 'hybrid'
        return best_result
    
    def serialize(self) -> str:
        return f"{self.strategy}:{self.params}"


# Mock LLM for testing
class MockLLM(LLMInterface):
    """Mock LLM that returns predefined responses for testing."""
    
    def __init__(self, name: str, ideas: list[str]):
        self._name = name
        self._ideas = ideas
        self._idea_index = 0
    
    @property
    def name(self) -> str:
        return self._name
    
    def generate(self, prompt: str, temperature: float = 0.7) -> str:
        if self._idea_index < len(self._ideas):
            idea = self._ideas[self._idea_index]
            self._idea_index += 1
            return idea
        return "No more ideas."
    
    def critique(self, idea: str, problem_context: str) -> tuple[float, str]:
        # Always approve for testing
        return 0.7, "Looks reasonable"


def demo():
    """Run a demonstration of the meta-solver."""
    print("="*60)
    print("Meta-Solver Demo: Knapsack Problem")
    print("="*60)
    
    # Create problem
    problem = KnapsackProblem(seed=42)
    
    # Create initial solution (simple greedy)
    base_solution = KnapsackSolution(strategy='greedy_ratio')
    
    # Mock LLMs with different "ideas"
    mock_llms = [
        MockLLM("mock_opus", [
            """
**Idea Name**: Dynamic Programming Approach
**Core Insight**: Knapsack has optimal substructure
**Approach**: Use DP to find exact optimal
**Expected Benefit**: Optimal solution guaranteed
**Confidence**: high
            """,
            """
**Idea Name**: Branch and Bound
**Core Insight**: Prune search space using bounds
**Approach**: Use fractional relaxation for upper bounds
**Confidence**: high
            """
        ]),
        MockLLM("mock_gemini", [
            """
**Idea Name**: Greedy by Value
**Core Insight**: High value items might be better
**Approach**: Sort by value, take greedily
**Confidence**: medium
            """,
            """
**Idea Name**: Hybrid Strategy
**Core Insight**: Different strategies work for different instances
**Approach**: Try multiple approaches, pick best
**Confidence**: medium
            """
        ])
    ]
    
    # Run with minimal configuration for demo
    from meta_solver.solver import MetaSolver, MetaSolverConfig
    
    config = MetaSolverConfig(
        max_iterations=3,
        ideas_per_iteration=2,
        use_pre_critique=False,  # Skip for demo
        temperatures=[0.7],
        verbose=True
    )
    
    solver = MetaSolver(
        problem=problem,
        base_solution=base_solution,
        llm_ensemble=mock_llms,
        config=config
    )
    
    final_solution = solver.run()
    
    print("\n" + "="*60)
    print("Final Solution Strategy:", final_solution.strategy)
    print("="*60)


if __name__ == "__main__":
    demo()
