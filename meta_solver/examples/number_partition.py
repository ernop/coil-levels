"""
Example: Number Partition Problem (Karmarkar-Karp / Differencing)

Given a multiset of integers, partition into two subsets such that 
the difference of their sums is minimized.

This is NP-hard and a great testbed for heuristics because:
1. Simple problem statement
2. Many possible approaches
3. Clear evaluation metric
4. Easy to generate instances of varying hardness
"""

import random
import time
import heapq
from dataclasses import dataclass
from typing import Any, Optional
import sys
import os

from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parents[2]))

from meta_solver.core import Problem, Solution, TestCase, EvalResult, Difficulty


@dataclass
class PartitionInstance:
    """A number partition problem instance."""
    numbers: list[int]
    optimal_diff: Optional[int] = None  # If known


class PartitionProblem(Problem):
    """Number Partition Problem."""
    
    def __init__(self, seed: int = 42):
        self.seed = seed
        random.seed(seed)
        self._generate_test_cases()
    
    def _generate_test_cases(self):
        """Generate test cases at different difficulties."""
        self.test_cases = {
            Difficulty.EASY: [],
            Difficulty.MEDIUM: [],
            Difficulty.HARD: []
        }
        
        # Easy: Small sets, small numbers
        for i in range(15):
            n = random.randint(4, 8)
            numbers = [random.randint(1, 100) for _ in range(n)]
            
            # Compute optimal via exhaustive search
            optimal = self._optimal_exhaustive(numbers)
            
            self.test_cases[Difficulty.EASY].append(TestCase(
                id=f"easy_{i}",
                data=PartitionInstance(numbers, optimal),
                difficulty=Difficulty.EASY,
                expected=optimal
            ))
        
        # Medium: Larger sets
        for i in range(10):
            n = random.randint(15, 25)
            numbers = [random.randint(1, 1000) for _ in range(n)]
            
            # Use KK as approximate optimal
            optimal = self._karmarkar_karp(numbers)
            
            self.test_cases[Difficulty.MEDIUM].append(TestCase(
                id=f"medium_{i}",
                data=PartitionInstance(numbers, optimal),
                difficulty=Difficulty.MEDIUM,
                expected=optimal
            ))
        
        # Hard: Large sets with large numbers
        for i in range(5):
            n = random.randint(40, 60)
            numbers = [random.randint(1, 100000) for _ in range(n)]
            
            optimal = self._karmarkar_karp(numbers)
            
            self.test_cases[Difficulty.HARD].append(TestCase(
                id=f"hard_{i}",
                data=PartitionInstance(numbers, optimal),
                difficulty=Difficulty.HARD,
                expected=optimal
            ))
    
    def _optimal_exhaustive(self, numbers: list[int]) -> int:
        """Exhaustive search for small instances."""
        n = len(numbers)
        total = sum(numbers)
        best_diff = total
        
        for mask in range(1 << n):
            subset_sum = sum(numbers[i] for i in range(n) if mask & (1 << i))
            diff = abs(total - 2 * subset_sum)
            best_diff = min(best_diff, diff)
        
        return best_diff
    
    def _karmarkar_karp(self, numbers: list[int]) -> int:
        """Karmarkar-Karp differencing algorithm."""
        heap = [-x for x in numbers]  # Max heap via negation
        heapq.heapify(heap)
        
        while len(heap) > 1:
            largest = -heapq.heappop(heap)
            second = -heapq.heappop(heap)
            diff = largest - second
            if diff > 0:
                heapq.heappush(heap, -diff)
        
        return -heap[0] if heap else 0
    
    def describe(self) -> str:
        return """
# Number Partition Problem

Given a set S of n positive integers, partition into two subsets A and B
such that |sum(A) - sum(B)| is minimized.

## Properties
- NP-hard (reduction from Subset Sum)
- Perfect partition (diff=0) may not exist
- Many local minima trap simple hill-climbing

## Instance Sizes
- Easy: 4-8 numbers, values 1-100
- Medium: 15-25 numbers, values 1-1000
- Hard: 40-60 numbers, values 1-100000

## Evaluation
- Score = 1 / (1 + diff / optimal_diff)  if optimal_diff > 0
- Score = 1.0 if diff == 0 (perfect partition)
- Higher is better

## Known Good Approaches
- Karmarkar-Karp differencing
- Complete Karmarkar-Karp (CKK)
- Simulated annealing
- Branch and bound with LDS
"""
    
    def describe_current_approach(self) -> str:
        return """
Current: Greedy differencing (simple form of KK)
- Repeatedly pair largest with smallest
- Assign to opposite partitions
- Works okay but misses better solutions
"""
    
    def get_test_cases(self, difficulty: Difficulty) -> list[TestCase]:
        return self.test_cases.get(difficulty, [])
    
    def evaluate(self, solution: 'PartitionSolution', test_case: TestCase) -> EvalResult:
        instance = test_case.data
        
        start = time.time()
        try:
            partition = solution.execute(test_case)
            elapsed = time.time() - start
            
            # Validate partition
            all_indices = set(partition[0]) | set(partition[1])
            if len(all_indices) != len(instance.numbers):
                return EvalResult(
                    test_case_id=test_case.id,
                    score=0.0,
                    time_taken=elapsed,
                    success=False,
                    details={'error': 'Invalid partition'}
                )
            
            sum_a = sum(instance.numbers[i] for i in partition[0])
            sum_b = sum(instance.numbers[i] for i in partition[1])
            diff = abs(sum_a - sum_b)
            
            # Score calculation
            optimal = test_case.expected
            if diff == 0:
                score = 1.0
            elif optimal == 0:
                score = 1.0 / (1 + diff)
            else:
                # Score based on how close to optimal
                score = optimal / (optimal + diff) if diff > optimal else 1.0
            
            return EvalResult(
                test_case_id=test_case.id,
                score=score,
                time_taken=elapsed,
                success=True,
                details={'diff': diff, 'optimal': optimal}
            )
            
        except Exception as e:
            return EvalResult(
                test_case_id=test_case.id,
                score=0.0,
                time_taken=time.time() - start,
                success=False,
                details={'error': str(e)}
            )


class PartitionSolution(Solution):
    """Solution strategies for number partition."""
    
    def __init__(self, strategy: str = "greedy"):
        self.strategy = strategy
        self.params = {}
    
    def clone(self) -> 'PartitionSolution':
        s = PartitionSolution(self.strategy)
        s.params = self.params.copy()
        return s
    
    def apply_modification(self, modification: dict) -> bool:
        approach = modification.get('approach', '').lower()
        
        if 'karmarkar' in approach or 'kk' in approach:
            self.strategy = 'karmarkar_karp'
            return True
        elif 'annealing' in approach or 'simulated' in approach:
            self.strategy = 'simulated_annealing'
            return True
        elif 'genetic' in approach:
            self.strategy = 'genetic'
            return True
        elif 'dynamic' in approach or 'dp' in approach:
            self.strategy = 'dp'
            return True
        elif 'hill' in approach:
            self.strategy = 'hill_climbing'
            return True
        elif 'random' in approach:
            self.strategy = 'random_restart'
            return True
        elif 'complete' in approach or 'ckk' in approach:
            self.strategy = 'complete_kk'
            return True
        
        return True
    
    def execute(self, test_case: TestCase) -> tuple[list[int], list[int]]:
        """Returns (partition_A_indices, partition_B_indices)."""
        instance = test_case.data
        
        method = getattr(self, f'_{self.strategy}', self._greedy)
        return method(instance.numbers)
    
    def _greedy(self, numbers: list[int]) -> tuple[list[int], list[int]]:
        """Simple greedy: assign to partition with smaller sum."""
        n = len(numbers)
        sorted_indices = sorted(range(n), key=lambda i: numbers[i], reverse=True)
        
        A, B = [], []
        sum_a, sum_b = 0, 0
        
        for i in sorted_indices:
            if sum_a <= sum_b:
                A.append(i)
                sum_a += numbers[i]
            else:
                B.append(i)
                sum_b += numbers[i]
        
        return A, B
    
    def _karmarkar_karp(self, numbers: list[int]) -> tuple[list[int], list[int]]:
        """Karmarkar-Karp differencing with partition tracking."""
        n = len(numbers)
        
        # Heap entries: (-value, set_of_indices, sign_of_indices)
        # signs: True means in A, False means in B
        heap = [(-numbers[i], frozenset([i]), {i: True}) for i in range(n)]
        heapq.heapify(heap)
        
        while len(heap) > 1:
            val1, set1, signs1 = heapq.heappop(heap)
            val2, set2, signs2 = heapq.heappop(heap)
            
            diff = (-val1) - (-val2)
            combined_set = set1 | set2
            
            # Merge signs, flipping set2's signs
            combined_signs = dict(signs1)
            for idx, sign in signs2.items():
                combined_signs[idx] = not sign
            
            if diff > 0:
                heapq.heappush(heap, (-diff, combined_set, combined_signs))
            elif combined_set:
                heapq.heappush(heap, (0, combined_set, combined_signs))
        
        if not heap:
            return list(range(n)), []
        
        _, _, final_signs = heap[0]
        
        A = [i for i in range(n) if final_signs.get(i, True)]
        B = [i for i in range(n) if not final_signs.get(i, True)]
        
        return A, B
    
    def _simulated_annealing(self, numbers: list[int]) -> tuple[list[int], list[int]]:
        """Simulated annealing approach."""
        n = len(numbers)
        
        # Start with greedy
        A, B = self._greedy(numbers)
        assignment = [0] * n
        for i in A:
            assignment[i] = 0
        for i in B:
            assignment[i] = 1
        
        sum_a = sum(numbers[i] for i in A)
        sum_b = sum(numbers[i] for i in B)
        best_diff = abs(sum_a - sum_b)
        best_assignment = assignment[:]
        
        temp = sum(numbers) / 4
        cooling = 0.995
        
        for _ in range(10000):
            # Random move: flip one element
            i = random.randint(0, n - 1)
            
            if assignment[i] == 0:
                new_diff = abs((sum_a - numbers[i]) - (sum_b + numbers[i]))
            else:
                new_diff = abs((sum_a + numbers[i]) - (sum_b - numbers[i]))
            
            old_diff = abs(sum_a - sum_b)
            delta = new_diff - old_diff
            
            if delta < 0 or random.random() < pow(2.718, -delta / max(temp, 0.001)):
                if assignment[i] == 0:
                    assignment[i] = 1
                    sum_a -= numbers[i]
                    sum_b += numbers[i]
                else:
                    assignment[i] = 0
                    sum_a += numbers[i]
                    sum_b -= numbers[i]
                
                if abs(sum_a - sum_b) < best_diff:
                    best_diff = abs(sum_a - sum_b)
                    best_assignment = assignment[:]
            
            temp *= cooling
        
        A = [i for i in range(n) if best_assignment[i] == 0]
        B = [i for i in range(n) if best_assignment[i] == 1]
        return A, B
    
    def _hill_climbing(self, numbers: list[int]) -> tuple[list[int], list[int]]:
        """Hill climbing with random restarts."""
        n = len(numbers)
        best_overall = (float('inf'), [], [])
        
        for _ in range(50):  # Restarts
            # Random start
            assignment = [random.randint(0, 1) for _ in range(n)]
            sum_a = sum(numbers[i] for i in range(n) if assignment[i] == 0)
            sum_b = sum(numbers[i] for i in range(n) if assignment[i] == 1)
            
            improved = True
            while improved:
                improved = False
                for i in range(n):
                    if assignment[i] == 0:
                        new_diff = abs((sum_a - numbers[i]) - (sum_b + numbers[i]))
                    else:
                        new_diff = abs((sum_a + numbers[i]) - (sum_b - numbers[i]))
                    
                    if new_diff < abs(sum_a - sum_b):
                        if assignment[i] == 0:
                            assignment[i] = 1
                            sum_a -= numbers[i]
                            sum_b += numbers[i]
                        else:
                            assignment[i] = 0
                            sum_a += numbers[i]
                            sum_b -= numbers[i]
                        improved = True
            
            diff = abs(sum_a - sum_b)
            if diff < best_overall[0]:
                A = [i for i in range(n) if assignment[i] == 0]
                B = [i for i in range(n) if assignment[i] == 1]
                best_overall = (diff, A, B)
        
        return best_overall[1], best_overall[2]
    
    def _complete_kk(self, numbers: list[int]) -> tuple[list[int], list[int]]:
        """Complete Karmarkar-Karp (explores decision tree)."""
        n = len(numbers)
        if n > 30:  # Fall back for large instances
            return self._karmarkar_karp(numbers)
        
        best = [float('inf')]
        best_partition = [None]
        
        def ckk(heap, depth=0):
            if len(heap) <= 1:
                diff = -heap[0][0] if heap else 0
                if diff < best[0]:
                    best[0] = diff
                    # Reconstruct partition
                    if heap:
                        _, _, signs = heap[0]
                        A = [i for i in range(n) if signs.get(i, True)]
                        B = [i for i in range(n) if not signs.get(i, True)]
                        best_partition[0] = (A, B)
                return
            
            # Sort for consistency
            heap = sorted(heap, reverse=True)
            
            val1, set1, signs1 = heap[0]
            val2, set2, signs2 = heap[1]
            rest = heap[2:]
            
            # Branch 1: Difference (standard KK move)
            diff = (-val1) - (-val2)
            combined_set = set1 | set2
            combined_signs = dict(signs1)
            for idx, sign in signs2.items():
                combined_signs[idx] = not sign
            
            new_heap = rest[:]
            if diff > 0:
                new_heap.append((-diff, combined_set, combined_signs))
            elif combined_set:
                new_heap.append((0, combined_set, combined_signs))
            ckk(new_heap, depth + 1)
            
            # Branch 2: Sum (both in same partition)
            total = (-val1) + (-val2)
            combined_signs2 = dict(signs1)
            combined_signs2.update(signs2)
            
            new_heap2 = rest[:]
            new_heap2.append((-total, combined_set, combined_signs2))
            ckk(new_heap2, depth + 1)
        
        initial_heap = [(-numbers[i], frozenset([i]), {i: True}) for i in range(n)]
        ckk(initial_heap)
        
        if best_partition[0]:
            return best_partition[0]
        return list(range(n)), []
    
    def serialize(self) -> str:
        return f"{self.strategy}:{self.params}"


def demo():
    """Demonstration of partition problem solver."""
    print("="*60)
    print("Number Partition Problem Demo")
    print("="*60)
    
    problem = PartitionProblem(seed=42)
    
    # Test different strategies
    strategies = ['greedy', 'karmarkar_karp', 'simulated_annealing', 'hill_climbing']
    
    for strat in strategies:
        print(f"\n--- Strategy: {strat} ---")
        solution = PartitionSolution(strategy=strat)
        
        total_score = 0
        count = 0
        
        for difficulty in [Difficulty.EASY, Difficulty.MEDIUM]:
            cases = problem.get_test_cases(difficulty)
            for tc in cases[:5]:
                result = problem.evaluate(solution, tc)
                total_score += result.score
                count += 1
        
        avg_score = total_score / count
        print(f"Average score: {avg_score:.4f}")


if __name__ == "__main__":
    demo()
