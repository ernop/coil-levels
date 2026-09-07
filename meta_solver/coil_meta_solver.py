"""Coil-specific configuration exploration with real bounded C# evaluation.

Idea providers propose explicit JSON search settings. The shared bridge checks
actual saved boards and independently validates every claimed solution.
"""

import os
import sys
import json
import time
import hashlib
import subprocess
import re
from dataclasses import dataclass, field, asdict
from typing import Any, Optional, Callable
from enum import Enum, auto
from collections import defaultdict
from pathlib import Path
from meta_solver.coil_bridge import CoilBridge, SearchConfig, load_board, search_score
from meta_solver.examples.coil_integration import CoilProblem, CoilSolution
from meta_solver.core import Comparator, EvalResult, Difficulty

# ============================================================================
# PROBLEM TAXONOMY: What kinds of improvements are possible?
# ============================================================================

class ImprovementCategory(Enum):
    """Categories of possible improvements to explore."""
    
    # Search strategy
    SEARCH_ORDER = auto()          # What order to try things
    SEARCH_PRUNING = auto()        # What to skip trying
    SEARCH_HEURISTIC = auto()      # How to score candidates
    
    # Dead-end handling
    DEADEND_DETECTION = auto()     # Recognizing dead-ends early
    DEADEND_AVOIDANCE = auto()     # Not creating dead-ends
    DEADEND_RECOVERY = auto()      # Getting out of dead-ends
    
    # Path structure
    PATH_REPRESENTATION = auto()   # How to represent the path
    PATH_MANIPULATION = auto()     # Operations on paths
    PATH_ANALYSIS = auto()         # Understanding path properties
    
    # Problem transformation
    SYMMETRY = auto()              # Exploiting symmetries
    DECOMPOSITION = auto()         # Breaking into subproblems
    PREPROCESSING = auto()         # Analyzing board before solving
    
    # Performance
    DATA_STRUCTURES = auto()       # Better data structures
    CACHING = auto()               # Memoization strategies
    PARALLELISM = auto()           # Parallel exploration
    
    # Learning
    PATTERN_LEARNING = auto()      # Learn from solved levels
    TRANSFER = auto()              # Apply insights across levels
    
    # Meta
    PARAMETER_TUNING = auto()      # Tune existing parameters
    ENSEMBLE = auto()              # Combine multiple approaches


@dataclass
class ExplorationBudget:
    """Track how much we've explored each category."""
    attempts: dict[ImprovementCategory, int] = field(default_factory=lambda: defaultdict(int))
    successes: dict[ImprovementCategory, int] = field(default_factory=lambda: defaultdict(int))
    last_success: dict[ImprovementCategory, float] = field(default_factory=dict)
    
    def record_attempt(self, category: ImprovementCategory, success: bool):
        self.attempts[category] += 1
        if success:
            self.successes[category] += 1
            self.last_success[category] = time.time()
    
    def get_priority(self, category: ImprovementCategory) -> float:
        """Higher priority = less explored or more successful."""
        attempts = self.attempts[category] + 1
        successes = self.successes[category]
        
        # UCB1-like exploration bonus
        exploration_bonus = 2.0 / (attempts ** 0.5)
        
        # Success rate
        success_rate = successes / attempts if attempts > 0 else 0.5
        
        # Recency bonus (try categories that haven't worked recently)
        recency = time.time() - self.last_success.get(category, 0)
        recency_bonus = min(recency / 3600, 1.0)  # Cap at 1 hour
        
        return exploration_bonus + success_rate * 0.5 + recency_bonus * 0.3
    
    def get_underexplored(self, min_attempts: int = 3) -> list[ImprovementCategory]:
        """Get categories that haven't been tried enough."""
        return [cat for cat in ImprovementCategory 
                if self.attempts[cat] < min_attempts]


# ============================================================================
# FAILURE ANALYSIS: Understanding why levels fail
# ============================================================================

@dataclass
class LevelFailure:
    """Detailed analysis of why a level failed."""
    level_id: str
    dimensions: tuple[int, int]
    wall_density: float
    time_spent: float
    cells_visited: int
    total_cells: int
    coverage: float
    
    # Failure characteristics
    stuck_position: Optional[tuple[int, int]] = None
    available_directions: int = 0
    path_length: int = 0
    backtrack_count: int = 0
    
    # Derived analysis
    failure_pattern: Optional[str] = None  # e.g., "corner_trap", "corridor_deadend"
    similar_to: list[str] = field(default_factory=list)


@dataclass
class FailureAnalysis:
    """Aggregate analysis of multiple failures."""
    failures: list[LevelFailure] = field(default_factory=list)
    
    def add_failure(self, failure: LevelFailure):
        self.failures.append(failure)
    
    def get_common_patterns(self) -> dict[str, int]:
        """Find common failure patterns."""
        patterns = defaultdict(int)
        for f in self.failures:
            if f.failure_pattern:
                patterns[f.failure_pattern] += 1
        return dict(patterns)
    
    def get_hardest_characteristics(self) -> dict[str, Any]:
        """What makes levels hard?"""
        if not self.failures:
            return {}
        
        return {
            'avg_wall_density': sum(f.wall_density for f in self.failures) / len(self.failures),
            'avg_coverage_at_fail': (sum(f.coverage for f in self.failures if f.coverage is not None) / len([f for f in self.failures if f.coverage is not None])) if any(f.coverage is not None for f in self.failures) else None,
            'common_patterns': self.get_common_patterns(),
        }
    
    def format_for_llm(self) -> str:
        """Format failure analysis for LLM consumption."""
        if not self.failures:
            return "No failures recorded yet."
        
        recent = self.failures[-5:]
        lines = ["## Recent Failures\n"]
        
        for f in recent:
            lines.append(f"- **Level {f.level_id}** ({f.dimensions[0]}x{f.dimensions[1]})")
            coverage = f"{f.coverage:.1%}" if f.coverage is not None else "unavailable"
            lines.append(f"  Coverage: {coverage}, Time: {f.time_spent:.2f}s")
            if f.failure_pattern:
                lines.append(f"  Pattern: {f.failure_pattern}")
            lines.append("")
        
        chars = self.get_hardest_characteristics()
        lines.append("## Failure Patterns")
        for pattern, count in chars.get('common_patterns', {}).items():
            lines.append(f"- {pattern}: {count} occurrences")
        
        return "\n".join(lines)


# ============================================================================
# IDEA MANAGEMENT: Avoiding repetitive loops
# ============================================================================

@dataclass
class Idea:
    """A proposed improvement."""
    id: str
    category: ImprovementCategory
    source: str  # Which LLM generated it
    raw_text: str
    extracted_approach: str
    
    # Semantic fingerprint for deduplication
    semantic_hash: str = ""
    
    # Tracking
    attempted: bool = False
    success: Optional[bool] = None
    score_delta: float = 0.0
    
    @staticmethod
    def compute_semantic_hash(text: str) -> str:
        """Fingerprint concrete JSON controls or normalized text; this is lexical, not semantic."""
        snippets = re.findall(r'\{[^{}]*\}', text)
        if len(snippets) == 1:
            try:
                normalized = json.dumps(json.loads(snippets[0]), sort_keys=True)
                return hashlib.sha256(normalized.encode()).hexdigest()[:16]
            except json.JSONDecodeError:
                pass
        normalized = re.sub(r'\s+', ' ', text.lower()).strip()
        return hashlib.sha256(normalized.encode()).hexdigest()[:16]



class IdeaMemory:
    """Memory of tried ideas to avoid repetition."""
    
    def __init__(self, similarity_threshold: float = 0.7):
        self.ideas: list[Idea] = []
        self.semantic_hashes: set[str] = set()
        self.similarity_threshold = similarity_threshold
        
        # Category-based tracking
        self.category_ideas: dict[ImprovementCategory, list[Idea]] = defaultdict(list)
    
    def is_novel(self, idea: Idea) -> bool:
        """Check if an idea is sufficiently novel."""
        # Exact semantic hash match
        if idea.semantic_hash in self.semantic_hashes:
            return False
        
        if re.search(r'\{[^{}]*\}', idea.raw_text):
            return True  # Distinct concrete parameter configurations must remain testable.

        # Check similarity within category
        category_ideas = self.category_ideas[idea.category]
        for existing in category_ideas[-10:]:  # Check recent ideas in same category
            if self._text_similarity(idea.extracted_approach, existing.extracted_approach) > self.similarity_threshold:
                return False
        
        return True
    
    def add(self, idea: Idea):
        """Add an idea to memory."""
        self.ideas.append(idea)
        self.semantic_hashes.add(idea.semantic_hash)
        self.category_ideas[idea.category].append(idea)
    
    def get_failed_approaches(self, category: Optional[ImprovementCategory] = None) -> list[str]:
        """Get descriptions of failed approaches."""
        ideas = self.ideas
        if category:
            ideas = self.category_ideas[category]
        
        return [i.extracted_approach for i in ideas if i.attempted and not i.success]
    
    def _text_similarity(self, a: str, b: str) -> float:
        """Simple Jaccard similarity on words."""
        words_a = set(a.lower().split())
        words_b = set(b.lower().split())
        if not words_a or not words_b:
            return 0.0
        return len(words_a & words_b) / len(words_a | words_b)


# ============================================================================
# MULTI-TIER EVALUATION: Fast rejection, thorough validation
# ============================================================================

@dataclass
class EvaluationTier:
    """A tier in the evaluation pipeline."""
    name: str
    levels: list[str]  # Level IDs or paths
    timeout_per_level: float
    min_pass_rate: float  # Must pass this many to proceed
    weight: float  # Importance in final score


class MultiTierEvaluator:
    """Evaluate real boards in nonempty size strata or explicitly configured tiers."""
    
    def __init__(self, solver_path: str, levels_dir: str, *, node_budget: int = 100000,
                 tiers: list[EvaluationTier] | None = None):
        self.solver_path = solver_path
        self.levels_dir = Path(levels_dir)
        self.node_budget = node_budget
        self.problem = CoilProblem(levels_dir, bridge=CoilBridge(solver_path, node_budget=node_budget))
        if tiers is None:
            self.tiers = [EvaluationTier(d.value, [c.metadata['path'] for c in self.problem.get_test_cases(d)],
                                         timeout, 0.0, 1.0)
                          for d, timeout in zip(Difficulty, (5.0, 10.0, 15.0))
                          if self.problem.get_test_cases(d)]
        else:
            self.tiers = tiers
        if not self.tiers or any(not t.levels or t.weight <= 0 or not 0 <= t.min_pass_rate <= 1 for t in self.tiers):
            raise ValueError('Evaluation tiers must be nonempty with positive weights and valid pass rates')
        if len({t.name for t in self.tiers}) != len(self.tiers):
            raise ValueError('Duplicate tier names')
        paths = [str(Path(path).resolve()) for t in self.tiers for path in t.levels]
        if len(paths) != len(set(paths)):
            raise ValueError('A board cannot appear twice in the evaluation tiers')
        self.baseline_results = {}

    def set_baseline(self, results: dict[str, dict]):
        expected = {path for t in self.tiers for path in t.levels}
        if set(results) != expected:
            raise ValueError('Baseline must cover every configured level exactly once')
        self.baseline_results = results.copy()

    def evaluate(self, solver_config: dict, stop_on_regression: bool = True,
                 stop_on_failure: bool = True) -> tuple[float, dict]:
        SearchConfig.from_dict(solver_config)
        all_results = {}
        weighted_score = 0.0
        weight = sum(t.weight for t in self.tiers)
        for tier in self.tiers:
            results = self._evaluate_tier(tier, solver_config)
            all_results[tier.name] = results
            passed = sum(r['passed'] for r in results.values()) / len(results)
            weighted_score += sum(search_score(r) for r in results.values()) / len(results) * tier.weight
            regressions = self._detect_regressions(results) if stop_on_regression else []
            if regressions or (stop_on_failure and passed < tier.min_pass_rate):
                return weighted_score / weight, {'rejected': True,
                    'reason': f'Regression on {regressions}' if regressions else f'Failed {tier.name} pass threshold',
                    'results': all_results}
        return weighted_score / weight, {'rejected': False, 'results': all_results}

    def _evaluate_tier(self, tier: EvaluationTier, config: dict) -> dict[str, dict]:
        return {path: self._run_solver(path, config, tier.timeout_per_level) for path in tier.levels}

    def _run_solver(self, level_path: str, config: dict, timeout: float) -> dict:
        bridge = CoilBridge(self.solver_path, node_budget=self.node_budget, timeout=timeout)
        board = load_board(level_path, bridge.max_cells)
        result = bridge.evaluate(board, SearchConfig.from_dict(config))
        return result | {'passed': result['solved'], 'time': result['wallSeconds']}

    def _detect_regressions(self, results: dict[str, dict]) -> list[str]:
        return [path for path, result in results.items()
                if self.baseline_results.get(path, {}).get('passed', False) and not result['passed']]


# ============================================================================
# PROMPT ENGINEERING: Getting good ideas from LLMs
# ============================================================================

class CoilPromptBuilder:
    """Builds prompts specifically for the coil problem."""
    
    PROBLEM_DESCRIPTION = """
# The Coil Puzzle

## Problem
A grid-based pathfinding puzzle where you must visit every non-wall cell exactly once.

## Rules
1. Pick any non-wall starting cell
2. Move in one of 4 directions (up/down/left/right)
3. When moving, you SLIDE until hitting a wall, edge, or visited cell
4. Continue until all cells visited (win) or stuck (fail)

## Challenge Scale
- Levels range from 3x3 to 2000x2000
- Brute force estimate for 2000x2000: 304 millennia
- Need ~10-12 orders of magnitude speedup

## What Makes It Hard
- Creating dead-ends is easy and hard to detect early
- The sliding mechanic creates complex dependencies
- Local decisions have global consequences
- Some configurations are unsolvable

## Current State of the Art
- Best known solvers use segment-based approaches
- Key techniques: tweaking (bumping segments), various heuristics
- Best AI iteration reached level 73 (0.018% of goal area)
"""

    INVESTIGATION_PROMPTS = {
        'deadend': """
Investigate dead-end patterns:
1. What board configurations lead to unavoidable dead-ends?
2. Can we detect these configurations early?
3. What local patterns predict global failure?
4. How can we avoid creating dead-ends in the first place?
""",
        'structure': """
Investigate path structure:
1. What properties do successful paths have?
2. Are there invariants that must be maintained?
3. Can we characterize "good" vs "bad" partial paths?
4. What's the minimum information needed to represent a path state?
""",
        'search': """
Investigate search strategies:
1. What's the branching factor at each decision point?
2. How can we order choices to find solutions faster?
3. What bounds/heuristics can prune the search space?
4. Is there a polynomial-time check for solvability?
""",
        'learning': """
Investigate learning opportunities:
1. What patterns appear in solved levels that don't appear in unsolved?
2. Can we learn a heuristic from solved examples?
3. Are there local patterns that transfer across levels?
4. What features correlate with solve time?
""",
    }

    @classmethod
    def build_idea_prompt(cls, 
                         exploration_budget: ExplorationBudget,
                         failure_analysis: FailureAnalysis,
                         idea_memory: IdeaMemory,
                         focus_category: Optional[ImprovementCategory] = None) -> str:
        """Build a prompt for idea generation."""
        
        # Select focus area
        if focus_category:
            focus = focus_category
        else:
            # Pick underexplored or high-priority category
            underexplored = exploration_budget.get_underexplored()
            if underexplored:
                focus = underexplored[0]
            else:
                # Pick by priority
                priorities = [(cat, exploration_budget.get_priority(cat)) 
                              for cat in ImprovementCategory]
                focus = max(priorities, key=lambda x: x[1])[0]
        
        # Get failed approaches in this category
        failed = idea_memory.get_failed_approaches(focus)
        failed_text = "\n".join(f"- {f}" for f in failed[-5:]) if failed else "None yet."
        
        prompt = f"""
{cls.PROBLEM_DESCRIPTION}

## Current Focus Area: {focus.name}

Category description: {cls._get_category_description(focus)}

## What Has Been Tried (and failed):
{failed_text}

## Failure Analysis
{failure_analysis.format_for_llm()}

## Your Task

Generate 1-3 concrete, implementable ideas in the category **{focus.name}**.

For each idea, provide:
1. **Name**: Brief descriptive name
2. **Category**: Confirm it's {focus.name}
3. **Core Insight**: The key observation that motivates this
4. **Implementation Sketch**: How would you implement this? Be specific about:
   - Data structures needed
   - When this triggers
   - Expected complexity
5. **Why Different**: How is this different from what's been tried?
6. **Risk Assessment**: What could go wrong?

Focus on ideas that are:
- Concrete and implementable
- Different from the failed approaches listed
- Likely to provide significant improvement (not just micro-optimizations)
"""
        return prompt

    @classmethod
    def build_investigation_prompt(cls, topic: str) -> str:
        """Build a prompt for investigating a specific aspect."""
        investigation = cls.INVESTIGATION_PROMPTS.get(topic, "")
        
        return f"""
{cls.PROBLEM_DESCRIPTION}

## Investigation: {topic.upper()}

{investigation}

Please provide a detailed analysis with:
1. Mathematical/algorithmic reasoning
2. Concrete examples
3. Actionable insights
4. Potential implementation approaches
"""

    @classmethod
    def _get_category_description(cls, cat: ImprovementCategory) -> str:
        descriptions = {
            ImprovementCategory.SEARCH_ORDER: "How to order exploration of choices",
            ImprovementCategory.SEARCH_PRUNING: "How to eliminate bad choices early",
            ImprovementCategory.SEARCH_HEURISTIC: "How to score/rank partial solutions",
            ImprovementCategory.DEADEND_DETECTION: "How to recognize dead-ends before they're reached",
            ImprovementCategory.DEADEND_AVOIDANCE: "How to make choices that don't create dead-ends",
            ImprovementCategory.DEADEND_RECOVERY: "How to escape from dead-end situations",
            ImprovementCategory.PATH_REPRESENTATION: "How to represent the path/state efficiently",
            ImprovementCategory.PATH_MANIPULATION: "Operations for modifying paths (tweaks, etc.)",
            ImprovementCategory.PATH_ANALYSIS: "Understanding properties of paths",
            ImprovementCategory.SYMMETRY: "Exploiting board/path symmetries",
            ImprovementCategory.DECOMPOSITION: "Breaking the problem into subproblems",
            ImprovementCategory.PREPROCESSING: "Analyzing the board before solving",
            ImprovementCategory.DATA_STRUCTURES: "Better data structures for operations",
            ImprovementCategory.CACHING: "Memoization and caching strategies",
            ImprovementCategory.PARALLELISM: "Parallel/concurrent solving",
            ImprovementCategory.PATTERN_LEARNING: "Learning patterns from solved instances",
            ImprovementCategory.TRANSFER: "Applying insights across different levels",
            ImprovementCategory.PARAMETER_TUNING: "Tuning existing parameters",
            ImprovementCategory.ENSEMBLE: "Combining multiple approaches",
        }
        return descriptions.get(cat, "Unknown category")


# ============================================================================
# SOLUTION ENSEMBLE: Don't throw away working approaches
# ============================================================================

@dataclass
class SolverVariant:
    """A specific solver configuration."""
    id: str
    config: dict
    score: float
    strengths: list[str]  # What levels/patterns it's good at
    weaknesses: list[str]  # What it struggles with
    creation_time: float = field(default_factory=time.time)


class SolutionEnsemble:
    """
    Maintain multiple solver variants.
    
    Key insight: Different approaches work better on different levels.
    Instead of having one "best" solver, maintain a portfolio.
    """
    
    def __init__(self, max_variants: int = 5):
        self.variants: list[SolverVariant] = []
        self.max_variants = max_variants
    
    def add_variant(self, variant: SolverVariant):
        """Add a new variant to the ensemble."""
        # Check if it's diverse enough from existing
        if self._is_diverse(variant):
            self.variants.append(variant)
            
            # Prune if too many
            if len(self.variants) > self.max_variants:
                self._prune()
    
    def _is_diverse(self, new: SolverVariant) -> bool:
        """Check if new variant is sufficiently different."""
        for existing in self.variants:
            # Compare strengths/weaknesses
            overlap = len(set(new.strengths) & set(existing.strengths))
            if overlap > len(new.strengths) * 0.8:
                # Too similar - only keep if significantly better
                if new.score <= existing.score * 1.1:
                    return False
        return True
    
    def _prune(self):
        """Remove lowest-performing, least-diverse variant."""
        if len(self.variants) <= self.max_variants:
            return
        
        # Score variants by performance + diversity contribution
        scores = []
        for i, v in enumerate(self.variants):
            others = self.variants[:i] + self.variants[i+1:]
            diversity = self._diversity_contribution(v, others)
            scores.append((i, v.score * 0.7 + diversity * 0.3))
        
        # Remove lowest scoring
        worst = min(scores, key=lambda x: x[1])[0]
        self.variants.pop(worst)
    
    def _diversity_contribution(self, variant: SolverVariant, others: list[SolverVariant]) -> float:
        """How much diversity does this variant add?"""
        if not others:
            return 1.0
        
        unique_strengths = set(variant.strengths)
        for other in others:
            unique_strengths -= set(other.strengths)
        
        return len(unique_strengths) / max(len(variant.strengths), 1)
    
    def select_for_level(self, level_characteristics: dict) -> SolverVariant:
        """Select best variant for a specific level."""
        tags = set(level_characteristics.get('tags', []))
        return max(self.variants, key=lambda v: (len(tags & set(v.strengths)) - len(tags & set(v.weaknesses)), v.score)) if self.variants else None


# ============================================================================
# MAIN META-SOLVER
# ============================================================================

class CoilMetaSolver:
    """
    Meta-solver for the Coil puzzle.
    
    Orchestrates:
    1. Idea generation from LLMs
    2. Multi-tier evaluation
    3. Solution ensemble management
    4. Failure analysis and learning
    """
    
    def __init__(self,
                 solver_path: str,
                 levels_dir: str,
                 llm_interfaces: list = None):
        
        self.solver_path = solver_path
        self.levels_dir = levels_dir
        self.llms = llm_interfaces or []
        
        # Core components
        self.exploration_budget = ExplorationBudget()
        self.failure_analysis = FailureAnalysis()
        self.idea_memory = IdeaMemory()
        self.solution_ensemble = SolutionEnsemble()
        
        # Evaluator
        self.evaluator = MultiTierEvaluator(solver_path, levels_dir)
        
        # State
        self.iteration = 0
        self.best_level_reached = 0
        self.stuck_count = 0
        self.current_config = {}
        self.last_results = {}
        self.current_score = 0.0
        
    def run(self, max_iterations: int = 100) -> SolverVariant:
        """Run the meta-solving loop."""
        
        print("="*60)
        print("Coil Meta-Solver Starting")
        print("="*60)
        
        if max_iterations > 0 and not self.llms:
            raise ValueError('Supply an idea provider, or use the evaluation CLI without an LLM')
        self._establish_baseline()
        while self.iteration < max_iterations:
            self.iteration += 1
            print(f"\n--- Iteration {self.iteration} ---")
            
            # Check if stuck
            if self.stuck_count > 10:
                print("Stuck! Forcing novelty search...")
                ideas = self._novelty_search()
            else:
                # Normal idea generation
                ideas = self._generate_ideas()
            
            print(f"Generated {len(ideas)} ideas")
            
            # Filter and evaluate
            for idea in ideas:
                if not self.idea_memory.is_novel(idea):
                    print(f"  Skipping non-novel: {idea.extracted_approach[:50]}")
                    continue
                
                print(f"  Testing: {idea.extracted_approach[:50]}")
                
                result = self._evaluate_idea(idea)
                self.idea_memory.add(idea)
                
                if result['success']:
                    print(f"  ✓ Success! Score delta: {result['score_delta']:.3f}")
                    self.stuck_count = 0
                    
                    # Create new variant
                    variant = SolverVariant(
                        id=idea.id,
                        config=result['config'],
                        score=result['score'],
                        strengths=result.get('strengths', []),
                        weaknesses=result.get('weaknesses', [])
                    )
                    self.solution_ensemble.add_variant(variant)
                else:
                    print(f"  ✗ Failed: {result.get('reason', 'Unknown')}")
                
                self.exploration_budget.record_attempt(idea.category, result['success'])
            
            # Update failure analysis
            self._update_failure_analysis()
            
            # Check progress
            if not any(idea.success for idea in ideas if idea.attempted):
                self.stuck_count += 1
            
            # Checkpoint
            if self.iteration % 10 == 0:
                self._checkpoint()
        
        return self.solution_ensemble.select_for_level({})
    
    def _generate_ideas(self) -> list[Idea]:
        """Generate ideas from LLMs."""
        ideas = []
        
        prompt = CoilPromptBuilder.build_idea_prompt(
            self.exploration_budget,
            self.failure_analysis,
            self.idea_memory
        ) + "\n" + self.evaluator.problem.describe()
        
        for llm in self.llms:
            try:
                response = llm.generate(prompt, temperature=0.7)
                parsed = self._parse_ideas(response, llm.name)
                ideas.extend(parsed)
            except Exception as e:
                print(f"Error from {llm.name}: {e}")
        
        return ideas
    
    def _novelty_search(self) -> list[Idea]:
        """Force exploration of underexplored categories."""
        underexplored = self.exploration_budget.get_underexplored()
        
        ideas = []
        for category in underexplored[:2]:  # Try 2 underexplored categories
            prompt = CoilPromptBuilder.build_idea_prompt(
                self.exploration_budget,
                self.failure_analysis,
                self.idea_memory,
                focus_category=category
            ) + "\n" + self.evaluator.problem.describe()
            
            for llm in self.llms[:1]:  # Just use first LLM
                try:
                    response = llm.generate(prompt, temperature=1.0)  # Higher temp for diversity
                    parsed = self._parse_ideas(response, llm.name)
                    ideas.extend(parsed)
                except Exception as e:
                    print(f"Error: {e}")
        
        return ideas
    
    def _parse_ideas(self, response: str, source: str) -> list[Idea]:
        """Parse LLM response into structured ideas."""
        ideas = []
        
        # Split on idea markers
        sections = re.split(r'\n(?=\d+\.|##|###|\*\*\d)', response)
        
        for section in sections:
            if not section.strip():
                continue
            
            # Extract name
            name_match = re.search(r'\*\*(?:Name)?[:\s]*([^*\n]+)\*\*', section)
            name = name_match.group(1).strip() if name_match else "Unnamed"
            
            # Determine category
            category = self._infer_category(section)
            
            # Compute semantic hash
            semantic_hash = Idea.compute_semantic_hash(section)
            
            idea = Idea(
                id=hashlib.md5(f"{source}:{section[:100]}".encode()).hexdigest()[:12],
                category=category,
                source=source,
                raw_text=section,
                extracted_approach=name,
                semantic_hash=semantic_hash
            )
            ideas.append(idea)
        
        return ideas
    
    def _infer_category(self, text: str) -> ImprovementCategory:
        """Infer the improvement category from idea text."""
        text_lower = text.lower()
        
        # Simple keyword matching
        if re.search(r'dead[ -]?end|stuck|trap', text_lower):
            if 'detect' in text_lower:
                return ImprovementCategory.DEADEND_DETECTION
            elif 'avoid' in text_lower:
                return ImprovementCategory.DEADEND_AVOIDANCE
            else:
                return ImprovementCategory.DEADEND_RECOVERY
        
        if any(w in text_lower for w in ['prune', 'skip', 'eliminate']):
            return ImprovementCategory.SEARCH_PRUNING
        
        if any(w in text_lower for w in ['order', 'priority', 'first']):
            return ImprovementCategory.SEARCH_ORDER
        
        if any(w in text_lower for w in ['heuristic', 'score', 'estimate']):
            return ImprovementCategory.SEARCH_HEURISTIC
        
        if any(w in text_lower for w in ['symmetr', 'mirror', 'rotat']):
            return ImprovementCategory.SYMMETRY
        
        if any(w in text_lower for w in ['cache', 'memo', 'store']):
            return ImprovementCategory.CACHING
        
        if any(w in text_lower for w in ['parallel', 'concurrent', 'thread']):
            return ImprovementCategory.PARALLELISM
        
        if any(w in text_lower for w in ['learn', 'pattern', 'train']):
            return ImprovementCategory.PATTERN_LEARNING
        
        # Default
        return ImprovementCategory.SEARCH_HEURISTIC
    
    @staticmethod
    def _flatten(details: dict) -> dict:
        return {path: result for tier in details['results'].values() for path, result in tier.items()}

    def _establish_baseline(self):
        if self.evaluator.baseline_results:
            return
        self.current_score, details = self.evaluator.evaluate(self.current_config, stop_on_regression=False, stop_on_failure=False)
        self.last_results = self._flatten(details)
        self.evaluator.set_baseline(self.last_results)
        self.solution_ensemble.add_variant(SolverVariant('baseline', self.current_config.copy(), self.current_score, [], []))
        self._update_failure_analysis()

    def _evaluate_idea(self, idea: Idea) -> dict:
        self._establish_baseline()
        idea.attempted = True
        idea.success = False
        candidate = CoilSolution(self.current_config)
        if not candidate.apply_modification({'raw_text': idea.raw_text}):
            return {'success': False, 'reason': 'No supported, changed JSON search configuration', 'score': self.current_score,
                    'score_delta': 0.0, 'config': self.current_config.copy()}
        config = asdict(candidate.config)
        score, details = self.evaluator.evaluate(config)
        self.last_results = self._flatten(details)
        def evaluations(results):
            return [EvalResult(path, search_score(r), r['time'], r['passed'], r) for path, r in results.items()]
        accepted, reason, delta = Comparator().compare(evaluations(self.evaluator.baseline_results), evaluations(self.last_results))
        accepted = accepted and not details['rejected']
        if accepted:
            self.current_config = config
            self.current_score = score
            self.evaluator.set_baseline(self.last_results)
        idea.success, idea.score_delta = accepted, delta
        self._update_failure_analysis()
        return {'success': accepted, 'reason': details.get('reason', reason), 'score': score,
                'score_delta': delta, 'config': config,
                'strengths': [path for path, r in self.last_results.items() if r['passed']],
                'weaknesses': [path for path, r in self.last_results.items() if not r['passed']]}

    def _update_failure_analysis(self):
        self.failure_analysis = FailureAnalysis()
        for path, result in self.last_results.items():
            if result['passed']:
                continue
            board = load_board(path)
            self.failure_analysis.add_failure(LevelFailure(path, (board.width, board.height),
                1 - board.open_cells / len(board.cells), result['time'], result.get('bestVisited'), board.open_cells,
                result.get('coverage'), failure_pattern=result['status']))

    def _checkpoint(self):
        directory = Path(__file__).resolve().parents[1] / 'output/meta-solver'
        directory.mkdir(parents=True, exist_ok=True)
        state = {'iteration': self.iteration, 'config': self.current_config, 'score': self.current_score,
                 'results': self.last_results, 'ideas': [dict(id=i.id, attempted=i.attempted, success=i.success,
                     scoreDelta=i.score_delta, approach=i.extracted_approach) for i in self.idea_memory.ideas]}
        pending = directory / 'checkpoint.pending.json'
        pending.write_text(json.dumps(state, indent=2, allow_nan=False) + '\n')
        pending.replace(directory / 'checkpoint.json')


# ============================================================================
# ENTRY POINT
# ============================================================================

def main():
    """Demo the coil meta-solver structure."""
    print("Coil Meta-Solver Framework")
    print("=" * 60)
    
    # Show the exploration categories
    print("\nImprovement Categories to Explore:")
    for cat in ImprovementCategory:
        desc = CoilPromptBuilder._get_category_description(cat)
        print(f"  {cat.name}: {desc}")
    
    # Show investigation topics
    print("\nInvestigation Topics:")
    for topic in CoilPromptBuilder.INVESTIGATION_PROMPTS:
        print(f"  - {topic}")
    
    # Show sample prompt
    print("\n" + "=" * 60)
    print("Sample Idea Generation Prompt:")
    print("=" * 60)
    
    budget = ExplorationBudget()
    failures = FailureAnalysis()
    memory = IdeaMemory()
    
    prompt = CoilPromptBuilder.build_idea_prompt(budget, failures, memory)
    print(prompt[:2000] + "..." if len(prompt) > 2000 else prompt)


if __name__ == "__main__":
    main()
