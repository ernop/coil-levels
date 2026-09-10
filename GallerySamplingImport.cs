using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace coil
{
    partial class Program
    {
        // Reuse the saved draws, including duplicates and incomplete batches. No sampling occurs here.
        static int PrepareSamplingGallery(string[] args)
        {
            if (args.Length != 0) throw new ArgumentException("prepare-sampling-gallery takes no arguments.");
            var count = 0;
            foreach (var source in new[] { "sampling-v1/uniform-4x4", "sampling-v1/chain-4x4",
                "deep-v1/uniform-5x5", "deep-v1/uniform-6x6", "deep-v1/uniform-7x7" })
            {
                var sourceDir = Path.Combine(Paths.Root, "gallery", source);
                using var batch = JsonDocument.Parse(File.ReadAllText(Path.Combine(sourceDir, "sampling.json")));
                var record = batch.RootElement;
                var batchSettings = record.EnumerateObject().Where(p => p.Name != "samples")
                    .ToDictionary(p => p.Name, p => p.Value.Clone());
                foreach (var sample in record.GetProperty("samples").EnumerateArray())
                {
                    var name = $"sample-{sample.GetProperty("index").GetInt32():D4}";
                    var board = File.ReadAllText(Path.Combine(sourceDir, name + ".board")).Trim();
                    var solution = File.ReadAllText(Path.Combine(sourceDir, name + ".solution")).Trim();
                    var level = FromSolution(board, solution);
                    level.Validate(); // Both mandatory physics validators, including singleton solutions.
                    var (width, height, walls) = CoilFormat.ParseBoard(board);
                    var dir = Path.Combine(Paths.Root, "gallery", "sampling-viewer", Path.GetFileName(source), name);
                    Directory.CreateDirectory(dir);
                    using var map = new Image<L8>(width, height);
                    for (var y = 0; y < height; y++)
                        for (var x = 0; x < width; x++) map[x, y] = new L8(walls[y * width + x] ? (byte)0 : (byte)255);
                    map.Save(Path.Combine(dir, "map.png"));
                    using (var preview = map.Clone(c => c.Resize(480, 480, KnownResamplers.NearestNeighbor)))
                        preview.Save(Path.Combine(dir, "preview.png"));
                    SaveDetail(map, Path.Combine(dir, "detail.png"));
                    var metadata = new {
                        schemaVersion = 1, side = width,
                        generator = record.GetProperty("generator").GetString(),
                        sourceBoard = $"gallery/{source}/{name}.board",
                        sourceSolution = $"gallery/{source}/{name}.solution",
                        samplingBatch = batchSettings, sampleRecord = sample.Clone(),
                        solutionSlides = CoilFormat.ParseSolution(solution).path.Length,
                        boardSha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(board))).ToLowerInvariant(),
                        validation = "Debug.DoDebug + CoilFormat.Validate; exact saved draw, no regeneration",
                        stats = BoardCharacterization.Describe(width, height, walls)
                    };
                    File.WriteAllText(Path.Combine(dir, "stats.json"), JsonSerializer.Serialize(metadata,
                        new JsonSerializerOptions { WriteIndented = true }));
                    count++;
                }
            }
            Console.WriteLine($"Prepared and physics-validated {count} saved draws for the standard gallery.");
            return 0;
        }
    }
}
