using System;
using System.Collections.Generic;
using System.Linq;
using coil;

static class SamplingTests
{
    static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    public static void Run()
    {
        var catalogue = new UniformBoardSampler(3, 3);
        Check(catalogue.Masks.Count == 171, "Uniform catalogue size.");
        for (var i = 0; i < catalogue.Masks.Count; i++)
        {
            var index = i;
            Check(catalogue.Draw(bound => index) == catalogue.Masks[i], "Each board gets exactly one uniform index.");
        }
        foreach (var mask in catalogue.Masks)
        {
            Check(catalogue.Step(mask, bound => 0) == mask, "Lazy steps counted.");
            for (var bit = 0; bit < 9; bit++)
            {
                var proposed = mask ^ (1 << bit);
                var actual = catalogue.Step(mask, bound => bound == 2 ? 1 : bit);
                Check(actual == (catalogue.Contains(proposed) ? proposed : mask), "Uniform-cell transition.");
                if (actual != mask) Check(catalogue.Step(actual, bound => bound == 2 ? 1 : bit) == mask, "Reverse transition has same descriptor.");
            }
        }
        var remaining = catalogue.Masks.ToHashSet(); var todo = new Stack<int>(); todo.Push(remaining.First()); remaining.Remove(todo.Peek());
        while (todo.Count > 0)
        {
            var mask = todo.Pop();
            for (var bit = 0; bit < 9; bit++) if (remaining.Remove(mask ^ (1 << bit))) todo.Push(mask ^ (1 << bit));
        }
        Check(remaining.Count == 0, "Solvable-board flip graph connected.");
        var paths = new List<BackwardRecipe>();
        void Visit(BackwardGenerator board)
        {
            paths.Add(board.Recipe());
            for (var d = 0; d < 4; d++) if (board.CanPrepend((Dir)d))
            {
                var child = BackwardGenerator.Replay(board.Recipe()); child.Prepend((Dir)d); Visit(child);
            }
        }
        for (var cell = 0; cell < 9; cell++) Visit(new BackwardGenerator(3, 3, cell % 3, cell / 3));
        var successes = 0;
        foreach (var recipe in paths)
        {
            var original = BackwardGenerator.Replay(recipe);
            var sampler = new ReversiblePathSampler(original);
            void Restored() => Check(sampler.Snapshot().RecipeJson() == original.RecipeJson(), "Inverse must restore exact ordered path.");
            foreach (var atStart in new[] { true, false })
            foreach (var grow in new[] { true, false })
            for (var d = 0; d < 4; d++)
            for (var length = 1; length <= 2; length++)
            {
                if (sampler.Endpoint(atStart, grow, d, length, 1, _ => 0))
                {
                    sampler.Snapshot().Validate(); successes++;
                    Check(sampler.Endpoint(atStart, !grow, d, length, 1, _ => 0), "Endpoint inverse rejected.");
                }
                Restored();
            }
            foreach (var grow in new[] { true, false })
            for (var a = 0; a < 9; a++)
            for (var d = 0; d < 4; d++)
            foreach (var right in new[] { true, false })
            {
                if (sampler.Rectangle(grow, a, d, 2, 1, right, 1, _ => 0))
                {
                    sampler.Snapshot().Validate(); successes++;
                    Check(sampler.Rectangle(!grow, a, d, 2, 1, right, 1, _ => 0), "Rectangle inverse rejected.");
                }
                Restored();
            }
        }
        foreach (var activity in new[] { 1, 2, 4 })
        foreach (var seed in new[] { 11, 22, 33 })
        {
            var rng = new Random(seed);
            var sampler = new ReversiblePathSampler(ReversiblePathSampler.Stripes(16, 12));
            for (var i = 0; i < 20000; i++) { sampler.Step(rng.Next, activity); if (i % 100 == 0) sampler.Snapshot().Validate(); }
            sampler.Snapshot().Validate();
        }
        Console.WriteLine($"Uniform-board indexing, symmetric connected transitions, {paths.Count} path states / {successes} reversible edits, and 180000 mutation steps passed.");
    }
}
