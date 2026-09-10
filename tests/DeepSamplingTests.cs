using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using coil;

static class DeepSamplingTests
{
    static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    public static void Run()
    {
        var catalogue = new UniformBoardSampler(3, 3);
        var rejection = new UniformRejectionSampler(3, 3);
        for (var board = 0; board < 512; board++)
        {
            var bit = 0;
            var result = rejection.TryDraw(_ => (board >> bit++) & 1);
            Check(result.HasValue == catalogue.Contains(board), "Uniform rejection membership must match every 3x3 board.");
            if (result.HasValue) CoilFormat.Validate(result.Value.board, result.Value.solution);
        }
        Check(rejection.Accepted == 171, "Exactly one accepted bit string per solvable board.");
        var incomplete = false;
        try { new UniformRejectionSampler(3, 3).TryDraw(_ => 1, 0); }
        catch (InvalidOperationException) { incomplete = true; }
        Check(incomplete, "Unknown solver decision must abort uniform rejection.");

        var states = new List<BackwardGenerator>();
        void Visit(BackwardGenerator board)
        {
            states.Add(board);
            for (var d = 0; d < 4; d++) if (board.CanPrepend((Dir)d))
            {
                var child = BackwardGenerator.Replay(board.Recipe()); child.Prepend((Dir)d); Visit(child);
            }
        }
        for (var cell = 0; cell < 9; cell++) Visit(new BackwardGenerator(3, 3, cell % 3, cell / 3));
        var routesChecked = 0;
        foreach (var board in states)
        {
            var sampler = new ReversiblePathSampler(board);
            for (var size = 2; size <= 3; size++)
            for (var y = 0; y <= 3 - size; y++) for (var x = 0; x <= 3 - size; x++)
            for (var entry = 0; entry < 4 * size; entry++)
            {
                var options = sampler.EnumerateBlock(x, y, size, entry);
                Check(sampler.Snapshot().RecipeJson() == board.RecipeJson(), "Enumeration must leave board and solution unchanged.");
                if (options == null) continue;
                var expectedSet = options.Routes.Select(r => string.Join(',', r)).ToHashSet();
                Check(expectedSet.Count == options.Routes.Count, "Conditional routes must not contain duplicates.");
                for (var index = 0; index < options.Routes.Count; index++)
                {
                    sampler.ApplyBlock(options, index); sampler.Snapshot().Validate(); routesChecked++;
                    var reverse = sampler.EnumerateBlock(x, y, size, entry);
                    Check(reverse != null && reverse.Routes.Select(r => string.Join(',', r)).ToHashSet().SetEquals(expectedSet),
                        "Every candidate must have the identical reverse conditional support.");
                    foreach (var activity in new[] { 1, 2, 4 })
                    {
                        var n = options.Original.Length; var m = options.Routes[index].Length;
                        Check(BigInteger.Pow(activity, board.Count) * BigInteger.Pow(activity, m) ==
                            BigInteger.Pow(activity, sampler.Count) * BigInteger.Pow(activity, n), "Exact conditional detailed balance.");
                    }
                    sampler.ApplyBlock(reverse, reverse.Routes.ToList().FindIndex(r => r.SequenceEqual(options.Original)));
                    Check(sampler.Snapshot().RecipeJson() == board.RecipeJson(), "Block inverse must restore the ordered solution.");
                }
            }
        }
        var random = new Random(82019);
        foreach (var activity in new[] { 1, 2, 4 })
        {
            var sampler = new ReversiblePathSampler(ReversiblePathSampler.Stripes(24, 19));
            for (var i = 0; i < 15000; i++) { sampler.DeepStep(random.Next, activity); if (i % 100 == 0) sampler.Snapshot().Validate(); }
            sampler.Snapshot().Validate();
            Check(sampler.BlockChanges > 0, "Block sampler must make changes on larger boards.");
        }
        Console.WriteLine($"Deep sampling: all 512 board decisions, {routesChecked} conditional routes and exact inverse supports, 45000 mixed steps passed.");
    }
}
