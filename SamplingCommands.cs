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
        static int RefreshSamplingDetails(string[] args)
        {
            if (args.Length != 1) throw new ArgumentException("refresh-sampling-details <collection>");
            foreach (var file in Directory.GetFiles(Path.GetFullPath(args[0], Paths.Root), "stats.json", SearchOption.AllDirectories))
            {
                using var data = JsonDocument.Parse(File.ReadAllText(file));
                if (data.RootElement.GetProperty("generator").GetString() != "reversible-path-v1")
                    throw new ArgumentException("Expected reversible-path-v1 collection.");
                if (data.RootElement.GetProperty("side").GetInt32() <= 128) continue;
                var dir = Path.GetDirectoryName(file);
                using var map = SixLabors.ImageSharp.Image.Load(Path.Combine(dir, "map.png"));
                using var solution = SixLabors.ImageSharp.Image.Load(Path.Combine(dir, "solution-map.png"));
                SaveDetail(map, Path.Combine(dir, "center-detail.png"));
                SaveDetail(solution, Path.Combine(dir, "center-solution-detail.png"));
            }
            return 0;
        }

        static int SampleUniform(string[] args)
        {
            var (pos, opts) = ParseArgs(args);
            if (pos.Count != 2) throw new ArgumentException("sample-uniform <side> <new-directory> [--count N] [--seed-hex HEX]");
            foreach (var key in opts.Keys) if (!new[] { "count", "seed-hex", "method", "burn-in", "stride" }.Contains(key)) throw new ArgumentException($"Unknown option {key}.");
            var side = int.Parse(pos[0]); var count = int.Parse(opts.GetValueOrDefault("count", "30"));
            if (count < 1) throw new ArgumentException("count must be positive.");
            var method = opts.GetValueOrDefault("method", "exact");
            var burnIn = int.Parse(opts.GetValueOrDefault("burn-in", "4096"));
            var stride = int.Parse(opts.GetValueOrDefault("stride", "128"));
            if ((method != "exact" && method != "chain") || burnIn < 0 || stride < 1) throw new ArgumentException("Invalid method, burn-in, or stride.");
            if (method == "exact" && (opts.ContainsKey("burn-in") || opts.ContainsKey("stride"))) throw new ArgumentException("burn-in and stride only apply to method chain.");
            var dir = Path.GetFullPath(pos[1], Paths.Root);
            if (Directory.Exists(dir)) throw new IOException($"Output exists: {dir}");
            var stream = opts.TryGetValue("seed-hex", out var hex) ? new SeededChoices(hex) : SeededChoices.Create();
            var catalogue = new UniformBoardSampler(side, side);
            var current = catalogue.Masks[0];
            if (method == "chain") for (var step = 0; step < burnIn; step++) current = catalogue.Step(current, stream.Next);
            Directory.CreateDirectory(dir);
            var samples = new List<object>();
            for (var i = 0; i < count; i++)
            {
                if (method == "chain") for (var step = 0; step < stride; step++) current = catalogue.Step(current, stream.Next);
                var mask = method == "exact" ? catalogue.Draw(stream.Next) : current;
                var stem = Path.Combine(dir, $"sample-{i + 1:D4}");
                var board = catalogue.Board(mask); var solution = catalogue.Solution(mask);
                var witness = FromSolution(board, solution); witness.Validate();
                File.WriteAllText(stem + ".board", board + "\n"); File.WriteAllText(stem + ".solution", solution + "\n");
                File.WriteAllText(stem + ".recipe.json", witness.RecipeJson());
                samples.Add(new { index = i + 1, mask, openCells = witness.Count });
            }
            File.WriteAllText(Path.Combine(dir, "sampling.json"), JsonSerializer.Serialize(new {
                generator = method == "exact" ? "exact-uniform-board-v1" : "uniform-board-chain-v1", method,
                side, count, seedHex = stream.SeedHex, randomAlgorithm = SeededChoices.Algorithm, catalogueSize = catalogue.Masks.Count,
                burnIn = method == "chain" ? burnIn : 0, stride = method == "chain" ? stride : 0,
                distribution = method == "exact" ? "uniform solvable boards; independent bounded-index draws under ideal random choices"
                    : "uniform stationary board distribution; finite burn-in; correlated chain samples; all stays counted", samples }, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine($"Wrote {count} {method} draws from {catalogue.Masks.Count} solvable boards -> {dir}");
            return 0;
        }

        static BackwardGenerator FromSolution(string board, string solution)
        {
            CoilFormat.Validate(board, solution);
            var (w, h, walls) = CoilFormat.ParseBoard(board);
            var (x, y, commands) = CoilFormat.ParseSolution(solution);
            var visited = new bool[w * h]; var path = new List<int> { y * w + x }; visited[path[0]] = true;
            foreach (var command in commands)
            {
                var dx = command == 'R' ? 1 : command == 'L' ? -1 : 0;
                var dy = command == 'D' ? 1 : command == 'U' ? -1 : 0;
                while (x + dx >= 0 && y + dy >= 0 && x + dx < w && y + dy < h &&
                    !walls[(y + dy) * w + x + dx] && !visited[(y + dy) * w + x + dx])
                {
                    x += dx; y += dy; path.Add(y * w + x); visited[y * w + x] = true;
                }
            }
            var growth = new System.Text.StringBuilder();
            for (var i = path.Count - 1; i > 0; i--)
                growth.Append(path[i - 1] / w < path[i] / w ? 'U' : path[i - 1] / w > path[i] / w ? 'D' : path[i - 1] > path[i] ? 'R' : 'L');
            return BackwardGenerator.Replay(new BackwardRecipe { Width = w, Height = h, FinishX = x, FinishY = y, Growth = growth.ToString() });
        }

        static int SamplingStudy(string[] args)
        {
            if (args.Length != 1) throw new ArgumentException("sampling-study <new-json-file>");
            var path = Path.GetFullPath(args[0], Paths.Root);
            if (File.Exists(path) || File.Exists(Path.ChangeExtension(path, ".paths.json"))) throw new IOException($"Study output exists: {path}");
            var results = new List<object>();
            foreach (var side in new[] { 2, 3, 4 })
            {
                var catalogue = new UniformBoardSampler(side, side);
                results.Add(catalogue.DistributionStudy());
                Console.WriteLine($"Propagated uniform-board chain on {side}x{side}: {catalogue.Masks.Count} states.");
            }
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
            File.WriteAllText(Path.ChangeExtension(path, ".paths.json"), JsonSerializer.Serialize(PathDistributionStudy.Run(), new JsonSerializerOptions { WriteIndented = true }));
            return 0;
        }

        static int ReversibleSpecimen(string[] args)
        {
            var (pos, opts) = ParseArgs(args);
            if (pos.Count != 2) throw new ArgumentException("reversible-specimen <new-directory> <side> [--seed-hex HEX] [--steps N] [--activity N] [--init stripes|singleton|backward] [--spacing N]");
            foreach (var key in opts.Keys)
                if (!new[] { "seed-hex", "steps", "activity", "init", "spacing" }.Contains(key)) throw new ArgumentException($"Unknown option {key}.");
            var dir = Path.GetFullPath(pos[0], Paths.Root);
            if (Directory.Exists(dir)) throw new IOException($"Output exists: {dir}");
            var side = int.Parse(pos[1]);
            var activity = int.Parse(opts.GetValueOrDefault("activity", "2"));
            var steps = long.Parse(opts.GetValueOrDefault("steps", (20L * side * side).ToString()));
            var spacing = int.Parse(opts.GetValueOrDefault("spacing", "2"));
            var init = opts.GetValueOrDefault("init", "stripes");
            if (steps < 0 || activity < 1) throw new ArgumentException("steps must be nonnegative and activity positive.");
            var stream = opts.TryGetValue("seed-hex", out var hex) ? new SeededChoices(hex) : SeededChoices.Create();
            var watch = Stopwatch.StartNew();
            long draws = 0;
            int Choose(int bound) { draws++; return stream.Next(bound); }
            var initial = init switch {
                "stripes" => ReversiblePathSampler.Stripes(side, side, spacing),
                "singleton" => new BackwardGenerator(side, side, Choose(side), Choose(side)),
                "backward" => BackwardGenerator.Generate(side, side, Choose),
                _ => throw new ArgumentException($"Unknown initialization {init}.")
            };
            var sampler = new ReversiblePathSampler(initial);
            var trace = new List<object> { new { step = 0L, openCells = sampler.Count } };
            var lastReport = watch.Elapsed.TotalSeconds;
            for (long i = 1; i <= steps; i++)
            {
                sampler.Step(Choose, activity);
                if (i % Math.Max(1, steps / 20) == 0 || i == steps)
                {
                    trace.Add(new { step = i, openCells = sampler.Count });
                    if (watch.Elapsed.TotalSeconds - lastReport >= 10)
                    {
                        Console.WriteLine($"{Path.GetFileName(dir)} step {i}/{steps}: {sampler.Count} open"); lastReport = watch.Elapsed.TotalSeconds;
                    }
                }
            }
            var final = sampler.Snapshot();
            var dynamics = new { activity, steps, initialization = init, spacing, initialOpenCells = initial.Count,
                endpointMax = 8, rectangleMax = 12, heightMax = 3, lazyProbability = 0.5,
                operationOrder = new[] { "start-grow", "start-delete", "finish-grow", "finish-delete", "rectangle-grow", "rectangle-delete" },
                attempted = sampler.Attempted, accepted = sampler.Accepted, reindexes = sampler.Reindexes, trace,
                stationaryTarget = "ordered-solution weight activity^openCells; board weight solutionCount * activity^openCells",
                mixing = "Finite run; convergence not established. Initialization can strongly influence output." };
            WriteBackwardSpecimen(dir, final, stream.SeedHex, watch.Elapsed.TotalSeconds, draws, side * side,
                "reversible-path-v1", "symmetric-edit-metropolis-v1", "step-budget", dynamics);
            return 0;
        }
    }
}
