using System;
using System.Collections.Generic;
using static coil.Navigation;
using static coil.Util;
using static coil.Coilutil;

namespace coil
{
    public static partial class Debug
    {
        /// <summary>
        /// Rebuild Rows and Hits from the Segs list alone and compare with the level's incremental bookkeeping.
        /// Any mismatch is a generator bug: throw, naming the check. (CoilFormat.Validate separately replays the
        /// game rules on the exported board; this checks the generator's internal invariants.)
        /// </summary>
        public static void DoDebug(Level l, bool show = false, bool validateBoard = false)
        {
            if (!validateBoard)
            {
                return;
            }
            if (show)
            {
                ShowSeg(l);
                ShowHit(l);
                Show(l);
            }

            ulong lastIndex = 0;
            Seg lastSeg = null;
            foreach (var seg in l.Segs)
            {
                if (seg.Len == 0)
                {
                    Fail(l, $"zero-length seg {seg}");
                }
                if (show)
                {
                    WL(seg.ToString());
                }
                if (lastSeg != null)
                {
                    if (seg.Index <= lastIndex)
                    {
                        Fail(l, $"seg indexes not increasing: {lastSeg} then {seg}");
                    }
                    var lastH = HDirs.Contains(lastSeg.Dir);
                    var thisH = HDirs.Contains(seg.Dir);
                    if (lastH == thisH)
                    {
                        Fail(l, $"consecutive segs not perpendicular: {lastSeg} then {seg}");
                    }
                    if (lastSeg.GetEnd() != seg.Start)
                    {
                        Fail(l, $"seg does not start where previous ended: {lastSeg} then {seg}");
                    }
                }
                lastIndex = seg.Index;
                lastSeg = seg;
            }

            //replay: which seg owns each square, and which squares each seg's end bumps into.
            var fakeRows = new Seg[l.Width * l.Height];
            var fakeHitCount = new int[l.Width * l.Height];
            var current = l.Segs.First.Value.Start;
            Seg last = null;
            foreach (var seg in l.Segs)
            {
                last = seg;
                for (var step = 0; step < seg.Len; step++)
                {
                    var idx = current.Item2 * l.Width + current.Item1;
                    if (fakeRows[idx] != null)
                    {
                        Fail(l, $"path revisits {current} in {seg}");
                    }
                    fakeRows[idx] = seg;
                    current = Add(current, seg.Dir);
                }
                if (fakeRows[current.Item2 * l.Width + current.Item1] != null)
                {
                    Fail(l, $"seg {seg} ends on an already-visited square {current}");
                }
                var hit = seg.GetHit();
                fakeHitCount[hit.Item2 * l.Width + hit.Item1]++;
            }
            fakeRows[current.Item2 * l.Width + current.Item1] = last;

            //each seg must be stopped by something: the square past its end is a wall or an earlier part of the path.
            foreach (var seg in l.Segs)
            {
                var blocker = l.GetRowValue(seg.GetHit());
                if (blocker != null && blocker.Index >= seg.Index)
                {
                    Fail(l, $"seg {seg} is not blocked: square past its end belongs to later {blocker}");
                }
            }

            for (var yy = 0; yy < l.Height; yy++)
            {
                for (var xx = 0; xx < l.Width; xx++)
                {
                    var sq = (xx, yy);
                    var idx = yy * l.Width + xx;
                    var real = l.GetRowValue(sq);
                    if ((real?.Index) != (fakeRows[idx]?.Index))
                    {
                        Fail(l, $"Rows mismatch at {sq}: level says {real?.ToString() ?? "empty"}, replay says {fakeRows[idx]?.ToString() ?? "empty"}");
                    }
                    if (l.Hits.GetCount(sq) != fakeHitCount[idx])
                    {
                        Fail(l, $"Hits mismatch at {sq}: level has {l.Hits.GetCount(sq)}, replay has {fakeHitCount[idx]}");
                    }
                    if (l.Cells[idx].Owner != (fakeRows[idx]?.Index ?? 0))
                    {
                        Fail(l, $"cached owner index mismatch at {sq}");
                    }
                    ulong minHit = 0;
                    foreach (var hit in l.Hits.Get(sq))
                    {
                        if (minHit == 0 || hit.Index < minHit)
                        {
                            minHit = hit.Index;
                        }
                    }
                    if (l.Cells[idx].MinHit != minHit)
                    {
                        Fail(l, $"cached minimum hit index mismatch at {sq}");
                    }
                }
            }
        }

        private static void Fail(Level l, string message)
        {
            var fn = Paths.In("abc.png");
            SaveWithPath(l, fn, quiet: true);
            throw new InvalidOperationException($"{message} [{l.LevelConfiguration.GetStr()}; board saved to {fn}]");
        }
    }
}
