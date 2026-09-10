using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace coil
{
    partial class Program
    {
        static int UniformRejection(string[] args)
        {
            var (pos, opts) = ParseArgs(args);
            if (pos.Count != 3 || opts.Keys.Any(key => !new[] { "count", "max-attempts", "seed-hex", "node-budget" }.Contains(key)))
                throw new ArgumentException("sample-uniform-rejection <width> <height> <new-directory> [--count N] [--max-attempts N] [--seed-hex HEX] [--node-budget N]");
            var sampler = new UniformRejectionSampler(int.Parse(pos[0]), int.Parse(pos[1]));
            var count = int.Parse(opts.GetValueOrDefault("count", "48"));
            var maximum = long.Parse(opts.GetValueOrDefault("max-attempts", "10000000"));
            var budget = long.Parse(opts.GetValueOrDefault("node-budget", long.MaxValue.ToString()));
            if (count < 1 || maximum < count || budget < 0) throw new ArgumentException("Invalid count, attempt limit, or node budget.");
            var directory = Path.GetFullPath(pos[2], Paths.Root);
            if (Directory.Exists(directory)) throw new IOException($"Output exists: {directory}");
            var stream = opts.TryGetValue("seed-hex", out var hex) ? new SeededChoices(hex) : SeededChoices.Create();
            Directory.CreateDirectory(directory);
            var samples = new List<object>(); var watch = Stopwatch.StartNew();
            for (long attempt = 0; attempt < maximum && samples.Count < count; attempt++)
            {
                var result = sampler.TryDraw(stream.Next, budget);
                if (!result.HasValue) continue;
                var witness = FromSolution(result.Value.board, result.Value.solution); witness.Validate();
                var stem = Path.Combine(directory, $"sample-{samples.Count + 1:D4}");
                File.WriteAllText(stem + ".board", witness.BoardString());
                File.WriteAllText(stem + ".solution", witness.SolutionString());
                File.WriteAllText(stem + ".code.json", UniversalConstruction.Json(UniversalConstruction.Encode(witness)));
                samples.Add(new { index = samples.Count + 1, openCells = witness.Count, attempt = sampler.Attempts });
            }
            File.WriteAllText(Path.Combine(directory, "sampling.json"), JsonSerializer.Serialize(new {
                generator = "uniform-board-rejection-v1", sampler.Width, sampler.Height,
                requested = count, completed = samples.Count == count, sampler.Attempts, sampler.Disconnected, sampler.Unsolvable,
                sampler.Accepted, sampler.SolverNodes, seconds = watch.Elapsed.TotalSeconds, stream.SeedHex,
                distribution = "Independent uniform solvable boards under ideal fair bits; repeats retained; incomplete solver decisions abort.", samples
            }, new JsonSerializerOptions { WriteIndented = true }));
            if (samples.Count != count) throw new InvalidOperationException($"Attempt limit reached: {samples.Count}/{count} uniform samples. Partial results and status saved.");
            Console.WriteLine($"Uniform {sampler.Width}x{sampler.Height}: {count} samples / {sampler.Attempts} proposals in {watch.Elapsed.TotalSeconds:F2}s.");
            return 0;
        }

        static int DeepSpecimen(string[] args)
        {
            var (pos, opts) = ParseArgs(args);
            if (pos.Count != 2 || opts.Keys.Any(key => !new[] { "seed-hex", "steps", "activity", "init", "kernel", "observations" }.Contains(key)))
                throw new ArgumentException("deep-specimen <new-directory> <side> [--seed-hex HEX] [--steps N] [--activity N] [--init horizontal|vertical|singleton|backward] [--kernel deep|legacy] [--observations N]");
            var directory = Path.GetFullPath(pos[0], Paths.Root);
            if (Directory.Exists(directory)) throw new IOException($"Output exists: {directory}");
            var side = int.Parse(pos[1]);
            if (side < 1 || (long)side * side > int.MaxValue) throw new ArgumentException("Invalid side.");
            var steps = long.Parse(opts.GetValueOrDefault("steps", (100L * side * side).ToString()));
            var activity = int.Parse(opts.GetValueOrDefault("activity", "2"));
            var observations = int.Parse(opts.GetValueOrDefault("observations", "200"));
            var kernel = opts.GetValueOrDefault("kernel", "deep"); var init = opts.GetValueOrDefault("init", "horizontal");
            if (steps < 4L * side * side || activity < 1 || observations < 2 || observations > steps || (kernel != "deep" && kernel != "legacy"))
                throw new ArgumentException("Require steps >= 4*area, positive activity, 2..steps observations, kernel deep|legacy.");
            var stream = opts.TryGetValue("seed-hex", out var hex) ? new SeededChoices(hex) : SeededChoices.Create();
            long draws = 0; int Choose(int bound) { draws++; return stream.Next(bound); }
            var seed = init switch {
                "horizontal" => ReversiblePathSampler.Stripes(side, side),
                "vertical" => Transpose(ReversiblePathSampler.Stripes(side, side)),
                "singleton" => new BackwardGenerator(side, side, Choose(side), Choose(side)),
                "backward" => BackwardGenerator.Generate(side, side, Choose),
                _ => throw new ArgumentException($"Unknown initialization {init}.")
            };
            var sampler = new ReversiblePathSampler(seed);
            var watch = Stopwatch.StartNew(); var lastReport = 0.0;
            var trace = new List<object> { Observe(sampler, 0, seed, seed) };
            BackwardGenerator previous = seed;
            for (long step = 1; step <= steps; step++)
            {
                if (kernel == "deep") sampler.DeepStep(Choose, activity); else sampler.Step(Choose, activity);
                if (step % Math.Max(1, steps / observations) == 0 || step == steps)
                {
                    var current = sampler.Snapshot();
                    trace.Add(Observe(sampler, step, current, previous)); previous = current;
                    if (watch.Elapsed.TotalSeconds - lastReport >= 10)
                    {
                        Console.WriteLine($"{Path.GetFileName(directory)}: {step}/{steps}, {sampler.Count} open, {sampler.BlockChanges} block changes");
                        lastReport = watch.Elapsed.TotalSeconds;
                    }
                }
            }
            var final = sampler.Snapshot();
            var dynamics = new { kernel, steps, activity, initialization = init, initialOpenCells = seed.Count,
                blockProbability = kernel == "deep" ? 0.125 : 0, blockSides = new[] { 2, 3, 4 },
                sampler.BlockAttempts, sampler.BlockEligible, sampler.BlockChanges, sampler.BlockCandidates,
                attempted = sampler.Attempted, accepted = sampler.Accepted, trace,
                stationaryTarget = "solutionCount(board) * activity^openCells(board)",
                mixing = "No certified mixing time. Compare multiple initializations, geometry, and within-chain persistence." };
            WriteBackwardSpecimen(directory, final, stream.SeedHex, watch.Elapsed.TotalSeconds, draws, side * side,
                kernel == "deep" ? "block-solution-edits-v1" : "reversible-path-v1",
                kernel == "deep" ? "block-heat-bath-mixture-v1" : "legacy-control-v1", "step-budget", dynamics);
            return 0;
        }

        static BackwardGenerator Transpose(BackwardGenerator board)
        {
            var recipe = board.Recipe();
            return BackwardGenerator.Replay(new BackwardRecipe { Width = recipe.Height, Height = recipe.Width,
                FinishX = recipe.FinishY, FinishY = recipe.FinishX,
                Growth = new string(recipe.Growth.Select(d => d == 'U' ? 'L' : d == 'R' ? 'D' : d == 'D' ? 'R' : 'U').ToArray()) });
        }

        static object Observe(ReversiblePathSampler sampler, long step, BackwardGenerator board, BackwardGenerator previous)
        {
            var horizontal = 0; var vertical = 0; var differing = 0;
            for (var cell = 0; cell < board.Width * board.Height; cell++)
            {
                if (board.IsOpen(cell) != previous.IsOpen(cell)) differing++;
                if (!board.IsOpen(cell)) continue;
                if (cell % board.Width + 1 < board.Width && board.IsOpen(cell + 1)) horizontal++;
                if (cell / board.Width + 1 < board.Height && board.IsOpen(cell + board.Width)) vertical++;
            }
            var path = board.ReversePath; var edgesH = 0;
            for (var i = 1; i < path.Count; i++) if (path[i] / board.Width == path[i - 1] / board.Width) edgesH++;
            return new { step, openCells = board.Count, openFraction = (double)board.Count / (board.Width * board.Height),
                adjacencyAnisotropy = horizontal + vertical == 0 ? 0 : (double)(horizontal - vertical) / (horizontal + vertical),
                absoluteAnisotropy = horizontal + vertical == 0 ? 0 : Math.Abs((double)(horizontal - vertical) / (horizontal + vertical)),
                pathAnisotropy = board.Count == 1 ? 0 : (double)(2 * edgesH - board.Count + 1) / (board.Count - 1),
                solutionSlides = CoilFormat.ParseSolution(board.SolutionString()).path.Length,
                changedFractionSincePrevious = (double)differing / (board.Width * board.Height), sampler.BlockChanges };
        }

        static int DeepDistributionStudy(string[] args)
        {
            if (args.Length != 1) throw new ArgumentException("deep-distribution-study <new-json-file>");
            var file = Path.GetFullPath(args[0], Paths.Root);
            if (File.Exists(file)) throw new IOException($"Output exists: {file}");
            var result = PathDistributionStudy.Run(deep: true);
            Directory.CreateDirectory(Path.GetDirectoryName(file));
            File.WriteAllText(file, JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
            return 0;
        }
    }
}
