using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace coil
{
    partial class Program
    {
        static int VerifyCollection(string[] args)
        {
            if (args.Length != 1) throw new ArgumentException("verify-collection <gallery-directory>");
            var files = Directory.GetFiles(Path.GetFullPath(args[0], Paths.Root), "stats.json", SearchOption.AllDirectories).OrderBy(f => f).ToArray();
            if (files.Length == 0) throw new InvalidOperationException("No specimens found");
            int done = 0;
            foreach (var file in files)
            {
                string dir = Path.GetDirectoryName(file);
                using var metadata = JsonDocument.Parse(File.ReadAllText(file));
                string board = ReadGzip(Path.Combine(dir, "level.board.gz"));
                string solution = ReadGzip(Path.Combine(dir, "level.solution.gz"));
                string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(board))).ToLowerInvariant();
                if (hash != metadata.RootElement.GetProperty("boardSha256").GetString()) throw new InvalidDataException($"Board hash mismatch: {dir}");
                CoilFormat.Validate(board, solution);
                var (w, h, wall) = CoilFormat.ParseBoard(board);
                string expected = JsonSerializer.Serialize(metadata.RootElement.GetProperty("stats"));
                string actual = JsonSerializer.Serialize(BoardCharacterization.Describe(w, h, wall));
                if (expected != actual) throw new InvalidDataException($"Characterization mismatch: {dir}");
                using (var map = Image.Load<L8>(Path.Combine(dir, "map.png")))
                {
                    if (map.Width != w || map.Height != h) throw new InvalidDataException($"Map dimensions mismatch: {dir}");
                    map.ProcessPixelRows(accessor => {
                        for (int y = 0; y < h; y++)
                        {
                            var row = accessor.GetRowSpan(y);
                            for (int x = 0; x < w; x++)
                                if (row[x].PackedValue != (wall[y * w + x] ? 0 : 255)) throw new InvalidDataException($"Map cell mismatch: {dir} {x},{y}");
                        }
                    });
                    VerifyDetail(map, dir);
                }
                foreach (var name in new[] { "detail.png", "preview.png" })
                {
                    var info = Image.Identify(Path.Combine(dir, name));
                    int side = name == "detail.png" ? 512 : 480;
                    if (info.Width != side || info.Height != side) throw new InvalidDataException($"Invalid {name}: {dir}");
                }
                done++;
                if (done % 25 == 0 || w >= 5000) Console.WriteLine($"Verified {done}/{files.Length}: {Path.GetFileName(dir)}");
            }
            Console.WriteLine($"Verified all {done} specimens: gzip replay, board hashes, exact geometry, map and crop cells, visible crop frames, preview dimensions.");
            return 0;
        }

        static void VerifyDetail(Image<L8> map, string directory)
        {
            using var detail = Image.Load<Rgba32>(Path.Combine(directory, "detail.png"));
            if (detail.Width != 512 || detail.Height != 512)
                throw new InvalidDataException($"Invalid detail dimensions: {directory}");
            // Caption regions must contain text over the baked-in frame, even when
            // the PNG is opened outside the HTML page. Do not depend on font rasterization.
            var background = new Rgba32(0x14, 0x29, 0x1f);
            if (!detail[0, 0].Equals(background) ||
                !HasCaption(24, 18, 488, 44) || !HasCaption(24, 467, 488, 510))
                throw new InvalidDataException($"Missing crop frame or caption: {directory}");
            if (map.Width >= 128 && map.Height >= 128)
            {
                int sx = map.Width / 2 - 64, sy = map.Height / 2 - 64;
                for (int y = 0; y < 384; y++)
                    for (int x = 0; x < 384; x++)
                    {
                        byte expected = map[sx + x / 3, sy + y / 3].PackedValue;
                        var actual = detail[64 + x, 64 + y];
                        if (actual.R != expected || actual.G != expected || actual.B != expected || actual.A != 255)
                            throw new InvalidDataException($"Crop cell mismatch: {directory} {x},{y}");
                    }
            }

            bool HasCaption(int x0, int y0, int x1, int y1)
            {
                int lightPixels = 0;
                for (int y = y0; y < y1; y++) for (int x = x0; x < x1; x++)
                {
                    var p = detail[x, y];
                    if (p.R > 150 && p.G > 150 && p.B > 150) lightPixels++;
                }
                return lightPixels > 100;
            }
        }
    }
}
