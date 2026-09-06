using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using coil;

static class RegressionTests
{
    static void Main()
    {
        CharacterizationTests.Run();
        SolutionLimits();
        HardnessOrdering();
        IndexCaches();
        SavedSolutions();
        Console.WriteLine("All regression checks passed.");
    }

    static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    static void SolutionLimits()
    {
        const string board = "x=2&y=2&board=....";
        //Eight solutions across four starts; limits must apply across the whole search.
        foreach (var limit in new long[] { 1, 2, 3, 7, 8, 100 })
        {
            var solver = new Solver(board) { MaxSolutions = limit };
            Check(solver.Solve(), "2x2 board should be solvable");
            Check(solver.SolutionsFound == Math.Min(limit, 8), $"solution limit {limit}: got {solver.SolutionsFound}");
            CoilFormat.Validate(board, solver.FirstSolution);
        }
        var single = new Solver("x=1&y=1&board=.");
        Check(single.Solve() && single.SolutionsFound == 1, "single-cell solution");
        CoilFormat.Validate("x=1&y=1&board=.", single.FirstSolution);
        foreach (var limit in new long[] { 0, -1 })
        {
            var rejected = false;
            try
            {
                new Solver(board) { MaxSolutions = limit }.Solve();
            }
            catch (ArgumentOutOfRangeException)
            {
                rejected = true;
            }
            Check(rejected, $"invalid solution limit {limit} must be rejected");
        }
        Console.WriteLine("Solution limits passed.");
    }

    static void HardnessOrdering()
    {
        var scores = new[]
        {
            new Hardness.Score { Seed = 1, BudgetExceeded = true, Proxy = 10 },
            new Hardness.Score { Seed = 2, BudgetExceeded = true, Proxy = 11 },
            new Hardness.Score { Seed = 3, NodesAll = 100, Proxy = 12 },
            new Hardness.Score { Seed = 4, NodesAll = 200, Proxy = 9 },
        };
        Check(scores.OrderByDescending(s => s.Rank(true)).Select(s => s.Seed).SequenceEqual(new[] { 2, 1, 4, 3 }),
            "exact ranking must sort exceeded searches by proxy, then completed searches by nodes");
        Check(scores.OrderByDescending(s => s.Rank(false)).Select(s => s.Seed).SequenceEqual(new[] { 3, 2, 1, 4 }),
            "proxy ranking must ignore search budget and node counts");
        Console.WriteLine("Hardness ordering passed.");
    }

    static void IndexCaches()
    {
        foreach (var spaceFilled in new[] { true, false })
        {
            for (var seed = 0; seed < 20; seed++)
            {
                var config = new LevelConfiguration(
                    TweakPickers.GetPickers("rnd99").First(),
                    SegPickers.GetSegPickers("Weighted4").First(),
                    new OptimizationSetup { UseSpaceFillingIndexes = spaceFilled, GlobalTweakLim = 20 },
                    new InitialWanderSetup());
                var level = new Level(config, 12, 12, new Random(seed), seed);
                level.InitialWander(config);
                config.SegPicker.Init(seed, level);
                config.TweakPicker.Init(seed);
                level.RepeatedlyTweak(false, 1, Stopwatch.StartNew(), quiet: true);
                Validate(level);
                level.TrimDeadEnds();
                Validate(level);
                if (spaceFilled)
                {
                    level.RedoAllIndexesSpaceFilled();
                    Validate(level);
                }
            }
        }
        Console.WriteLine("Index caches and game-rule replay passed for 40 generated levels.");
    }

    static void Validate(Level level)
    {
        coil.Debug.DoDebug(level, validateBoard: true);
        CoilFormat.Validate(CoilFormat.BoardString(level), CoilFormat.SolutionString(level));
    }

    static void SavedSolutions()
    {
        var boards = Directory.GetFiles(Paths.In("levels/hard"), "*.board", SearchOption.AllDirectories);
        Check(boards.Length > 0, "hard-level fixtures are missing");
        foreach (var board in boards)
        {
            CoilFormat.Validate(File.ReadAllText(board), File.ReadAllText(Path.ChangeExtension(board, ".solution")));
        }
        Console.WriteLine($"Replayed {boards.Length} saved hard-level solutions.");
    }
}
