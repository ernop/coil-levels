using System;
using System.Collections.Generic;
using System.Linq;

using static coil.Navigation;
using static coil.Util;
using static coil.Debug;

namespace coil
{
    //Levels are generated with an artifical solid boundary outside it, handled by putting minint in there.
    public partial class Level : BaseLevel
    {

        //w/h are the "real" version
        public Level(int width, int height, Random rnd, bool test, bool debug)
        {
            Rnd = rnd;
            Width = width + 2;
            Height = height + 2;
            Segs = new LinkedList<Seg>();
            DoDebug = debug;
            Hits = new HitManager(Width, Height, DoDebug, this);
            InitBoard();
            MakeLevel(test);
        }

        public void Tweak2(bool saveState, int saveEvery)
        {
            var tweakct = 0;
            //randomize this

            var success = true;
            while (success) {
                success = false;
                //control this
                LinkedListNode<Seg> segnode = Segs.First;
                //WL("Another round.");
                while (segnode != null)
                {
                    //DoDebug(this, true);
                    //WL(segnode.Value.ToString());
                    var rtweaks = GetTweaks(segnode.Value, tweakct, true);
                    var ltweaks = GetTweaks(segnode.Value, tweakct, false);
                    var tweaks = new List<Tweak>();
                    tweaks.AddRange(rtweaks);
                    tweaks.AddRange(ltweaks);
                    //use this to control picking - maybe incorporate right/left into it.
                    var tweak = PickTweak(tweaks);
                    
                    if (tweak == null)
                    {
                        //WL("No tweaks were available for seg.");
                        segnode = segnode.Next;
                        continue;
                    }
                    //WL($"Got: {tweaks.Count} picked {tweak} for {segnode.Value}");
                    //DoDebug(this, true, true);
                    ApplyTweak(segnode, tweak);
                    if (tweakct % 100 == 0)
                    {
                        WL($"tweakct: {tweakct} {Report(this)}");
                    }
                    if (saveState && tweakct % saveEvery == 0)
                    {
                        var fn = $"../../../output/{Width-2}x{Height-2}/Segs{Segs.Count}-empty.png";
                        var pathfn = $"../../../output/{Width - 2}x{Height - 2}/Segs{Segs.Count}-path.png";
                        //SaveEmpty(this, fn);
                        SaveWithPath(this, pathfn);
                    }
                    DoDebug(this, true, false);
                    success = true;
                    tweakct++;
                    break;
                }
            }
        }

        private void ApplyTweak(LinkedListNode<Seg> segnode, Tweak tweak)
        {
            var seg = segnode.Value;
            var prevnode = segnode.Previous;
            var nextnode = segnode.Next;
            if (tweak.ShortTweak && tweak.LongTweak)
            {
                //DoDebug(this, true);
                //remove hit, fill in squares
                UnapplySeg(seg);
                //DoDebug(this, false);
                //remove hit, lengthen and dig, add hit
                Lengthen(prevnode.Value, tweak);
                //DoDebug(this, false);
                //move start back, increase length, dig squares
                Unlengthen(nextnode.Value, tweak);
                //DoDebug(this, false);
                var seg3 = MakeSeg(tweak, TweakSection.Three, seg.Index);
                //add the hit, dig out board.
                ApplySeg(seg3);
                //DoDebug(this, false);
                Segs.Remove(segnode);
                Segs.AddAfter(prevnode, seg3);
                //no index adjustments
                //DoDebug(this, true);
            }
            else if (tweak.ShortTweak)
            {
                UnapplySeg(seg);
                //DoDebug(this, false);
                Lengthen(prevnode.Value, tweak);
                //DoDebug(this, false);
                var seg3 = MakeSeg(tweak, TweakSection.Three, seg.Index);
                var seg4 = MakeSeg(tweak, TweakSection.Four, seg.Index + 1);
                var seg5 = MakeSeg(tweak, TweakSection.Five, seg.Index + 2);
                ApplySeg(seg3);
                //DoDebug(this, false);
                ApplySeg(seg4);
                //DoDebug(this, false);
                ApplySeg(seg5, endEarly: true);
                //DoDebug(this, false);
                var seg5end = Add(seg5.Start, seg5.Dir, seg5.Len);
                if (nextnode != null)
                {
                    Rows[seg5end] = nextnode.Value;
                }
                else
                {
                    Rows[seg5end] = seg5;
                }
                Segs.Remove(segnode);
                var seg3node = Segs.AddAfter(prevnode, seg3);
                var seg4node = Segs.AddAfter(seg3node, seg4);
                var seg5node = Segs.AddAfter(seg4node, seg5);
                AdjustIndexAfter(seg5node, 2);
                //DoDebug(this, true);
            }
            else if (tweak.LongTweak)
            {
                UnapplySeg(seg);
                Unlengthen(nextnode.Value, tweak);
                var seg1 = MakeSeg(tweak, TweakSection.One, seg.Index);
                var seg2 = MakeSeg(tweak, TweakSection.Two, seg.Index + 1);
                var seg3 = MakeSeg(tweak, TweakSection.Three, seg.Index + 2);
                ApplySeg(seg1);
                ApplySeg(seg2);
                ApplySeg(seg3);
                Segs.Remove(segnode);
                var seg1node = Segs.AddAfter(prevnode, seg1);
                var seg2node = Segs.AddAfter(seg1node, seg2);
                var seg3node = Segs.AddAfter(seg2node, seg3);
                AdjustIndexAfter(seg3node, 2);
                //DoDebug(this, true);
            }
            else
            {
                //DoDebug(this, false);
                UnapplySeg(seg);
                //DoDebug(this, false);
                var seg1 = MakeSeg(tweak, TweakSection.One, seg.Index);
                var seg2 = MakeSeg(tweak, TweakSection.Two, seg.Index + 1);
                var seg3 = MakeSeg(tweak, TweakSection.Three, seg.Index + 2);
                var seg4 = MakeSeg(tweak, TweakSection.Four, seg.Index + 3);
                var seg5 = MakeSeg(tweak, TweakSection.Five, seg.Index + 4);
                //DoDebug(this, false);
                ApplySeg(seg1);
                //DoDebug(this, false);
                ApplySeg(seg2);
                //DoDebug(this, false);
                ApplySeg(seg3);
                //DoDebug(this, false);
                ApplySeg(seg4);
                //DoDebug(this, false);
                ApplySeg(seg5);
                //we need to fix up the ownership of the last square of seg5.
                var seg5end = Add(seg5.Start, seg5.Dir, seg5.Len);
                if (nextnode != null)
                {
                    Rows[seg5end] = nextnode.Value;
                }
                else
                {
                    Rows[seg5end] = seg5;
                }
                //DoDebug(this, false);
                
                var seg1node = Segs.AddAfter(segnode, seg1);
                var seg2node = Segs.AddAfter(seg1node, seg2);
                var seg3node = Segs.AddAfter(seg2node, seg3);
                var seg4node = Segs.AddAfter(seg3node, seg4);
                var seg5node = Segs.AddAfter(seg4node, seg5);
                Segs.Remove(segnode);
                AdjustIndexAfter(seg5node, 4);
                //DoDebug(this, true);
            }
        }

        public void Unlengthen(Seg seg, Tweak tweak)
        {
            //back up the seg.
            //adjust length
            //adjust start

            var candidate = seg.Start;
            var len = 0;
            var backingDirection = Rot(Rot(seg.Dir));
            while (len <= tweak.Len2)
            {
                len++;
                Rows[candidate] = seg;
                candidate = Add(candidate, backingDirection);
            }
            seg.Len += tweak.Len2;
            var newStart = Add(seg.Start, backingDirection, tweak.Len2);
            //adjust start.
            seg.Start = newStart;
        }

        public void Lengthen(Seg seg, Tweak tweak)
        {
            //we lengthen by tweak.Len1.
            //remove old hit
            //add new hit.

            var oldHit = Add(seg.Start, seg.Dir, seg.Len + 1);
            Hits.Remove(oldHit, seg);

            var segEnd = Add(seg.Start, seg.Dir, seg.Len);
            var len = 0;
            while (len <= tweak.Len2)
            {
                Rows[segEnd] = seg;
                segEnd = Add(segEnd, seg.Dir);
                len++;
            }
            seg.Len += tweak.Len2;
            var hit = Add(seg.Start, seg.Dir, seg.Len+1);
            Hits.Add(hit, seg);
        }

        //completely remove both start and end.
        //this causes complications for others!
        public void UnapplySeg(Seg seg)
        {
            //fill in the squares
            //remove the hit
            var len = 0;
            var candidate = seg.Start;
            while (len <= seg.Len)
            {
                Rows[candidate] = null;
                candidate = Add(candidate, seg.Dir);
                len++;
            }
            var hit = Add(seg.Start, seg.Dir, seg.Len+1);
            Hits.Remove(hit, seg);
        }

        //you own the first square of a seg but not the last (in Rows)
        public void ApplySeg(Seg seg, bool endEarly = false)
        {
            //dig out squares of rows.
            //add hit at the end.
            var len = 0;
            var target = seg.Len;
            if (endEarly)
            {
                //target--;
            }
            var candidate = seg.Start;
            while (len < target)
            {
                //we never fill the origin square of the seg
                Rows[candidate] = seg;
                len++;
                candidate = Add(candidate, seg.Dir);
            }
            var hit = Add(seg.Start, seg.Dir, seg.Len+1);
            Hits.Add(hit, seg);
        }

        /// <summary>
        /// Somewhat convoluted way to create segs.
        /// </summary>
        public static Seg MakeSeg(Tweak tweak, TweakSection section, int index)
        {
            switch (section)
            {
                case TweakSection.One:
                    var seg = new Seg(tweak.Seg.Start, tweak.Seg.Dir, tweak.Len1);
                    seg.Index = index;
                    return seg;
                case TweakSection.Two:
                    var seg2start = Add(tweak.Seg.Start, tweak.Seg.Dir, tweak.Len1);
                    var seg2 = new Seg(seg2start, tweak.Len2dir, tweak.Len2);
                    seg2.Index = index;
                    return seg2;
                case TweakSection.Three:
                    var seg2start2 = Add(tweak.Seg.Start, tweak.Seg.Dir, tweak.Len1, true);
                    var seg3start = Add(seg2start2, tweak.Len2dir, tweak.Len2);
                    var seg3 = new Seg(seg3start, tweak.Seg.Dir, tweak.Len3);
                    seg3.Index = index;
                    return seg3;
                case TweakSection.Four:
                    var seg4base = Add(tweak.Seg.Start, tweak.Seg.Dir, tweak.Len1 + tweak.Len3);
                    var seg4start = Add(seg4base, tweak.Len2dir, tweak.Len2);
                    var seg4 = new Seg(seg4start, Rot(Rot(tweak.Len2dir)), tweak.Len2);
                    seg4.Index = index;
                    return seg4;
                case TweakSection.Five:
                    var seg5start = Add(tweak.Seg.Start, tweak.Seg.Dir, tweak.Len1 + tweak.Len3);
                    var seg5len = tweak.Seg.Len - tweak.Len1 - tweak.Len3;
                    var seg5 = new Seg(seg5start, tweak.Seg.Dir, seg5len);
                    seg5.Index = index;
                    return seg5;
                default:
                    throw new Exception("Invalid tweakSection.");
            }
        }

        public void AdjustIndexAfter(LinkedListNode<Seg> seg, int amount)
        {
            var el = seg.Next;
            while (el != null)
            {
                el.Value.Index += amount;
                el = el.Next;
            }
        }

        public void TestTweaks()
        {
            var tweaks1 = GetTweaks(Segs.First.Next.Value, 0, false);
            //should be 7.

            var tweaks2 = GetTweaks(Segs.First.Next.Next.Value, 0, true);

            var tweaks3 = GetTweaks(Segs.First.Next.Next.Next.Value, 0, true);
        }
               
        /// <summary>
        /// couple strategies here.
        /// It would be nice to quickly know all available tweaks and then just pick one based on level generation rules.
        /// Assume  base is a horizontal path staring at 0,0 and going to x,0.
        /// You will tweak up.
        /// We need to know the connectivity of every point xx,y, y>0, xx>0, with every other point xx2, y2, y2>y, xx2>xx, and als o the same going upwards.
        /// So for every len1 up, walk horizontally and figire it out.  as you do that you'll virtually also figure out the vertical.
        /// //TODO bug: this will miss SL tweaks which just extend outwards.
        /// </summary>
        public List<Tweak> GetTweaks(Seg seg, int tweakct, bool right)
        {
            //TODO if seg.Index==1 there is no prior tweak; hard to do an early tweak there but maybe possible
            var res = new List<Tweak>();
            //DoDebug(this);

            var len1dir = right
                ? Rot(seg.Dir)
                : ARot(seg.Dir);

            var len2dir = seg.Dir;
            var len3dir = right ? ARot(len2dir) : Rot(len2dir);

            //for start pt index s, how far can you go up?
            var verticals = new Dictionary<int, int>();

            //how far away can you return to this square from.
            //zero means nothing and can be due to multiple causes.
            var returnableDistance = new Dictionary<int, int>();

            //first figure out how far up you can go from each square, and how far down you can return to each square
            //each with their accompanying overlap into the next square being validated.
            for (var st = 0; st <= seg.Len; st++)
            {
                var stpt = Add(seg.Start, seg.Dir, st, true);

                var vertical = GetSafeLength(stpt, len1dir, seg.Index);

                verticals[st] = vertical;

                //now figure out if this st allows returning (len3hit is legit)
                var hitsq = Add(stpt, len3dir);
                if (Rows[hitsq] == null || Rows[hitsq].Index <= seg.Index + 1)
                {
                    //either hit a full square or an earlier segment.
                    //now figure out how far above you can return from
                    var clen = 0;
                    var candidate = Add(stpt, len1dir);
                    while (true)
                    {
                        if (!InBounds(candidate))
                        {
                            break;
                        }
                        if (Rows[candidate] != null)
                        {
                            break;
                        }
                        if (Hits.Get(candidate).Any(hseg => hseg.Index < seg.Index))
                        {
                            break;
                        }
                        clen++;
                        candidate = Add(candidate, len1dir);
                    }
                    returnableDistance[st] = clen;
                }
                else
                {
                    returnableDistance[st] = 0;
                }
            }

            //what does the set of all possible tweaks even look like?
            //hhhhhhhhh
            //   hh   h
            //       hh
            //        h
            //=======>h
            //e   e  |

            //maybe just find loops ignoring hits and check them later?
            //Note: there is never a need to have len1>1.  Just repeatedly apply 1 and you'll get n
            //DoDebug(this);
            //check every square in every vertical for a possible len1 starting up
            for (var st = 0; st <= seg.Len - 2; st++) //last checked is 2 before end
            {
                var stpt = Add(seg.Start, seg.Dir, st, true);

                //todo later expand this to cover all verticals, although it's not really necessary.
                var v = verticals[st]+1;
                while (v > 1)
                {
                    v--;
                    if (verticals[st] < v)
                    {
                        continue;
                    }
                    //figure out valid return points
                    //if earlytweak >st, if other >st+1
                    var lengthMinimum = 2;
                    if (st == 0)
                    {
                        lengthMinimum = 1;
                    }

                    //we know ups and downs.
                    //for the current len1st, find the downs which are greater than the limitation away.
                    var len2st = Add(stpt, len1dir, v);
                    var len2maxindex = GetSafeLength(len2st, len2dir, seg.Index) + st;
                    //We have a candidate st with vertical>0
                    //we know how far over we can go.
                    //go over all squares within that to find a down that satisfies.
                    for (var len2endcandidate = st + lengthMinimum; len2endcandidate <= len2maxindex && len2endcandidate <= seg.Len; len2endcandidate++)
                    {
                        if (returnableDistance[len2endcandidate] < v)
                        {
                            continue;
                        }
                        var len2 = len2endcandidate - st;
                        //this is cacheable - GSL(2,1)=1+GSL(3,1) assuming GSL(2,1)>1

                        //st=st, v=1, len2=len2
                        //we need to check:
                        //eptiness from st+v for len2 sqs
                        //that plus one for hittable
                        //down down from there is valid (can shortcircuit from verticals)
                        //once you get to the end of len2 you are good.
                        var shortTweak = st == 0;
                        var longTweak = st + len2 == seg.Len;
                        var tw = new Tweak(seg, right, st, v, len2, len1dir, shortTweak, longTweak);
                        res.Add(tw);
                    
                    }
                    if (res.Count > 0)
                    {
                        break;
                    }
                }
                if (res.Count > 0)
                {
                    break;
                }
            }

            //TODO this is a hack. Basically don't have shorttweaks where seg.index is 1 because it changes the first answer.
            res = res.Where(tw => tw.Seg.Index != 1 || 
                tw.Seg.Index == 1 && (!tw.ShortTweak && !tw.LongTweak)).ToList();
            return res;

        }

        //How far can you go from start in dir, including not overwriting an existing path,
        //not replacing a square which is hit by an earlier path, and not trickling straight into an already occupied square?
        public int GetSafeLength((int, int) start, Dir dir, int index, int? max = null)
        {
            var candidate = Add(start, dir);
            var res = 1;

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
                if (Hits.Get(candidate).Any(hc => hc != null && hc.Index < index - 1))
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

        public Tweak PickTweak(List<Tweak> tweaks)
        {
            if (!tweaks.Any())
            {
                return null;
            }
            tweaks = tweaks.OrderByDescending(el => el.Len1 + el.Len2 + el.Len3).ToList();

            var normal = tweaks.Where(t => t.LongTweak == false && t.ShortTweak == false);
            if (normal.Any())
            {
                return normal.First();
            }

            var longs = tweaks.Where(t => t.LongTweak == true);
            if (longs.Any())
            {
                return longs.First();
            }

            var shorts = tweaks.Where(t => t.ShortTweak == true);
            if (shorts.Any())
            {
                return shorts.First();
            }

            if (tweaks.Any())
            {
                return tweaks.First();
            }
            return null;
        }
    }
}
