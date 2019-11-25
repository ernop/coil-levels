using System;
using System.Collections.Generic;
using System.Text;
using static coil.Navigation;
using static coil.Util;

namespace coil
{
    public static class Debug
    {
        //this should really check everything about the board - regen hits and replay everything.
        public static void DoDebug(Level l, bool validateBoard = false, bool show=false)
        {
            if (l.DoDebug)
            {
                if (show)
                {
                    ShowSeg(l);
                    ShowHit(l);
                    Show(l);
                }

                //always validate segs.
                var last = 0;

                foreach (var seg in l.Segs)
                {

                    if (seg.Len == 0)
                    {
                        var ae = 3;
                    }
                    if (show)
                    {
                        WL(seg.ToString());
                    }
                    if (seg.Index != last + 1)
                    {
                        var ae = 3;
                    }
                    last++;

                }

                Seg lastSeg = null;
                foreach (var seg in l.Segs)
                {
                    if (lastSeg != null)
                    {
                        if ((HDirs.Contains(lastSeg.Dir) && !VDirs.Contains(seg.Dir))
                            || (VDirs.Contains(lastSeg.Dir) && !HDirs.Contains(seg.Dir)))
                        {
                            var ae = 3;
                        }
                    }
                    lastSeg = seg;
                }
            }


            if (validateBoard)
            {
                //recalculate the entire board and segs.
                var fakeRows = new Dictionary<(int, int), Seg>();
                var fakeHits = new Dictionary<(int, int), List<Seg>>();
                for (var yy = 0; yy < l.Height; yy++)
                {
                    for (var xx = 0; xx < l.Width; xx++)
                    {
                        fakeHits[(xx, yy)] = new List<Seg>();
                        fakeRows[(xx, yy)] = null;
                    }
                }

                var current = l.Segs.First.Value.Start;
                fakeRows[current] = l.Segs.First.Value;

                Seg lastSeg = null;
                foreach (var seg in l.Segs)
                {
                    lastSeg = seg;
                    //trace path
                    var lstep = 0;
                    while (lstep < seg.Len)
                    {
                        fakeRows[current] = seg;
                        current = Add(current, seg.Dir);
                        lstep++;
                    }

                    //track hits.
                    var seghit = Add(seg.Start, seg.Dir, seg.Len + 1);
                    if (!fakeHits.ContainsKey(seghit))
                    {
                        fakeHits[seghit] = new List<Seg>();
                    }
                    fakeHits[seghit].Add(seg);
                }
                var end = Add(lastSeg.Start, lastSeg.Dir, lastSeg.Len);
                fakeRows[end] = lastSeg;

                //validate that every hit is in a null row!
                //this is not currently true.
                //foreach (var key in l.Hits.GetKeys())
                //{
                //    var realHits = l.Hits.Get(key);
                //    if (realHits.Count > 0)
                //    {
                //        if (l.Rows[key] != null)
                //        {
                //            var ae = 44;
                //        }
                //    }
                //}

                //check both ways!
                foreach (var key in l.Hits.GetKeys())
                {
                    var realHitvalue = l.Hits.Get(key);
                    var fakeHitValue = new List<Seg>();
                    if (fakeHits.ContainsKey(key))
                    {
                        fakeHitValue = fakeHits[key];
                    }

                    //just check count for now.
                    if (realHitvalue.Count != fakeHitValue.Count)
                    {
                        var ae = 3;
                    }
                }

                foreach (var key in fakeHits.Keys)
                {
                    var realHitvalue = l.Hits.Get(key);
                    var fakeHitValue = fakeHits[key];
                    //just check count for now.
                    if (realHitvalue.Count != fakeHitValue.Count)
                    {
                        var ae = 3;
                    }
                }

                foreach (var sq in l.Rows.Keys)
                {
                    if (l.Rows[sq]?.Index != fakeRows[sq]?.Index)
                    {
                        var ae = 3;
                    }
                    if (fakeRows[sq]?.Index != l.Rows[sq]?.Index)
                    {
                        var ae = 3;
                    }
                }
                //validate fakehits and fakerows match rows!
            }
        }
    }
}
