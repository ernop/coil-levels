# Historical framework summary

The real Coil bridge is now implemented. See [README.md](README.md) for current commands, supported controls, actual evaluation results, and limitations. The design sketch below predates that implementation.

# Meta-Solver Implementation Summary

## What We Built

A generic, modular meta-solver framework that can iterate on difficult optimization problems
using LLMs as "idea generators" and rigorous testing for validation.

### Files Created

```
meta_solver/
├── __init__.py           # Package exports
├── core.py               # Core abstractions (Problem, Solution, LLMInterface, etc.)
├── solver.py             # Main MetaSolver orchestrator
├── llm_providers.py      # Real LLM API implementations (Anthropic, OpenAI, Google)
├── test_framework.py     # Tests for the framework
├── requirements.txt      # Dependencies
├── README.md             # Usage documentation
├── DESIGN_DISCUSSION.md  # Deep dive on design decisions
├── SUMMARY.md            # This file
└── examples/
    ├── knapsack_example.py    # Proof-of-concept with Knapsack problem
    ├── number_partition.py    # Another example with Number Partition
    └── coil_integration.py    # Real C# evaluation bridge (see README)
```

## Test Results

All framework tests pass:
```
============================================================
Meta-Solver Framework Tests
============================================================
Testing IdeaExtractor...    ✓
Testing Comparator...       ✓
Testing History...          ✓
Testing PromptBuilder...    ✓
Testing simple solving...   ✓ (found x=42 from initial guess 0)
============================================================
Results: 5 passed, 0 failed
```

Example runs:
- **Knapsack**: Meta-solver went from 0.989 (greedy) → 1.0 (DP) in 1 iteration
- **Number Partition**: Different strategies range from 0.30 (greedy) to 1.0 (hill climbing)
- **Coil Integration**: Framework loads 100 levels across difficulty tiers

## The Core Loop

```
┌──────────────────────────────────────────────────────────────┐
│ while not satisfied:                                          │
│                                                                │
│   1. BUILD PROMPT                                             │
│      - Problem description                                    │
│      - Current approach                                       │
│      - Recent failures                                        │
│      - What's been tried                                      │
│                                                                │
│   2. GENERATE IDEAS (parallel across LLMs)                   │
│      - Claude (careful reasoning)                             │
│      - GPT-4 (creative connections)                          │
│      - Gemini (different perspective)                         │
│      - Same model @ different temperatures                    │
│                                                                │
│   3. PRE-CRITIQUE (cheap filter)                             │
│      - Quick LLM review of each idea                          │
│      - Skip obviously bad ideas before expensive tests        │
│                                                                │
│   4. TEST IDEAS                                               │
│      - Easy cases first (fast fail)                           │
│      - Medium cases if easy passes                            │
│      - Hard cases for final validation                        │
│                                                                │
│   5. COMPARE TO BASELINE                                      │
│      - Statistical significance                               │
│      - Regression protection                                  │
│      - Multi-metric evaluation                                │
│                                                                │
│   6. INCORPORATE OR REJECT                                    │
│      - Update current solution if better                      │
│      - Record failure with reason                             │
│      - Update history for future context                      │
└──────────────────────────────────────────────────────────────┘
```

## Key Architectural Decisions

### 1. Pluggable Abstractions

```python
# Problem interface - define any optimization problem
class Problem(ABC):
    def describe(self) -> str: ...
    def get_test_cases(self, difficulty) -> list: ...
    def evaluate(self, solution, test_case) -> EvalResult: ...

# Solution interface - any representation of a solution
class Solution(ABC):
    def clone(self) -> Solution: ...
    def apply_modification(self, modification) -> bool: ...
    def execute(self, test_case) -> Any: ...

# LLM interface - any LLM provider
class LLMInterface(ABC):
    def generate(self, prompt, temperature) -> str: ...
    def critique(self, idea, context) -> tuple[float, str]: ...
```

### 2. Failure as Data

Every rejected idea is recorded with:
- What the idea was
- Why it was rejected
- What it scored on which tests

This prevents re-trying the same bad ideas and informs future idea generation.

### 3. Stratified Testing

Testing pyramid:
```
     ┌─────┐
     │HARD │  Final validation only
     ├─────┤
   ┌─┤MED  ├─┐  If easy passes
   │ ├─────┤ │
┌──┴─┤EASY ├─┴──┐  Always test first
│    └─────┘    │
│   Fast fail   │
└───────────────┘
```

### 4. Regression Protection

Default comparator:
- Requires minimum improvement (1% default)
- Rejects changes that regress on ANY individual test case
- Configurable tolerances

## Applying to Coil Problem

Historical integration sketch (superseded by `coil_bridge.py`):

### 1. Create C# Runner Interface

```python
def run_coil_solver(level_path: str, config: dict, timeout: float) -> dict:
    """Run the C# solver and return results."""
    result = subprocess.run(
        ["dotnet", "run", "--", level_path, json.dumps(config)],
        capture_output=True,
        timeout=timeout
    )
    return parse_output(result.stdout)
```

### 2. Define Configuration Space

Ideas could modify:
- `SegPicker` weights and strategies
- `TweakSection` parameters
- Navigation heuristics
- Dead-end detection thresholds

### 3. Test Case Selection

- **Easy**: Small grids (< 20x20), high solve rate
- **Medium**: Medium grids, moderate solve rate  
- **Hard**: Large grids or currently unsolved levels

### 4. Evaluation Metrics

```python
score = (
    0.5 * solved_rate +           # Did it solve?
    0.3 * (1 / log(time + 1)) +   # How fast?
    0.2 * (1 / backtracks)        # How efficient?
)
```

## Next Steps

1. **Connect Real APIs**: Set environment variables for API keys
2. **Build C# Bridge**: Create subprocess interface to existing solver
3. **Curate Test Sets**: Identify easy/medium/hard levels
4. **Baseline Measurement**: Get current solver performance metrics
5. **Run Experiments**: Let the meta-solver iterate
6. **Analyze Results**: What kinds of ideas helped/hurt?

## Discussion Points

### When Does This Approach Shine?

✅ Problems with:
- Clear evaluation metrics
- Many possible heuristics to try
- Expensive to manually tune
- Benefit from diverse approaches

### When Might It Struggle?

❓ Challenges:
- LLMs may not suggest truly novel algorithms
- Implementation gap (idea → working code)
- Local optima in idea space
- Expensive if LLM calls are costly

### Potential Enhancements

1. **Automatic Code Generation**: Have LLM write actual implementation
2. **Ensemble of Solutions**: Keep multiple good solutions
3. **Transfer Learning**: Apply insights across problem families
4. **Human-in-the-Loop**: Have human review promising ideas
5. **Meta-Learning**: Learn what kinds of prompts work best

---

*This framework is a proof-of-concept. For production use, add proper logging,
error handling, persistence, and monitoring.*
