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
            Console.WriteLine($"Verified all {done} specimens: gzip replay, board hashes, exact geometry, map cells, preview dimensions.");
            return 0;
        }
    }
}
