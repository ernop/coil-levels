using System;
using System.Linq;
using coil;

static class SolverEvaluationTests
{
    static bool Oracle(int width, int height, int open)
    {
        bool Search(int position, int visited)
        {
            if (visited == open) return true;
            foreach (var (dx, dy) in new[] { (0, -1), (1, 0), (0, 1), (-1, 0) })
            {
                int x = position % width, y = position / width, nextVisited = visited;
                while (x + dx >= 0 && y + dy >= 0 && x + dx < width && y + dy < height)
                {
                    int bit = 1 << ((y + dy) * width + x + dx);
                    if ((open & bit) == 0 || (nextVisited & bit) != 0) break;
                    x += dx; y += dy; nextVisited |= bit;
                }
                if (nextVisited != visited && Search(y * width + x, nextVisited)) return true;
            }
            return false;
        }
        for (int start = 0; start < width * height; start++)
            if ((open & (1 << start)) != 0 && Search(start, 1 << start)) return true;
        return false;
    }

    public static void Run()
    {
        for (int mask = 0; mask < 512; mask++)
        {
            string board = "x=3&y=3&board=" + new string(Enumerable.Range(0, 9).Select(i => (mask & (1 << i)) != 0 ? '.' : 'X').ToArray());
            bool expected = Oracle(3, 3, mask);
            foreach (string order in new[] { "URDL", "DRUL" })
            foreach (string starts in new[] { "natural", "reverse", "low-degree", "high-degree" })
            foreach (bool pruning in new[] { true, false })
            {
                var solver = new Solver(board) { DirectionOrder = order, StartOrder = starts, UseFeasibility = pruning };
                if (solver.Solve() != expected) throw new Exception($"Search mismatch: {mask}, {order}, {starts}, {pruning}");
                if (expected) CoilFormat.Validate(board, solver.FirstSolution);
            }
        }
        foreach (int budget in new[] { 0, 1, 2 })
        {
            var solver = new Solver("x=3&y=3&board=.........") { NodeBudget = budget };
            solver.Solve();
            if (solver.Nodes > budget || !solver.BudgetExceeded) throw new Exception("Search exceeded strict node bound");
        }
        var depth = new Solver("x=3&y=3&board=.........") { DepthLimit = 1 };
        if (depth.Solve() || !depth.DepthExceeded || depth.BudgetExceeded) throw new Exception("Depth limit was not distinguished");
        foreach (string board in new[] { "x=0&y=1&board=", "x=-1&y=-1&board=X", "x=2147483647&y=2147483647&board=", "x=1&x=1&y=1&board=." })
        {
            bool rejected = false;
            try { CoilFormat.ParseBoard(board); } catch (FormatException) { rejected = true; }
            if (!rejected) throw new Exception("Invalid dimensions/query accepted");
        }
        Console.WriteLine("Search evaluation passed all 512 3x3 masks across 16 search configurations, strict budgets, and depth limits.");
    }
}
