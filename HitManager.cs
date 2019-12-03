using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using static coil.Debug;
using static coil.Util;

namespace coil
{
    public class HitManager
    {
        //pointing to the xest segment hitting it? (low/highest)        
        //actually we need both. we need lowest for when we want to erase one - for example
        //seg2 wants to erase a point which is logged as being hit by 4 - would consider it fine.
        //but that would break 1.
        //and we need a backup hit beause imagine if 2 and 10 hit a sq.
        //and we replace 10 with a longtweak so it doesn't hit anymore. we still need to know 2 hit it!

        /// <summary>
        /// TODO: test converting this to an array of lists.  Currently represents 50% of time when generating big levels.
        /// </summary>
        //private Dictionary<(int, int), List<Seg>> Hits { get; set; }

        private List<Seg>[] Hits { get; set; }

        private bool Debug { get; set; }
        private Level Level { get; set; }

        public int GetHitIndex((int, int) pos)
        {
            return pos.Item2 * Level.Width + pos.Item1;
        }

        public HitManager(int width, int height, bool debug, Level level)
        {
            Debug = debug;
            Level = level;
            Hits = new List<Seg>[Level.Height*Level.Width];
            for (var yy = 0; yy < height; yy++)
            {
                for (var xx = 0; xx < width; xx++)
                {
                    Hits[GetHitIndex((xx, yy))] = new List<Seg>();
                }
            }
        }

        public bool Contains((int,int) key)
        {
            return Hits[GetHitIndex(key)].Any();
        }

        public List<Seg> Get((int,int) pos)
        {
            return Hits[GetHitIndex(pos)];
        }

        public void Remove((int,int) pos, Seg seg)
        {
            var l = Hits[GetHitIndex(pos)];
            if (Debug)
            {
                if (!l.Contains(seg))
                {
                    WL("Bad!");
                    var ae = 32;
                }
            }
            l.Remove(seg);
        }

        public void Add((int,int) pos, Seg seg)
        {
            var l = Hits[GetHitIndex(pos)];
            if (Debug)
            {
                var overlaps =l.Where(ss => ss.Index == seg.Index);
                //you can temporarily have segs with the same index
                foreach (var ol in overlaps)
                {
                    if (ol.Start == seg.Start)
                    {
                        DoDebug(Level, true);
                        WL("Bad!");
                        var ae = 43;
                    }
                }
            }

            l.Add(seg);
        }

        public int GetCount((int,int) pos)
        {
            return Hits[GetHitIndex(pos)].Count;
        }
    }
}
