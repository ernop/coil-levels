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
        static int GenAny(string[] args)
        {
            var (pos, opts) = ParseArgs(args);
            foreach (var key in opts.Keys)
                if (!new[] { "code", "code-file", "out", "sampler", "steps", "activity" }.Contains(key))
                    throw new ArgumentException($"Unknown gen-any option --{key}.");
            var suppliedCode = opts.ContainsKey("code") || opts.ContainsKey("code-file");
            if (opts.ContainsKey("code") && opts.ContainsKey("code-file"))
                throw new ArgumentException("Choose --code or --code-file, not both.");
            if (suppliedCode && new[] { "sampler", "steps", "activity" }.Any(opts.ContainsKey))
                throw new ArgumentException("Construction codes supply all choices; sampling options cannot be combined with them.");
            var stem = opts.TryGetValue("out", out var output) ? Path.GetFullPath(output)
                : Paths.In($"output/universal/{Guid.NewGuid():N}");
            foreach (var extension in new[] { ".board", ".solution", ".recipe.json", ".code.json", ".generation.json" })
                if (File.Exists(stem + extension)) throw new IOException($"Output already exists: {stem + extension}");

            var watch = Stopwatch.StartNew();
            BackwardGenerator board;
            var samplerName = suppliedCode ? "construction-code" : opts.GetValueOrDefault("sampler", "deep");
            object dynamics = null;
            if (opts.TryGetValue("code-file", out var codeFile))
            {
                if (pos.Count != 0) throw new ArgumentException("--code-file supplies dimensions; omit positional arguments.");
                board = UniversalConstruction.Decode(UniversalConstruction.Read(File.ReadAllText(codeFile)));
            }
            else
            {
                if (pos.Count != 2) throw new ArgumentException("gen-any requires width height, or --code-file FILE.");
                var width = int.Parse(pos[0]); var height = int.Parse(pos[1]);
                if (width < 1 || height < 1 || (long)width * height > int.MaxValue)
                    throw new ArgumentOutOfRangeException(nameof(width), "Dimensions must be positive and fit an array.");
                if (opts.TryGetValue("code", out var hex)) board = UniversalConstruction.Decode(width, height, hex);
                else
                {
                    var random = new FreshChoices();
                    if (samplerName == "backward")
                    {
                        if (opts.ContainsKey("steps") || opts.ContainsKey("activity"))
                            throw new ArgumentException("--steps and --activity apply only to --sampler deep or edits.");
                        board = BackwardGenerator.Generate(width, height, random.Next);
                        dynamics = new { randomSource = "system-random-bytes", random.Draws,
                            distribution = "Uniform target size and legal growth choices; early trapping biases occupancy." };
                    }
                    else if (samplerName == "edits" || samplerName == "deep")
                    {
                        var area = checked(width * height);
                        var steps = long.Parse(opts.GetValueOrDefault("steps", (100L * area).ToString()));
                        var activity = int.Parse(opts.GetValueOrDefault("activity", "2"));
                        if (steps < 4L * area || activity < 1)
                            throw new ArgumentException("gen-any requires at least 4*area edit steps and activity >= 1 to retain all-board support.");
                        var edits = new ReversiblePathSampler(ReversiblePathSampler.Stripes(width, height));
                        var initialOpenCells = edits.Count;
                        var lastReport = watch.Elapsed.TotalSeconds;
                        for (long i = 0; i < steps; i++)
                        {
                            if (samplerName == "deep") edits.DeepStep(random.Next, activity);
                            else edits.Step(random.Next, activity);
                            if (i % 1000000 == 0 && watch.Elapsed.TotalSeconds - lastReport >= 10)
                            {
                                Console.WriteLine($"gen-any: {i}/{steps} edit steps, {edits.Count} open cells");
                                lastReport = watch.Elapsed.TotalSeconds;
                            }
                        }
                        board = edits.Snapshot();
                        dynamics = new { steps, activity, initialOpenCells, initialization = "stripes", random.Draws,
                            randomSource = "system-random-bytes", edits.Attempted, edits.Accepted,
                            blockProbability = samplerName == "deep" ? 0.125 : 0, edits.BlockAttempts, edits.BlockEligible,
                            edits.BlockChanges, edits.BlockCandidates,
                            stationaryTarget = "solutionCount(board) * activity^openCells(board)",
                            distribution = "Finite run; initialization bias remains; not uniform boards." };
                    }
                    else throw new ArgumentException($"Unknown sampler '{samplerName}'; expected deep, edits, or backward.");
                }
            }
            board.Validate();
            var code = UniversalConstruction.Encode(board);
            var replay = UniversalConstruction.Decode(code);
            if (replay.BoardString() != board.BoardString() || replay.SolutionString() != board.SolutionString())
                throw new InvalidDataException("Construction code failed to reproduce its board and solution.");
            Directory.CreateDirectory(Path.GetDirectoryName(stem));
            File.WriteAllText(stem + ".board", board.BoardString() + "\n");
            File.WriteAllText(stem + ".solution", board.SolutionString() + "\n");
            File.WriteAllText(stem + ".recipe.json", board.RecipeJson() + "\n");
            File.WriteAllText(stem + ".code.json", UniversalConstruction.Json(code) + "\n");
            File.WriteAllText(stem + ".generation.json", JsonSerializer.Serialize(new {
                generator = "universal-construction-v1", sampler = samplerName, codeFormat = UniversalConstruction.Format,
                board.Width, board.Height, openCells = board.Count, solutionSlides = CoilFormat.ParseSolution(board.SolutionString()).path.Length,
                constructionHexDigits = code.CodeHex.Length, generationSeconds = watch.Elapsed.TotalSeconds, dynamics,
                coverage = "Every nonempty solvable board at these dimensions has a construction code; no trimming or maximality requirement.",
                validation = "Debug.DoDebug + CoilFormat.Validate; construction code replay matched board and solution"
            }, new JsonSerializerOptions { WriteIndented = true }) + "\n");
            Console.WriteLine($"{board.Width}x{board.Height}: {board.Count} open cells, {code.CodeHex.Length} construction hex digits -> {stem}.board");
            return 0;
        }

        static int EncodeBoard(string[] args)
        {
            var (pos, opts) = ParseArgs(args);
            if (pos.Count != 2 || opts.Keys.Any(key => key != "out"))
                throw new ArgumentException("encode-board <file.board[.gz]> <file.solution[.gz]> [--out FILE]");
            var output = opts.TryGetValue("out", out var file) ? Path.GetFullPath(file) : Path.GetFullPath(pos[0] + ".code.json");
            if (File.Exists(output)) throw new IOException($"Output already exists: {output}");
            string Read(string path) => path.EndsWith(".gz", StringComparison.OrdinalIgnoreCase) ? ReadGzip(path) : File.ReadAllText(path);
            var board = FromSolution(Read(pos[0]), Read(pos[1]));
            var code = UniversalConstruction.Encode(board);
            var replay = UniversalConstruction.Decode(code);
            if (board.BoardString() != replay.BoardString() || board.SolutionString() != replay.SolutionString())
                throw new InvalidDataException("Construction code failed to reproduce the supplied solution.");
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            File.WriteAllText(output, UniversalConstruction.Json(code) + "\n");
            Console.WriteLine($"{board.Width}x{board.Height}: {board.Count} open cells, {code.CodeHex.Length} hex digits, validated replay -> {output}");
            return 0;
        }
    }
}
