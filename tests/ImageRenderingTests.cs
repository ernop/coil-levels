using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using coil;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

static class ImageRenderingTests
{
    public static void Run()
    {
        var config = new LevelConfiguration(TweakPickers.GetPickers("rnd99").Single(),
            SegPickers.GetSegPickers("Weighted4").Single(), new OptimizationSetup(), new InitialWanderSetup());
        var level = new Level(config, 5, 9, new Random(0), 0);
        var tiles = new Dictionary<string, Image>();
        var rows = new List<List<string>>();
        string directory = Path.Combine(Path.GetTempPath(), "coil-render-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            for (int y = 0; y < level.Height; y++)
            {
                var row = new List<string>(); rows.Add(row);
                for (int x = 0; x < level.Width; x++)
                {
                    string key = $"{x},{y}"; row.Add(key);
                    tiles[key] = new Image<Rgba32>(15, 15, ColorAt(x, y));
                }
            }
            foreach (int scale in new[] { 1, 3, 15 })
            {
                string file = Path.Combine(directory, $"rows-{scale}.png");
                ImageUtil.Save(tiles, level, rows, file, "", quiet: true, overrideScale: scale);
                using var image = Image.Load<Rgba32>(file);
                Require(image.Width == level.Width * scale && image.Height == level.Height * scale, "custom-scale dimensions");
                for (int y = 0; y < image.Height; y++) for (int x = 0; x < image.Width; x++)
                    Require(image[x, y].Equals(ColorAt(x / scale, y / scale)), "rendered row/column must match source coordinates");
            }
            var large = new Level(config, 100, 80, new Random(0), 0);
            var cornerRows = Enumerable.Range(0, large.Height).Select(y => Enumerable.Repeat("0,0", large.Width).ToList()).ToList();
            string corner = Path.Combine(directory, "corner.png");
            ImageUtil.Save(tiles, large, cornerRows, corner, "", quiet: true, corner: true);
            using var crop = Image.Load<Rgba32>(corner);
            Require(crop.Width == 80 * 15 && crop.Height == 55 * 15 + 44, "cropped tile map reserves space for an on-image label");
            Require(crop[0, crop.Height - 1].Equals(new Rgba32(0x14, 0x29, 0x1f)), "crop footer is baked into the PNG");
            int lightPixels = 0;
            for (int y = 55 * 15; y < crop.Height; y++) for (int x = 0; x < crop.Width; x++)
                if (crop[x, y].R > 150 && crop[x, y].G > 150 && crop[x, y].B > 150) lightPixels++;
            Require(lightPixels > 100, "crop footer contains a visible caption");
        }
        finally
        {
            foreach (var tile in tiles.Values) tile.Dispose();
            Directory.Delete(directory, true);
        }
        Console.WriteLine("PNG renderer passed exact row/column pixels at scales 1, 3, 15 and a visibly labeled corner crop.");
    }
    static Rgba32 ColorAt(int x, int y) => new Rgba32((byte)(x * 31), (byte)(y * 21), 93);
    static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
