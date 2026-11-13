using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;

namespace coil
{
    partial class Program
    {
        static int PrepareArchiveGallery(string[] args)
        {
            if (args.Length != 0) throw new ArgumentException("prepare-archive-gallery takes no arguments.");
            using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(Paths.Root, "gallery/imported-boards.json")));
            int count = 0, validated = 0;
            foreach (var entry in manifest.RootElement.GetProperty("boards").EnumerateArray())
            {
                var dir = Path.Combine(Paths.Root, entry.GetProperty("path").GetString());
                using var imported = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, "import.json")));
                var metadata = imported.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => (object)p.Value.Clone());
                var board = ReadGzip(Path.Combine(dir, "level.board.gz"));
                var (width, height, walls) = CoilFormat.ParseBoard(board);
                var solution = Path.Combine(dir, "level.solution.gz");
                if (File.Exists(solution))
                {
                    FromSolution(board, ReadGzip(solution)).Validate();
                    metadata["validation"] = "Debug.DoDebug + CoilFormat.Validate; exact archived solution replayed";
                    validated++;
                }
                else metadata["validation"] = "Archived layout checked for format and dimensions. No saved solution; solvability has not been independently verified by this import.";
                metadata["stats"] = BoardCharacterization.Describe(width, height, walls);
                using var map = ImageUtil.WallMap(width, height, walls);
                ImageUtil.SavePng(map, Path.Combine(dir, "map.png"));
                // Preserve rectangular boards and keep cells square in previews.
                double scale = Math.Min(1, 240.0 / Math.Max(width, height));
                using var preview = ImageUtil.ResizeBox(map, Math.Max(1, (int)Math.Round(width * scale)), Math.Max(1, (int)Math.Round(height * scale)));
                ImageUtil.SavePng(preview, Path.Combine(dir, "preview.png"));
                File.WriteAllText(Path.Combine(dir, "stats.json"), JsonSerializer.Serialize(metadata));
                count++;
                if (count % 250 == 0) Console.WriteLine($"Prepared {count} archived boards");
            }
            Console.WriteLine($"Prepared {count} archived layouts; {validated} supplied solutions passed both physics validators.");
            return 0;
        }
    }
}
