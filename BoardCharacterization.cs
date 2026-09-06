using System;
using System.Collections.Generic;
using System.Linq;

namespace coil
{
    // Full-board geometry with O(width + height + number of 32-cell tiles) scratch space beyond the input.
    // No path search, graph objects, room decomposition, or solution-derived features.
    public static class BoardCharacterization
    {
        public static object Describe(int w, int h, bool[] wall)
        {
            if (w < 1 || h < 1 || wall.Length != checked(w * h)) throw new ArgumentException("Invalid board dimensions");
            long open = 0, hEdges = 0, vEdges = 0, isolated = 0;
            var degree = new long[5];
            var horizontal = new SortedDictionary<int, long>();
            var vertical = new SortedDictionary<int, long>();
            var verticalRun = new int[w];
            var openSquare = new int[w + 1];
            var wallSquare = new int[w + 1];
            var openSquares = new SortedDictionary<int, long>();
            var wallSquares = new SortedDictionary<int, long>();
            var maxOpen = new int[3];
            var maxWall = new int[3];
            var layerOpen = new long[(Math.Min(w, h) + 1) / 2];
            var layerTotal = new long[layerOpen.Length];
            long mirrorX = 0, mirrorY = 0, rotate = 0;
            var tileOpen = new long[((w + 31) / 32) * ((h + 31) / 32)];
            var tileTotal = new long[tileOpen.Length];
            for (int y = 0; y < h; y++)
            {
                int run = 0, prevOpen = 0, prevWall = 0;
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    bool o = !wall[i];
                    int layer = Math.Min(Math.Min(x, w - 1 - x), Math.Min(y, h - 1 - y));
                    layerTotal[layer]++;
                    int tile = y / 32 * ((w + 31) / 32) + x / 32;
                    tileTotal[tile]++;
                    if (o)
                    {
                        open++; layerOpen[layer]++; tileOpen[tile]++; run++; verticalRun[x]++;
                        int d = 0;
                        if (x > 0 && !wall[i - 1]) d++;
                        if (x + 1 < w && !wall[i + 1]) { d++; hEdges++; }
                        if (y > 0 && !wall[i - w]) d++;
                        if (y + 1 < h && !wall[i + w]) { d++; vEdges++; }
                        degree[d]++;
                    }
                    else
                    {
                        Add(horizontal, run); run = 0;
                        Add(vertical, verticalRun[x]); verticalRun[x] = 0;
                        if ((x == 0 || !wall[i - 1]) && (x == w - 1 || !wall[i + 1]) &&
                            (y == 0 || !wall[i - w]) && (y == h - 1 || !wall[i + w])) isolated++;
                    }
                    int aboveOpen = openSquare[x + 1], aboveWall = wallSquare[x + 1];
                    openSquare[x + 1] = o ? 1 + Math.Min(prevOpen, Math.Min(openSquare[x], aboveOpen)) : 0;
                    wallSquare[x + 1] = !o ? 1 + Math.Min(prevWall, Math.Min(wallSquare[x], aboveWall)) : 0;
                    prevOpen = aboveOpen; prevWall = aboveWall;
                    Add(openSquares, openSquare[x + 1]); Add(wallSquares, wallSquare[x + 1]);
                    if (openSquare[x + 1] > maxOpen[2]) maxOpen = new[] { x - openSquare[x + 1] + 1, y - openSquare[x + 1] + 1, openSquare[x + 1] };
                    if (wallSquare[x + 1] > maxWall[2]) maxWall = new[] { x - wallSquare[x + 1] + 1, y - wallSquare[x + 1] + 1, wallSquare[x + 1] };
                    if (wall[i] == wall[y * w + w - 1 - x]) mirrorX++;
                    if (wall[i] == wall[(h - 1 - y) * w + x]) mirrorY++;
                    if (wall[i] == wall[(h - 1 - y) * w + w - 1 - x]) rotate++;
                }
                Add(horizontal, run);
            }
            foreach (int run in verticalRun) Add(vertical, run);
            double area = (double)w * h, p = open / area;
            double chance = p * p + (1 - p) * (1 - p);
            double? Adjust(long matches) => chance == 1 ? null : (matches / area - chance) / (1 - chance);
            var density = tileOpen.Select((n, i) => (double)n / tileTotal[i]).ToArray();
            double mean = density.Average();
            return new {
                width = w, height = h, openCells = open, openFraction = p,
                degreeCounts = degree, isolatedWallFraction = area == open ? (double?)null : isolated / (area - open),
                interfacePerOpen = open == 0 ? (double?)null : (4 * open - 2.0 * (hEdges + vEdges)) / open,
                axisBias = hEdges + vEdges == 0 ? (double?)null : (double)(hEdges - vEdges) / (hEdges + vEdges),
                horizontalRuns = Summary(horizontal), verticalRuns = Summary(vertical),
                largestOpenSquare = maxOpen, largestWallSquare = maxWall,
                openSquareCounts = Cumulative(openSquares), wallSquareCounts = Cumulative(wallSquares),
                edgeLayers = layerOpen.Select((n, i) => new { distance = i, openFraction = (double)n / layerTotal[i] }),
                tileDensity = new { side = 32, columns = (w + 31) / 32, rows = (h + 31) / 32,
                    min = density.Min(), max = density.Max(), variance = density.Select(d => (d - mean) * (d - mean)).Average() },
                symmetry = new { mirrorX = Adjust(mirrorX), mirrorY = Adjust(mirrorY), rotate180 = Adjust(rotate),
                    rawMirrorX = mirrorX / area, rawMirrorY = mirrorY / area, rawRotate180 = rotate / area },
                scope = "Exact full-board geometry. Density variance uses nonoverlapping 32-cell tiles, including partial edge tiles equally. No room or proof analysis."
            };
        }
        static void Add(SortedDictionary<int, long> counts, int n)
        {
            if (n > 0) counts[n] = counts.GetValueOrDefault(n) + 1;
        }
        static object Summary(SortedDictionary<int, long> counts)
        {
            long count = counts.Values.Sum();
            return new { count, mean = count == 0 ? (double?)null : counts.Sum(kv => (double)kv.Key * kv.Value) / count,
                max = counts.Count == 0 ? 0 : counts.Keys.Last(), histogram = counts };
        }
        static SortedDictionary<int, long> Cumulative(SortedDictionary<int, long> counts)
        {
            var result = new SortedDictionary<int, long>(); long total = 0;
            if (counts.Count > 0) for (int n = counts.Keys.Last(); n >= 1; n--) { total += counts.GetValueOrDefault(n); result[n] = total; }
            return result;
        }
    }
}
