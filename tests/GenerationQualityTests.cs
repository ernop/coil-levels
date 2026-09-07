using System;
using System.Linq;
using coil;

static class GenerationQualityTests
{
    static LevelConfiguration Configuration(int? steps = null) => new LevelConfiguration(
        TweakPickers.GetPickers("rnd99").Single(), SegPickers.GetSegPickers("Weighted4").Single(),
        new OptimizationSetup { UseSpaceFillingIndexes = true, GlobalTweakLim = 20 },
        new InitialWanderSetup { GoMax = true, StepLimit = steps });

    public static void Run()
    {
        // This seed used to spiral almost across the entire 500-square board before tweaking.
        var oldConfig = Configuration(int.MaxValue);
        var old = new Level(oldConfig, 500, 500, new Random(303), 303);
        old.InitialWander(oldConfig);
        Require(Rejected(old), "unbounded spiral must fail the output quality policy");
        var boundedConfig = Configuration();
        var bounded = new Level(boundedConfig, 500, 500, new Random(303), 303);
        bounded.InitialWander(boundedConfig);
        Require(bounded.Segs.Count == 8, "full-slide default must stop after eight segments");
        GenerationQuality.Validate(bounded);
        var explicitConfig = Configuration(3);
        var explicitWalk = new Level(explicitConfig, 500, 500, new Random(303), 303);
        explicitWalk.InitialWander(explicitConfig);
        Require(explicitWalk.Segs.Count == 3, "explicit step limit must be retained");

        // Synthetic occupancy fixtures isolate the guard's threshold and rectangular indexing.
        foreach (var size in new[] { (41, 41), (120, 40), (100, 100) })
        {
            int limit = GenerationQuality.MaximumOpenSquareSide(size.Item1, size.Item2);
            var level = new Level(Configuration(), size.Item1, size.Item2, new Random(0), 0);
            var owner = new Seg((1, 1), Dir.Right, 1);
            for (int y = 1; y <= limit; y++) for (int x = 1; x <= limit; x++) level.SetRowValue((x, y), owner);
            GenerationQuality.Validate(level);
            for (int y = 1; y <= limit + 1; y++) for (int x = 1; x <= limit + 1; x++) level.SetRowValue((x, y), owner);
            Require(Rejected(level), "first oversized square must fail, including rectangular boards");
        }
        var names = TweakPickers.GetPickers(null).Select(p => p.Name).ToArray();
        Require(names.Distinct().Count() == names.Length, "picker names must be unambiguous");
        Require(names.Contains("len23-10th-exact") && names.Contains("len23-10th"), "historical and exact probability choices must both be accessible");
        Console.WriteLine("Generation quality passed: historical spiral, bounded and explicit walks, threshold fixtures, unique picker names.");
    }

    static bool Rejected(Level level)
    {
        try { GenerationQuality.Validate(level); return false; }
        catch (InvalidOperationException e) when (e.Message.StartsWith("Generation quality failed:")) { return true; }
    }
    static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
