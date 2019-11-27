using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;

using static coil.Navigation;
using static coil.Util;
using static coil.Debug;

namespace coil
{
    //Levels are generated with an artifical solid boundary outside it, handled by putting minint in there.
    public partial class Level : BaseLevel
    {
        public int Index;
        public TweakPicker TweakPicker;

        /// <summary>
        /// Compute a unique string representation of Segs for comparison
        /// </summary>
        public string GetHash()
        {
            var str = "";
            foreach (var seg in Segs)
            {
                str += $",{seg.Index}-{seg.Dir}-{seg.Len}";
            }

            return str;
        }

        //w/h are the "real" version
        public Level(LevelConfiguration lc, Log log, int width, int height, Random rnd, bool validateBoard, int index)
        {
            LevelConfiguration = lc;
            TweakPicker = lc.TweakPicker;
            Index = index;
            Rnd = rnd;
            Width = width + 2;
            Height = height + 2;
            Segs = new LinkedList<Seg>();
            DoBoardValidation = validateBoard;
            Hits = new HitManager(Width, Height, DoBoardValidation, this);
            InitBoard();
        }

        //public void ApplySaveAndUndoTweak(Tweak tweak, string fn)
        //{
        //    var fakeLevel = new Level(Width - 2, Height - 2, Rnd, false, 0, TweakPicker);
        //    Seg lastSeg = null;
        //    LinkedListNode<Seg> tweakSegNode = null;
        //    foreach (var seg in Segs)
        //    {
        //        lastSeg = seg;
        //        var fakeSeg = new Seg(seg.Start, seg.Dir, seg.Len);
        //        fakeSeg.Index = seg.Index;
        //        fakeLevel.ApplySeg(fakeSeg);
        //        fakeLevel.Segs.AddLast(fakeSeg);
        //        if (seg.Index == tweak.SegNode.Value.Index)
        //        {
        //            tweakSegNode = fakeLevel.Segs.Last;
        //        }
        //    }
        //    fakeLevel.Rows[lastSeg.GetEnd()] = lastSeg;

        //    //gotta copy the tweak too.
        //    var fakeTweak = new Tweak(tweakSegNode, tweak.Right, tweak.Len1, tweak.Len2, tweak.Len3, tweak.Len2dir, tweak.ShortTweak, tweak.LongTweak);

        //    fakeLevel.ApplyTweak(fakeTweak);
        //    SaveWithPath(fakeLevel, fn);
        //    //SaveEmpty(fakeLevel, fn.Replace(".png","-empty.png"));
        //}


        //we have to validate that the full possible set of tweaks for this seg is really being generated.
        //create an image of each tweak.
        //public void PossiblySaveAvailableTweaks(List<Tweak> tweaks, int tweakct)
        //{
        //    if (false)
        //    {
        //        var ii = 0;
        //        foreach (var testtweak in tweaks)
        //        {
        //            var fn = $"../../../output/{Width - 2}x{Height - 2}/Tweaks-{Index}-{tweakct}-{ii}.png";
        //            ApplySaveAndUndoTweak(testtweak, fn);
        //            ii++;
        //        }
        //    }
        //}

        public void RepeatedlyTweak(bool saveState, int saveEvery)
        {
            var tweakct = 0;
            //start at the last.
            //try it, then try the next
            var success = true;
            var loopct = 0;
            var sw = Stopwatch.StartNew();
            while (success)
            {
                loopct++;
                success = false;
                var currentSegnode = Segs.Last;

                //TODO add segpicking logic

                while (currentSegnode != null)
                {
                    //control this
                    //this makes it obvious that lastsegnode may not even be in segs anymore!
                    var nextSegnode = currentSegnode.Previous;

                    var rtweaks = GetTweaks(currentSegnode, true);
                    var ltweaks = GetTweaks(currentSegnode, false);

                    var tweaks = new List<Tweak>();

                    tweaks.AddRange(rtweaks);
                    tweaks.AddRange(ltweaks);

                    if (tweaks.Count() == 0)
                    {
                        currentSegnode = nextSegnode;
                        continue;
                    }

                    var tweak = LevelConfiguration.TweakPicker.Picker.Invoke(tweaks);
                    if (tweak == null)
                    {
                        currentSegnode = nextSegnode;
                        continue;
                    }

                    //PossiblySaveAvailableTweaks(tweaks, tweakct);
                    
                    WL($"Got tweaks: {currentSegnode.Value.Index,4} ({currentSegnode.Value.Len,4}) {tweaks.Count(),10}. picked:{tweak}");

                    ApplyTweak(tweak);
                    tweakct++;
                    if (tweakct % 1000 == 0)
                    {
                        WL($"Loop={loopct} tweakct={tweakct} {Report(this, sw.Elapsed)}");
                    }
                    if (saveState && tweakct > 0 && tweakct % saveEvery == 0)
                    {
                        //var fn = $"../../../output/{Width - 2}x{Height - 2}/Tweaks-{Index}-{tweakct}-empty.png";
                        var pathfn = $"../../../output/{Width - 2}x{Height - 2}/t-lc{LevelConfiguration.GetStr()}-i{Index}-l{loopct}-tw{tweakct}-p.png";
                        //SaveEmpty(this, fn);
                        SaveWithPath(this, pathfn);
                    }

                    success = true;
                    currentSegnode = nextSegnode;
                }
            }
        }

        public (Dictionary<int, int>, Dictionary<int, int>) GetVerticalsAndReturnables(LinkedListNode<Seg> segnode, bool right)
        {
            var seg = segnode.Value;
            //TODO if seg.Index==1 there is no prior tweak; hard to do an early tweak there but maybe possible

            var len2dir = right
                ? Rot(seg.Dir)
                : ARot(seg.Dir);

            var len3dir = seg.Dir;
            var len4dir = right ? ARot(len3dir) : Rot(len3dir);

            //for start pt index s, how far can you go up?
            var verticals = new Dictionary<int, int>();

            //how far away can you return to this square from.
            //zero means nothing and can be due to multiple causes.
            var returnableDistance = new Dictionary<int, int>();

            var segEnd = seg.GetEnd();

            //first figure out how far up you can go from each square, and how far down you can return to each square
            //each with their accompanying overlap into the next square being validated.

            int? knownLen2max = null;
            if (TweakPicker.MaxLen2.HasValue && LevelConfiguration.OptimizationSetup.UseTweakLen2RuleInGetVerticals)
            {
                knownLen2max = TweakPicker.MaxLen2.Value;
            }

            for (var st = 0; st <= seg.Len; st++)
            {
                var stpt = Add(seg.Start, seg.Dir, st, true);
                var vertical = GetSafeLength(segnode, stpt, len2dir, seg.Index, knownLen2max);
                //TODO add in verticals[seg.Len]=0, and returns[0]=0
                verticals[st] = vertical;

                //now figure out if this st allows returning (len3hit is legit)
                var hitsq = Add(stpt, len4dir);
                //when the next seg is just length one, the hitsq will mistakenly be owned by seg.Next.Next

                //bump into a full square, or the next segment, or the start of the next-next segment if next len is zero

                //old system: comlpex comparisions to generate returns
                //new: hitsq is valid if it's full, earlier path, or we returned to end of current seg.

                if (stpt == segEnd)
                {
                    var ae = 44;
                }

                if (Rows[hitsq]==null || Rows[hitsq].Index<seg.Index || stpt == segEnd)
                {
                    //either hit a full square or an earlier segment.
                    //now figure out how far above you can return from
                    var clen = 0;
                    var candidate = Add(stpt, len2dir);
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

                        if (knownLen2max.HasValue && clen > knownLen2max)
                        {
                            break;
                        }
                        candidate = Add(candidate, len2dir);
                    }
                    returnableDistance[st] = clen;
                    //TODO these are being calculated as if there was no max.
                }
                else
                {
                    returnableDistance[st] = 0;
                }
            }

            return (verticals, returnableDistance);
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
        public List<Tweak> GetTweaks(LinkedListNode<Seg> segnode, bool right)
        {
            var res = new List<Tweak>();
            var seg = segnode.Value;
            var len2dir = right
                ? Rot(seg.Dir)
                : ARot(seg.Dir);

            var len3dir = seg.Dir;
            var len4dir = right ? ARot(len3dir) : Rot(len3dir);
            var s = GetVerticalsAndReturnables(segnode, right);
            var verticals = s.Item1;
            var returnableDistance = s.Item2;

            //what does the set of all possible tweaks even look like?
            //hhhhhhhhh
            //   hh   h
            //       hh
            //        h
            //=======>h
            //e   e  |

            //maybe just find loops ignoring hits and check them later?
            //Note: there is never a need to have len1>1.  Just repeatedly apply 1 and you'll get n
            //check every square in every vertical for a possible len1 starting up

            //at st and v, what is GetSafeLength in the dir?
            //DoDebug(this, true);
            var STVCache = new Dictionary<(int, int), int>();

            for (var st = 0; st <= seg.Len - 2 || st == 0 && st <= seg.Len - 1; st++) //last checked is 2 before end
            {
                var stpt = Add(seg.Start, seg.Dir, st, true);

                //todo later expand this to cover all verticals, although it's not really necessary.
                if (verticals[st] == 0)
                {
                    continue;
                }
                var v = verticals[st];

                //TODO the problem occurs here.
                if (TweakPicker.MaxLen2.HasValue && LevelConfiguration.OptimizationSetup.UseTweakLen2RuleInGetTweaks)
                {
                    var newmax = Math.Min(TweakPicker.MaxLen2.Value, v);
                    if (v > newmax)
                    {
                        v = newmax;
                    }
                }
                while (v > 0)
                {
                    //figure out valid return points
                    //if earlytweak >st, if other >st+1
                    var lengthMinimum = 2;
                    if (st == 0)
                    {
                        lengthMinimum = 1;
                    }

                    //we know ups and downs.
                    //for the current len1st, find the downs which are greater than the limitation away.
                    var len2st = Add(stpt, len2dir, v);

                    //this is costly and is repeated calculation.
                    //we're at some st and some st, and looking right.
                    //bu we probably did this at st-1 too. if that was nonzero, use it!
                    int len3roomavailable = 0;
                    var foundInCache = false;

                    if (st > 0 && LevelConfiguration.OptimizationSetup.UseSTVCache)
                    {
                        var previousStvCacheKey = (st - 1, v);
                        if (STVCache.ContainsKey(previousStvCacheKey))
                        {
                            var prevValue = STVCache[previousStvCacheKey];
                            if (prevValue > 0)
                            {
                                len3roomavailable = prevValue - 1;
                                //WL($"Got cache. {st},{v}={len2maxindex}");
                                STVCache[(st, v)] = len3roomavailable;
                                foundInCache = true;
                            }
                        }
                    }

                    if (!foundInCache)
                    {
                        int len3knownmax = seg.Len - st;
                        //TODO this is actually incorrect because it'll lead to STVcache falsely thinking there is less room to go len2 than there is.
                        //TODO add a wrapper for each of these caching methods to verify they produce the same board as no cache.
                        if (TweakPicker.MaxLen3.HasValue && LevelConfiguration.OptimizationSetup.UseTweakLen3Rule)
                        {
                            len3knownmax = Math.Min(TweakPicker.MaxLen3.Value, len3knownmax);
                        }
                        len3roomavailable = GetSafeLength(segnode, len2st, len3dir, seg.Index, len3knownmax);

                        //problem: this has information about seg maxlength in it
                        //but when we fall back to using the cache we can run over.
                        STVCache[(st, v)] = len3roomavailable;
                    }
                    //We have a candidate st with vertical>0
                    //we know how far over we can go.
                    //go over all squares within that to find a down that satisfies.
                    var len3effectivemax = Math.Min(len3roomavailable, seg.Len);

                    //checking 
                    for (var len3endcandidate = st + lengthMinimum; len3endcandidate <= len3effectivemax + st; len3endcandidate++)
                    {
                        if (returnableDistance[len3endcandidate] < v)
                        {
                            continue;
                        }
                        var len3 = len3endcandidate - st;
                        //this is cacheable - GSL(2,1)=1+GSL(3,1) assuming GSL(2,1)>1

                        //st=st, v=1, len2=len2
                        //we need to check:
                        //eptiness from st+v for len2 sqs
                        //that plus one for hittable
                        //down down from there is valid (can shortcircuit from verticals)
                        //once you get to the end of len2 you are good.
                        var shortTweak = st == 0;
                        var longTweak = st + len3 == seg.Len;
                        var tw = new Tweak(segnode, right, st, v, len3, len2dir, shortTweak, longTweak);
                        res.Add(tw);
                    }
                    v--;
                }
            }

            //TODO this is a hack. Basically don't have shorttweaks where seg.index is 1 because it changes the first answer.
            res = res.Where(tw => (tw.SegNode.Previous != null && tw.SegNode.Next!=null) ||
                (!(tw.SegNode.Previous==null && tw.ShortTweak)
                && !(tw.SegNode.Next==null && tw.LongTweak))).ToList();
            //TODO ==1 and equal length
            return res;
        }

        //How far can you go from start in dir, including not overwriting an existing path,
        //not replacing a square which is hit by an earlier path, and not trickling straight into an already occupied square?
        public int GetSafeLength(LinkedListNode<Seg> segnode, (int, int) start, Dir dir, uint index, int? max = null)
        {
            var candidate = Add(start, dir);
            var res = 1;
            //res refers to candidate index. So if there's a failure, step back one square

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

                //this needs to be fixed since there is no longer a guarantee that the previous seg will have index == currentseg.index-1

                
                if (LevelConfiguration.OptimizationSetup.UseSpaceFillingIndexes)
                {
                    var previousSegIndex = segnode.Previous?.Value?.Index ?? 0;

                    if (Hits.Get(candidate).Any(hc => hc.Index < previousSegIndex))
                    {
                        res--;
                        break;
                    }
                }
                else
                {
                    if (Hits.Get(candidate).Any(hc => hc.Index < index - 1))
                    {
                        res--;
                        break;
                    }
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

        private void ApplyTweak(Tweak tweak)
        {
            var segnode = tweak.SegNode;
            var seg = segnode.Value;
            var prevnode = segnode.Previous;
            var nextnode = segnode.Next;
            if (tweak.ShortTweak && tweak.LongTweak)
            {
                //remove hit, fill in squares
                UnapplySeg(seg);
                //remove hit, lengthen and dig, add hit
                Lengthen(prevnode.Value, tweak);
                //move start back, increase length, dig squares
                Unlengthen(nextnode.Value, tweak);
                var seg3 = MakeSeg(tweak, TweakSection.Three, seg.Index);
                //add the hit, dig out board.
                ApplySeg(seg3);

                Segs.AddAfter(segnode, seg3);
                Segs.Remove(segnode);
                //no index adjustments
            }
            else if (tweak.ShortTweak)
            {
                UnapplySeg(seg);
                Lengthen(prevnode.Value, tweak);
                var seg3 = MakeSeg(tweak, TweakSection.Three, seg.Index);
                var seg4 = MakeSeg(tweak, TweakSection.Four, seg.Index + 1);
                var seg5 = MakeSeg(tweak, TweakSection.Five, seg.Index + 2);
                ApplySeg(seg3);
                ApplySeg(seg4);
                ApplySeg(seg5, endEarly: true);
                var seg5end = Add(seg5.Start, seg5.Dir, seg5.Len);
                if (nextnode != null)
                {
                    Rows[seg5end] = nextnode.Value;
                }
                else
                {
                    Rows[seg5end] = seg5;
                }

                var seg3node = Segs.AddAfter(segnode, seg3);
                var seg4node = Segs.AddAfter(seg3node, seg4);
                var seg5node = Segs.AddAfter(seg4node, seg5);
                Segs.Remove(segnode);
                if (LevelConfiguration.OptimizationSetup.UseSpaceFillingIndexes)
                {
                    SpaceFillIndexes(new List<LinkedListNode<Seg>>() { seg3node, seg4node, seg5node });
                }
                else
                {
                    AdjustIndexAfter(seg5node, 2);
                }
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

                var seg1node = Segs.AddAfter(segnode, seg1);
                var seg2node = Segs.AddAfter(seg1node, seg2);
                var seg3node = Segs.AddAfter(seg2node, seg3);
                Segs.Remove(segnode);
                if (LevelConfiguration.OptimizationSetup.UseSpaceFillingIndexes)
                {
                    SpaceFillIndexes(new List<LinkedListNode<Seg>>() { seg1node, seg2node, seg3node });
                }
                else
                {
                    AdjustIndexAfter(seg3node, 2);
                }
            }
            else
            {
                UnapplySeg(seg);
                var seg1 = MakeSeg(tweak, TweakSection.One, seg.Index);
                var seg2 = MakeSeg(tweak, TweakSection.Two, seg.Index + 1);
                var seg3 = MakeSeg(tweak, TweakSection.Three, seg.Index + 2);
                var seg4 = MakeSeg(tweak, TweakSection.Four, seg.Index + 3);
                var seg5 = MakeSeg(tweak, TweakSection.Five, seg.Index + 4);
                ApplySeg(seg1);
                ApplySeg(seg2);
                ApplySeg(seg3);
                ApplySeg(seg4);
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

                var seg1node = Segs.AddAfter(segnode, seg1);
                var seg2node = Segs.AddAfter(seg1node, seg2);
                var seg3node = Segs.AddAfter(seg2node, seg3);
                var seg4node = Segs.AddAfter(seg3node, seg4);
                var seg5node = Segs.AddAfter(seg4node, seg5);
                Segs.Remove(segnode);
                if (LevelConfiguration.OptimizationSetup.UseSpaceFillingIndexes)
                {
                    SpaceFillIndexes(new List<LinkedListNode<Seg>>() { seg1node, seg2node, seg3node, seg4node, seg5node });
                }
                else {
                    AdjustIndexAfter(seg5node, 4); 
                }
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
            var hit = Add(seg.Start, seg.Dir, seg.Len + 1);
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
            var hit = Add(seg.Start, seg.Dir, seg.Len + 1);
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
            var hit = Add(seg.Start, seg.Dir, seg.Len + 1);
            Hits.Add(hit, seg);
        }

        /// <summary>
        /// Somewhat convoluted way to create segs.
        /// </summary>
        public static Seg MakeSeg(Tweak tweak, TweakSection section, uint index)
        {
            switch (section)
            {
                case TweakSection.One:
                    var seg = new Seg(tweak.SegNode.Value.Start, tweak.SegNode.Value.Dir, tweak.Len1);
                    seg.Index = index;
                    return seg;
                case TweakSection.Two:
                    var seg2start = Add(tweak.SegNode.Value.Start, tweak.SegNode.Value.Dir, tweak.Len1);
                    var seg2 = new Seg(seg2start, tweak.Len2dir, tweak.Len2);
                    seg2.Index = index;
                    return seg2;
                case TweakSection.Three:
                    var seg2start2 = Add(tweak.SegNode.Value.Start, tweak.SegNode.Value.Dir, tweak.Len1, true);
                    var seg3start = Add(seg2start2, tweak.Len2dir, tweak.Len2);
                    var seg3 = new Seg(seg3start, tweak.SegNode.Value.Dir, tweak.Len3);
                    seg3.Index = index;
                    return seg3;
                case TweakSection.Four:
                    var seg4base = Add(tweak.SegNode.Value.Start, tweak.SegNode.Value.Dir, tweak.Len1 + tweak.Len3);
                    var seg4start = Add(seg4base, tweak.Len2dir, tweak.Len2);
                    var seg4 = new Seg(seg4start, Rot(Rot(tweak.Len2dir)), tweak.Len2);
                    seg4.Index = index;
                    return seg4;
                case TweakSection.Five:
                    var seg5start = Add(tweak.SegNode.Value.Start, tweak.SegNode.Value.Dir, tweak.Len1 + tweak.Len3);
                    var seg5len = tweak.SegNode.Value.Len - tweak.Len1 - tweak.Len3;
                    var seg5 = new Seg(seg5start, tweak.SegNode.Value.Dir, seg5len);
                    seg5.Index = index;
                    return seg5;
                default:
                    throw new Exception("Invalid tweakSection.");
            }
        }

        public void SpaceFillIndexes(List<LinkedListNode<Seg>> todo)
        {
            //figure out the range
            //pick indexes for each one
            //figure out if there is enough room - if not, redo all
            var r0 = todo.First().Previous;
            var r1 = todo.Last().Next;

            uint rangestart = r0?.Value?.Index ?? 0;
            uint rangeend = r1?.Value?.Index ?? uint.MaxValue;
            var gap = rangeend - rangestart;
            if (gap - 1 < todo.Count)
            {
                RedoAllIndexesSpaceFillndexes();
                return;
                //need to reassign everything
            }

            //plus one to leave space at the end too
            uint chunksize = gap / ((uint)todo.Count+1);
            uint current = rangestart + chunksize;
            WL($"Previous to next: {rangestart} => {rangeend}");
            foreach (var el in todo)
            {
                el.Value.Index = current;
                //WL($"Assigned {current}");
                current += chunksize;
                
            }
        }

        //this will leave some spurious space at the beginning when doing a full redo
        public void RedoAllIndexesSpaceFillndexes()
        {
            var l = new List<LinkedListNode<Seg>>();
            var first = Segs.First;
            while (first != null) { 
                l.Add(first);
                first = first.Next;
            }
            SpaceFillIndexes(l);
        }

        public void AdjustIndexAfter(LinkedListNode<Seg> seg, uint amount)
        {
            var el = seg.Next;
            while (el != null)
            {
                el.Value.Index += amount;
                el = el.Next;
            }
        }
    }
}
