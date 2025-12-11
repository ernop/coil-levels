# Meta-Solver Design Discussion

## Core Concept

The meta-solver is essentially an **automated research assistant** that:
1. Takes a hard optimization/puzzle problem
2. Iteratively generates hypotheses (ideas) using diverse LLMs
3. Rigorously tests each hypothesis
4. Keeps improvements, learns from failures
5. Repeats until satisfied

This mimics how a human researcher would approach a hard problem, but with:
- Unlimited patience
- Perfect memory of failed attempts
- Ability to try many approaches in parallel
- No ego about abandoning bad ideas

## Key Design Decisions

### 1. Multi-LLM Ensemble (Diversity of Thought)

**Rationale**: Different LLMs have different "thinking styles" and training data:
- **Claude/Opus**: Strong at mathematical reasoning, careful analysis
- **GPT-4**: Good at creative connections, broad knowledge
- **Gemini**: Often has different perspectives, good at structured problems

Using multiple models increases the chance of finding novel approaches.

**Enhancement Ideas**:
- Weight models by past success rate on this problem type
- Use model specialization (some for creativity, some for rigor)
- Cross-pollinate: feed one model's ideas to another for refinement

### 2. Pre-Critique Filter (Cheap Before Expensive)

**Rationale**: Testing an idea (running actual computations) is expensive. 
Having an LLM quickly review "does this make sense?" filters obviously bad ideas.

**Current Implementation**:
```
Ideas → Pre-Critique → Only promising ones get tested
```

**Enhancement Ideas**:
- Multi-stage critique: quick filter → detailed analysis → test
- Use a different model for critique than generation (adversarial)
- Learn critique thresholds from past data

### 3. Stratified Testing (Easy → Hard)

**Rationale**: If an idea fails on easy cases, no point testing on hard ones.
This saves computation and gives faster feedback.

**Current Flow**:
```
Easy tests (must pass) → Medium tests → Hard tests (final validation)
```

**Enhancement Ideas**:
- Adaptive difficulty: spend more time on cases where methods differ
- Use easy cases to estimate hard case performance
- Bootstrap: test on random subset first

### 4. History as Context

**Rationale**: Past failures are valuable information. They tell the LLM:
- What NOT to suggest again
- What approaches have been tried
- Where the current solution struggles

**Enhancement Ideas**:
- Cluster similar failed ideas to identify "dead ends"
- Extract patterns from successful ideas
- Weight recent failures more heavily

### 5. Regression Protection

**Rationale**: Improvements that break existing functionality are dangerous.
Better to reject a 10% improvement that regresses 5% on some cases.

**Current Implementation**: Requires no regression on any individual test case.

**Enhancement Ideas**:
- Pareto optimization: keep multiple solutions on the frontier
- Allow small regressions if overall improvement is significant
- Track which cases are "critical" vs "nice to have"

## Potential Improvements to Explore

### 1. Self-Modification of the Prompt

Instead of static prompt templates, have the meta-solver learn what prompts work best:
```python
# After N iterations, analyze what worked
successful_ideas = get_successful_ideas()
common_patterns = extract_patterns(successful_ideas)
# Modify prompt to encourage these patterns
```

### 2. Automatic Implementation

Currently, ideas are extracted as text and the `apply_modification` method 
interprets them. A more powerful approach:

```python
# Have LLM generate actual code
code = llm.generate(f"Write a {language} function implementing: {idea}")
# Sandbox execute and test
result = sandbox_execute(code, test_cases)
```

This requires:
- Safe code execution (sandboxing)
- Language-specific code validation
- Graceful failure handling

### 3. Hierarchical Ideas

Break problems into sub-problems:
```
Main Problem
├── Sub-problem A
│   ├── Approach A1
│   └── Approach A2
└── Sub-problem B
    ├── Approach B1
    └── Approach B2
```

Test combinations of sub-solutions.

### 4. Competitive Evolution

Maintain a population of solutions:
```python
population = [solution1, solution2, ..., solutionN]
# Each iteration:
# 1. Generate ideas for worst performers
# 2. Apply best ideas to create children
# 3. Keep best performers, cull worst
```

### 5. Transfer Learning Across Problems

If the meta-solver works on Knapsack, can it transfer insights to:
- Subset Sum (related structure)
- Bin Packing (similar constraints)
- Scheduling (same mathematical family)

Store successful strategies with problem features:
```python
strategy_db = {
    "greedy_by_ratio": {
        "worked_for": ["knapsack", "scheduling"],
        "features": ["capacity constraint", "value maximization"]
    }
}
```

## Application to Coil Problem

For the coil puzzle specifically, the meta-solver could:

1. **Define the Problem Interface**:
   ```csharp
   class CoilProblem : Problem {
       string Describe() => "Find Hamiltonian path through grid...";
       TestCase[] GetTestCases(Difficulty d) => LoadLevels(d);
       EvalResult Evaluate(Solution s, TestCase tc) => RunSolver(s, tc);
   }
   ```

2. **Ideas Could Include**:
   - Different segment picking strategies
   - Tweaking parameters in the existing heuristics
   - New navigation patterns
   - Dead-end detection improvements
   - Symmetry exploitation

3. **Evaluation Metrics**:
   - Solve rate (% of levels solved)
   - Time to solve
   - Path quality (for partial solutions)

4. **Specific Enhancements for Coil**:
   - Use unsolved levels as "hard" test cases
   - Analyze patterns in unsolved vs solved levels
   - Try ensemble of different solving strategies

## Philosophical Notes

### The "Alien Mathematician" Problem

LLMs generate ideas from their training distribution. Truly novel insights 
(like Karmarkar-Karp for partition) might be outside this distribution.

**Mitigation**:
- High temperature sampling for diversity
- Explicit prompting for "unconventional" ideas
- Combining partial ideas from multiple sources

### When Does This Beat Manual Optimization?

| Meta-Solver Wins | Human Wins |
|------------------|------------|
| Many parameters to tune | Need deep domain insight |
| Clear evaluation metric | Fuzzy success criteria |
| Time to explore | Quick intuitive leap needed |
| Parallelizable testing | Sequential debugging |

### Convergence Guarantees

This is essentially a form of **generate-and-test search** with:
- No guarantee of finding global optimum
- Dependent on LLM quality and prompt design
- Can get stuck in local optima

The "patience" parameter prevents infinite loops, but doesn't guarantee quality.

## Next Steps for Implementation

1. **Connect to real LLM APIs** (Anthropic, OpenAI, Google)
2. **Build coil problem interface** connecting to C# solver
3. **Define meaningful test case splits** (easy/medium/hard levels)
4. **Instrument for logging and analysis**
5. **Run initial experiments** with current heuristics as baseline
6. **Iterate on prompt engineering** based on results
