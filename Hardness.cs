using System;
using System.Collections.Generic;

using static coil.Coilutil;

namespace coil
{
    /// <summary>
    /// Hardness scoring for generated levels. Two scores:
    ///  - Exact: full-tree effort of the reference <see cref="Solver"/> (every candidate start, every solution). This is
    ///    what a pruning DFS bot actually pays, but it grows exponentially with board size and is only affordable up to
    ///    roughly 35x35 per level.
    ///  - Proxy: an O(cells) linear model of log(exact effort) fitted on 960 trimmed 24x24 levels (32 generator configs x
    ///    30 seeds): R^2 0.38, Spearman 0.61 against the exact score; the top decile by proxy averages 2x the geometric-
    ///    mean effort (top decile by exact: 3.3x). Use it when exact scoring is too slow. See HARDNESS.md.
    /// </summary>
    public static class Hardness
    {
        public struct Score
        {
            public int Seed;
            public string Board;
            public string Solution;
            public long NodesAll;
            public long Solutions;
            public bool BudgetExceeded;
            public double Proxy;
            public double OpenPct;
            public int HardDecisions;
            public double IsolatedWallPct;
            public double Deg2Pct;

            //exact when we have it, otherwise the proxy. Exceeded budgets sort above completed searches, then by proxy.
            public (bool exceeded, long nodes, double proxy) Rank(bool exact) =>
                (exact && BudgetExceeded, exact && !BudgetExceeded ? NodesAll : 0,
                    !exact || BudgetExceeded ? Proxy : 0);
        }

        /// <summary>Predicted ln(full-tree nodes) for a 24x24 board; only the ordering is meaningful at other sizes.</summary>
        public static double Proxy(double hardDecisionsPer100Open, double isolatedWallPct, double deg2Pct, double openPct)
        {
            return 0.2365 * hardDecisionsPer100Open + 0.0412 * isolatedWallPct - 0.0095 * deg2Pct + 0.0093 * openPct + 10.04;
        }

        public static Score Measure(Level level, int seed, bool exact, long budget)
        {
            var board = CoilFormat.BoardString(level);
            var (w, h, open, deg, isolatedWallPct) = Solver.BoardFacts(board);
            var (_, hardDecisions) = GetDecisions(level);
            var s = new Score
            {
                Seed = seed,
                Board = board,
                Solution = CoilFormat.SolutionString(level),
                OpenPct = 100.0 * open / (w * h),
                HardDecisions = hardDecisions.Count,
                IsolatedWallPct = isolatedWallPct,
                Deg2Pct = 100.0 * deg[2] / open,
            };
            s.Proxy = Proxy(100.0 * hardDecisions.Count / open, isolatedWallPct, s.Deg2Pct, s.OpenPct);
            if (exact)
            {
                var solver = new Solver(board) { NodeBudget = budget, MaxSolutions = long.MaxValue };
                solver.Solve();
                s.NodesAll = solver.Nodes;
                s.Solutions = solver.SolutionsFound;
                s.BudgetExceeded = solver.BudgetExceeded;
            }
            return s;
        }
    }
}
