using System;
using System.Collections.Generic;
using System.Linq;

using static coil.Navigation;
using static coil.Util;
using static coil.Coilutil;
using static coil.SegDescriptor;

namespace coil
{
    public class BaseLevel
    {
        public bool DoBoardValidation { get; set; }
        public LevelConfiguration LevelConfiguration { get; set; }
        public int Width { get; protected set; }

        public int Height { get; protected set; }

        public Random Rnd { get; protected set; }

        //null meaning not owned by any segment
        //pointing at a segment will include the xest segment covering it.
        //public Dictionary<(int, int), Seg> Rows { get; protected set; }
        public Seg[] Rows { get; set; }
        public Seg GetRowValue((int,int) pos)
        {
            var index = pos.Item2*Width+pos.Item1;
            return Rows[index];
        }
        public void SetRowValue((int,int) pos, Seg seg)
        {
            var index = pos.Item2 * Width + pos.Item1;
            Rows[index] = seg;
        }

        public HitManager Hits;

        //Path[1] is the first path.  So the first path will leave a trail of 1s in rows.
        public LinkedList<Seg> Segs { get; protected set; }

        //set up empty board with strong border bigger than the input
        protected void InitBoard()
        {

            Rows = new Seg[Height * Width];
            for (var yy = 0; yy < Height; yy++)
            {
                for (var xx = 0; xx < Width; xx++)
                {
                    //Rows[(xx, yy)] = null;
                    SetRowValue((xx, yy), null);
                }
            }
        }

        // only used by initial random walk.
        public int GetAvailableSegmentLengthInDirection((int, int) start, Dir dir, int min = 0, int? max = 0)
        {
            var res = 0;
            var candidate = Add(start, dir);
            while (GetRowValue(candidate) == null && Hits.GetCount(candidate) == 0 && InBounds(candidate) && (min == 0 || res < min) && (max == 0 || max == null || res < max))
            {
                res++;
                candidate = Add(candidate, dir);
            }

            return res;
        }

        //check dependencies looking some direction from start safely overriding spaces.
        //take up an equally distributed set of the space you can fill.
        //keep trying til failure
        public Seg MakeRandomSegFrom((int, int) start, List<Dir> dirs, int min = 0, int? max = 0)
        {
            //project over available directions and pick one, then create segment and return it.
            var validDirs = new List<Tuple<Dir, int>>();
            foreach (var dir in dirs)
            {
                var availableLength = GetAvailableSegmentLengthInDirection(start, dir, min, max);
                if (availableLength > 0)
                {
                    validDirs.Add(new Tuple<Dir, int>(dir, availableLength));
                }
            }

            if (!validDirs.Any())
            {
                return null;
            }

            //equally distributed
            var choice = validDirs[Rnd.Next(validDirs.Count)];
            int len = 0;
            if (LevelConfiguration.InitialWanderSetup.GoMax)
            {
                len = choice.Item2;
            }
            else
            {
                len = Rnd.Next(choice.Item2 - 1) + 1;
            }
            var seg = new Seg(start, choice.Item1, len);
            return seg;
        }

        //only used by initial random walk
        private void AddSeg(Seg seg)
        {
            seg.Index = (uint)Segs.Count + 1;

            var candidate = seg.Start;
            var ii = 0;

            //gotta go to the end
            while (ii <= seg.Len)
            {
                SetRowValue(candidate, seg);
                candidate = Add(candidate, seg.Dir);
                ii++;
            }

            Hits.Add(candidate, seg);

            // WL("Hits after adding seg.");
            // ShowSeg(this);
            // ShowHit(this);

            Segs.AddLast(seg);
        }

        /// <summary>
        /// construct a level for testing.
        /// </summary>
        public void SimpleBentPath((int, int) start, List<SegDescriptor> segDescriptors)
        {
            var cur = start;
            foreach (var sd in segDescriptors)
            {
                var seg = new Seg(cur, sd.Dir, sd.Len);
                AddSeg(seg);
                cur = Add(seg.Start, seg.Dir, seg.Len);
            }
        }

        public void InitialWander()
        {
            var start = (1,1);
            if (LevelConfiguration.InitialWanderSetup.StartPoint.HasValue)
            {
                start = LevelConfiguration.InitialWanderSetup.StartPoint.Value;
            }
            else
            {
                start = GetRandomPoint();
            }
            
            //hack;
            var nextDirs = AllDirs;
            int? max = null;
            if (LevelConfiguration.InitialWanderSetup.MaxLen.HasValue)
            {
                max = LevelConfiguration.InitialWanderSetup.MaxLen;
            }
            var segCount = 0;
            while (true)
            {
                var seg = MakeRandomSegFrom(start, nextDirs, max:max);
                if (seg == null)
                {
                    break;
                }

                AddSeg(seg);
                segCount++;
                start = GetEnd(seg);
                switch (seg.Dir)
                {
                    case Dir.Up:
                    case Dir.Down:
                        nextDirs = HDirs;
                        break;

                    case Dir.Right:
                    case Dir.Left:
                        nextDirs = VDirs;
                        break;

                    default:
                        throw new Exception("Bad");
                }
                if (LevelConfiguration.InitialWanderSetup.StepLimit.HasValue && LevelConfiguration.InitialWanderSetup.StepLimit.Value == segCount) {
                    break;
                }
            }
        }

        public (int, int) GetRandomPoint()
        {
            int x;
            int y;
            if (Width > 20 && Height > 20)
            {
                x = Rnd.Next(Width - 7) + 3;
                y = Rnd.Next(Height - 7) + 3;
            }
            else
            {
                x = Rnd.Next(Width - 2) + 1;
                y = Rnd.Next(Height - 2) + 1;
            }

            return (x, y);
        }

        public bool InBounds((int, int) candidate)
        {
            if (candidate.Item1 == 0 || candidate.Item1 == Width - 1 || candidate.Item2 == 0 || candidate.Item2 == Height - 1)
            {
                return false;
            }

            return true;
        }

        public void MaybeSaveDuringTweaking(bool saveTweaks, int saveEvery, Tweak tweak, int tweakct, int tweakfailct)
        {
            if (saveTweaks)
            {
                if (tweakct % saveEvery == 0)
                {
                    Console.WriteLine($"Applied tweak: {tweak} {tweakct}");
                    SaveWithPath(this, $"../../../tweaks/Tweak-{tweakct}.png");

                    //SaveEmpty(this, $"../../../tweaks/Tweak-{tweakct}-empty.png");
                }
            }

            if (tweakct % 100 == 0)
            {
                WL($"Tweakct: {tweakct,6} fails: {tweakfailct,6}");
            }
        }

        public int TotalLength()
        {
            var len = 1;
            foreach (var seg in Segs)
            {
                len += seg.Len;
            }
            return len;
        }

        //return the point one advanced from the given seg and point.
        public List<(int, int)> Iterate()
        {
            var res = new List<(int, int)>();
            var currentSeg = Segs.First;
            var st = 0;
            var current = Segs.First.Value.Start;
            res.Add(current);
            while (true)
            {
                if (currentSeg == null || currentSeg.Value == null)
                {
                    break;
                }
                st++;
                if (st <= currentSeg.Value.Len)
                {
                    current = Add(currentSeg.Value.Start, currentSeg.Value.Dir, st);
                    res.Add(current);
                }
                else
                {
                    st = 0;
                    currentSeg = currentSeg.Next;
                }
            }

            (int,int)? last = null;
            foreach (var el in res)
            {
                if (last != null)
                {
                    var d = GridDist(el, last.Value);
                    if (d != 1)
                    {
                        WL("A");
                    }
                }
                last = el;
            }

            return res;

        }
    }
}
