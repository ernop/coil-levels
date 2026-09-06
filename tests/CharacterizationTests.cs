using System;
using System.Linq;
using System.Text.Json;
using coil;

static class CharacterizationTests
{
    public static void Run()
    {
        // Exhaustive small inputs compare the rolling DP against direct enumeration.
        for (int mask = 0; mask < 512; mask++)
        {
            var wall = Enumerable.Range(0, 9).Select(i => (mask & (1 << i)) != 0).ToArray();
            var d = JsonSerializer.SerializeToElement(BoardCharacterization.Describe(3, 3, wall));
            int open = wall.Count(v => !v);
            Require(d.GetProperty("openCells").GetInt32() == open, "open count");
            foreach (bool targetWall in new[] { false, true })
            {
                var spectrum = d.GetProperty(targetWall ? "wallSquareCounts" : "openSquareCounts");
                for (int size = 1; size <= 3; size++)
                {
                    int count = 0;
                    for (int y = 0; y + size <= 3; y++)
                        for (int x = 0; x + size <= 3; x++)
                        {
                            bool all = true;
                            for (int yy = y; yy < y + size; yy++)
                                for (int xx = x; xx < x + size; xx++) all &= wall[yy * 3 + xx] == targetWall;
                            if (all) count++;
                        }
                    long actual = spectrum.TryGetProperty(size.ToString(), out var value) ? value.GetInt64() : 0;
                    Require(actual == count, "square placements");
                }
            }
            foreach (string key in new[] { "horizontalRuns", "verticalRuns" })
            {
                long total = d.GetProperty(key).GetProperty("histogram").EnumerateObject().Sum(p => int.Parse(p.Name) * p.Value.GetInt64());
                Require(total == open, "runs partition open cells");
            }
            Require(d.GetProperty("degreeCounts").EnumerateArray().Sum(v => v.GetInt32()) == open, "degree population");
            var rotated = new bool[9];
            for (int y = 0; y < 3; y++) for (int x = 0; x < 3; x++) rotated[x * 3 + 2 - y] = wall[y * 3 + x];
            var r = JsonSerializer.SerializeToElement(BoardCharacterization.Describe(3, 3, rotated));
            Require(d.GetProperty("horizontalRuns").GetRawText() == r.GetProperty("verticalRuns").GetRawText(), "rotation swaps runs");
            if (d.GetProperty("axisBias").ValueKind != JsonValueKind.Null)
                Require(Math.Abs(d.GetProperty("axisBias").GetDouble() + r.GetProperty("axisBias").GetDouble()) < 1e-12, "rotation reverses bias");
        }
        var corridor = JsonSerializer.SerializeToElement(BoardCharacterization.Describe(5, 1, new bool[5]));
        Require(corridor.GetProperty("degreeCounts")[1].GetInt32() == 2, "corridor endpoints");
        Require(corridor.GetProperty("axisBias").GetDouble() == 1, "horizontal corridor bias");
        Require(Math.Abs(corridor.GetProperty("interfacePerOpen").GetDouble() - 2.4) < 1e-12, "exterior interface counted");
        var split = Enumerable.Range(0, 64 * 32).Select(i => i % 64 >= 32).ToArray();
        var tiles = JsonSerializer.SerializeToElement(BoardCharacterization.Describe(64, 32, split));
        Require(tiles.GetProperty("tileDensity").GetProperty("variance").GetDouble() == 0.25, "two opposite-density tiles");
        Require(tiles.GetProperty("symmetry").GetProperty("mirrorX").GetDouble() == -1, "opposite left/right halves");
        Require(tiles.GetProperty("symmetry").GetProperty("mirrorY").GetDouble() == 1, "vertical reflection of halves");
        var partial = Enumerable.Range(0, 33 * 2).Select(i => i % 33 < 32).ToArray();
        var partialTiles = JsonSerializer.SerializeToElement(BoardCharacterization.Describe(33, 2, partial));
        Require(partialTiles.GetProperty("tileDensity").GetProperty("variance").GetDouble() == 0.25, "partial edge tiles have equal weight");
        Console.WriteLine("Characterization passed all 512 binary 3x3 boards, rotations, and corridor fixtures.");
    }
    static void Require(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException("Characterization: " + label);
    }
}
