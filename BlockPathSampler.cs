using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace coil
{
    public sealed class BlockRoutes
    {
        public int Before { get; init; }
        public int After { get; init; }
        public int[] Original { get; init; }
        public IReadOnlyList<int[]> Routes { get; init; }
    }

    public sealed partial class ReversiblePathSampler
    {
        public long BlockAttempts { get; private set; }
        public long BlockEligible { get; private set; }
        public long BlockChanges { get; private set; }
        public long BlockCandidates { get; private set; }

        // The rectangle and entry edge are selected independently of the state.
        // All cells outside the selected contiguous visit to the rectangle are
        // frozen, including other visits to this same rectangle.
        public BlockRoutes EnumerateBlock(int x, int y, int size, int entry)
        {
            if (size < 2 || size > 4 || x < 0 || y < 0 || x + size > Width || y + size > Height || entry < 0 || entry >= 4 * size)
                throw new ArgumentException("Block must be an in-bounds square of side 2..4 with a perimeter entry.");
            var side = entry / size; var along = entry % size;
            var ax = side == 1 ? x + size : side == 3 ? x - 1 : x + along;
            var ay = side == 0 ? y - 1 : side == 2 ? y + size : y + along;
            if (ax < 0 || ay < 0 || ax >= Width || ay >= Height) return null;
            var a = ay * Width + ax;
            bool Inside(int cell) => cell >= 0 && cell % Width >= x && cell % Width < x + size && cell / Width >= y && cell / Width < y + size;
            if (order[a] == 0 || !Inside(next[a])) return null;
            removed.Clear(); added.Clear();
            var b = next[a];
            while (Inside(b)) { removed.Add(b); b = next[b]; }
            var original = removed.ToArray();
            var entrance = original[0];
            var permitted = new HashSet<int>(original);
            for (var yy = y; yy < y + size; yy++) for (var xx = x; xx < x + size; xx++)
                if (order[yy * Width + xx] == 0) permitted.Add(yy * Width + xx);
            var candidates = new List<int[]>();
            var used = new HashSet<int>();
            void Visit(int cell)
            {
                added.Add(cell); used.Add(cell);
                var canFinish = b < 0 || Enumerable.Range(0, 4).Any(d => Neighbor(cell, d) == b);
                if (canFinish && Replace(a, b, 1, _ => 0, reuseRemoved: true, probe: true)) candidates.Add(added.ToArray());
                // Full enumeration has no time/node cutoff. Truncating the set
                // of alternatives would invalidate this conditional sampler.
                for (var d = 0; d < 4; d++)
                {
                    var child = Neighbor(cell, d);
                    if (permitted.Contains(child) && !used.Contains(child)) Visit(child);
                }
                used.Remove(cell); added.RemoveAt(added.Count - 1);
            }
            Visit(entrance);
            if (!candidates.Any(route => route.SequenceEqual(original)))
                throw new InvalidOperationException("Block conditional omitted its original legal route.");
            return new BlockRoutes { Before = a, After = b, Original = original, Routes = candidates };
        }

        public void ApplyBlock(BlockRoutes options, int index)
        {
            ArgumentNullException.ThrowIfNull(options);
            if (index < 0 || index >= options.Routes.Count) throw new ArgumentOutOfRangeException(nameof(index));
            var cell = next[options.Before];
            foreach (var expected in options.Original)
            {
                if (cell != expected) throw new InvalidOperationException("Block options are stale.");
                cell = next[cell];
            }
            if (cell != options.After) throw new InvalidOperationException("Block options have a stale exit.");
            removed.Clear(); removed.AddRange(options.Original);
            added.Clear(); added.AddRange(options.Routes[index]);
            if (!Replace(options.Before, options.After, 1, _ => 0, reuseRemoved: true))
                throw new InvalidOperationException("Enumerated block route failed its legality check.");
        }

        public bool BlockStep(Func<int, int> choose, int activity)
        {
            if (activity < 1) throw new ArgumentOutOfRangeException(nameof(activity));
            BlockAttempts++;
            var maximum = Math.Min(4, Math.Min(Width, Height));
            if (maximum < 2) return false;
            var size = 2 + choose(maximum - 1);
            var options = EnumerateBlock(choose(Width - size + 1), choose(Height - size + 1), size, choose(4 * size));
            if (options == null) return false;
            BlockEligible++; BlockCandidates += options.Routes.Count;
            var weights = options.Routes.Select(route => BigInteger.Pow(activity, route.Length)).ToArray();
            var ticket = ExactRandom.Below(weights.Aggregate(BigInteger.Zero, (sum, weight) => sum + weight), choose);
            var chosen = 0;
            while (ticket >= weights[chosen]) { ticket -= weights[chosen]; chosen++; }
            var changed = !options.Routes[chosen].SequenceEqual(options.Original);
            ApplyBlock(options, chosen);
            if (changed) BlockChanges++;
            return changed;
        }

        public void DeepStep(Func<int, int> choose, int activity = 2)
        {
            // Fixed mixture: the legacy kernel retains its all-state coverage.
            if (choose(8) == 0) BlockStep(choose, activity);
            else Step(choose, activity);
        }
    }

    public static class ExactRandom
    {
        public static BigInteger Below(BigInteger bound, Func<int, int> choose)
        {
            if (bound < 1) throw new ArgumentOutOfRangeException(nameof(bound));
            if (bound <= int.MaxValue) return choose((int)bound);
            var bits = (bound - 1).GetBitLength();
            var bytes = new byte[(bits + 7) / 8];
            while (true)
            {
                for (var i = 0; i < bytes.Length; i++) bytes[i] = (byte)choose(256);
                bytes[bytes.Length - 1] &= (byte)(255 >> (int)(8 * bytes.Length - bits));
                var value = new BigInteger(bytes, isUnsigned: true, isBigEndian: false);
                if (value < bound) return value;
            }
        }
    }
}
