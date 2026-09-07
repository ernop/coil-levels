using System;
using System.Diagnostics;
using System.Collections.Generic;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.Fonts;

using static coil.Util;
using System.Numerics;
using SixLabors.ImageSharp.Drawing.Processing;

using System.Threading;
using System.Threading.Tasks;

namespace coil
{

    public static class ImageUtil
    {
        public const int Scale = 15;

        /// <summary>
        /// Whole-board map at px pixels per cell: open cells white, walls black, nothing else. This is what a solver
        /// sees. The tile renderer (<see cref="Save"/>) is 15 px per cell and cannot draw boards past a few hundred
        /// cells a side; this writes pixels directly and handles 5000x5000 at 1 px.
        /// </summary>
        public static void SaveMap(BaseLevel level, string fn, int px)
        {
            var w = level.Width - 2;
            var h = level.Height - 2;
            using (var img = new Image<L8>(w * px, h * px))
            {
                var white = new L8(255);
                var black = new L8(0);
                img.ProcessPixelRows(accessor =>
                {
                    for (var y = 0; y < h * px; y++)
                    {
                        var row = accessor.GetRowSpan(y);
                        var cellY = y / px + 1;
                        for (var x = 0; x < w * px; x++)
                        {
                            row[x] = level.GetRowValue((x / px + 1, cellY)) == null ? black : white;
                        }
                    }
                });
                img.Save(fn);
            }
        }

        //Comic Sans MS was the original; it is not installed on Linux. One name, no fallback list.
        public const string FontFamilyName = "DejaVu Sans";

        public static Font Font = new Font(SystemFonts.Get(FontFamilyName), 17, FontStyle.Bold);
        public static Font MedFont= new Font(SystemFonts.Get(FontFamilyName), 27, FontStyle.Regular);
        public static Font BigFont = new Font(SystemFonts.Get(FontFamilyName), 36, FontStyle.Bold);

        public static void Save(Dictionary<string, Image> images, BaseLevel level, List<List<string>> outstrings, string fn, string subtitle, bool quiet = false,
            List<PointText> pointTexts = null, bool arrows = false, int? overrideScale = null, List<(int,int)> highlights = null, bool corner = false)
        {
            var st = Stopwatch.StartNew();
            //juggle the path to determine what should be written in each square.

            var effectiveScale = overrideScale ?? Scale;
            if (effectiveScale < 1) throw new ArgumentOutOfRangeException(nameof(overrideScale));

            var usingHeight = corner ? Math.Min(level.Height, 55) : level.Height;
            var usingWidth = corner ? Math.Min(level.Width, 80) : level.Width;

            //allows partial segments
            var imageHeight = usingHeight * effectiveScale;
            var imageWidth = usingWidth * effectiveScale;
            
            var writeSubtitle = false;
            int? extra = null;
            var subtitleLineCount = 1;
            var subtitleLines = subtitle?.Split("\n");
            var subtitleLineHeight = 0;
            if (!String.IsNullOrEmpty(subtitle))
            {
                subtitleLineCount = subtitleLines.Length;
                
                extra = Math.Max(effectiveScale, (int)(1.1* subtitleLineCount * usingHeight))+8;
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
            
            bool cropped = usingWidth < level.Width || usingHeight < level.Height;
            int cropCaptionHeight = cropped ? 44 : 0;
            using (var result = new Image<Rgba32>(imageWidth, imageHeight + cropCaptionHeight))
            {
                if (outstrings != null)
                {
                    var strips = new Image<Rgba32>[usingHeight];
                    var scaledTiles = new Dictionary<string, Image>();
                    try
                    {
                        foreach (var pair in images)
                            if (pair.Value.Width != effectiveScale || pair.Value.Height != effectiveScale)
                                scaledTiles[pair.Key] = pair.Value.Clone(c => c.Resize(effectiveScale, effectiveScale, KnownResamplers.NearestNeighbor));
                        Parallel.For(0, usingHeight, y =>
                        {
                            var strip = new Image<Rgba32>(imageWidth, effectiveScale);
                            strips[y] = strip;
                            for (int x = 0; x < usingWidth; x++)
                            {
                                string key = outstrings[y][x];
                                var tile = scaledTiles.TryGetValue(key, out var resized) ? resized : images[key];
                                strip.Mutate(c => c.DrawImage(tile, new Point(x * effectiveScale, 0), 1f));
                            }
                        });
                        // Parallel completion order must never change board row order.
                        for (int y = 0; y < usingHeight; y++)
                        {
                            int row = y;
                            result.Mutate(c => c.DrawImage(strips[row], new Point(0, row * effectiveScale), 1f));
                        }
                    }
                    finally
                    {
                        foreach (var strip in strips) strip?.Dispose();
                        foreach (var tile in scaledTiles.Values) tile.Dispose();
                    }
                }
                if (writeSubtitle)
                {
                    //TODO make this bigger in proportion to the size of the image so it stays readable.
                    var location = new PointF(0, 0);
                    var color = SixLabors.ImageSharp.Color.Black;
                    var font = new Font(SystemFonts.Get(FontFamilyName), (int)(extra*0.9/subtitleLineCount), FontStyle.Bold);
                    //font = sparklineFont.AvailableStyles;
                    //result.Mutate(oo => oo.DrawText(subtitle, font, color, location));

                    var lines = subtitle.Split("\n");
                    
                    var currentLine = 0;
                    foreach (var line in lines)
                    {
                        var ul = new Vector2(0, imageHeight - extra.Value + currentLine * subtitleLineHeight);
                        result.Mutate(oo => oo.DrawText(line, font, color, ul));
                        currentLine++;
                    }
                }

                if (pointTexts != null)
                {
                    
                    if (arrows)
                    {
                        //arrow width scales with board height+width.
                        int arrowWidth = (int)((usingHeight + usingWidth) * 0.003)+1;
                        PointText lastPoint = null;
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

                if (cropped)
                {
                    string label = imageWidth >= 500
                        ? $"CROP · upper-left detail of {level.Width - 2} x {level.Height - 2} board"
                        : "CROP";
                    result.Mutate(c => c
                        .Fill(Color.ParseHex("14291f"), new RectangleF(0, imageHeight, imageWidth, cropCaptionHeight))
                        .DrawText(label, Font, Color.White, new PointF(8, imageHeight + 12)));
                }
                result.Save(fn);
            }

            if (!quiet)
            {
                Console.WriteLine($"Saved to: {fn}");
            }
            //WL($"Save took: {st.Elapsed}");
        }

        //adjust point to center of square.
        public static void DrawArrowFrom(Image<Rgba32> image, PointText start, PointText end, int arrowWidth, int effectiveScale)
        {
            var s = new PointF(start.Point.Item1 * effectiveScale + effectiveScale / 2, start.Point.Item2 * effectiveScale + effectiveScale / 2);
            var e = new PointF(end.Point.Item1* effectiveScale + effectiveScale / 2, end.Point.Item2 * effectiveScale + effectiveScale / 2);
            
            image.Mutate(oo => oo.DrawLine(SixLabors.ImageSharp.Color.White, arrowWidth+3, s, e));
            image.Mutate(oo => oo.DrawLine(SixLabors.ImageSharp.Color.Violet, arrowWidth, s, e));
        }

        public static void DrawTextAtPoint(Image<Rgba32> image, (int, int) point, string text, int effectiveScale) {

            var location = new PointF(0, 0);
            //result.Mutate(oo => oo.DrawText(subtitle, font, color, location));
            var pointful = new PointF(point.Item1 * effectiveScale - 5, point.Item2 * effectiveScale - 5);
            var pointf = new PointF(point.Item1 * effectiveScale, point.Item2 * effectiveScale);
            image.Mutate(oo => oo.DrawText(text, BigFont, SixLabors.ImageSharp.Color.Yellow, pointful));
            image.Mutate(oo => oo.DrawText(text, MedFont, SixLabors.ImageSharp.Color.Black, pointf));
        }

        public static Dictionary<string, Image> GetImages()
        {
            var stem = Paths.Root;
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
            keyfp = $"{stem}/tiles/ru-easy.png";
            d["ru-easy"] = Image.Load<Rgba32>(keyfp);
            d["ul-easy"] = d["ru-easy"].Clone(oo => oo.Rotate(-90));
            d["ld-easy"] = d["ru-easy"].Clone(oo => oo.Rotate(-180));
            d["dr-easy"] = d["ru-easy"].Clone(oo => oo.Rotate(-270));

            d["rd-easy"] = d["ru-easy"].Clone(oo => oo.Flip(FlipMode.Vertical));
            d["ur-easy"] = d["rd-easy"].Clone(oo => oo.Rotate(-90));
            d["lu-easy"] = d["rd-easy"].Clone(oo => oo.Rotate(-180));
            d["dl-easy"] = d["rd-easy"].Clone(oo => oo.Rotate(-270));

            //"Decision hard" tiles
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
                keyfp = $"{stem}/tiles/{key}.png";
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