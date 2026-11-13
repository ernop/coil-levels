using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SkiaSharp;

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
            using (var image = ImageUtil.Load(Path.Combine(dir, "map.png"), SKColorType.Gray8))
            {
                SaveDetail(image, Path.Combine(dir, "detail.png"));
                using var preview = ImageUtil.ResizeBox(image, 480, 480);
                ImageUtil.SavePng(preview, Path.Combine(dir, "preview.png"));
            }
            File.WriteAllText(Path.Combine(dir, "stats.pending.json"), JsonSerializer.Serialize(new {
                schemaVersion = 1, side, seed, options = opts, generationSeconds, generationPolicy = GenerationQuality.Policy,
                boardSha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(board))).ToLowerInvariant(),
                validation = "Debug.DoDebug + CoilFormat.Validate; persisted gzip pair replayed", stats
            }, new JsonSerializerOptions { WriteIndented = true }));
            // Consumers treat stats.json as the completion marker, so publish it atomically.
            File.Move(Path.Combine(dir, "stats.pending.json"), Path.Combine(dir, "stats.json"));
            Console.WriteLine($"{Path.GetFileName(dir)} complete in {sw.Elapsed.TotalSeconds:0.0}s");
            return 0;
        }
        static void SaveDetail(SKBitmap map, string path)
        {
            int width = Math.Min(128, map.Width), height = Math.Min(128, map.Height);
            int x = Math.Max(0, map.Width / 2 - 64), y = Math.Max(0, map.Height / 2 - 64);
            bool cropped = width < map.Width || height < map.Height;
            using var crop = ImageUtil.ResizeNearest(map, SKRectI.Create(x, y, width, height), 384, 384);
            using var frame = ImageUtil.NewRgba(512, 512, ImageUtil.CropFrame);
            using (var canvas = new SKCanvas(frame))
            using (var font = ImageUtil.MakeFont(17, bold: true))
            using (var small = ImageUtil.MakeFont(13))
            using (var light = new SKPaint { Color = new SKColor(0xa5, 0xea, 0xc6) })
            {
                canvas.DrawRect(SKRect.Create(60, 60, 392, 392), light);
                canvas.DrawBitmap(crop, 64, 64, ImageUtil.Unscaled);
                ImageUtil.DrawText(canvas, $"{(cropped ? "CROP" : "WHOLE BOARD")} · {width} x {height} of {map.Width} x {map.Height}", font, SKColors.White, 24, 18);
                ImageUtil.DrawText(canvas, $"x={x}..{x + width - 1}, y={y}..{y + height - 1} (zero-based)", small, SKColors.White, 24, 467);
                ImageUtil.DrawText(canvas, cropped ? "The board continues beyond the marked crop frame." : "Every board cell is shown within the frame.", small, SKColors.White, 24, 488);
            }
            ImageUtil.SavePng(frame, path);
        }

        static int RefreshDetails(string[] args)
        {
            if (args.Length != 1) throw new ArgumentException("refresh-details <gallery-directory>");
            int count = 0;
            foreach (var file in Directory.GetFiles(Path.GetFullPath(args[0], Paths.Root), "stats.json", SearchOption.AllDirectories))
            {
                string dir = Path.GetDirectoryName(file);
                using var map = ImageUtil.Load(Path.Combine(dir, "map.png"), SKColorType.Gray8);
                SaveDetail(map, Path.Combine(dir, "detail.png"));
                count++;
            }
            Console.WriteLine($"Refreshed {count} visibly labeled crop images");
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
