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
        public LinkedList<Seg> Segs { get; private set; }

        //w/h are the "real" version
        public Level(int width, int height, Random rnd)
        {
            Rnd = rnd;
            Width = width + 2;
            Height = height + 2;
            Segs = new LinkedList<Seg>();
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

            Segs.AddLast(seg);
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
            //it's interesting to use the linkedlist feature of segs to drill super hard into recently added segments.
            //

            while (success)
            {
                success = false;
                foreach (var seg in Segs.OrderByDescending(el=>el.Index))
                {
                    if (seg.Index % 13 == 0)
                    {
                        continue;
                    }
                    if (seg.Len < 3)
                    {
                        continue;
                    }
                    var tweak = GetTweak(seg, tweakct);
                    if (tweak == null)
                    {
                        tweakfailct++;
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
                        if (tweakct % 100 == 0)
                        {
                            WL($"Tweakct: {tweakct,6} fails: {tweakfailct,6}");
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
            var len1Choices = Enumerable.Range(1, maxLen1)
                                        .OrderByDescending(el => el)
                                        .ToList();
            //len1Choices.Shuffle(Rnd);
            while (len1Choices.Any())
            {
                var len1 = PopFirst(len1Choices);
                var len1last = Add(tweakStartSq, len1dir, len1);

                //very annoying that this tries to go as far as possible
                //it may fail to find shorter valid tweak len2s
                var maxLen2 = GetSafeLength(len1last, seg.Dir, index: seg.Index, max: len2max);

                //this goes too far down; if there's a cluster, it should back off.
                if (maxLen2 == 0 || maxLen2 == 1)
                {
                    continue;
                }

                var len2Choices = Enumerable.Range(2, maxLen2 - 1)
                                            .OrderByDescending(el => el)
                                            .ToList();

                while (len2Choices.Any())
                {
                    var len2 = PopFirst(len2Choices);
                    var len2last = Add(len1last, seg.Dir, len2);

                    //figure out if it's a return fromtweak or from longtweak
                    // ShowSeg(this);

                    //we are inefficient because we repeatedly try to return. i.e. go out 10, left 10, then try to return.  go out 9, left 10, try to return. this is backwards.
                    //we should look out from seg3end and only ever go out that far when we're trying it! this whole thing is somewhat backwards.
                    //new structure:
                    //foreach start: determine len1s (which is also the len3)
                    //then pick a start (as start) and another one (as len3end) and check whether len2 can exist.
                    //much fewer combinations and if you have a bad section like this it will still work:
                    //>--------------->
                    //
                    //
                    //<----------<
                    //>----------^
                    //
                    //
                    //<---------------s-<
                    //i.e. trying to build a tweak from S as start in the bottom row. you'll go up far, left around the obvious block, and try to return a ton of times.  the farther
                    //the top block is away the more expensive it will be.
                    //the better way to do this would be to determine "maxlen1" from each start.
                    //it'd be 2 for most of the path so you'd only check that, and like 5 for the first few s. So you can just take your choice
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

            return res;
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

            LinkedListNode<Seg> segNode = Segs.Find(seg);
            LinkedListNode<Seg> oldNextSeg = segNode.Next;

            if (tweak.LongTweak)
            {
                len3effectiveLength += oldNextSeg.Value.Len;

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

            //what about the squares hit at the end of the original seg+1? in a longtweak it's not done anymore.
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
                Hits[len3candidate].Remove(oldNextSeg.Value);

                var originalEnd = Add(seg.Start, seg.Dir, seg.Len + 1);
                Hits[originalEnd].Remove(seg); //hmm this is very suspicious
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

            //have to fix the original segment.
            seg.Len = tweak.Start;

            //add in new segments
            if (tweak.LongTweak)
            {
                Segs.Remove(oldNextSeg);
            }
            else
            {
                Segs.AddAfter(segNode, seg4);
            }

            Segs.AddAfter(segNode, seg3);
            Segs.AddAfter(segNode, seg2);
            Segs.AddAfter(segNode, seg1);
            //debug = true;
            if (debug)
            {
                ShowSeg(this);
                ShowHit(this);
                Show(this);
            }
        }

        //private void ValidateSegIndexes()
        //{
        //    foreach (var seg in Segs)
        //    {
        //        if (seg.Index != Segs.IndexOf(seg) + 1)
        //        {
        //            //bad
        //            //var ae = 3;
        //        }
        //    }
        //}

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
