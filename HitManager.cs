using System;
using System.Collections.Generic;

namespace coil
{
    /// <summary>
    /// For every square, which segs "hit" it: a seg's hit square is the one past its end, the square that
    /// stops the slide. A square may be hit by several segs. The generator's hot loops only ever ask
    /// "is this square hit by a seg earlier than X?", so the minimum hitting index per square is kept in a
    /// the level's Cell array (0 = never hit) and the per-square lists are only touched when hits are added or removed.
    /// </summary>
    public class HitManager
    {
        private readonly List<Seg>[] Lists;
        private readonly BaseLevel.Cell[] Cells;
        private readonly int Width;
        private static readonly List<Seg> Empty = new List<Seg>();

        public HitManager(int width, int height, BaseLevel.Cell[] cells)
        {
            Width = width;
            Lists = new List<Seg>[width * height];
            Cells = cells;
        }

        private int Idx((int, int) pos) => pos.Item2 * Width + pos.Item1;

        public bool Contains((int, int) pos) => Cells[Idx(pos)].MinHit != 0;

        public List<Seg> Get((int, int) pos) => Lists[Idx(pos)] ?? Empty;

        public int GetCount((int, int) pos) => Lists[Idx(pos)]?.Count ?? 0;

        public void Add((int, int) pos, Seg seg)
        {
            var i = Idx(pos);
            var l = Lists[i];
            if (l == null)
            {
                l = new List<Seg>(2);
                Lists[i] = l;
            }
            else if (l.Contains(seg))
            {
                throw new InvalidOperationException($"Hits.Add: {seg} already hits {pos}");
            }
            l.Add(seg);
            ref var m = ref Cells[i].MinHit;
            if (m == 0 || seg.Index < m)
            {
                m = seg.Index;
            }
        }

        public void Remove((int, int) pos, Seg seg)
        {
            var i = Idx(pos);
            var l = Lists[i];
            if (l == null || !l.Remove(seg))
            {
                throw new InvalidOperationException($"Hits.Remove: {seg} does not hit {pos}");
            }
            Cells[i].MinHit = MinOf(l);
        }

        public bool CandidateIsHitByLessThan((int, int) pos, ulong index)
        {
            var m = Cells[Idx(pos)].MinHit;
            return m != 0 && m < index;
        }

        /// <summary>Call after seg indexes have been reassigned wholesale (RedoAllIndexesSpaceFilled).</summary>
        public void RecomputeMinIndexes()
        {
            for (var i = 0; i < Lists.Length; i++)
            {
                var l = Lists[i];
                if (l != null)
                {
                    Cells[i].MinHit = MinOf(l);
                }
            }
        }

        private static ulong MinOf(List<Seg> l)
        {
            ulong m = 0;
            foreach (var s in l)
            {
                if (m == 0 || s.Index < m)
                {
                    m = s.Index;
                }
            }
            return m;
        }
    }
}
