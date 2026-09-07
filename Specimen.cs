using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.Fonts;

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
                SaveDetail(image, Path.Combine(dir, "detail.png"));
                image.Mutate(c => c.Resize(480, 480, KnownResamplers.Box));
                image.Save(Path.Combine(dir, "preview.png"));
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
        static void SaveDetail(Image map, string path)
        {
            int width = Math.Min(128, map.Width), height = Math.Min(128, map.Height);
            int x = Math.Max(0, map.Width / 2 - 64), y = Math.Max(0, map.Height / 2 - 64);
            bool cropped = width < map.Width || height < map.Height;
            using var crop = map.Clone(c => c.Crop(new Rectangle(x, y, width, height)).Resize(384, 384, KnownResamplers.NearestNeighbor));
            using var frame = new Image<Rgba32>(512, 512, Color.ParseHex("14291f"));
            var font = new Font(SystemFonts.Get(ImageUtil.FontFamilyName), 17, FontStyle.Bold);
            var small = new Font(SystemFonts.Get(ImageUtil.FontFamilyName), 13);
            frame.Mutate(c => c
                .Fill(Color.ParseHex("a5eac6"), new RectangleF(60, 60, 392, 392))
                .DrawImage(crop, new Point(64, 64), 1f)
                .DrawText($"{(cropped ? "CROP" : "WHOLE BOARD")} · {width} x {height} of {map.Width} x {map.Height}", font, Color.White, new PointF(24, 18))
                .DrawText($"x={x}..{x + width - 1}, y={y}..{y + height - 1} (zero-based)", small, Color.White, new PointF(24, 467))
                .DrawText(cropped ? "The board continues beyond the marked crop frame." : "Every board cell is shown within the frame.", small, Color.White, new PointF(24, 488)));
            frame.Save(path);
        }

        static int RefreshDetails(string[] args)
        {
            if (args.Length != 1) throw new ArgumentException("refresh-details <gallery-directory>");
            int count = 0;
            foreach (var file in Directory.GetFiles(Path.GetFullPath(args[0], Paths.Root), "stats.json", SearchOption.AllDirectories))
            {
                string dir = Path.GetDirectoryName(file);
                using var map = Image.Load(Path.Combine(dir, "map.png"));
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
