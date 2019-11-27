using System;
using System.Collections.Generic;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.Fonts;
using System.Drawing;
using System.Drawing.Drawing2D;

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.Linq;
using SixLabors.ImageSharp.Processing.Processors.Text;
using static coil.Util;
using System.Numerics;

namespace coil
{

    public static class ImageUtil
    {
        public static int Scale = 15;

        public static void Save(Dictionary<string, Image> images, BaseLevel level, List<List<string>> outstrings, string fn, string subtitle, bool quiet = false)
        {
            //juggle the path to determine what should be written in each square.
            //allows partial segments
            var imageHeight = level.Height * Scale;
            var imageWidth = level.Width * Scale;
            var writeSubtitle = false;
            if (!String.IsNullOrEmpty(subtitle))
            {
                imageHeight += 1 * Scale+8;
                writeSubtitle = true;
            }
            
            
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
                if (writeSubtitle)
                {
                    //var fo = SystemFonts.Find("Arial");
                    var cs = SystemFonts.Find("Comic Sans MS");
                    var font = new Font(cs, 17, FontStyle.Bold);
                    var location = new SixLabors.Primitives.PointF(0, 0);
                    var color = SixLabors.ImageSharp.Color.Black;
                    //result.Mutate(oo => oo.DrawText(subtitle, font, color, location));
                    var center = new Vector2(0, imageHeight-18);
                    try
                    {
                        result.Mutate(oo => oo.DrawText(subtitle, font, color, center));
                    }catch (Exception ex)
                    {
                        //silly imagesharp, writing even when you claim you can't.
                    }

                }
                result.Save($"{fn}");
            }

            if (!quiet)
            {
                Console.WriteLine($"Saved to: {fn}");
            }
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

            //"Decision hard" tiles
            keyfp = $"{stem}/tiles/ru-easy.png";
            d["ru-easy"] = Image.Load<Rgba32>(keyfp);
            d["ul-easy"] = d["ru-easy"].Clone(oo => oo.Rotate(-90));
            d["ld-easy"] = d["ru-easy"].Clone(oo => oo.Rotate(-180));
            d["dr-easy"] = d["ru-easy"].Clone(oo => oo.Rotate(-270));

            d["rd-easy"] = d["ru-easy"].Clone(oo => oo.Flip(FlipMode.Vertical));
            d["ur-easy"] = d["rd-easy"].Clone(oo => oo.Rotate(-90));
            d["lu-easy"] = d["rd-easy"].Clone(oo => oo.Rotate(-180));
            d["dl-easy"] = d["rd-easy"].Clone(oo => oo.Rotate(-270));

            //"Decision easy" tiles
            keyfp = $"{stem}/tiles/ru-hard.png";
            d["ru-hard"] = Image.Load<Rgba32>(keyfp);
            d["ul-hard"] = d["ru-hard"].Clone(oo => oo.Rotate(-90));
            d["ld-hard"] = d["ru-hard"].Clone(oo => oo.Rotate(-180));
            d["dr-hard"] = d["ru-hard"].Clone(oo => oo.Rotate(-270));

            d["rd-hard"] = d["ru-hard"].Clone(oo => oo.Flip(FlipMode.Vertical));
            d["ur-hard"] = d["rd-hard"].Clone(oo => oo.Rotate(-90));
            d["lu-hard"] = d["rd-hard"].Clone(oo => oo.Rotate(-180));
            d["dl-hard"] = d["rd-hard"].Clone(oo => oo.Rotate(-270));

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