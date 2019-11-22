using System;
using System.Collections.Generic;
using System.Linq;

using static coil.Navigation;
using static coil.Util;

namespace coil
{
    //Levels are generated with an artifical solid boundary outside it, handled by putting minint in there.
    public class Level : BaseLevel
    {
        private static bool _DoDebug = false;

        //w/h are the "real" version
        public Level(int width, int height, Random rnd, bool test)
        {
            Rnd = rnd;
            Width = width + 2;
            Height = height + 2;
            Segs = new LinkedList<Seg>();
            InitBoard();
            MakeLevel(test);
        }

        public void Tweak(bool saveTweaks, int saveEvery)
        {
            var success = true;
            var tweakct = 0;
            var tweakfailct = 0;
            while (success)
            {
                success = false;
                foreach (var seg in Segs.OrderByDescending(el => el.Index))
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
                        MaybeSaveDuringTweaking(saveTweaks, saveEvery, tweak, tweakct, tweakfailct);

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

        public void TestTweaks()
        {
            GetTweaks(Segs.First.Next.Value, 1, false);
        }

        /// <summary>
        /// couple strategies here.
        /// It would be nice to quickly know all available tweaks and then just pick one based on level generation rules.
        /// Assume  base is a horizontal path staring at 0,0 and going to x,0.
        /// You will tweak up.
        /// We need to know the connectivity of every point xx,y, y>0, xx>0, with every other point xx2, y2, y2>y, xx2>xx, and als o the same going upwards.
        /// So for every len1 up, walk horizontally and figire it out.  as you do that you'll virtually also figure out the vertical.
        /// </summary>
        public List<Tweak> GetTweaks(Seg seg, int tweakct, bool right)
        {
            var res = new List<Tweak>();
            //from each square how much upper room is there starting from zero.
            //these can be used to determine len1; index is "start"
            var len1dir = right
                ? Rot(seg.Dir)
                : ARot(seg.Dir);

            var len2dir = right ? ARot(len1dir) : Rot(len1dir);
            var len3dir = right ? ARot(len2dir) : Rot(len2dir);

            var verticals = new Dictionary<int, int>();
            var returns = new List<int>();
            var maxVertical = 0;
            for (var st = 0; st <= seg.Len; st++)
            {
                var stpt = Add(seg.Start, seg.Dir, st, true);
                var vertical = GetSafeLength(stpt, len1dir, seg.Index);

                verticals[st] = vertical;
                maxVertical = Math.Max(maxVertical, vertical);

                //now we have to figure out how much of each up can be returned from.
                //from the point at index i of verticals how far up can you come DOWN from

                //ensure the next square to the right is hittable
                returns[0] = 0;
                if (st == 0) { continue; }
                var maxVert = Add(stpt, len1dir, vertical);
                returns[st] = GetMaxReturnFrom(maxVert, len3dir);

            }



            for (var st = 0;st<=seg.Len; st++)
            {
                var stpt = Add(seg.Start, seg.Dir, st, true);
            }


            //for every point in Vertical, we need to know how far over we can go.
            //hhhhhh
            //   h
            //    h
            //----->
            //0,1 => 3
            //0,2 ==> 2
            //actually short-circuiting is allowed due to overall structure.
            //if 0,n ==m, then 0,n+1 <=m

            //go upwards from each start.
            //

            Util.SaveEmpty(this, "../../../tweaks/tweak.png");

            //valid points which you can go down legally from
            var returns = new Dictionary<(int,int), bool>();

            for (var y = 1; y < maxVertical; y++)
            {
                //find the horizontal strips available s,e, such that verticals s..e are all >=y
                //AND all of which are returnable to.
                for (var st = 1; st <= seg.Len; st++)
                {
                    //figure out how far over we can go.
                    var start = Add(seg.Start, seg.Dir, st);
                    var len2start = Add(start, len1dir, y);
                    //we know len2start is valid
                    var xspan = GetOpenSquareSpanFrom(len2start, seg.Dir, maxOver);
                    maxOver = Math.Min(maxOver, xspan);
                }
            }

            return res;

        }

        public int GetOpenSquareSpanFrom((int, int) pt, Dir d, int maxOver)
        {
            return 0;
        }

        public Tweak PickTweak(List<Tweak> tweaks)
        {
            return tweaks.First();
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
                //BUT we do allow running over index-1 squares so you can do earlytweaks.
                if (Hits[candidate].Any(hc => hc != null && hc.Index < index - 1))
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
        //TODO generalize this and also allow pretweaks (start=0) and double tweaks (start=0, len2==seg.len)
        public void ApplyTweak(Seg seg, Tweak tweak)
        {
            var par = 0;
            var len1start = Add(seg.Start, seg.Dir, tweak.Start);
            var candidate = Add(len1start, seg.Dir);
            Hits[candidate].Add(seg);
            var debug = false;
            Debug();

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
            Debug();

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
            Debug();

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

            Debug();

            //what about the squares hit at the end of the original seg+1? in a longtweak it's not done anymore.
            while (len3filled < len3effectiveLength)
            {
                Rows[len3candidate] = seg3;
                len3candidate = Add(len3candidate, len3dir);
                len3filled++;
            }

            seg3.Len = len3effectiveLength;

            Debug();

            if (tweak.LongTweak)
            {
                Hits[len3candidate].Remove(oldNextSeg.Value);

                var originalEnd = Add(seg.Start, seg.Dir, seg.Len + 1);
                Hits[originalEnd].Remove(seg); //hmm this is very suspicious
            }

            Debug();

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
                Debug();
            }

            Debug();

            foreach (var s in Segs.Where(ss => ss.Index > seg.Index))
            {
                s.Index += 3 + seg4bump;
            }

            Debug();

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
            Debug();
        }

        public void Debug()
        {
            if (_DoDebug)
            {
                ShowSeg(this);
                ShowHit(this);
                Show(this);
            }
        }
    }
}
