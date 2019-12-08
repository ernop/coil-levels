using System;
using System.Collections.Generic;
using System.Text;
using static coil.Navigation;
using static coil.Util;
using static coil.Coilutil;

namespace coil
{
    public static class Debug
    {
        //this should really check everything about the board - regen hits and replay everything.
        public static void DoDebug(Level l, bool show=false, bool validateBoard = false)
        {
            if (validateBoard)
            {
                if (show)
                {
                    ShowSeg(l);
                    ShowHit(l);
                    Show(l);
                }

                //always validate segs.
                uint lastIndex = 0;

                foreach (var seg in l.Segs)
                {
                    if (seg.Len == 0)
                    {
                        WL("Bada");
                    }
                    if (show)
                    {
                        WL(seg.ToString());
                    }
                    if (lastIndex == 0)
                    {
                        lastIndex = seg.Index;
                        continue;
                    }
                    if (seg.Index < lastIndex)
                    {
                        //in preparation for well-spaced indexes, this should be > last rather than ==last+1
                        WL("Badb");
                    }
                    lastIndex = seg.Index;

                }

                Seg lastSeg = null;
                foreach (var seg in l.Segs)
                {
                    if (lastSeg != null)
                    {
                        if ((HDirs.Contains(lastSeg.Dir) && !VDirs.Contains(seg.Dir))
                            || (VDirs.Contains(lastSeg.Dir) && !HDirs.Contains(seg.Dir)))
                        {
                            WL("Badc");
                        }
                    }
                    lastSeg = seg;
                }
          
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

                Seg lastSeg2 = null;
                foreach (var seg in l.Segs)
                {
                    lastSeg2 = seg;
                    //trace path
                    var lstep = 0;
                    while (lstep < seg.Len)
                    {
                        fakeRows[current] = seg;
                        current = Add(current, seg.Dir);
                        lstep++;
                    }

                    if (fakeRows[current] != null)
                    {
                        WL("Badd");
                    }

                    //track hits.
                    var seghit = Add(seg.Start, seg.Dir, seg.Len + 1);
                    if (!fakeHits.ContainsKey(seghit))
                    {
                        fakeHits[seghit] = new List<Seg>();
                    }
                    fakeHits[seghit].Add(seg);
                }
                var end = Add(lastSeg2.Start, lastSeg2.Dir, lastSeg2.Len);
                fakeRows[end] = lastSeg2;

                //validate that every hit is in a null row!
                //this is not currently true.
                foreach (var seg in l.Segs)
                {
                    var candidate = seg.Start;
                    var ii = 1;
                    //you don't own your last square (unless at end, not accounted for)
                    while (ii < seg.Len)
                    {
                        candidate = Add(candidate, seg.Dir);
                        var rv = l.GetRowValue(candidate);
                        if (rv.Index == seg.Index)
                        {
                            ii++;
                            continue;
                        }
                        Show(l);
                        ShowSeg(l);
                        SaveWithPath(l, "../../../abc.png");
                        WL("Bad - mismapped square");
                        
                    }

                    var segend = seg.GetHit();
                    var hit = l.GetRowValue(segend);
                    if (hit == null)
                    {
                        continue;
                    }
                    if (hit.Index < seg.Index)
                    {
                        continue;
                    }
                    WL("Bade");
                    Show(l);
                    ShowSeg(l);
                    SaveWithPath(l, "../../../abc.png");
                    WL(l.LevelConfiguration.GetStr());
                }

                //check both ways!

                for (var xx = 0; xx < l.Width; xx++)
                {
                    for (var yy = 0; yy < l.Height; yy++)
                    {
                        var key = (xx, yy);

                        var realHitvalue = l.Hits.Get(key);
                        var fakeHitValue = new List<Seg>();
                        if (fakeHits.ContainsKey(key))
                        {
                            fakeHitValue = fakeHits[key];
                        }

                        //just check count for now.
                        if (realHitvalue.Count != fakeHitValue.Count)
                        {
                            WL("Badf");
                        }
                    }
                }

                foreach (var key in fakeHits.Keys)
                {
                    var realHitvalue = l.Hits.Get(key);
                    var fakeHitValue = fakeHits[key];
                    //just check count for now.
                    if (realHitvalue.Count != fakeHitValue.Count)
                    {
                        WL("Badg");
                        var ae = 3;
                    }
                }

                for (var yy = 0; yy < l.Height;yy++)
                {
                    for (var xx = 0; xx < l.Width; xx++)
                    {
                        var sq = (xx, yy);
                    
                        if (l.GetRowValue(sq)?.Index != fakeRows[sq]?.Index)
                        {
                            WL("Badh");
                            ShowSeg(l);
                            Show(l);
                            SaveWithPath(l, "../../../abc.png");
                        }
                        if (fakeRows[sq]?.Index != l.GetRowValue(sq)?.Index)
                        {
                            WL("Badi");
                        }
                    }
                }
                //validate fakehits and fakerows match rows!
            }
        }
    }
}
