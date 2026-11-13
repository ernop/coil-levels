using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SkiaSharp;

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
                using (var map = ImageUtil.Load(Path.Combine(dir, "map.png"), SKColorType.Gray8))
                {
                    if (map.Width != w || map.Height != h) throw new InvalidDataException($"Map dimensions mismatch: {dir}");
                    ReadOnlySpan<byte> pixels = map.GetPixelSpan();
                    for (int y = 0; y < h; y++)
                        for (int x = 0; x < w; x++)
                            if (pixels[y * map.RowBytes + x] != (wall[y * w + x] ? 0 : 255)) throw new InvalidDataException($"Map cell mismatch: {dir} {x},{y}");
                    VerifyDetail(map, dir);
                }
                foreach (var name in new[] { "detail.png", "preview.png" })
                {
                    var size = ImageUtil.Dimensions(Path.Combine(dir, name));
                    int side = name == "detail.png" ? 512 : 480;
                    if (size.Width != side || size.Height != side) throw new InvalidDataException($"Invalid {name}: {dir}");
                }
                done++;
                if (done % 25 == 0 || w >= 5000) Console.WriteLine($"Verified {done}/{files.Length}: {Path.GetFileName(dir)}");
            }
            Console.WriteLine($"Verified all {done} specimens: gzip replay, board hashes, exact geometry, map and crop cells, visible crop frames, preview dimensions.");
            return 0;
        }

        static void VerifyDetail(SKBitmap map, string directory)
        {
            using var detail = ImageUtil.Load(Path.Combine(directory, "detail.png"), SKColorType.Rgba8888);
            if (detail.Width != 512 || detail.Height != 512)
                throw new InvalidDataException($"Invalid detail dimensions: {directory}");
            // Caption regions must contain text over the baked-in frame, even when
            // the PNG is opened outside the HTML page. Do not depend on font rasterization.
            if (detail.GetPixel(0, 0) != ImageUtil.CropFrame ||
                !HasCaption(24, 18, 488, 44) || !HasCaption(24, 467, 488, 510))
                throw new InvalidDataException($"Missing crop frame or caption: {directory}");
            if (map.Width >= 128 && map.Height >= 128)
            {
                // Premultiplied bytes equal the stored color wherever alpha is 255, which every crop pixel must be.
                ReadOnlySpan<byte> gray = map.GetPixelSpan(), rgba = detail.GetPixelSpan();
                int sx = map.Width / 2 - 64, sy = map.Height / 2 - 64;
                for (int y = 0; y < 384; y++)
                    for (int x = 0; x < 384; x++)
                    {
                        byte expected = gray[(sy + y / 3) * map.RowBytes + sx + x / 3];
                        var actual = rgba.Slice((64 + y) * detail.RowBytes + (64 + x) * 4, 4);
                        if (actual[0] != expected || actual[1] != expected || actual[2] != expected || actual[3] != 255)
                            throw new InvalidDataException($"Crop cell mismatch: {directory} {x},{y}");
                    }
            }

            bool HasCaption(int x0, int y0, int x1, int y1)
            {
                ReadOnlySpan<byte> pixels = detail.GetPixelSpan();
                int lightPixels = 0;
                for (int y = y0; y < y1; y++) for (int x = x0; x < x1; x++)
                {
                    var p = pixels.Slice(y * detail.RowBytes + x * 4, 4);
                    if (p[0] > 150 && p[1] > 150 && p[2] > 150) lightPixels++;
                }
                return lightPixels > 100;
            }
        }
    }
}
