using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace coil
{
    partial class Program
    {
        static int BackwardSpecimen(string[] args)
        {
            var (pos, opts) = ParseArgs(args);
            if (pos.Count != 2) throw new ArgumentException("backward-specimen <new-directory> <side> [--seed-hex HEX]");
            foreach (var key in opts.Keys)
                if (key != "seed-hex") throw new ArgumentException($"Unknown backward-specimen option --{key}.");
            var dir = Path.GetFullPath(pos[0], Paths.Root);
            if (Directory.Exists(dir)) throw new IOException($"Specimen directory already exists: {dir}");
            var side = int.Parse(pos[1]);
            var stream = opts.TryGetValue("seed-hex", out var hex) ? new SeededChoices(hex) : SeededChoices.Create();
            var watch = Stopwatch.StartNew();
            var choiceCount = 0;
            var target = 0;
            int Choose(int bound)
            {
                var result = stream.Next(bound);
                if (choiceCount++ == 1) target = result + 1;
                return result;
            }
            var level = BackwardGenerator.Generate(side, side, Choose);
            level.Validate();
            var generationSeconds = watch.Elapsed.TotalSeconds;
            WriteBackwardSpecimen(dir, level, stream.SeedHex, generationSeconds, choiceCount, target);
            return 0;
        }

        static void WriteBackwardSpecimen(string dir, BackwardGenerator level, string seedHex, double generationSeconds,
            long choiceCount, int target, string generator = "backward-growth-v1", string sampler = "uniform-legal-cell-v1",
            string stopReason = null, object dynamics = null)
        {
            if (Directory.Exists(dir)) throw new IOException($"Specimen directory already exists: {dir}");
            level.Validate();
            if (level.Width != level.Height) throw new ArgumentException("Specimen export requires a square board.");
            var side = level.Width;
            var board = level.BoardString();
            var solution = level.SolutionString();
            var (_, _, walls) = CoilFormat.ParseBoard(board);
            var geometry = BoardCharacterization.Describe(side, side, walls);
            var path = level.ReversePath;
            var minX = path.Min(p => p % side);
            var minY = path.Min(p => p / side);
            var maxX = path.Max(p => p % side);
            var maxY = path.Max(p => p / side);
            var bounds = new Rectangle(minX, minY, maxX - minX + 1, maxY - minY + 1);
            var legalExtensions = Enumerable.Range(0, 4).Where(d => level.CanPrepend((Dir)d)).Select(d => CoilFormat.DirChar((Dir)d).ToString()).ToArray();
            Directory.CreateDirectory(dir);
            WriteGzip(Path.Combine(dir, "level.board.gz"), board);
            WriteGzip(Path.Combine(dir, "level.solution.gz"), solution);
            File.WriteAllText(Path.Combine(dir, "recipe.json"), level.RecipeJson(seedHex));
            var code = UniversalConstruction.Encode(level);
            File.WriteAllText(Path.Combine(dir, "construction.code.json"), UniversalConstruction.Json(code));
            var decoded = UniversalConstruction.Decode(UniversalConstruction.Read(File.ReadAllText(Path.Combine(dir, "construction.code.json"))));
            if (decoded.BoardString() != board || decoded.SolutionString() != solution)
                throw new InvalidDataException("Persisted construction code differs from exported board/solution.");
            var persisted = BackwardGenerator.Replay(JsonSerializer.Deserialize<BackwardRecipe>(File.ReadAllText(Path.Combine(dir, "recipe.json"))));
            persisted.Validate();
            if (persisted.BoardString() != ReadGzip(Path.Combine(dir, "level.board.gz")) ||
                persisted.SolutionString() != ReadGzip(Path.Combine(dir, "level.solution.gz")))
                throw new InvalidDataException("Persisted recipe differs from exported board/solution.");
            CoilFormat.Validate(ReadGzip(Path.Combine(dir, "level.board.gz")), ReadGzip(Path.Combine(dir, "level.solution.gz")));
            using var map = new Image<L8>(side, side);
            map.ProcessPixelRows(accessor => {
                for (var y = 0; y < side; y++)
                {
                    var row = accessor.GetRowSpan(y);
                    for (var x = 0; x < side; x++) row[x] = new L8(walls[y * side + x] ? (byte)0 : (byte)255);
                }
            });
            map.Save(Path.Combine(dir, "map.png"));
            using (var preview = map.Clone(c => c.Resize(480, 480, side <= 480 ? KnownResamplers.NearestNeighbor : KnownResamplers.Box)))
                preview.Save(Path.Combine(dir, "preview.png"));
            if (generator != "backward-growth-v1") SaveDetail(map, Path.Combine(dir, "detail.png"));
            else SaveBackwardDetail(map, bounds, Path.Combine(dir, "detail.png"), "OPEN CELLS: WHITE / WALLS: BLACK");
            using var colored = new Image<Rgb24>(side, side);
            for (var i = 0; i < path.Count; i++)
            {
                var cell = path[path.Count - 1 - i];
                var t = path.Count == 1 ? 0 : (double)i / (path.Count - 1);
                colored[cell % side, cell / side] = new Rgb24((byte)(55 + 200 * t), (byte)(180 - 70 * t), (byte)(240 - 160 * t));
            }
            colored.Save(Path.Combine(dir, "solution-map.png"));
            SaveBackwardDetail(colored, bounds, Path.Combine(dir, "solution-detail.png"), "FORWARD PLAY: BLUE START -> ORANGE FINISH");
            if (side > 128)
            {
                SaveDetail(map, Path.Combine(dir, "center-detail.png"));
                SaveDetail(colored, Path.Combine(dir, "center-solution-detail.png"));
            }
            var metadata = new {
                schemaVersion = 1, generator, sampler, dynamics,
                constructionFormat = UniversalConstruction.Format, constructionCode = "construction.code.json",
                constructionHexDigits = code.CodeHex.Length,
                side, seedHex, seedBits = seedHex.Length * 4,
                randomAlgorithm = SeededChoices.Algorithm, generationPolicy = "complete-unfiltered-v1",
                generationSeconds, choiceCount, targetOpenCells = target,
                stopReason = stopReason ?? (level.Count == target ? "target-reached" : "trapped"), legalExtensions,
                occupiedBounds = new { x = minX, y = minY, width = bounds.Width, height = bounds.Height },
                occupiedBoundsFill = (double)level.Count / (bounds.Width * bounds.Height),
                solutionSlides = CoilFormat.ParseSolution(solution).path.Length,
                start = new { x = path[path.Count - 1] % side, y = path[path.Count - 1] / side },
                finish = new { x = path[0] % side, y = path[0] / side },
                boardSha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(board))).ToLowerInvariant(),
                validation = "Debug.DoDebug + CoilFormat.Validate; persisted recipe and gzip pair replayed",
                stats = geometry
            };
            File.WriteAllText(Path.Combine(dir, "stats.pending.json"), JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(Path.Combine(dir, "stats.pending.json"), Path.Combine(dir, "stats.json"));
            Console.WriteLine($"{Path.GetFileName(dir)}: {side}x{side}, open={level.Count}/{target}, bounds={bounds.Width}x{bounds.Height}, {metadata.stopReason}");
        }

        static void SaveBackwardDetail(Image map, Rectangle occupied, string path, string legend)
        {
            var x = Math.Max(0, occupied.X - 1);
            var y = Math.Max(0, occupied.Y - 1);
            var cropWidth = Math.Min(map.Width, occupied.Right + 1) - x;
            var cropHeight = Math.Min(map.Height, occupied.Bottom + 1) - y;
            var scale = Math.Min(384.0 / cropWidth, 384.0 / cropHeight);
            using var crop = map.Clone(c => c.Crop(new Rectangle(x, y, cropWidth, cropHeight))
                .Resize(Math.Max(1, (int)(cropWidth * scale)), Math.Max(1, (int)(cropHeight * scale)), KnownResamplers.NearestNeighbor));
            using var frame = new Image<Rgba32>(512, 512, Color.ParseHex("14291f"));
            var font = new Font(SystemFonts.Get(ImageUtil.FontFamilyName), 14);
            frame.Mutate(c => c
                .DrawImage(crop, new Point((512 - crop.Width) / 2, 64 + (384 - crop.Height) / 2), 1f)
                .DrawText($"OCCUPIED-REGION CROP: {cropWidth} x {cropHeight}", font, Color.White, new PointF(18, 16))
                .DrawText($"x={x}, y={y}; full board {map.Width} x {map.Height}", font, Color.White, new PointF(18, 38))
                .DrawText(legend, new Font(SystemFonts.Get(ImageUtil.FontFamilyName), 12), Color.White, new PointF(18, 462))
                .DrawText("Crop scale differs from the full-board preview.", font, Color.White, new PointF(18, 486)));
            frame.Save(path);
        }
    }
}
