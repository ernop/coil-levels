using System;
using System.Collections.Generic;
using System.Linq;

using static coil.Navigation;
using static coil.Util;

namespace coil
{
    //Levels are generated with an artifical solid boundary outside it, handled by putting minint in there.
    public class Level
    {
        public int Width { get; }

        public int Height { get; }

        public Random Rnd { get; }

        //null meaning not owned by any segment
        //pointing at a segment will include the xest segment covering it.
        public Dictionary<(int, int), Seg> Rows { get; private set; }

        //pointing to the xest segment hitting it? (low/highest)        
        //actually we need both. we need lowest for when we want to erase one - for example
        //seg2 wants to erase a point which is logged as being hit by 4 - would consider it fine.
        //but that would break 1.
        //and we need a backup hit beause imagine if 2 and 10 hit a sq.
        //and we replace 10 with a longtweak so it doesn't hit anymore. we still need to know 2 hit it!
        public Dictionary<(int, int), List<Seg>> Hits { get; private set; }

        //Path[1] is the first path.  So the first path will leave a trail of 1s in rows.
        public List<Seg> Segs { get; private set; }

        //w/h are the "real" version
        public Level(int width, int height, Random rnd)
        {
            Rnd = rnd;
            Width = width + 2;
            Height = height + 2;
            Segs = new List<Seg>();
            InitBoard();
            MakeLevel();
        }

        //set up empty board with strong border bigger than the input
        private void InitBoard()
        {
            Rows = new Dictionary<(int, int), Seg>();
            Hits = new Dictionary<(int, int), List<Seg>>();
            for (var yy = 0; yy < Height; yy++)
            {
                for (var xx = 0; xx < Width; xx++)
                {
                    Hits[(xx, yy)] = new List<Seg>();
                    Rows[(xx, yy)] = null;
                }
            }
        }

        public bool InBounds((int, int) candidate)
        {
            if (candidate.Item1 == 0 || candidate.Item1 == Width - 1 || candidate.Item2 == 0 || candidate.Item2 == Height - 1)
            {
                return false;
            }

            return true;
        }

        // only used by initial random walk.
        public int GetAvailableSegmentLengthInDirection((int, int) start, Dir dir)
        {
            var res = 0;
            var candidate = Add(start, dir);
            while (Rows[candidate] == null && Hits[candidate].Count == 0 && InBounds(candidate))
            {
                res++;
                candidate = Add(candidate, dir);
            }

            return res;
        }

        //check dependencies looking some direction from start safely overriding spaces.
        //take up an equally distributed set of the space you can fill.
        //keep trying til failure
        public Seg MakeRandomSegFrom((int, int) start, List<Dir> dirs)
        {
            //project over available directions and pick one, then create segment and return it.
            var validDirs = new List<Tuple<Dir, int>>();
            foreach (var dir in dirs)
            {
                var availableLength = GetAvailableSegmentLengthInDirection(start, dir);
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
            var len = Rnd.Next(choice.Item2 - 1) + 1;
            var seg = new Seg(start, choice.Item1, len);
            return seg;
        }

        //only used by initial random walk
        private void AddSeg(Seg seg)
        {
            seg.Index = Segs.Count + 1;

            var candidate = seg.Start;
            var ii = 0;

            //gotta go to the end
            while (ii <= seg.Len)
            {
                Rows[candidate] = seg;
                candidate = Add(candidate, seg.Dir);
                ii++;
            }

            Hits[candidate].Add(seg);

            // WL("Hits after adding seg.");
            // ShowSeg(this);
            // ShowHit(this);
            Segs.Add(seg);
        }

        private void InitialWander()
        {
            var start = GetRandomPoint();

            //hack;
            var nextDirs = AllDirs;

            while (true)
            {
                var seg = MakeRandomSegFrom(start, nextDirs);
                if (seg == null)
                {
                    break;
                }

                AddSeg(seg);
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
            }
        }

        private void MakeLevel()
        {
            InitialWander();
        }

        public void Tweak(bool saveTweaks, int saveEvery)
        {
            //locate a enough segment.
            var success = true;
            var tweakct = 0;
            var tweakfailct = 0;
            var segFails = new Dictionary<Seg, int>();

            // WL(tweakct.ToString());

            while (success)
            {
                success = false;

                // ShowSeg(this);
                foreach (var seg in Segs)
                {
                    if (!segFails.ContainsKey(seg))
                    {
                        segFails[seg] = 0;
                    }
                }

                var segChoices = Segs.Where(ss => ss.Len >= 3)
                                     .OrderByDescending(ss => Math.Sqrt(ss.Index) + ss.Len + Rnd.Next(3))
                                     .ToList();

                while (segChoices.Any())
                {
                    var seg = Util.PopFirst(segChoices);
                    var tweak = GetTweak(seg, tweakct);
                    if (tweak == null)
                    {
                        tweakfailct++;
                        segFails[seg]++;
                    }
                    else
                    {
                        ApplyTweak(seg, tweak);
                        tweakct++;
                        if (saveTweaks)
                        {
                            if (tweakct % saveEvery == 0)
                            {
                                Console.WriteLine($"Applied tweak: {tweak} {tweakct}");
                                SaveWithPath(this, $"../../../tweaks/Tweak-{tweakct}.png");
                                //SaveEmpty(this, $"../../../tweaks/Tweak-{tweakct}-empty.png");
                            }
                        }

                        success = true;
                        break;
                    }
                }
            }
        }

        public Tweak GetTweak(Seg seg, int tweakct)
        {
            var n = Rnd.Next(2);
            List<bool> rl;
            if (n == 0)
            {
                rl = Bools;
            }
            else
            {
                rl = Bools2;
            }

            var starts = Enumerable.Range(1, seg.Len - 2)
                                   .ToList()
                                   .OrderBy(el => el)
                                   .ToList();

            //starts.Shuffle(Rnd);
            while (starts.Any())
            {
                var start = PopFirst(starts);
                foreach (var right in rl)
                {
                    var tw = InnerGetTweak(seg, right, start);
                    if (tw != null)
                    {
                        return tw;
                    }
                }
            }

            return null;
        }

        

        public Tweak InnerGetTweak(Seg seg, bool right, int start)
        {
            //validate initial turn.
            var tweakStartSq = Add(seg.Start, seg.Dir, start);
            var len1dir = right
                ? Rot(seg.Dir)
                : ARot(seg.Dir);

            //dig til you hit the border OR 
            var maxLen1 = GetSafeLength(tweakStartSq, len1dir, seg.Index);
            if (maxLen1 == 0)
            {
                return null;
            }

            var approvedLen1 = 0;
            var approvedLen2 = 0;
            var found = false;
            var len2max = seg.Len - start;
            var len3dir = Rot(Rot(len1dir));
            var longTweak = false;

            //this can get super cubic!
            var len1Choices = Enumerable.Range(1, maxLen1).OrderByDescending(el => el).ToList();
            //len1Choices.Shuffle(Rnd);
            while (len1Choices.Any())
            {
                var len1 = PopFirst(len1Choices);
                var len1last = Add(tweakStartSq, len1dir, len1);

                // if (seg.Index==12 && right){
                //     var a = 23;
                // }
                //very annoying that this tries to go as far as possible
                //it may fail to find shorter valid tweak len2s
                var maxLen2 = GetSafeLength(len1last, seg.Dir, index: seg.Index, max: len2max);

                //this goes too far down; if there's a cluster, it should back off.
                if (maxLen2 == 0 || maxLen2 == 1)
                {
                    continue;
                }

                var len2Choices = Enumerable.Range(2, maxLen2 - 1)
                                            .ToList()
                                            .OrderByDescending(el => el)
                                            .ToList();

                while (len2Choices.Any())
                {
                    var len2 = PopFirst(len2Choices);
                    var len2last = Add(len1last, seg.Dir, len2);

                    //figure out if it's a return fromtweak or from longtweak
                    // ShowSeg(this);
                    bool okay = false;
                    if (start + len2 == seg.Len)
                    {
                        okay = ReturnFromLongTweak(len2last, len3dir, seg.Index, len1);
                        longTweak = true;
                    }
                    else
                    {
                        okay = ReturnFromTweak(len2last, len3dir, seg.Index, len1);
                        longTweak = false;
                    }

                    if (!okay)
                    {
                        continue;
                    }

                    //got one
                    approvedLen1 = len1;
                    approvedLen2 = len2;
                    found = true;
                    break;
                }

                if (found)
                {
                    break;
                }
            }

            if (!found)
            {
                return null;
            }

            var tweak = new Tweak(right, start, approvedLen1, approvedLen2, len1dir, longTweak);
            return tweak;
        }

        //including not hitting a later empty sq
        //todo seems something of a duplicate of the random walk
        //but this case can optionally override squares that later paths hit - they are okay
        //leaving as hit, or being part of an earlier path
        //capped at 60
        public int GetSafeLength((int, int) start, Dir dir, int index, int? max = null)
        {
            var candidate = Add(start, dir);
            var res = 1;

            //TODO rewrite this loop.
            //we may be returning to ourself.

            while (true)
            {
                if (!InBounds(candidate))
                {
                    res--;
                    break;
                }

                if (Rows[candidate] != null)
                {
                    var hitSegment = Rows[candidate];

                    //hit some other path; pull back.
                    res--;
                    if (hitSegment.Index > index)
                    {
                        //not only can we not overlap it, we can't even go up to it.
                        res--;
                    }

                    break;
                }

                //not running over earlier locked squares
                if (Hits[candidate].Any(hc => hc != null && hc.Index < index))
                {
                    res--;
                    break;
                }

                //some trickiness here with the indexes
                if (max.HasValue && res > max)
                {
                    res = max.Value;
                    break;
                }

                res++;
                candidate = Add(candidate, dir);
            }

            if (res < 0)
            {
                return 0;
            }

            return Math.Min(16, res);
        }

        //just make sure the space is clear, and that the overlap hit is okay.
        public bool ReturnFromTweak((int, int) start, Dir dir, int index, int len)
        {
            var candidate = Add(start, dir);
            var ii = 1;
            while (ii < len)
            {
                if (Rows[candidate] != null)
                {
                    return false;
                }

                candidate = Add(candidate, dir);
                ii++;
            }

            if (Rows[candidate] == null)
            {
                return false;
            }

            var hitSegment = Rows[candidate];
            if (hitSegment.Index != index)
            {
                //how did you hit someone else?
                return false;
            }

            var nextHit = Add(candidate, dir);
            if (Rows[nextHit] != null && Rows[nextHit].Index > index)
            {
                return false;
            }

            return true;
        }

        //return, and expect the next square to be the next segment
        public bool ReturnFromLongTweak((int, int) start, Dir dir, int index, int len)
        {
            var candidate = Add(start, dir);
            var ii = 1;
            while (ii < len)
            {
                if (Rows[candidate] != null)
                {
                    return false;
                }

                candidate = Add(candidate, dir);
                ii++;
            }

            if (Rows[candidate] == null)
            {
                return false;
            }

            var hitSegment = Rows[candidate];
            if (hitSegment.Index != index + 1)
            {
                //you sohuld have hit someone else
                return false;
            }

            var nextHit = Add(candidate, dir);
            if (Rows[nextHit] != null && Rows[nextHit].Index != index + 1)
            {
                return false;
            }

            return true;
        }

        //only tweaks with nonzero start and nonzero 4th segment.
        public void ApplyTweak(Seg seg, Tweak tweak)
        {
            var par = 0;
            var len1start = Add(seg.Start, seg.Dir, tweak.Start);
            var candidate = Add(len1start, seg.Dir);
            Hits[candidate].Add(seg);
            var debug = false;
            if (debug)
            {
                ShowSeg(this);
                ShowHit(this);
                Show(this);
            }

            //TODO: target square of seg1 is getting set in Rows as screwed up.
            //validate this and should be good to go with long tweaks!

            //fill in squares
            while (par < tweak.Len2)
            {
                Rows[candidate] = null;
                candidate = Add(candidate, seg.Dir);
                par++;
            }

            //filled in the body. now go up.
            var seg1 = new Seg(len1start, tweak.Len1Dir, tweak.Len1);
            seg1.Index = seg.Index + 1;
            Rows[len1start] = seg1;
            var len1candidate = Add(len1start, tweak.Len1Dir);
            var len1filled = 0;
            while (len1filled < tweak.Len1)
            {
                Rows[len1candidate] = seg1;
                len1filled++;
                len1candidate = Add(len1candidate, tweak.Len1Dir);
            }

            Hits[len1candidate].Add(seg1);
            if (debug)
            {
                ShowSeg(this);
                ShowHit(this);
                Show(this);
            }

            var len2start = Add(len1start, tweak.Len1Dir, tweak.Len1);
            var seg2 = new Seg(len2start, seg.Dir, tweak.Len2);
            Rows[len2start] = seg2;
            seg2.Index = seg.Index + 2;
            var len2filled = 0;
            var len2candidate = Add(len2start, seg.Dir);
            while (len2filled < tweak.Len2)
            {
                Rows[len2candidate] = seg2;
                len2candidate = Add(len2candidate, seg.Dir);
                len2filled++;
            }

            Hits[len2candidate].Add(seg2);
            if (debug)
            {
                ShowSeg(this);
                ShowHit(this);
                Show(this);
            }

            var len3start = Add(len2start, seg.Dir, tweak.Len2);
            var len3dir = Rot(Rot(tweak.Len1Dir));
            var seg3 = new Seg(len3start, len3dir, tweak.Len1);
            Rows[len3start] = seg3;
            seg3.Index = seg.Index + 3;

            var len3filled = 0;
            var len3candidate = Add(len3start, len3dir);

            //perhaps we should just keep going here til we hit a null/earlier filled sq?
            //that way it's easier
            var len3effectiveLength = tweak.Len1;
            Seg oldNextSeg = null;

            if (tweak.LongTweak)
            {
                oldNextSeg = Segs[seg.Index];
                len3effectiveLength += oldNextSeg.Len;

                //also take over hits that that segment might have had.
                //do we actually use this? yes.
                //TODO we should check for segs that are still alive, but aren't in the board.
            }

            if (debug)
            {
                ShowSeg(this);
                ShowHit(this);
                Show(this);
            }

            //what about the square's hit at the end of the original seg+1? in a longtweak it's not done anymore.
            while (len3filled < len3effectiveLength)
            {
                Rows[len3candidate] = seg3;
                len3candidate = Add(len3candidate, len3dir);
                len3filled++;
            }

            seg3.Len = len3effectiveLength;

            if (debug)
            {
                ShowSeg(this);
                ShowHit(this);
                Show(this);
            }

            if (tweak.LongTweak)
            {
                Hits[len3candidate].Remove(oldNextSeg);

                var originalEnd = Add(seg.Start, seg.Dir, seg.Len + 1);
                Hits[originalEnd].Remove(seg); //hmm this is very suspicious

                //possibly having a simple hit map is not sufficient and i need to know everybody who hits something.
                //or at least the lowest/highest seg index which does so.
            }

            if (debug)
            {
                ShowSeg(this);
                ShowHit(this);
                Show(this);
            }

            var len3hit = Add(len3candidate, len3dir);
            Hits[len3candidate].Add(seg3);

            //TODO have to make sure seg3's hit is valid.

            var len4len = seg.Len - tweak.Start - tweak.Len2;

            Seg seg4 = null;
            var seg4bump = 0;

            if (tweak.LongTweak)
            {
                seg4bump = -1;

                //we have taken over the squares.
            }
            else
            {
                var len4start = Add(len3start, len3dir, tweak.Len1);
                seg4bump = 1;
                seg4 = new Seg(len4start, seg.Dir, len4len);
                Rows[len4start] = seg4;
                var len4filled = 0;
                var len4candidate = Add(len4start, seg.Dir);

                //last step of seg4 do NOT write it since it'll overwrite.
                while (len4filled < len4len - 1)
                {
                    Rows[len4candidate] = seg4;
                    len4candidate = Add(len4candidate, seg.Dir);
                    len4filled++;
                }

                seg4.Index = seg.Index + 4;

                //we didn't fill in the last len4 sq, but it is there and needs to hit the *next* sq.

                var virtualSeg4EndSq = Add(len4candidate, seg.Dir);
                Hits[virtualSeg4EndSq].Add(seg4);
                Hits[virtualSeg4EndSq].Remove(seg);
                if (debug)
                {
                    ShowSeg(this);
                    ShowHit(this);
                    Show(this);
                }
            }

            if (debug)
            {
                ShowSeg(this);
                ShowHit(this);
                Show(this);
            }

            //adjust indexes
            if (tweak.LongTweak)
            {
                var ae = 34;
            }

            foreach (var s in Segs.Where(ss => ss.Index > seg.Index))
            {
                s.Index += 3 + seg4bump;
            }

            if (debug)
            {
                ShowSeg(this);
                ShowHit(this);
                Show(this);
            }

            //what about all old hits and rows? those are fucked up too.
            //restructure data.  rather than inlining values, inline pointers to segments
            //that wya when segments get updated things are okay.

            //have to fix the original segment.
            seg.Len = tweak.Start;

            //add in new segments

            if (tweak.LongTweak)
            {
                Segs.RemoveAt(Segs.IndexOf(oldNextSeg));
            }
            else
            {
                Segs.Insert(seg.Index, seg4);
            }

            Segs.Insert(seg.Index, seg3);
            Segs.Insert(seg.Index, seg2);
            Segs.Insert(seg.Index, seg1);

            // foreach (var segs in Segs){
            //     WL(segs.ToString());
            // }
            if (debug)
            {
                ShowSeg(this);
                ShowHit(this);
                Show(this);
            }
        }

        public (int, int) GetRandomPoint()
        {
            int x = 0;
            int y = 0;
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
    }
}
