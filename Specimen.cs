using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace coil
{
    partial class Program
    {
        static int Specimen(string[] args)
        {
            var (pos, opts) = ParseArgs(args);
            if (pos.Count != 3) throw new ArgumentException("specimen <directory> <side> <seed> [gen options]");
            int side = int.Parse(pos[1]), seed = int.Parse(pos[2]);
            if (side < 3 || side > 10000) throw new ArgumentOutOfRangeException("side", "Expected 3..10000");
            string dir = Path.GetFullPath(pos[0], Paths.Root);
            if (Directory.Exists(dir)) throw new IOException($"Specimen directory already exists: {dir}");
            var sw = Stopwatch.StartNew();
            var (level, _, _) = GenerateLevel(MakeConfiguration(opts), side, side, seed, quiet: true,
                trimDeadEnds: !opts.ContainsKey("keep-deadends"));
            double generationSeconds = sw.Elapsed.TotalSeconds;
            string board = CoilFormat.BoardString(level), solution = CoilFormat.SolutionString(level);
            var (_, _, walls) = CoilFormat.ParseBoard(board);
            var stats = BoardCharacterization.Describe(side, side, walls);
            Directory.CreateDirectory(dir);
            WriteGzip(Path.Combine(dir, "level.board.gz"), board);
            WriteGzip(Path.Combine(dir, "level.solution.gz"), solution);
            // Replay the persisted interchange, not just the in-memory strings.
            CoilFormat.Validate(ReadGzip(Path.Combine(dir, "level.board.gz")), ReadGzip(Path.Combine(dir, "level.solution.gz")));
            ImageUtil.SaveMap(level, Path.Combine(dir, "map.png"), 1);
            using (var image = Image.Load(Path.Combine(dir, "map.png")))
            {
                using (var detail = image.Clone(c => c.Crop(new Rectangle(Math.Max(0, side / 2 - 64), Math.Max(0, side / 2 - 64), Math.Min(128, side), Math.Min(128, side)))
                    .Resize(512, 512, KnownResamplers.NearestNeighbor))) detail.Save(Path.Combine(dir, "detail.png"));
                image.Mutate(c => c.Resize(480, 480, KnownResamplers.Box));
                image.Save(Path.Combine(dir, "preview.png"));
            }
            File.WriteAllText(Path.Combine(dir, "stats.pending.json"), JsonSerializer.Serialize(new {
                schemaVersion = 1, side, seed, options = opts, generationSeconds,
                boardSha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(board))).ToLowerInvariant(),
                validation = "Debug.DoDebug + CoilFormat.Validate; persisted gzip pair replayed", stats
            }, new JsonSerializerOptions { WriteIndented = true }));
            // Consumers treat stats.json as the completion marker, so publish it atomically.
            File.Move(Path.Combine(dir, "stats.pending.json"), Path.Combine(dir, "stats.json"));
            Console.WriteLine($"{Path.GetFileName(dir)} complete in {sw.Elapsed.TotalSeconds:0.0}s");
            return 0;
        }
        static void WriteGzip(string path, string value)
        {
            using var file = File.Create(path);
            using var gzip = new GZipStream(file, CompressionLevel.SmallestSize);
            using var writer = new StreamWriter(gzip, new UTF8Encoding(false));
            writer.Write(value);
        }
        static string ReadGzip(string path)
        {
            using var gzip = new GZipStream(File.OpenRead(path), CompressionMode.Decompress);
            using var reader = new StreamReader(gzip);
            return reader.ReadToEnd();
        }
    }
}
