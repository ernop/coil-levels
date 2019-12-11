using System;
using System.Collections.Generic;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.Fonts;

using static coil.Util;
using System.Numerics;
using SixLabors.Primitives;
using SixLabors.Shapes;

namespace coil
{

    public static class ImageUtil
    {
        public const int Scale = 15;

        public static Font Font = new Font(SystemFonts.Find("Comic Sans MS"), 17, FontStyle.Bold);
        public static Font MedFont= new Font(SystemFonts.Find("Comic Sans MS"), 27, FontStyle.Regular);
        public static Font BigFont = new Font(SystemFonts.Find("Comic Sans MS"), 36, FontStyle.Bold);

        public static void Save(Dictionary<string, Image> images, BaseLevel level, List<List<string>> outstrings, string fn, string subtitle, bool quiet = false,
            List<PointText> pointTexts = null, bool arrows = false, int? overrideScale = null, List<(int,int)> highlights = null)
        {
            //juggle the path to determine what should be written in each square.

            var effectiveScale = overrideScale.HasValue ? overrideScale.Value : Scale;

            //allows partial segments
            var imageHeight = level.Height * effectiveScale;
            var imageWidth = level.Width * effectiveScale;
            
            var writeSubtitle = false;
            int? extra = null;
            var subtitleLineCount = 1;
            var subtitleLines = subtitle?.Split("\n");
            var subtitleLineHeight = 0;
            if (!String.IsNullOrEmpty(subtitle))
            {
                subtitleLineCount = subtitleLines.Length;
                
                extra = Math.Max(effectiveScale, (int)(1.1* subtitleLineCount * level.Height))+8;
                imageHeight += extra.Value;
                writeSubtitle = true;
                subtitleLineHeight = extra.Value / subtitleLineCount;
            }
            
            if (highlights != null)
            {
                foreach (var hl in highlights)
                {
                    outstrings[hl.Item2][hl.Item1] = "hh";
                }
            }
            
            using (var result = new Image<Rgba32>(imageWidth, imageHeight))
            {
                if (outstrings != null)
                {
                    for (var yy = 0; yy < level.Height; yy++)
                    {
                        for (var xx = 0; xx < level.Width; xx++)
                        {
                            var target = new SixLabors.Primitives.Point(xx * effectiveScale, yy * effectiveScale);
                            var key = outstrings[yy][xx];

                            result.Mutate(oo => oo.DrawImage(images[key], target, 1f));

                        }
                    }
                }
                if (writeSubtitle)
                {
                    //TODO make this bigger in proportion to the size of the image so it stays readable.
                    var location = new SixLabors.Primitives.PointF(0, 0);
                    var color = SixLabors.ImageSharp.Color.Black;
                    var font = new Font(SystemFonts.Find("Comic Sans MS"), (int)(extra*0.9/subtitleLineCount), FontStyle.Bold);
                    //font = sparklineFont.AvailableStyles;
                    //result.Mutate(oo => oo.DrawText(subtitle, font, color, location));

                    var lines = subtitle.Split("\n");
                    
                    var currentLine = 0;
                    foreach (var line in lines)
                    {
                        var ul = new Vector2(0, imageHeight - extra.Value + currentLine * subtitleLineHeight);
                        try
                        {
                            result.Mutate(oo => oo.DrawText(line, font, color, ul));
                        }
                        catch (Exception ex)
                        {
                            //imagesharp throws exceptions even when it writes.
                            //but failures will break the next line writing.
                        }
                        currentLine++;
                    }

                    

                }

                if (pointTexts != null)
                {
                    
                    if (arrows)
                    {
                        //arrow width scales with board height+width.
                        int arrowWidth = (int)((level.Width + level.Height) * 0.003)+1;
                        PointText? lastPoint = null;
                        foreach (var pt in pointTexts)
                        {
                            if (lastPoint!=null)
                            {
                                DrawArrowFrom(result, lastPoint, pt, arrowWidth, effectiveScale);
                            }
                            lastPoint = pt;
                        }
                    }
                    else
                    {
                        foreach (var pt in pointTexts)
                        {
                            DrawTextAtPoint(result, pt.Point, pt.Text, effectiveScale);
                        }
                    }
                }

                result.Save($"{fn}");
            }

            if (!quiet)
            {
                Console.WriteLine($"Saved to: {fn}");
            }
        }

        //adjust point to center of square.
        public static void DrawArrowFrom(Image<Rgba32> image, PointText start, PointText end, int arrowWidth, int effectiveScale)
        {
            var s = new PointF(start.Point.Item1 * effectiveScale + effectiveScale / 2, start.Point.Item2 * effectiveScale + effectiveScale / 2);
            var e = new PointF(end.Point.Item1* effectiveScale + effectiveScale / 2, end.Point.Item2 * effectiveScale + effectiveScale / 2);
            
            var go = new GraphicsOptions(true, 1.0f);

            image.Mutate(oo => oo.DrawLines(SixLabors.ImageSharp.Color.White, arrowWidth+3, s, e));
            image.Mutate(oo => oo.DrawLines(SixLabors.ImageSharp.Color.Violet, arrowWidth, s, e));
        }

        public static void DrawTextAtPoint(Image<Rgba32> image, (int, int) point, string text, int effectiveScale) {

            var location = new SixLabors.Primitives.PointF(0, 0);
            //result.Mutate(oo => oo.DrawText(subtitle, font, color, location));
            try {
                var pointful = new PointF(point.Item1 * effectiveScale - 5, point.Item2 * effectiveScale - 5);
                var pointf = new PointF(point.Item1 * effectiveScale, point.Item2* effectiveScale);
                image.Mutate(oo => oo.DrawText(text, BigFont, SixLabors.ImageSharp.Color.Yellow, pointful));
                image.Mutate(oo => oo.DrawText(text, MedFont, SixLabors.ImageSharp.Color.Black, pointf));
            }
            catch (Exception ex)
            {
                var aa = 0;
                //silly imagesharp, writing even when you claim you can't.
            }
        }

        public static Dictionary<string, Image> GetImages()
        {
            var stem = "../../..";
            var keyfp = $"{stem}/tiles/rr.png";

            var d = new Dictionary<string, Image>();

            //highlight
            d["hh"] = Image.Load<Rgba32>($"{stem}/tiles/hh.png");

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