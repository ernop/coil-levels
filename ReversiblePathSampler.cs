using System;
using System.Collections.Generic;
using System.Text;

namespace coil
{
    // Edits have inverses; the player's solution need not work backward.
    // Ordered solution states, stationary weight activity^openCells.
    // Integer activity permits exact rational acceptance for deletions.
    // This is NOT uniform over boards: solution multiplicity also weights them.
    public sealed partial class ReversiblePathSampler
    {
        private static readonly int[] Dx = { 0, 1, 0, -1 }, Dy = { -1, 0, 1, 0 };
        private readonly int[] next, previous;
        private readonly ulong[] order;
        private readonly List<int> removed = new List<int>(), added = new List<int>();
        private readonly List<ulong> oldOrder = new List<ulong>();
        private int first, last;
        public int Width { get; }
        public int Height { get; }
        public int Count { get; private set; }
        public long Steps { get; private set; }
        public long[] Accepted { get; } = new long[6];
        public long[] Attempted { get; } = new long[6];
        public int Reindexes { get; private set; }

        public ReversiblePathSampler(BackwardGenerator seed)
        {
            seed.Validate(); Width = seed.Width; Height = seed.Height;
            next = new int[checked(Width * Height)]; previous = new int[next.Length]; order = new ulong[next.Length];
            Array.Fill(next, -1); Array.Fill(previous, -1);
            var path = seed.ReversePath; Count = path.Count;
            first = path[Count - 1]; last = path[0];
            for (var i = Count - 1; i >= 0; i--)
            {
                if (i > 0) next[path[i]] = path[i - 1];
                if (i < Count - 1) previous[path[i]] = path[i + 1];
            }
            Reindex(); Reindexes = 0;
        }

        private int Neighbor(int cell, int direction)
        {
            if (cell < 0) return -1;
            var x = cell % Width + Dx[direction]; var y = cell / Width + Dy[direction];
            return x < 0 || y < 0 || x >= Width || y >= Height ? -1 : y * Width + x;
        }

        private int Direction(int from, int to) => to / Width < from / Width ? 0
            : to / Width > from / Width ? 2 : to > from ? 1 : 3;

        private bool TurnLegal(int cell)
        {
            if (cell < 0 || order[cell] == 0 || previous[cell] < 0 || next[cell] < 0) return true;
            var d = Direction(previous[cell], cell);
            var ahead = Neighbor(cell, d);
            return next[cell] == ahead || ahead < 0 || order[ahead] == 0 || order[ahead] < order[cell];
        }

        private void Reindex()
        {
            var gap = ulong.MaxValue / ((ulong)Count + 1); ulong value = gap;
            for (var cell = first; cell >= 0; cell = next[cell]) { order[cell] = value; value += gap; }
            Reindexes++;
        }

        private void Link(int a, int b, List<int> middle)
        {
            var before = a;
            foreach (var cell in middle)
            {
                previous[cell] = before;
                if (before >= 0) next[before] = cell; else first = cell;
                before = cell;
            }
            if (before >= 0) next[before] = b; else first = b;
            if (b >= 0) previous[b] = before; else last = before;
        }

        private bool Replace(int a, int b, int activity, Func<int, int> choose, bool reuseRemoved = false, bool probe = false)
        {
            if (activity < 1) throw new ArgumentOutOfRangeException(nameof(activity));
            if (Count - removed.Count + added.Count < 1) return false;
            foreach (var cell in added)
                if (cell < 0 || (order[cell] != 0 && !(reuseRemoved && removed.Contains(cell)))) return false;
            // The descriptor constructors produce simple, disjoint rectangles or rays.
            var lower = a < 0 ? 0 : order[a]; var upper = b < 0 ? ulong.MaxValue : order[b];
            if ((upper - lower) / ((ulong)added.Count + 1) == 0)
            {
                Reindex(); lower = a < 0 ? 0 : order[a]; upper = b < 0 ? ulong.MaxValue : order[b];
            }
            oldOrder.Clear();
            foreach (var cell in removed) { oldOrder.Add(order[cell]); order[cell] = 0; }
            var gap = (upper - lower) / ((ulong)added.Count + 1);
            for (var i = 0; i < added.Count; i++) order[added[i]] = lower + ((ulong)i + 1) * gap;
            Link(a, b, added);
            var legal = TurnLegal(a) && TurnLegal(b);
            foreach (var cell in added)
            {
                legal &= TurnLegal(cell);
                // Opening a former wall can invalidate an earlier stopping cell.
                for (var d = 0; d < 4; d++) legal &= TurnLegal(Neighbor(cell, d));
            }
            if (legal)
                for (var i = added.Count; i < removed.Count; i++)
                    if (choose(activity) != 0) { legal = false; break; }
            if (legal && !probe)
            {
                Count += added.Count - removed.Count;
                return true;
            }
            foreach (var cell in added) order[cell] = 0;
            for (var i = 0; i < removed.Count; i++) order[removed[i]] = oldOrder[i];
            Link(a, b, removed);
            return legal;
        }

        // For both birth and death, direction points from the retained endpoint
        // toward the added/removed straight ray. Same descriptor, same probability.
        public bool Endpoint(bool atStart, bool grow, int direction, int length, int activity, Func<int, int> choose)
        {
            if (direction < 0 || direction > 3 || length < 1) throw new ArgumentException("Invalid endpoint descriptor.");
            removed.Clear(); added.Clear();
            var retained = atStart ? first : last;
            if (grow)
            {
                var cell = retained;
                for (var i = 0; i < length; i++)
                {
                    cell = Neighbor(cell, direction);
                    if (cell < 0 || order[cell] != 0) return false;
                    added.Add(cell);
                }
                if (atStart) added.Reverse();
            }
            else
            {
                if (length >= Count) return false;
                for (var i = 0; i < length; i++)
                {
                    removed.Add(retained);
                    retained = atStart ? next[retained] : previous[retained];
                }
                var cell = retained;
                for (var i = removed.Count - 1; i >= 0; i--)
                {
                    if (Neighbor(cell, direction) != removed[i]) return false;
                    cell = removed[i];
                }
                if (!atStart) removed.Reverse();
            }
            return Replace(atStart ? -1 : retained, atStart ? retained : -1, activity, choose);
        }

        public bool Rectangle(bool grow, int anchor, int direction, int length, int height, bool right, int activity, Func<int, int> choose)
        {
            if (anchor < 0 || anchor >= order.Length || direction < 0 || direction > 3 || length < 2 || height < 1)
                throw new ArgumentException("Invalid rectangle descriptor.");
            if (order[anchor] == 0) return false;
            removed.Clear(); added.Clear();
            var side = (direction + (right ? 1 : 3)) % 4;
            // Trace the old and proposed routes. Both include b but exclude a;
            // remove b after checking that the old route follows the linked path.
            var straight = grow ? removed : added;
            var detour = grow ? added : removed;
            var cell = anchor;
            for (var i = 0; i < length; i++) { cell = Neighbor(cell, direction); if (cell < 0) return false; straight.Add(cell); }
            var b = cell;
            cell = anchor;
            foreach (var leg in new[] { (side, height), (direction, length), ((side + 2) % 4, height) })
                for (var i = 0; i < leg.Item2; i++) { cell = Neighbor(cell, leg.Item1); if (cell < 0) return false; detour.Add(cell); }
            var before = anchor;
            foreach (var old in removed) { if (next[before] != old) return false; before = old; }
            if (before != b) throw new InvalidOperationException("Rectangle endpoints disagree.");
            removed.RemoveAt(removed.Count - 1); added.RemoveAt(added.Count - 1);
            return Replace(anchor, b, activity, choose);
        }

        public void Step(Func<int, int> choose, int activity = 1, int endpointMax = 8, int rectangleMax = 12, int heightMax = 3)
        {
            if (activity < 1 || endpointMax < 1 || rectangleMax < 2 || heightMax < 1) throw new ArgumentException("Invalid sampler limits.");
            Steps++;
            if (choose(2) == 0) return;
            var type = choose(6); Attempted[type]++;
            bool accepted;
            if (type < 4)
                accepted = Endpoint(type < 2, type % 2 == 0, choose(4), 1 + choose(endpointMax), activity, choose);
            else
                accepted = Rectangle(type == 4, choose(order.Length), choose(4), 2 + choose(rectangleMax - 1), 1 + choose(heightMax), choose(2) == 0, activity, choose);
            if (accepted) Accepted[type]++;
        }

        public BackwardGenerator Snapshot()
        {
            var forwardCount = 0; var before = -1; ulong lastOrder = 0;
            for (var cell = first; cell >= 0; cell = next[cell])
            {
                if (++forwardCount > Count || previous[cell] != before || order[cell] <= lastOrder)
                    throw new InvalidOperationException("Linked path/order invariant failed.");
                lastOrder = order[cell]; before = cell;
            }
            var owned = 0; foreach (var value in order) if (value != 0) owned++;
            if (forwardCount != Count || before != last || owned != Count)
                throw new InvalidOperationException("Linked path/occupancy count mismatch.");
            var growth = new StringBuilder(Count - 1);
            var visited = 1;
            for (var cell = last; previous[cell] >= 0; cell = previous[cell])
            {
                if (++visited > Count) throw new InvalidOperationException("Path cycle.");
                growth.Append("URDL"[Direction(cell, previous[cell])]);
            }
            if (visited != Count) throw new InvalidOperationException("Path count mismatch.");
            var result = BackwardGenerator.Replay(new BackwardRecipe { Width = Width, Height = Height,
                FinishX = last % Width, FinishY = last / Width, Growth = growth.ToString() });
            result.Validate();
            return result;
        }

        public static BackwardGenerator Stripes(int width, int height, int spacing = 2)
        {
            if (width < 1 || height < 1 || spacing < 2) throw new ArgumentException("Invalid stripe dimensions.");
            var path = new List<int>(); var x = 0; var y = 0; var right = true;
            path.Add(0);
            while (true)
            {
                var end = right ? width - 1 : 0;
                while (x != end) { x += right ? 1 : -1; path.Add(y * width + x); }
                if (y + spacing >= height) break;
                for (var i = 0; i < spacing; i++) { y++; path.Add(y * width + x); }
                right = !right;
            }
            var growth = new StringBuilder(path.Count - 1);
            for (var i = path.Count - 1; i > 0; i--)
            {
                var delta = path[i - 1] - path[i];
                growth.Append(path[i - 1] / width < path[i] / width ? 'U' : path[i - 1] / width > path[i] / width ? 'D' : delta > 0 ? 'R' : 'L');
            }
            var finish = path[path.Count - 1];
            return BackwardGenerator.Replay(new BackwardRecipe { Width = width, Height = height,
                FinishX = finish % width, FinishY = finish / width, Growth = growth.ToString() });
        }
    }
}
