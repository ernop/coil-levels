using System;
using System.Collections.Generic;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace coil
{

    public static class ImageUtil
    {
        public static int Scale = 15;
        public static void Save(Dictionary<string, Image> images, Level level, List<List<string>> outstrings, string fn)
        {
            //juggle the path to determine what should be written in each square.
            //allows partial segments
            var imageWidth = level.Width * Scale;
            var imageHeight = level.Height * Scale;

            Console.Write($"Size: {imageWidth}x{imageHeight}");
            using (var result = new Image<Rgba32>(imageWidth, imageHeight))
            {
                for (var yy = 0; yy < level.Height; yy++)
                {
                    for (var xx = 0; xx < level.Width; xx++)
                    {
                        var target = new SixLabors.Primitives.Point(xx * Scale, yy * Scale);
                        var key = outstrings[yy][xx];

                        result.Mutate(oo => oo.DrawImage(images[key], target, 1f));
                    }
                }

                result.Save($"{fn}");
            }

            Console.WriteLine($"Saved to: {fn}");
        }

        public static Dictionary<string, Image> GetImages()
        {
            var stem = "../../..";
            var keyfp = $"{stem}/tiles/rr.png";

            var d = new Dictionary<string, Image>();

            d["rr"] = Image.Load<Rgba32>(keyfp);
            d["dd"] = d["rr"].Clone(oo => oo.Rotate(90));
            d["ll"] = d["rr"].Clone(oo => oo.Rotate(180));
            d["uu"] = d["rr"].Clone(oo => oo.Rotate(270));

            keyfp = $"{stem}/tiles/ru.png";
            d["ru"] = Image.Load<Rgba32>(keyfp);
            d["ul"] = d["ru"].Clone(oo => oo.Rotate(-90));
            d["ld"] = d["ru"].Clone(oo => oo.Rotate(-180));
            d["dr"] = d["ru"].Clone(oo => oo.Rotate(-270));

            d["rd"] = d["ru"].Clone(oo => oo.Flip(FlipMode.Vertical));
            d["ur"] = d["rd"].Clone(oo => oo.Rotate(-90));
            d["lu"] = d["rd"].Clone(oo => oo.Rotate(-180));
            d["dl"] = d["rd"].Clone(oo => oo.Rotate(-270));

            keyfp = $"{stem}/tiles/su.png";
            d["su"] = Image.Load<Rgba32>(keyfp);
            d["sr"] = d["su"].Clone(oo => oo.Rotate(90));
            d["sd"] = d["su"].Clone(oo => oo.Rotate(180));
            d["sl"] = d["su"].Clone(oo => oo.Rotate(270));

            keyfp = $"{stem}/tiles/ue.png";
            d["ue"] = Image.Load<Rgba32>(keyfp);
            d["re"] = d["ue"].Clone(oo => oo.Rotate(90));
            d["de"] = d["ue"].Clone(oo => oo.Rotate(180));
            d["le"] = d["ue"].Clone(oo => oo.Rotate(270));

            var extraKeys = new List<string>() { "b", "x", "h", "empty", "s", "e" };
            foreach (var key in extraKeys)
            {
                keyfp = $"../../../tiles/{key}.png";
                d[key] = Image.Load<Rgba32>(keyfp);

                if (key == "empty")
                {
                    d["."] = Image.Load<Rgba32>(keyfp);
                }
            }

            return d;
        }
    }
}