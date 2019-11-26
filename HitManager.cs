using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using static coil.Debug;

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
        private Dictionary<(int, int), List<Seg>> Hits { get; set; }
        private bool Debug { get; set; }
        private Level Level { get; set; }

        public HitManager(int width, int height, bool debug, Level level)
        {
            Debug = debug;
            Level = level;
            Hits = new Dictionary<(int, int), List<Seg>>();
            for (var yy = 0; yy < height; yy++)
            {
                for (var xx = 0; xx < width; xx++)
                {
                    Hits[(xx, yy)] = new List<Seg>();
                }
            }
        }

        public bool Contains((int,int) key)
        {
            return Hits.ContainsKey(key);
        }

        public List<(int,int)> GetKeys()
        {
            return Hits.Keys.ToList();
        }

        public List<Seg> Get((int,int) pos)
        {
            return Hits[pos];
        }

        public void Remove((int,int) pos, Seg seg)
        {
            if (Debug)
            {
                if (!Hits[pos].Contains(seg))
                {
                    var ae = 32;
                }
            }
            Hits[pos].Remove(seg);
        }

        public void Add((int,int) pos, Seg seg)
        {
            if (Debug)
            {
                var overlaps = Hits[pos].Where(ss => ss.Index == seg.Index);
                //you can temporarily have segs with the same index
                foreach (var ol in overlaps)
                {
                    if (ol.Start == seg.Start)
                    {
                        DoDebug(Level, true);
                        var ae = 43;
                    }
                }
            }

            Hits[pos].Add(seg);
        }

        public int GetCount((int,int) pos)
        {
            return Hits[pos].Count;
        }
    }
}
