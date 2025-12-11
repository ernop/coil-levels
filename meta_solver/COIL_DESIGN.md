# Coil Meta-Solver: Design Decisions & Strategy

## Understanding the Problem

After reviewing [coilbench](https://github.com/adum/coilbench), here's what we're dealing with:

**The Challenge:**
- Levels scale from 3x3 to 2000x2000 (4 million cells!)
- Brute force: 304 millennia for the hardest level
- Best AI iteration so far: Level 73 (0.018% of goal)
- Only 4 humans have ever solved all levels
- 47 people passed level 300 → there ARE solutions!

**Key Insight from results.md:**
> "Optimizations are specific to the task and must be created, not known algorithms"
> "Dozens of interesting optimization approaches can be creatively applied in concert"

This tells us: **There's no single trick. It requires combining many clever ideas.**

---

## Your Questions, Answered

### 1. How Do We Avoid Repetitive Loops?

**The Problem:** LLMs often suggest the same ideas repeatedly, wasting time.

**Solutions Implemented:**

#### A. Semantic Clustering
```python
# Don't just match text - extract semantic fingerprint
def compute_semantic_hash(text):
    keywords = extract_technical_terms(text)  # "backtrack", "cache", etc.
    return hash(sorted(keywords))

# Similar semantic fingerprints = same idea family
if idea.semantic_hash in seen_hashes:
    skip("Already tried this family of ideas")
```

#### B. Exploration Budget by Category
```
Category              | Attempts | Successes | Priority
--------------------- | -------- | --------- | --------
DEADEND_DETECTION     | 12       | 2         | 0.45
SEARCH_PRUNING        | 8        | 0         | 0.52
PATH_REPRESENTATION   | 0        | 0         | 0.89  ← Try this!
CACHING               | 3        | 1         | 0.61
```

Priority formula (UCB1-like):
```
priority = exploration_bonus + success_rate + recency_bonus
         = 2/√attempts     + successes/attempts + time_since_last_success
```

#### C. Forced Novelty Mode
When stuck (10+ iterations without progress):
- Switch to **underexplored categories**
- Increase temperature to 1.0 for more diverse outputs
- Explicitly prompt: "Suggest something UNLIKE what's been tried"

#### D. Time-Bounded Exploration
```python
MAX_TIME_PER_DIRECTION = 30 minutes
MAX_ATTEMPTS_PER_CATEGORY = 20

if category.time_spent > MAX_TIME_PER_DIRECTION:
    mark_as_exhausted(category)
    force_switch_to_different_category()
```

---

### 2. How Do We Know What New Ideas to Try?

**The Problem:** The space of possible improvements is vast and unstructured.

**Solution: Structured Taxonomy**

I've identified **18 improvement categories** across 6 major areas:

```
SEARCH
├── Order      → What order to try things
├── Pruning    → What to skip
└── Heuristic  → How to score candidates

DEAD-ENDS
├── Detection  → Recognize early
├── Avoidance  → Don't create them
└── Recovery   → Escape them

PATH STRUCTURE
├── Representation → How to represent state
├── Manipulation   → Operations (tweaks, etc.)
└── Analysis       → Understanding properties

TRANSFORMATION
├── Symmetry      → Exploit symmetries
├── Decomposition → Break into subproblems
└── Preprocessing → Analyze before solving

PERFORMANCE
├── Data Structures → Better structures
├── Caching         → Memoization
└── Parallelism     → Concurrent exploration

LEARNING
├── Patterns  → Learn from solved levels
├── Transfer  → Apply across levels
├── Tuning    → Tune parameters
└── Ensemble  → Combine approaches
```

**How to navigate this:**
1. Track which categories have been explored
2. Prioritize underexplored + historically successful
3. When one category stalls, pivot to another
4. Periodically do "novelty search" in random unexplored category

**Investigation Prompts for Stuck States:**
```
"Why does level 47 specifically fail?"
"What patterns exist in levels 1-46 that break at 47?"
"What's different about the board structure of level 47?"
```

---

### 3. What If We Incorporate a Bad Change?

**The Problem:** A change might look good on some levels but cause regressions.

**Solution: Multi-Tier Evaluation with Canaries**

```
TIER 1: QUICK (2 min)
├── 5 small levels (3x3 to 10x10)
├── 5s timeout each
├── Must pass 90% to continue
└── Purpose: Fast rejection of obviously bad ideas

TIER 2: MEDIUM (10 min)
├── 10 medium levels (15x15 to 30x30)
├── 30s timeout each
├── Must pass 80% to continue
└── Purpose: Reasonable validation

TIER 3: THOROUGH (20 min)
├── 20 harder levels (40x40 to 60x60)
├── 60s timeout each
├── Must pass 70% to continue
└── Purpose: Full validation

TIER 4: CANARY (5 min)
├── 5 hand-picked "sensitive" levels
├── 60s timeout each
├── Must pass 100% - ANY regression = rejection
└── Purpose: Regression detection
```

**Canary Levels:** Levels where regressions commonly appear
- Level 47: Often first to break
- Level 50: Different structure
- Level 55, 60, 63: Known edge cases

**Solution Ensemble:** Don't throw away working approaches!
```python
class SolutionEnsemble:
    """Keep multiple solver variants"""
    
    variants = [
        SolverVariant("fast_greedy", score=0.7, good_at=["small levels"]),
        SolverVariant("thorough_search", score=0.85, good_at=["complex boards"]),
        SolverVariant("hybrid", score=0.9, good_at=["medium levels"]),
    ]
    
    def select_for_level(self, level):
        # Different variants work better on different levels!
        return best_match(level.characteristics, self.variants)
```

**Rollback Strategy:**
```python
if regression_detected:
    revert_to_last_known_good()
    add_to_failure_memory("Caused regression on levels X, Y, Z")
    # This failure info helps future idea generation
```

---

### 4. How Do We Help "Ideas Agents" Come Up With Suggestions?

**The Problem:** LLMs need context and structure to generate useful ideas.

**Solution: Rich Context Prompts**

#### A. Provide Problem Understanding
```markdown
# The Coil Puzzle

Rules:
1. Pick any starting cell
2. SLIDE until hitting wall/edge/visited
3. Visit all cells exactly once

Why It's Hard:
- Dead-ends are easy to create
- Local decisions have global consequences
- 304 millennia brute force for 2000x2000
```

#### B. Show What's Been Tried (and failed)
```markdown
## Failed Approaches in DEADEND_DETECTION:

1. "Look-ahead by 2 moves" - Too slow, O(n²) per decision
2. "Count reachable cells" - Missed corner cases
3. "Island detection" - Didn't account for sliding mechanic

## Why they failed:
- #1: Solved small levels but timeout on large
- #2: 15% regression on level 47
- #3: Conceptually wrong for this problem
```

#### C. Provide Failure Analysis
```markdown
## Recent Failures

- Level 47 (20x19): Coverage 78%, got stuck at (15, 12)
  Pattern: "corridor_deadend" - created unreachable pocket
  
- Level 52 (22x21): Coverage 92%, timeout
  Pattern: "late_trap" - good until last 8% of cells
  
## Common Patterns
- corridor_deadend: 12 occurrences
- corner_trap: 8 occurrences
- late_trap: 5 occurrences
```

#### D. Ask Specific Questions
```markdown
## Investigation Request

The solver keeps failing on levels where there are:
- Long corridors (width=1) connecting larger areas
- 3+ "chambers" that must all be visited

Question: How can we detect when entering a corridor
will make another area unreachable?
```

#### E. Require Concrete Implementations
```markdown
For each idea, provide:
1. **Data structures needed** (specific types)
2. **When this triggers** (which decision point)
3. **Expected complexity** (big-O)
4. **Why different from failed approaches**
5. **Risk assessment** (what could go wrong)
```

---

### 5. What Actually ARE the Ways to Move Forward?

Based on analyzing the coilbench problem and existing solvers:

#### TIER 1: LOW-HANGING FRUIT (likely to help immediately)

**A. Better Dead-End Detection**
```
Current: Check if any moves available
Better: Check if all remaining regions are still reachable
        Use Union-Find for connectivity tracking
        O(α(n)) per query instead of O(n)
```

**B. Forced Move Detection**
```
If only one direction doesn't immediately dead-end → take it
Can chain: forced moves reduce search space significantly
```

**C. Look-Ahead Scoring**
```
For each direction, simulate 1-2 moves ahead
Score by: cells visited + connectivity preserved
Choose highest scoring (not just random/first)
```

#### TIER 2: MEDIUM-TERM IMPROVEMENTS

**D. Island Detection**
```
After each move, check: did we create an isolated region?
If yes, and we can't reach it later → prune this branch
```

**E. Parity Arguments**
```
Like chess bishop on same color - some cells have parity
If wrong parity cells remain and we can't fix → fail fast
```

**F. Segment-Level Operations**
```
Current C# solver uses "tweaks" - good!
Optimize: which segment to tweak next?
          how far to bump?
          can we predict good tweaks?
```

#### TIER 3: HARDER BUT HIGH-POTENTIAL

**G. Constraint Propagation**
```
Like Sudoku solvers - propagate constraints
"If I go right, then I MUST go down next, then..."
Reduce choices at each step
```

**H. Pattern Database**
```
Pre-compute solutions for small subregions
Lookup and apply when similar subregion appears
```

**I. Monte Carlo Tree Search**
```
Random playouts to estimate move quality
UCB for selection: balance exploit vs explore
Good for large boards where exact search fails
```

#### TIER 4: RESEARCH-LEVEL

**J. Learning a Heuristic**
```
Train on solved levels
Features: local pattern, connectivity, coverage
Predict: P(this move leads to solution)
```

**K. Decomposition**
```
Identify "bridges" where board can be split
Solve subproblems independently
Combine solutions (tricky!)
```

**L. Symmetry Breaking**
```
Many boards have symmetry
Solve once, apply to symmetric variants
```

---

## Revised Meta-Solver Architecture

```
┌──────────────────────────────────────────────────────────────────┐
│                        OUTER LOOP                                 │
│                                                                   │
│  ┌─────────────────────────────────────────────────────────────┐ │
│  │                    IDEA GENERATION                           │ │
│  │                                                              │ │
│  │   ┌──────────┐   ┌──────────┐   ┌──────────┐               │ │
│  │   │  Opus    │   │  GPT-4   │   │  Gemini  │               │ │
│  │   │ (reason) │   │ (create) │   │(diverse) │               │ │
│  │   └────┬─────┘   └────┬─────┘   └────┬─────┘               │ │
│  │        │              │              │                      │ │
│  │        └──────────────┼──────────────┘                      │ │
│  │                       ▼                                     │ │
│  │               ┌──────────────┐                              │ │
│  │               │  Idea Pool   │                              │ │
│  │               │  + Semantic  │                              │ │
│  │               │  Dedup       │                              │ │
│  │               └──────────────┘                              │ │
│  └─────────────────────────────────────────────────────────────┘ │
│                              │                                    │
│                              ▼                                    │
│  ┌─────────────────────────────────────────────────────────────┐ │
│  │                    PRE-FILTERING                             │ │
│  │                                                              │ │
│  │   ┌─────────────┐    ┌─────────────┐    ┌─────────────┐    │ │
│  │   │  Novelty    │───▶│  Category   │───▶│    LLM      │    │ │
│  │   │  Check      │    │  Budget OK? │    │  Critique   │    │ │
│  │   └─────────────┘    └─────────────┘    └─────────────┘    │ │
│  └─────────────────────────────────────────────────────────────┘ │
│                              │                                    │
│                              ▼                                    │
│  ┌─────────────────────────────────────────────────────────────┐ │
│  │                IMPLEMENTATION ATTEMPT                        │ │
│  │                                                              │ │
│  │   Idea ──▶ Code Change ──▶ Compile ──▶ Basic Sanity Test   │ │
│  │                                                              │ │
│  └─────────────────────────────────────────────────────────────┘ │
│                              │                                    │
│                              ▼                                    │
│  ┌─────────────────────────────────────────────────────────────┐ │
│  │                MULTI-TIER EVALUATION                         │ │
│  │                                                              │ │
│  │   QUICK ──▶ MEDIUM ──▶ THOROUGH ──▶ CANARY                 │ │
│  │   (2min)    (10min)    (20min)      (5min)                  │ │
│  │                                                              │ │
│  │   Early exit if tier fails minimum pass rate                │ │
│  │   Canary = 100% required (regression protection)            │ │
│  └─────────────────────────────────────────────────────────────┘ │
│                              │                                    │
│                              ▼                                    │
│  ┌─────────────────────────────────────────────────────────────┐ │
│  │                    DECISION                                  │ │
│  │                                                              │ │
│  │   ACCEPT if:                                                 │ │
│  │     - All tiers passed                                       │ │
│  │     - Net improvement > threshold                            │ │
│  │     - No canary regressions                                  │ │
│  │                                                              │ │
│  │   REJECT if:                                                 │ │
│  │     - Any tier failed                                        │ │
│  │     - Caused regression                                      │ │
│  │     → Record WHY in failure memory                           │ │
│  │                                                              │ │
│  └─────────────────────────────────────────────────────────────┘ │
│                              │                                    │
│                              ▼                                    │
│  ┌─────────────────────────────────────────────────────────────┐ │
│  │                UPDATE STATE                                  │ │
│  │                                                              │ │
│  │   • Update exploration budget                                │ │
│  │   • Update failure analysis                                  │ │
│  │   • Update solution ensemble                                 │ │
│  │   • Check if stuck → trigger novelty search                  │ │
│  │                                                              │ │
│  └─────────────────────────────────────────────────────────────┘ │
│                                                                   │
└───────────────────────────────────────────────────────────────────┘
```

---

## Implementation Roadmap

### Phase 1: Foundation (Now)
- [x] Basic meta-solver framework
- [x] Improvement category taxonomy
- [x] Semantic deduplication
- [x] Multi-tier evaluation structure

### Phase 2: Integration (Next)
- [ ] Connect to actual coilbench solver
- [ ] Implement subprocess runner
- [ ] Set up level categorization (easy/medium/hard/canary)
- [ ] Establish baseline measurements

### Phase 3: Intelligence
- [ ] Failure pattern extraction
- [ ] Automatic investigation prompts
- [ ] Cross-LLM idea synthesis
- [ ] Solution ensemble management

### Phase 4: Learning
- [ ] Pattern extraction from solved levels
- [ ] Transfer learning between levels
- [ ] Automatic parameter tuning

---

## Key Metrics to Track

1. **Highest Level Solved** - Primary metric
2. **Time to Solve Level N** - Secondary metric  
3. **Ideas per Improvement** - Efficiency of ideation
4. **Category Success Rates** - Which areas are productive
5. **Regression Frequency** - Quality of changes
6. **Ensemble Diversity** - Multiple approaches maintained

---

## Final Thoughts

The key insight is that this is a **multi-faceted** optimization problem:
- No single trick will solve it
- Different approaches work on different levels
- Iteration and diversity are essential
- Learning from failures is critical

The meta-solver should be a **research assistant** that:
1. Systematically explores the space of improvements
2. Remembers what worked and what didn't
3. Maintains diverse approaches
4. Focuses attention where it's needed most
5. Protects against regressions

This is exactly the kind of problem where iterative AI-assisted development should shine.
