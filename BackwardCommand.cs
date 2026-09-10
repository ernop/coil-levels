using System;
using System.IO;
using System.Text.Json;

namespace coil
{
    partial class Program
    {
        static int GenBackward(string[] args)
        {
            var (pos, opts) = ParseArgs(args);
            foreach (var key in opts.Keys)
                if (key != "seed" && key != "seed-hex" && key != "recipe" && key != "out")
                    throw new ArgumentException($"Unknown gen-backward option --{key}.");
            BackwardGenerator board;
            string seedHex = null;
            if (opts.TryGetValue("recipe", out var recipeFile))
            {
                if (pos.Count != 0 || opts.ContainsKey("seed") || opts.ContainsKey("seed-hex"))
                    throw new ArgumentException("--recipe supplies dimensions and all choices; do not combine with dimensions or --seed.");
                var recipe = JsonSerializer.Deserialize<BackwardRecipe>(File.ReadAllText(recipeFile),
                    new JsonSerializerOptions { UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow });
                board = BackwardGenerator.Replay(recipe);
                seedHex = recipe.SeedHex;
            }
            else
            {
                if (pos.Count != 2) throw new ArgumentException("gen-backward requires width height, or --recipe FILE.");
                Func<int, int> choose;
                if (opts.TryGetValue("seed", out var seed))
                {
                    if (opts.ContainsKey("seed-hex")) throw new ArgumentException("Choose --seed or --seed-hex, not both.");
                    choose = new Random(int.Parse(seed)).Next;
                }
                else
                {
                    var stream = opts.TryGetValue("seed-hex", out var hex) ? new SeededChoices(hex) : SeededChoices.Create();
                    seedHex = stream.SeedHex;
                    choose = stream.Next;
                }
                board = BackwardGenerator.Generate(int.Parse(pos[0]), int.Parse(pos[1]), choose);
            }
            // Validate before creating any output, including recipes.
            board.Validate();
            var stem = opts.TryGetValue("out", out var output) ? Path.GetFullPath(output)
                : Paths.In($"output/{board.Width}x{board.Height}/backward-{Guid.NewGuid():N}");
            foreach (var extension in new[] { ".board", ".solution", ".recipe.json" })
                if (File.Exists(stem + extension)) throw new IOException($"Output already exists: {stem + extension}");
            Directory.CreateDirectory(Path.GetDirectoryName(stem));
            File.WriteAllText(stem + ".board", board.BoardString() + "\n");
            File.WriteAllText(stem + ".solution", board.SolutionString() + "\n");
            File.WriteAllText(stem + ".recipe.json", board.RecipeJson(seedHex) + "\n");
            Console.WriteLine($"{board.Width}x{board.Height}: {board.Count} open cells, validated -> {stem}.board");
            return 0;
        }
    }
}
