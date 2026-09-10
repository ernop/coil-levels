using System;
using System.Collections.Generic;
using System.Linq;
using coil;

static class BackwardGenerationTests
{
    public static void Run()
    {
        LargeSeeds();
        NonMaximalAndReplay();
        IllegalExtensions();
        UniversalCodes();
        Exhaustive(1, 1);
        Exhaustive(1, 4);
        Exhaustive(4, 1);
        Exhaustive(4, 4);
    }

    static void UniversalCodes()
    {
        // Forward play visits (0,2),(1,2),(1,1),(0,1),(0,0). Reverse
        // traversal would turn after the first D, which is an unblocked slide.
        var oneWay = BackwardGenerator.Replay(new BackwardRecipe {
            Width = 3, Height = 3, FinishX = 0, FinishY = 0, Growth = "DRDL"
        });
        Check(oneWay.BoardString() == "x=3&y=3&board=.XX..X..X" && oneWay.SolutionString() == "x=0&y=2&path=RULU",
            "Forward-only fixture must have the intended board and solution.");
        oneWay.Validate();
        Check(UniversalConstruction.Encode(oneWay).CodeHex == "1EB5", "Version 1 construction code reference vector.");
        var reverseRejected = false;
        try { CoilFormat.Validate(oneWay.BoardString(), "x=0&y=0&path=DRDL"); }
        catch (InvalidOperationException) { reverseRejected = true; }
        Check(reverseRejected, "The reverse traversal must fail Mortal Coil physics.");
        RoundTripCode(oneWay);
        var nonMaximal = BackwardGenerator.Replay(new BackwardRecipe {
            Width = 3, Height = 3, FinishX = 1, FinishY = 0, Growth = "LD"
        });
        Check(Enumerable.Range(0, 4).Any(d => nonMaximal.CanPrepend((Dir)d)), "L fixture remains extensible.");
        RoundTripCode(nonMaximal);
        // The public numeric input accepts zero, odd-length hex, a high sign
        // bit, leading zeros, redundant high digits, and codes far above 512 bits.
        foreach (var hex in new[] { "0", "F", "80", "0001", new string('F', 4097) })
            UniversalConstruction.Decode(7, 11, hex).Validate();
        var rng = new Random(913);
        for (var i = 0; i < 200; i++)
        {
            var bytes = new byte[128]; rng.NextBytes(bytes);
            UniversalConstruction.Decode(8, 5, Convert.ToHexString(bytes)).Validate();
        }
        // Linear-size million-cell encoding and replay, with genuine high
        // digits so this exercises more than integer-header parsing.
        var large = ReversiblePathSampler.Stripes(1000, 1000);
        var code = UniversalConstruction.Encode(large);
        Check(code.CodeHex.Length > 128, "Million-cell board requires more than a fixed 512-bit code here.");
        RoundTripCode(large);
        Console.WriteLine("Universal codes: one-way play, non-maximal board, arbitrary hex input, and 1000x1000 replay passed.");
    }

    static void RoundTripCode(BackwardGenerator board)
    {
        var code = UniversalConstruction.Encode(board);
        var replay = UniversalConstruction.Decode(UniversalConstruction.Read(UniversalConstruction.Json(code)));
        Check(replay.BoardString() == board.BoardString() && replay.SolutionString() == board.SolutionString(),
            "Universal code must reproduce the exact board and forward solution.");
        Check(UniversalConstruction.Encode(replay).CodeHex == code.CodeHex, "Canonical construction code is stable.");
    }

    static void LargeSeeds()
    {
        var hex = Convert.ToHexString(Enumerable.Range(0, 64).Select(i => (byte)i).ToArray());
        var stream = new SeededChoices(hex);
        var expected = new[] { 466129, 70574, 658379, 818983, 424692, 663479, 332961, 416562 };
        Check(expected.SequenceEqual(expected.Select(_ => stream.Next(1000000))), "SHA-256 seed stream reference vector.");
        var first = new SeededChoices(hex);
        var second = new SeededChoices(hex.ToLowerInvariant());
        var longer = new SeededChoices(hex + "01");
        var values = Enumerable.Range(0, 100).Select(_ => first.Next(int.MaxValue)).ToArray();
        Check(values.SequenceEqual(values.Select(_ => second.Next(int.MaxValue))), "Large seed is reproducible across hex casing.");
        Check(!values.SequenceEqual(values.Select(_ => longer.Next(int.MaxValue))), "Seed suffix participates in the stream.");
        var board = BackwardGenerator.Generate(40, 30, new SeededChoices(hex).Next);
        var replay = BackwardGenerator.Generate(40, 30, new SeededChoices(hex).Next);
        Check(board.RecipeJson(hex) == replay.RecipeJson(hex), "Seed reproduces the complete construction recipe.");
        board.Validate();
        Console.WriteLine("Large seed stream reference vector and reproducibility passed.");
    }

    static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    static void NonMaximalAndReplay()
    {
        // Finish cell, target minus one, then the two selected legal moves.
        var choices = new Queue<int>(new[] { 0, 2, 0, 1 });
        var board = BackwardGenerator.Generate(3, 3, bound => choices.Dequeue());
        board.Validate();
        Check(board.Count == 3 && choices.Count == 0, "Stop at the chosen size.");
        Check(Enumerable.Range(0, 4).Any(d => board.CanPrepend((Dir)d)),
            "Non-maximal board must be emitted while further digging is possible.");
        var replay = BackwardGenerator.Replay(board.Recipe());
        replay.Validate();
        Check(replay.BoardString() == board.BoardString() && replay.SolutionString() == board.SolutionString(),
            "Recipe must reproduce both board and solution.");
        var single = BackwardGenerator.Generate(3, 3, bound => 0);
        single.Validate();
        Check(single.Count == 1 && single.SolutionString() == "x=0&y=0&path=", "Singleton output.");
        for (var seed = 0; seed < 100; seed++)
            BackwardGenerator.Generate(30, 20, new Random(seed).Next).Validate();
        Console.WriteLine("Backward stopping, replay, singleton, and 100 sampled boards passed.");
    }

    static void IllegalExtensions()
    {
        var board = BackwardGenerator.Replay(new BackwardRecipe
        {
            Width = 2, Height = 3, FinishX = 0, FinishY = 0, Growth = "RDL"
        });
        board.Validate();
        Check(!board.CanPrepend(Dir.Down), "Cannot turn with an unvisited open square straight ahead.");
        var before = board.RecipeJson();
        var rejected = false;
        try { board.Prepend(Dir.Down); }
        catch (InvalidOperationException) { rejected = true; }
        Check(rejected && board.RecipeJson() == before, "Illegal extension must throw without modifying state.");
        Check(!board.CanPrepend(Dir.Left) && !board.CanPrepend(Dir.Up), "Reject boundary and occupied cell.");
    }

    static void Exhaustive(int width, int height)
    {
        var generated = new Dictionary<int, HashSet<string>>();
        void Visit(BackwardGenerator board)
        {
            board.Validate();
            RoundTripCode(board);
            var mask = 0;
            foreach (var cell in board.ReversePath) mask |= 1 << cell;
            if (!generated.TryGetValue(mask, out var solutions))
                generated[mask] = solutions = new HashSet<string>();
            Check(solutions.Add(board.SolutionString()), "Duplicate construction for an ordered solution.");
            for (var d = 0; d < 4; d++)
            {
                if (!board.CanPrepend((Dir)d)) continue;
                var next = BackwardGenerator.Replay(board.Recipe());
                next.Prepend((Dir)d);
                Visit(next);
            }
        }
        for (var cell = 0; cell < width * height; cell++)
            Visit(new BackwardGenerator(width, height, cell % width, cell / width));

        var validBoards = 0;
        long solutionCount = 0;
        for (var mask = 1; mask < 1 << (width * height); mask++)
        {
            var cells = new string(Enumerable.Range(0, width * height)
                .Select(cell => (mask & (1 << cell)) != 0 ? '.' : 'X').ToArray());
            var solver = new Solver($"x={width}&y={height}&board={cells}")
            {
                MaxSolutions = long.MaxValue, NodeBudget = long.MaxValue
            };
            solver.Solve();
            var actual = generated.TryGetValue(mask, out var paths) ? paths.Count : 0;
            Check(!solver.BudgetExceeded && solver.SolutionsFound == actual,
                $"Backward completeness {width}x{height} mask={mask}: generated {actual}, solver {solver.SolutionsFound}.");
            if (actual > 0) validBoards++;
            solutionCount += actual;
        }
        Console.WriteLine($"Backward + universal-code exhaustive {width}x{height}: {validBoards} boards, {solutionCount} solutions; exact agreement.");
    }
}
