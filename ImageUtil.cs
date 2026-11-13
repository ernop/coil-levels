using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;

using SkiaSharp;

using static coil.Util;

namespace coil
{

    public static class ImageUtil
    {
        public const int Scale = 15;

        //Background of every on-image crop label and detail frame.
        public static readonly SKColor CropFrame = new SKColor(0x14, 0x29, 0x1f);

        //Bitmaps are drawn unscaled at whole-pixel offsets, so each source pixel lands on exactly one pixel.
        public static readonly SKSamplingOptions Unscaled = new SKSamplingOptions(SKFilterMode.Nearest);

        /// <summary>
        /// Whole-board map at px pixels per cell: open cells white, walls black, nothing else. This is what a solver
        /// sees. The tile renderer (<see cref="Save"/>) is 15 px per cell and cannot draw boards past a few hundred
        /// cells a side; this writes pixels directly and handles 5000x5000 at 1 px.
        /// </summary>
        public static void SaveMap(BaseLevel level, string fn, int px)
        {
            var w = level.Width - 2;
            var h = level.Height - 2;
            using (var img = NewGray(w * px, h * px))
            {
                var pixels = img.GetPixelSpan();
                for (var y = 0; y < h * px; y++)
                {
                    var row = pixels.Slice(y * img.RowBytes, w * px);
                    var cellY = y / px + 1;
                    for (var x = 0; x < w * px; x++)
                    {
                        row[x] = level.GetRowValue((x / px + 1, cellY)) == null ? (byte)0 : (byte)255;
                    }
                }
                SavePng(img, fn);
            }
        }

        //Comic Sans MS was the original; it is not installed on Linux. One name, no fallback list.
        public const string FontFamilyName = "DejaVu Sans";

        private static readonly SKTypeface Regular = Typeface(SKFontStyle.Normal);
        private static readonly SKTypeface Bold = Typeface(SKFontStyle.Bold);

        //fontconfig answers a missing family with another one; refuse that instead of drawing in it.
        private static SKTypeface Typeface(SKFontStyle style)
        {
            var typeface = SKTypeface.FromFamilyName(FontFamilyName, style);
            if (typeface?.FamilyName != FontFamilyName)
            {
                throw new InvalidOperationException($"Font family {FontFamilyName} is not installed.");
            }
            return typeface;
        }

        public static SKFont MakeFont(float size, bool bold = false) => new SKFont(bold ? Bold : Regular, size);

        public static readonly SKFont Font = MakeFont(17, bold: true);
        public static readonly SKFont MedFont = MakeFont(27);
        public static readonly SKFont BigFont = MakeFont(36, bold: true);

        /// <summary>Draws text whose line starts at top; Skia's own y is the baseline.</summary>
        public static void DrawText(SKCanvas canvas, string text, SKFont font, SKColor color, float x, float top)
        {
            using var paint = new SKPaint { Color = color, IsAntialias = true };
            canvas.DrawText(text, x, top + BaselineBelowTop(font), SKTextAlign.Left, font, paint);
        }

        /// <summary>
        /// Distance from the top of a line of text to its baseline in the OpenType typographic line box: sTypoAscender
        /// plus half of sTypoLineGap, from the font's OS/2 table. The layouts here are sized for this box; Skia's
        /// Ascent comes from the hhea table instead and sits 0.07 em lower in DejaVu Sans.
        /// </summary>
        private static float BaselineBelowTop(SKFont font)
        {
            var os2 = font.Typeface.GetTableData(0x4F532F32); //'OS/2'
            short Int16At(int offset) => (short)(os2[offset] << 8 | os2[offset + 1]);
            return font.Size * (Int16At(68) + Int16At(72) / 2f) / font.Typeface.UnitsPerEm;
        }

        public static void Save(Dictionary<string, SKBitmap> images, BaseLevel level, List<List<string>> outstrings, string fn, string subtitle, bool quiet = false,
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
            using (var result = NewRgba(imageWidth, imageHeight + cropCaptionHeight, SKColors.Transparent))
            {
                using (var canvas = new SKCanvas(result))
                {
                    if (outstrings != null)
                    {
                        var scaledTiles = new Dictionary<string, SKBitmap>();
                        try
                        {
                            foreach (var pair in images)
                                if (pair.Value.Width != effectiveScale || pair.Value.Height != effectiveScale)
                                    scaledTiles[pair.Key] = ResizeNearest(pair.Value, effectiveScale, effectiveScale);
                            for (int y = 0; y < usingHeight; y++)
                            {
                                for (int x = 0; x < usingWidth; x++)
                                {
                                    string key = outstrings[y][x];
                                    var tile = scaledTiles.TryGetValue(key, out var resized) ? resized : images[key];
                                    canvas.DrawBitmap(tile, x * effectiveScale, y * effectiveScale, Unscaled);
                                }
                            }
                        }
                        finally
                        {
                            foreach (var tile in scaledTiles.Values) tile.Dispose();
                        }
                    }
                    if (writeSubtitle)
                    {
                        //TODO make this bigger in proportion to the size of the image so it stays readable.
                        using var font = MakeFont((int)(extra.Value * 0.9 / subtitleLineCount), bold: true);
                        var currentLine = 0;
                        foreach (var line in subtitleLines)
                        {
                            DrawText(canvas, line, font, SKColors.Black, 0, imageHeight - extra.Value + currentLine * subtitleLineHeight);
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
                                    DrawArrowFrom(canvas, lastPoint, pt, arrowWidth, effectiveScale);
                                }
                                lastPoint = pt;
                            }
                        }
                        else
                        {
                            foreach (var pt in pointTexts)
                            {
                                DrawTextAtPoint(canvas, pt.Point, pt.Text, effectiveScale);
                            }
                        }
                    }

                    if (cropped)
                    {
                        string label = imageWidth >= 500
                            ? $"CROP · upper-left detail of {level.Width - 2} x {level.Height - 2} board"
                            : "CROP";
                        using var frame = new SKPaint { Color = CropFrame };
                        canvas.DrawRect(SKRect.Create(0, imageHeight, imageWidth, cropCaptionHeight), frame);
                        DrawText(canvas, label, Font, SKColors.White, 8, imageHeight + 12);
                    }
                }
                SavePng(result, fn);
            }

            if (!quiet)
            {
                Console.WriteLine($"Saved to: {fn}");
            }
            //WL($"Save took: {st.Elapsed}");
        }

        //adjust point to center of square.
        public static void DrawArrowFrom(SKCanvas canvas, PointText start, PointText end, int arrowWidth, int effectiveScale)
        {
            var s = new SKPoint(start.Point.Item1 * effectiveScale + effectiveScale / 2, start.Point.Item2 * effectiveScale + effectiveScale / 2);
            var e = new SKPoint(end.Point.Item1* effectiveScale + effectiveScale / 2, end.Point.Item2 * effectiveScale + effectiveScale / 2);

            using var pen = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = arrowWidth + 3, Color = SKColors.White };
            canvas.DrawLine(s, e, pen);
            pen.StrokeWidth = arrowWidth;
            pen.Color = SKColors.Violet;
            canvas.DrawLine(s, e, pen);
        }

        public static void DrawTextAtPoint(SKCanvas canvas, (int, int) point, string text, int effectiveScale) {
            DrawText(canvas, text, BigFont, SKColors.Yellow, point.Item1 * effectiveScale - 5, point.Item2 * effectiveScale - 5);
            DrawText(canvas, text, MedFont, SKColors.Black, point.Item1 * effectiveScale, point.Item2 * effectiveScale);
        }

        public static Dictionary<string, SKBitmap> GetImages()
        {
            var d = new Dictionary<string, SKBitmap>();
            SKBitmap Tile(string name) => Load($"{Paths.Root}/tiles/{name}.png", SKColorType.Rgba8888);

            //highlight
            d["hh"] = Tile("hh");

            d["rr"] = Tile("rr");
            d["dd"] = Rotate(d["rr"], 90);
            d["ll"] = Rotate(d["rr"], 180);
            d["uu"] = Rotate(d["rr"], 270);

            //A corner tile, its turns and its mirror image's turns: plain, then "decision easy" and "decision hard".
            foreach (var suffix in new[] { "", "-easy", "-hard" })
            {
                var ru = Tile("ru" + suffix);
                d["ru" + suffix] = ru;
                d["ul" + suffix] = Rotate(ru, -90);
                d["ld" + suffix] = Rotate(ru, -180);
                d["dr" + suffix] = Rotate(ru, -270);

                var rd = FlipVertical(ru);
                d["rd" + suffix] = rd;
                d["ur" + suffix] = Rotate(rd, -90);
                d["lu" + suffix] = Rotate(rd, -180);
                d["dl" + suffix] = Rotate(rd, -270);
            }

            d["su"] = Tile("su");
            d["sr"] = Rotate(d["su"], 90);
            d["sd"] = Rotate(d["su"], 180);
            d["sl"] = Rotate(d["su"], 270);

            d["ue"] = Tile("ue");
            d["re"] = Rotate(d["ue"], 90);
            d["de"] = Rotate(d["ue"], 180);
            d["le"] = Rotate(d["ue"], 270);

            foreach (var key in new[] { "b", "x", "h", "empty", "s", "e" })
            {
                d[key] = Tile(key);
            }
            d["."] = d["empty"];

            foreach (var tile in d.Values) tile.SetImmutable();
            return d;
        }

        /// <summary>A board's map at 1 px per cell, from a parsed board: open cells white, walls black.</summary>
        public static SKBitmap WallMap(int width, int height, bool[] walls)
        {
            var map = NewGray(width, height);
            var pixels = map.GetPixelSpan();
            for (var y = 0; y < height; y++)
                for (var x = 0; x < width; x++) pixels[y * map.RowBytes + x] = walls[y * width + x] ? (byte)0 : (byte)255;
            return map;
        }

        public static SKBitmap NewGray(int width, int height)
        {
            var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Gray8, SKAlphaType.Opaque));
            bitmap.Erase(SKColors.Black);
            return bitmap;
        }

        public static SKBitmap NewRgba(int width, int height, SKColor background)
        {
            var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
            bitmap.Erase(background);
            return bitmap;
        }

        //Opaque, so it is written as an RGB PNG.
        public static SKBitmap NewRgb(int width, int height)
        {
            var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Opaque));
            bitmap.Erase(SKColors.Black);
            return bitmap;
        }

        /// <summary>
        /// Decodes an image as Gray8, which refuses a color image, or as premultiplied Rgba8888. No color space
        /// conversion: the stored values are the pixels.
        /// </summary>
        public static SKBitmap Load(string path, SKColorType colorType)
        {
            using var codec = SKCodec.Create(path) ?? throw new InvalidDataException($"Not a readable image: {path}");
            var info = new SKImageInfo(codec.Info.Width, codec.Info.Height, colorType,
                colorType == SKColorType.Gray8 ? SKAlphaType.Opaque : SKAlphaType.Premul);
            var bitmap = new SKBitmap(info);
            var result = codec.GetPixels(info, bitmap.GetPixels());
            if (result != SKCodecResult.Success)
            {
                bitmap.Dispose();
                throw new InvalidDataException($"Decoding {path} as {colorType} failed: {result}");
            }
            return bitmap;
        }

        public static SKSizeI Dimensions(string path)
        {
            using var codec = SKCodec.Create(path) ?? throw new InvalidDataException($"Not a readable image: {path}");
            return codec.Info.Size;
        }

        public static void SavePng(SKBitmap bitmap, string path)
        {
            using var stream = File.Create(path);
            if (!bitmap.Encode(stream, SKEncodedImageFormat.Png, 100))
            {
                throw new IOException($"Encoding {path} as PNG failed.");
            }
        }

        /// <summary>A copy turned by a multiple of 90 degrees, clockwise for positive degrees.</summary>
        public static SKBitmap Rotate(SKBitmap source, int degrees)
        {
            if (degrees % 90 != 0) throw new ArgumentOutOfRangeException(nameof(degrees), "Only quarter turns.");
            int w = source.Width, h = source.Height;
            return ((degrees % 360 + 360) % 360) switch
            {
                0 => Remap(source, w, h, (x, y) => (x, y)),
                90 => Remap(source, h, w, (x, y) => (y, h - 1 - x)),
                180 => Remap(source, w, h, (x, y) => (w - 1 - x, h - 1 - y)),
                _ => Remap(source, h, w, (x, y) => (w - 1 - y, x)),
            };
        }

        /// <summary>A copy mirrored top to bottom.</summary>
        public static SKBitmap FlipVertical(SKBitmap source) =>
            Remap(source, source.Width, source.Height, (x, y) => (x, source.Height - 1 - y));

        public static SKBitmap ResizeNearest(SKBitmap source, int width, int height) =>
            ResizeNearest(source, SKRectI.Create(0, 0, source.Width, source.Height), width, height);

        /// <summary>
        /// The rectangle from of source resized to width x height by nearest neighbor: output column x copies source
        /// column from.Left + (int)(x * (from.Width / (float)width)), the one under its left edge; rows likewise.
        /// </summary>
        public static SKBitmap ResizeNearest(SKBitmap source, SKRectI from, int width, int height)
        {
            var widthFactor = from.Width / (float)width;
            var heightFactor = from.Height / (float)height;
            return Remap(source, width, height, (x, y) => (from.Left + (int)(x * widthFactor), from.Top + (int)(y * heightFactor)));
        }

        /// <summary>
        /// A gray map resized by a box filter: each pixel is the mean, rounded half up, of the source pixels whose
        /// centers lie within half an output pixel of its center on both axes. Enlarging repeats the nearest pixel.
        /// </summary>
        public static SKBitmap ResizeBox(SKBitmap gray, int width, int height)
        {
            if (gray.ColorType != SKColorType.Gray8) throw new ArgumentException("A box resize takes a gray map.", nameof(gray));
            var columns = BoxWindows(gray.Width, width);
            var rows = BoxWindows(gray.Height, height);
            var result = NewGray(width, height);
            ReadOnlySpan<byte> from = gray.GetPixelSpan();
            var to = result.GetPixelSpan();
            for (var y = 0; y < height; y++)
            {
                var (top, rowCount) = rows[y];
                for (var x = 0; x < width; x++)
                {
                    var (left, columnCount) = columns[x];
                    long sum = 0;
                    for (var sy = top; sy < top + rowCount; sy++)
                    {
                        foreach (var value in from.Slice(sy * gray.RowBytes + left, columnCount)) sum += value;
                    }
                    long count = (long)rowCount * columnCount;
                    to[y * result.RowBytes + x] = (byte)((2 * sum + count) / (2 * count));
                }
            }
            return result;
        }

        //For each output pixel, the run of source pixels whose centers lie within half an output pixel of its center.
        private static (int start, int count)[] BoxWindows(int sourceLength, int length)
        {
            var ratio = (double)sourceLength / length;
            var scale = Math.Max(1, ratio);
            var windows = new (int, int)[length];
            for (var i = 0; i < length; i++)
            {
                var center = (i + 0.5) * ratio - 0.5;
                int start = -1, count = 0;
                var last = Math.Min(sourceLength - 1, (int)Math.Ceiling(center + scale));
                for (var j = Math.Max(0, (int)Math.Floor(center - scale)); j <= last; j++)
                {
                    var offset = (float)((j - center) / scale);
                    if (offset > -0.5f && offset <= 0.5f)
                    {
                        if (start < 0) start = j;
                        count++;
                    }
                }
                windows[i] = (start, count);
            }
            return windows;
        }

        //A width x height copy whose pixel (x, y) is source's pixel sourceOf(x, y), bytes unchanged.
        private static SKBitmap Remap(SKBitmap source, int width, int height, Func<int, int, (int x, int y)> sourceOf)
        {
            var result = new SKBitmap(new SKImageInfo(width, height, source.ColorType, source.AlphaType));
            var bytes = source.BytesPerPixel;
            ReadOnlySpan<byte> from = source.GetPixelSpan();
            var to = result.GetPixelSpan();
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var (sx, sy) = sourceOf(x, y);
                    from.Slice(sy * source.RowBytes + sx * bytes, bytes).CopyTo(to.Slice(y * result.RowBytes + x * bytes, bytes));
                }
            }
            return result;
        }
    }
}
