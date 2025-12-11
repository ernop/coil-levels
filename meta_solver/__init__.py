"""
Meta-Solver Framework

A self-improving optimization system using LLM ensembles.
"""

from .core import (
    Problem, Solution, LLMInterface, History, Comparator,
    PromptBuilder, Idea, Attempt, EvalResult, TestCase, Difficulty
)
from .solver import MetaSolver, MetaSolverConfig, Evaluator, IdeaExtractor

__all__ = [
    'Problem', 'Solution', 'LLMInterface', 'History', 'Comparator',
    'PromptBuilder', 'Idea', 'Attempt', 'EvalResult', 'TestCase', 'Difficulty',
    'MetaSolver', 'MetaSolverConfig', 'Evaluator', 'IdeaExtractor'
]
