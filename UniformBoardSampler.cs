using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace coil
{
    // Exact membership and uniform sampling on small fixed rectangles. No
    // solver timeout is interpreted as an unsolvable board.
    public sealed class UniformBoardSampler
    {
        public int Width { get; }
        public int Height { get; }
        public int Area => Width * Height;
        public IReadOnlyList<int> Masks => masks.AsReadOnly();
        private readonly List<int> masks = new List<int>();
        private readonly Dictionary<int, string> solutions = new Dictionary<int, string>();

        public UniformBoardSampler(int width, int height)
        {
            if (width < 1 || height < 1 || (long)width * height > 16)
                throw new ArgumentException("Exact catalogue supports positive rectangles up to 16 cells.");
            Width = width; Height = height;
            for (var mask = 1; mask < 1 << Area; mask++)
            {
                var solver = new Solver(Board(mask));
                var solved = solver.Solve();
                if (solver.BudgetExceeded || solver.TimedOut || solver.DepthExceeded)
                    throw new InvalidOperationException($"Incomplete catalogue solve: mask {mask}.");
                if (!solved) continue;
                CoilFormat.Validate(Board(mask), solver.FirstSolution);
                masks.Add(mask); solutions.Add(mask, solver.FirstSolution);
            }
        }

        public bool Contains(int mask) => solutions.ContainsKey(mask);
        public string Solution(int mask) => solutions[mask];
        public string Board(int mask) => $"x={Width}&y={Height}&board=" +
            new string(Enumerable.Range(0, Area).Select(i => (mask & (1 << i)) == 0 ? 'X' : '.').ToArray());
        public int Draw(Func<int, int> choose) => masks[choose(masks.Count)];
        public int Step(int mask, Func<int, int> choose)
        {
            if (!Contains(mask)) throw new ArgumentException("Chain must start on a solvable board.");
            if (choose(2) == 0) return mask;
            var proposed = mask ^ (1 << choose(Area));
            return Contains(proposed) ? proposed : mask;
        }

        public object DistributionStudy(int maximumSteps = 4096)
        {
            var index = masks.Select((mask, i) => (mask, i)).ToDictionary(p => p.mask, p => p.i);
            var neighbors = masks.Select(mask => Enumerable.Range(0, Area).Select(i => mask ^ (1 << i))
                .Where(Contains).Select(m => index[m]).ToArray()).ToArray();
            var histogram = new int[Area + 1];
            foreach (var mask in masks) histogram[BitOperations.PopCount((uint)mask)]++;
            var starts = new[] { masks[0], masks[masks.Count - 1] };
            var studies = new List<object>();
            foreach (var start in starts.Distinct())
            {
                var probability = new double[masks.Count]; probability[index[start]] = 1;
                var points = new List<object>();
                for (var step = 0; step <= maximumSteps; step++)
                {
                    if (step == 0 || (step & (step - 1)) == 0 || step == maximumSteps)
                        points.Add(new { step, totalVariation = probability.Sum(p => Math.Abs(p - 1.0 / masks.Count)) / 2,
                            meanOpenCells = probability.Select((p, i) => p * BitOperations.PopCount((uint)masks[i])).Sum() });
                    if (step == maximumSteps) break;
                    var next = new double[masks.Count];
                    for (var i = 0; i < masks.Count; i++)
                    {
                        var flow = probability[i] / (2 * Area);
                        next[i] += probability[i] - neighbors[i].Length * flow;
                        foreach (var neighbor in neighbors[i]) next[neighbor] += flow;
                    }
                    probability = next;
                }
                studies.Add(new { startMask = start, points });
            }
            return new { width = Width, height = Height, solvableBoards = masks.Count, occupancyCounts = histogram,
                meanOpenCells = masks.Average(m => BitOperations.PopCount((uint)m)),
                probabilityAtLeastHalfOpen = masks.Count(m => BitOperations.PopCount((uint)m) * 2 >= Area) / (double)masks.Count,
                chain = "lazy uniform-cell flip; unsolvable proposals stay; all stays counted", studies,
                scope = "Exact finite catalogue and propagated probabilities; no Monte Carlo error. Does not bound large-board mixing." };
        }
    }
}
