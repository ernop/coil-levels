"""
Meta-Solver Main Orchestrator

Coordinates the idea generation, evaluation, and incorporation loop.
"""

import time
import re
from dataclasses import dataclass, field
from typing import Optional
from concurrent.futures import ThreadPoolExecutor, as_completed

from .core import (
    Problem, Solution, LLMInterface, History, Comparator,
    PromptBuilder, Idea, Attempt, EvalResult, TestCase, Difficulty
)


@dataclass
class MetaSolverConfig:
    """Configuration for the meta-solver."""
    max_iterations: int = 100
    ideas_per_iteration: int = 3
    max_parallel_evals: int = 4
    
    # Evaluation settings
    easy_first: bool = True  # Test on easy cases before hard
    min_easy_score: float = 0.8  # Must pass easy cases to proceed
    
    # Pre-critique settings
    use_pre_critique: bool = True
    min_critique_score: float = 0.4  # Skip ideas below this
    
    # Temperature settings for diversity
    temperatures: list[float] = field(default_factory=lambda: [0.3, 0.7, 1.0])
    
    # Stopping conditions
    target_score: float = 1.0  # Stop if we reach this
    patience: int = 20  # Stop after this many iterations without improvement
    
    # Logging
    verbose: bool = True
    checkpoint_every: int = 10


class IdeaExtractor:
    """Extracts structured ideas from LLM responses."""
    
    @staticmethod
    def extract_ideas(response: str, source_llm: str) -> list[Idea]:
        """Parse LLM response into structured ideas."""
        ideas = []
        
        # Split on common section markers
        # Look for patterns like "## Idea", "**Idea", "1.", "Idea 1:", etc.
        section_pattern = r'(?:^|\n)(?:#{1,3}\s*(?:Idea\s*\d*)|(?:\d+\.)\s*|\*\*Idea[^*]*\*\*)'
        
        # Try splitting on "Idea" headers first
        sections = re.split(r'\n(?=#{1,3}\s*Idea|\*\*Idea)', response)
        
        # If that didn't work well, try splitting on numbered items
        if len(sections) < 2:
            sections = re.split(r'\n(?=\d+\.\s)', response)
        
        for i, section in enumerate(sections):
            section = section.strip()
            if len(section) < 30:  # Skip tiny sections
                continue
            
            # Skip sections that are just headers without content
            if section.count('\n') < 1 and '**' not in section and ':' not in section:
                continue
            
            # Extract confidence if mentioned
            confidence = 0.5  # Default
            conf_match = re.search(r'confidence[:\s]*(low|medium|high)', section.lower())
            if conf_match:
                confidence = {'low': 0.3, 'medium': 0.6, 'high': 0.85}[conf_match.group(1)]
            
            # Try to extract the idea name from various formats
            name = None
            
            # Try: **Idea Name**: Something
            name_match = re.search(r'\*\*Idea\s*(?:Name)?[:\s]*\*\*[:\s]*([^\n*]+)', section)
            if name_match:
                name = name_match.group(1).strip()
            
            # Try: ## Idea 1: Something
            if not name:
                name_match = re.search(r'#+\s*Idea\s*\d*[:\s]*([^\n]+)', section)
                if name_match:
                    name = name_match.group(1).strip()
            
            # Try: First line after stripping markers
            if not name:
                first_line = section.split('\n')[0]
                # Remove markdown formatting
                first_line = re.sub(r'[#*]+', '', first_line).strip()
                first_line = re.sub(r'^\d+\.\s*', '', first_line).strip()
                if len(first_line) > 5:
                    name = first_line[:60]
            
            if not name:
                name = f"Idea-{i}"
            
            idea = Idea(
                id=Idea.generate_id(section, source_llm),
                source_llm=source_llm,
                raw_text=section,
                extracted_approach=name,
                confidence=confidence
            )
            ideas.append(idea)
        
        # If no ideas extracted, treat whole response as one idea
        if not ideas and len(response.strip()) > 50:
            ideas.append(Idea(
                id=Idea.generate_id(response, source_llm),
                source_llm=source_llm,
                raw_text=response,
                extracted_approach="Extracted idea",
                confidence=0.5
            ))
        
        return ideas


class MetaSolver:
    """
    Main meta-solver orchestrator.
    
    Runs the outer loop:
    1. Build prompt with problem + failure context
    2. Query LLM ensemble for ideas
    3. Pre-critique and filter ideas
    4. Implement and test promising ideas
    5. Accept improvements, record failures
    6. Repeat
    """
    
    def __init__(self,
                 problem: Problem,
                 base_solution: Solution,
                 llm_ensemble: list[LLMInterface],
                 config: Optional[MetaSolverConfig] = None,
                 evaluator: Optional['Evaluator'] = None,
                 comparator: Optional[Comparator] = None):
        
        self.problem = problem
        self.base_solution = base_solution
        self.llms = llm_ensemble
        self.config = config or MetaSolverConfig()
        self.comparator = comparator or Comparator()
        self.history = History()
        
        # Use provided evaluator or create default
        self.evaluator = evaluator or Evaluator(problem, self.config)
        
        # Current best solution
        self.current_solution = base_solution.clone()
        self.current_score = 0.0
        
        # Tracking
        self.iteration = 0
        self.iterations_without_improvement = 0
        
    def run(self) -> Solution:
        """Run the meta-solver loop until stopping condition."""
        
        # Initial baseline evaluation
        self._log("Establishing baseline...")
        baseline_results = self.evaluator.full_eval(self.current_solution)
        if not baseline_results:
            raise ValueError("No evaluation cases; refusing to establish an empty baseline")
        self.current_score = sum(r.score for r in baseline_results) / len(baseline_results)
        self._log(f"Baseline score: {self.current_score:.4f}")
        
        while not self._should_stop():
            self.iteration += 1
            self._log(f"\n{'='*50}")
            self._log(f"Iteration {self.iteration}")
            self._log(f"{'='*50}")
            
            # Step 1: Generate ideas from LLM ensemble
            ideas = self._generate_ideas()
            self._log(f"Generated {len(ideas)} ideas")
            
            # Step 2: Pre-critique and filter
            if self.config.use_pre_critique:
                ideas = self._pre_critique(ideas)
                self._log(f"After critique: {len(ideas)} ideas")
            
            # Step 3: Test each idea
            improvement_found = False
            for idea in ideas:
                if idea.id in self.history.ideas_seen:
                    self._log(f"Skipping already-tried idea: {idea.extracted_approach}")
                    continue
                
                self._log(f"\nTesting: {idea.extracted_approach}")
                attempt = self._test_idea(idea, baseline_results)
                self.history.add_attempt(attempt)
                
                if attempt.accepted:
                    improvement_found = True
                    self._log(f"✓ ACCEPTED! Improvement: {attempt.improvement_over_baseline:.4f}")
                    self.current_score = attempt.aggregate_score
                    baseline_results = attempt.results
                else:
                    self._log(f"✗ Rejected: {attempt.rejection_reason}")
            
            if improvement_found:
                self.iterations_without_improvement = 0
            else:
                self.iterations_without_improvement += 1
            
            # Checkpoint
            if self.iteration % self.config.checkpoint_every == 0:
                self._checkpoint()
        
        self._log(f"\nCompleted after {self.iteration} iterations")
        self._log(f"Final score: {self.current_score:.4f}")
        return self.current_solution
    
    def _generate_ideas(self) -> list[Idea]:
        """Generate ideas from the LLM ensemble."""
        prompt = PromptBuilder.build_idea_prompt(self.problem, self.history)
        all_ideas = []
        
        # Query each LLM, potentially with different temperatures
        for llm in self.llms:
            for temp in self.config.temperatures:
                try:
                    response = llm.generate(prompt, temperature=temp)
                    ideas = IdeaExtractor.extract_ideas(response, f"{llm.name}@{temp}")
                    all_ideas.extend(ideas)
                except Exception as e:
                    self._log(f"Error from {llm.name}: {e}")
        
        # Deduplicate by idea ID
        seen = set()
        unique_ideas = []
        for idea in all_ideas:
            if idea.id not in seen:
                seen.add(idea.id)
                unique_ideas.append(idea)
        
        return unique_ideas
    
    def _pre_critique(self, ideas: list[Idea]) -> list[Idea]:
        """Use an LLM to critique and filter ideas before expensive testing."""
        if not self.llms:
            return ideas
        
        # Use the first LLM as the critic
        critic = self.llms[0]
        filtered = []
        
        for idea in ideas:
            try:
                score, reasoning = critic.critique(
                    idea.raw_text, 
                    self.problem.describe()
                )
                if score >= self.config.min_critique_score:
                    filtered.append(idea)
                else:
                    self._log(f"Pre-filtered: {idea.extracted_approach} (score: {score:.2f})")
            except Exception as e:
                # On error, keep the idea
                filtered.append(idea)
        
        return filtered
    
    def _test_idea(self, idea: Idea, baseline_results: list[EvalResult]) -> Attempt:
        """Test an idea by implementing and evaluating it."""
        
        # Clone the current solution
        candidate = self.current_solution.clone()
        
        # Try to apply the idea (this is problem-specific)
        # For now, we'll pass the idea to the solution's apply_modification
        modification = {
            'idea_id': idea.id,
            'approach': idea.extracted_approach,
            'raw_text': idea.raw_text
        }
        
        success = candidate.apply_modification(modification)
        if not success:
            return Attempt(
                idea_id=idea.id,
                implementation=None,
                results=[],
                aggregate_score=0.0,
                improvement_over_baseline=0.0,
                accepted=False,
                rejection_reason="Failed to apply modification"
            )
        
        # Evaluate the candidate
        if self.config.easy_first:
            # Quick check on easy cases first
            easy_results = self.evaluator.eval_difficulty(candidate, Difficulty.EASY)
            easy_score = sum(r.score for r in easy_results) / max(len(easy_results), 1)
            
            if easy_results and easy_score < self.config.min_easy_score:
                return Attempt(
                    idea_id=idea.id,
                    implementation=modification,
                    results=easy_results,
                    aggregate_score=easy_score,
                    improvement_over_baseline=easy_score - self.current_score,
                    accepted=False,
                    rejection_reason=f"Failed easy cases (score: {easy_score:.2f})"
                )
        
        # Full evaluation
        candidate_results = self.evaluator.full_eval(candidate)
        aggregate_score = sum(r.score for r in candidate_results) / len(candidate_results) if candidate_results else 0.0
        
        # Compare against baseline
        accepted, reason, improvement = self.comparator.compare(
            baseline_results, 
            candidate_results
        )
        
        if accepted:
            # Update current solution
            self.current_solution = candidate
        
        return Attempt(
            idea_id=idea.id,
            implementation=modification,
            results=candidate_results,
            aggregate_score=aggregate_score,
            improvement_over_baseline=improvement,
            accepted=accepted,
            rejection_reason=None if accepted else reason
        )
    
    def _should_stop(self) -> bool:
        """Check stopping conditions."""
        if self.iteration >= self.config.max_iterations:
            self._log("Stopping: max iterations reached")
            return True
        
        if self.current_score >= self.config.target_score:
            self._log("Stopping: target score reached")
            return True
        
        if self.iterations_without_improvement >= self.config.patience:
            self._log("Stopping: patience exhausted")
            return True
        
        return False
    
    def _checkpoint(self):
        """Save checkpoint of current state."""
        self._log(f"Checkpoint at iteration {self.iteration}")
        self._log(f"History: {self.history.to_json()}")
    
    def _log(self, message: str):
        """Log a message if verbose."""
        if self.config.verbose:
            print(message)


class Evaluator:
    """Evaluates solutions against test cases."""
    
    def __init__(self, problem: Problem, config: MetaSolverConfig):
        self.problem = problem
        self.config = config
        
        # Cache test cases
        self._test_cases: dict[Difficulty, list[TestCase]] = {}
    
    def get_test_cases(self, difficulty: Difficulty) -> list[TestCase]:
        """Get test cases at a difficulty level (cached)."""
        if difficulty not in self._test_cases:
            self._test_cases[difficulty] = self.problem.get_test_cases(difficulty)
        return self._test_cases[difficulty]
    
    def eval_difficulty(self, solution: Solution, difficulty: Difficulty) -> list[EvalResult]:
        """Evaluate on all cases at a difficulty level."""
        cases = self.get_test_cases(difficulty)
        results = []
        
        for case in cases:
            result = self.problem.evaluate(solution, case)
            results.append(result)
        
        return results
    
    def full_eval(self, solution: Solution) -> list[EvalResult]:
        """Full evaluation across all difficulty levels."""
        all_results = []
        
        for difficulty in [Difficulty.EASY, Difficulty.MEDIUM, Difficulty.HARD]:
            results = self.eval_difficulty(solution, difficulty)
            all_results.extend(results)
        
        return all_results
