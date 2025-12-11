# Meta-Solver Framework

A self-improving optimization framework that uses multiple LLMs as "idea generators" 
and rigorously tests ideas against sample problems.

## Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                        OUTER LOOP                                │
│  ┌─────────────┐    ┌──────────────┐    ┌─────────────────┐    │
│  │   Problem   │───▶│   Context    │───▶│  LLM Ensemble   │    │
│  │ Definition  │    │   Builder    │    │ (Opus,Gemini,..)│    │
│  └─────────────┘    └──────────────┘    └────────┬────────┘    │
│         │                                         │             │
│         │                                         ▼             │
│         │           ┌──────────────┐    ┌─────────────────┐    │
│         │           │   History    │◀───│  Idea Parser    │    │
│         │           │  (failures,  │    │  & Extractor    │    │
│         │           │   attempts)  │    └────────┬────────┘    │
│         │           └──────────────┘             │             │
│         │                  │                     ▼             │
│         │                  │           ┌─────────────────┐     │
│         │                  │           │  Pre-Critique   │     │
│         │                  │           │  (cheap filter) │     │
│         │                  │           └────────┬────────┘     │
│         │                  │                    │              │
│         ▼                  ▼                    ▼              │
│  ┌─────────────────────────────────────────────────────┐      │
│  │                   EVALUATION ENGINE                   │      │
│  │  ┌─────────┐  ┌───────────┐  ┌─────────────────┐    │      │
│  │  │ Easy    │─▶│  Medium   │─▶│  Hard Samples   │    │      │
│  │  │ Samples │  │  Samples  │  │  (if promoted)  │    │      │
│  │  └─────────┘  └───────────┘  └─────────────────┘    │      │
│  └──────────────────────────┬──────────────────────────┘      │
│                              │                                  │
│                              ▼                                  │
│  ┌──────────────────────────────────────────────────────┐     │
│  │                  COMPARATOR                           │     │
│  │  • Statistical significance testing                   │     │
│  │  • Multi-metric evaluation                           │     │
│  │  • Regression detection                              │     │
│  └──────────────────────────┬───────────────────────────┘     │
│                              │                                  │
│                              ▼                                  │
│  ┌──────────────────────────────────────────────────────┐     │
│  │                 INCORPORATION                         │     │
│  │  • Merge improvements into main solution             │     │
│  │  • Update history with results                       │     │
│  │  • Checkpoint successful configurations              │     │
│  └──────────────────────────────────────────────────────┘     │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

## Key Components

1. **Problem Interface**: Abstract definition of your problem
2. **Solution Interface**: How solutions are represented and executed
3. **LLM Ensemble**: Multiple models for diverse idea generation
4. **Pre-Critique**: Cheap filtering before expensive tests
5. **Evaluator**: Runs solutions against stratified test cases
6. **Comparator**: Determines if improvements are statistically significant
7. **History Manager**: Tracks what's been tried and why it failed/succeeded

## Usage

```python
from meta_solver import MetaSolver, Problem

# Define your problem
class MyPuzzle(Problem):
    def describe(self) -> str:
        return "..."
    
    def get_test_cases(self, difficulty: str) -> list:
        return [...]
    
    def evaluate(self, solution, test_case) -> float:
        return score

# Run the meta-solver
solver = MetaSolver(
    problem=MyPuzzle(),
    llm_ensemble=['opus', 'gemini', 'gpt4'],
    max_iterations=100
)
best_solution = solver.run()
```

## Design Principles

1. **Ideas are cheap, testing is expensive**: Generate many ideas, filter aggressively
2. **Failures are data**: Track and learn from what didn't work
3. **Diversity matters**: Different LLMs think differently
4. **Small wins compound**: Accept incremental improvements
5. **Regression protection**: Never accept a change that hurts existing cases
