using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;

using static coil.Navigation;
using static coil.Util;
using static coil.Debug;
using static coil.Coilutil;
using static coil.Reportutil;

namespace coil
{
    //Levels are generated with an artifical solid boundary outside it, handled by putting minint in there.
    public partial class Level : BaseLevel
    {
        public int Index;
        public TweakPicker TweakPicker;

        //w/h are the "real" version
        public Level(LevelConfiguration lc, int width, int height, Random rnd, int index)
        {
            LevelConfiguration = lc;
            TweakPicker = lc.TweakPicker;
            Index = index;
            Rnd = rnd;
            Width = width + 2;
            Height = height + 2;
            Segs = new LinkedList<Seg>();
            InitBoard();
            Hits = new HitManager(Width, Height, Cells);
        }

        public TweakStats RepeatedlyTweak(bool saveState, int saveEvery, Stopwatch st, bool quiet = false)
        {
            LevelConfiguration.SegPicker.Quiet = quiet;
            //after doing the tweak we will call the segpicker with previous, new segs, next seg, success

            //initial setup.

            //change to do this all in the picker itself - it should keep state

            var stats = new TweakStats();
            var lastProgress = st.Elapsed;

            var current = LevelConfiguration.SegPicker.PickSeg(null, null, stats, false);
            var tweakct = 0;
            var lastLoopCt = 0;
            while (current != null)
            {
                var tweaks = TweakBuffer;
                tweaks.Clear();
                GetTweaks(current, true, tweaks);
                GetTweaks(current, false, tweaks);
                //WL($"Generated {tweaks.Count} for {current.Value}: {current.Value.History}");
                if (!tweaks.Any())
                {
                    stats.NoTweaks++;
                    current = LevelConfiguration.SegPicker.PickSeg(null, null, stats, false);
                    continue;
                }

                //TODO is it bad that when we loop, we re-add every seg even ones which clearly haven't had anything change in the neighborhood.
                //most of the time the algo runs is recalculating things downstream of this decision

                var tweak = LevelConfiguration.TweakPicker.Picker.Invoke(tweaks);
                //if (tweak == null)
                //{
                //    //this should never happen now that I'm not using exclusionary tweakpickers.
                //    stats.NoTweaksQualify++;
                //    current = LevelConfiguration.SegPicker.PickSeg(null, null, stats, false);
                //    WL("noqualifying");
                //    current.Value.History += " noq";
                //    continue;
                //}
                //WL($"\tGottweak {tweak}");

                //seg is always destroyed. BUT, prev/next can also be modified.
                //can I just pass that along and have it removed/added with new len?
                var newSegs = ApplyTweak(tweak);
                stats.SuccessCt++;
                var modifiedSegs = ModifiedBuffer;
                modifiedSegs.Clear();
                
                //these should never be null since we don't allow longtweaks where next is null, or shorts to start.
                if (tweak.LongTweak)
                {
                    modifiedSegs.Add(newSegs.Last().Next);
                }
                if (tweak.ShortTweak)
                {
                    modifiedSegs.Add(newSegs.First().Previous);
                }

                //var nei = Solverutil.GetNeighbors(newSegs.Last().Value);

                var newLoop = false;
                if (stats.loopct > lastLoopCt)
                {
                    lastLoopCt = stats.loopct;
                    newLoop = true;
                }
                PossiblySaveDuringTweak(saveState, saveEvery, stats, newLoop: newLoop);

                //GetCoveragePercent walks every seg, so report on a clock, not a tweak count.
                if (!quiet && (st.Elapsed - lastProgress).TotalSeconds >= 5)
                {
                    lastProgress = st.Elapsed;
                    WL($"{stats.loopct} Segs={Segs.Count} Cov={GetCoveragePercent(this, out int _):0.0} {st.Elapsed} tweakSuccess:{(stats.SuccessCt * 1.0 / (stats.NoTweaks+stats.NoTweaksQualify+stats.SuccessCt)* 100.0).ToString("##0.0")}%");
                }
                current = LevelConfiguration.SegPicker.PickSeg(newSegs, modifiedSegs, stats, true);
                //WL($"success, advanced to: {current?.Value}");
                
            }

            return stats;
        }

        public void PossiblySaveDuringTweak(bool saveState, int saveEvery, TweakStats stats, List<(int,int)> neighbors = null, bool newLoop = false)
        {
            if ((saveState && stats.SuccessCt % saveEvery == 0) || newLoop)
            {
                var loopText = "";
                if (newLoop)
                {
                    if (false)
                    {
                        //bit of a hack here - but i want to see details about progress per loop
                        var repdata = GetReport(this, TimeSpan.FromSeconds(0), stats);
                        var rep = Report(repdata, multiline: true);
                        loopText = $"loop={stats.loopct} ";
                        var pathfn = $"{Paths.Root}/output/{Width - 2}x{Height - 2}/t-{LevelConfiguration.GetStr()}-i{Index}-l{stats.loopct}-tw{stats.SuccessCt}-p-{loopText}.png";
                        SaveWithPath(this, pathfn, subtitle: rep, quiet: true);
                    }
                }
                else
                {
                    var pathfn = $"{Paths.Root}/output/{Width - 2}x{Height - 2}/t-{LevelConfiguration.GetStr()}-i{Index}-l{stats.loopct}-tw{stats.SuccessCt}-p.png";
                    SaveWithPath(this, pathfn, subtitle: $"{loopText}SegCt:{Segs.Count}", highlights: neighbors);
                }

                //var fn = $"{Paths.Root}/output/{Width - 2}x{Height - 2}/Tweaks-{Index}-{tweakct}-empty.png";
                //SaveEmpty(this, fn);
            }
        }

        public int GetVertical(int st, LinkedListNode<Seg> segnode, Dir len2dir, int? knownLen2max)
        {
            var seg = segnode.Value;
            var stpt = Add(seg.Start, seg.Dir, st, true);
            var vertical = GetSafeLength(segnode, stpt, len2dir, seg.Index, knownLen2max);
            //TODO add in verticals[seg.Len]=0, and returns[0]=0
            return vertical;
        }

        /// <summary>
        /// There is some cacheability.  i.e. if x,y is returnable, then x,y-1 is also returnable, y>1
        /// </summary>
        public int GetReturnable(int st, Dir len2dir, int? knownLen2max, Dir len4dir, LinkedListNode<Seg> segnode)
        {
            var seg = segnode.Value;
            var segEnd = seg.GetEnd();
            var stpt = Add(seg.Start, seg.Dir, st, true);
            var hitsq = Add(stpt, len4dir);
            //when the next seg is just length one, the hitsq will mistakenly be owned by seg.Next.Next

            //bump into a full square, or the next segment, or the start of the next-next segment if next len is zero

            //old system: complex comparisons to generate returns
            //new: hitsq is valid if it's full, earlier path, or we returned to end of current seg.

            //if (stpt == segEnd)
            //{
            //    var ae = 44;
            //}

            var rowIndex = GetRowIndex(hitsq);

            if (rowIndex == 0 || rowIndex < seg.Index || stpt == segEnd)
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
                    if (GetRowIndex(candidate) != 0)
                    {
                        break;
                    }
                    if (Hits.CandidateIsHitByLessThan(candidate, seg.Index))
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
                return clen;
                //TODO these are being calculated as if there was no max.
            }
            else
            {
                return 0;
            }
        }

        /// <summary>
        /// couple strategies here.
        /// It would be nice to quickly know all available tweaks and then just pick one based on level generation rules.
        /// Assume  base is a horizontal path staring at 0,0 and going to x,0.
        /// You will tweak up.
        /// We need to know the connectivity of every point xx,y, y>0, xx>0, with every other point xx2, y2, y2>y, xx2>xx, and als o the same going upwards.
        /// So for every len1 up, walk horizontally and figire it out.  as you do that you'll virtually also figure out the vertical.
        /// 
        /// TODO: This is still to fine-grained. Is there a higher concept of what a tweak is?
        /// i.e. metadata about there region "above" a seg (and returanbles) which can abstractly be used to pull out a seg
        /// without having to pregenerate every single one and pick from among them?
        /// </summary>
        private readonly List<Tweak> TweakBuffer = new List<Tweak>();
        private readonly List<LinkedListNode<Seg>> ModifiedBuffer = new List<LinkedListNode<Seg>>();
        private readonly List<Seg> MadeBuffer = new List<Seg>(5);
        private readonly List<LinkedListNode<Seg>> NodesBuffer = new List<LinkedListNode<Seg>>(5);
        private int[] ReturnableCache = new int[64];

        //STV cache: how far seg3 can run from (len1, len2), per len1 row. Rows come from a pool and are only
        //allocated for the len1 values this call visits, so the per-call reset is O(seg.Len) and never zeroes
        //a large table. (A Dictionary here cost ~10% of the run in Dictionary.Clear.)
        private int[][] StvRows = new int[64][];
        //valid prefix length of each row (pooled rows may be longer and hold stale values past it).
        private int[] StvRowLen = new int[64];
        private readonly Stack<int[]> StvPool = new Stack<int[]>();

        private int[] RentStvRow(int len)
        {
            if (StvPool.Count > 0)
            {
                var r = StvPool.Pop();
                if (r.Length >= len)
                {
                    return r;
                }
            }
            return new int[Math.Max(len, 16)];
        }

        /// <summary>
        /// Append the tweaks available on one side of a seg to res. Per-call limits (TweakLim, GlobalTweakLim)
        /// count only the tweaks this call adds.
        /// </summary>
        public void GetTweaks(LinkedListNode<Seg> segnode, bool right, List<Tweak> res)
        {
            //TODO even better than my current attept to take the first reasonable tweak, would be to be able to apply a size limit to a tweak.
            //simply taking max 50 or something with rarer longer ones would solve the "too long section" problem.
            //TODO also, generating the entire order for every len1 and len2 candidate when the first one is likely to hit is pointless.
            //what does a function look like this: F(10) = 5,6,4,7,3,8,2,9,1...?
            var startCount = res.Count;
            var seg = segnode.Value;
            var len2dir = right
                ? Rot(seg.Dir)
                : ARot(seg.Dir);

            var len3dir = seg.Dir;
            var len4dir = right ? ARot(len3dir) : Rot(len3dir);

            int? knownLen2max = null;
            if (TweakPicker.MaxLen2.HasValue && LevelConfiguration.OptimizationSetup.UseTweakLen2RuleInGetVerticals)
            {
                knownLen2max = TweakPicker.MaxLen2.Value;
            }

            //what does the set of all possible tweaks even look like?
            //hhhhhhhhh
            //   hh   h
            //       hh
            //        h
            //=======>h
            //e   e  |

            if (ReturnableCache.Length < seg.Len + 1)
            {
                ReturnableCache = new int[Math.Max(seg.Len + 1, ReturnableCache.Length * 2)];
                StvRows = new int[ReturnableCache.Length][];
                StvRowLen = new int[ReturnableCache.Length];
            }
            //GetReturnable depends only on the absolute position along the seg, not on len2: cache it per position.
            Array.Fill(ReturnableCache, -1, 0, seg.Len + 1);
            try
            {
                GetTweaksCore(segnode, seg, right, res, startCount, len2dir, len3dir, len4dir, knownLen2max);
            }
            finally
            {
                for (var i = 0; i <= seg.Len; i++)
                {
                    if (StvRows[i] != null)
                    {
                        StvPool.Push(StvRows[i]);
                        StvRows[i] = null;
                    }
                }
            }
        }

        private void GetTweaksCore(LinkedListNode<Seg> segnode, Seg seg, bool right, List<Tweak> res, int startCount, Dir len2dir, Dir len3dir, Dir len4dir, int? knownLen2max)
        {

            var len1start = 0;
            if (segnode.Previous == null)
            {
                len1start = 1;
            }

            //TODO it would be nice to start in the middle and break up segs from there.
            //or start at 1/3 and go forward so that stvcache still works...
            
            //RETURNABLE cache
            foreach (var len1 in Pivot(len1start, seg.Len-1))
            {
                if (len1!=0 && len1 == seg.Len - 1)
                {
                    continue;
                }
                var len1end = Add(seg.Start, seg.Dir, len1, true);

                //todo later expand this to cover all verticals, although it's not really necessary.

                var len2max = GetVertical(len1, segnode, len2dir, knownLen2max);

                if (len2max==0)
                {
                    continue;
                }
                var stvRow = RentStvRow(len2max + 1);
                Array.Fill(stvRow, -1, 0, len2max + 1);
                StvRows[len1] = stvRow;
                StvRowLen[len1] = len2max + 1;

                int len3absolutemax = seg.Len - len1;
                //TODO this is actually incorrect because it'll lead to STVcache falsely thinking there is less room to go len2 than there is.
                if (TweakPicker.MaxLen3.HasValue && LevelConfiguration.OptimizationSetup.UseTweakLen3Rule)
                {
                    len3absolutemax = Math.Min(TweakPicker.MaxLen3.Value, len3absolutemax);
                }

                //it would be nice to prefer moving right here so we could take advantage of stvcache (which is basically useless now)
                foreach (var len2 in Pivot(1,len2max))
                {
                    //what is the structural problem that causes this bug?
                    //I don't distinguish between absolute points (pt), absolute x positions (x), relative x positions (st)
                    //figure out valid return points
                    //if earlytweak >st, if other >st+1
                    var lengthMinimum = 2;
                    if (len1 == 0)
                    {
                        lengthMinimum = 1;
                    }

                    //we know ups and downs.
                    //for the current len1st, find the downs which are greater than the limitation away.
                    var len3start = Add(len1end, len2dir, len2);

                    //this is costly and is repeated calculation.
                    //we're at some st and some st, and looking right.
                    //bu we probably did this at st-1 too. if that was nonzero, use it!
                    var len3available = 0;
                    var foundInCache = false;

                    if (len1 > 0 && LevelConfiguration.OptimizationSetup.UseSTVCache)
                    {
                        var prevRow = StvRows[len1 - 1];
                        if (prevRow != null && len2 < StvRowLen[len1 - 1] && prevRow[len2] > 0)
                        {
                            len3available = prevRow[len2] - 1;
                            stvRow[len2] = len3available;
                            foundInCache = true;
                        }
                    }

                    if (!foundInCache)
                    {
                        len3available = GetSafeLength(segnode, len3start, len3dir, seg.Index, len3absolutemax);
                        //problem: this has information about seg maxlength in it
                        //but when we fall back to using the cache we can run over.
                        stvRow[len2] = len3available;
                    }
                    //We have a candidate st with vertical>0
                    //we know how far over we can go.
                    //go over all squares within that to find a down that satisfies.
                    //var len3effectivemax = Math.Min(len3available, seg.Len);

                    //pick longest len3s first.
                    //var len3choices = new List<int>();
                    //for (var len3 = len3available; len3 >= lengthMinimum; len3--)
                    //{
                    //    len3choices.Add(len3);
                    //}
                    //len3choices = len3choices.OrderBy(el => el > len3available * 2 / 3 ? el : el + len3available).ToList();

                    //var ba;

                    //foreach (var len3 in Pivot(lengthMinimum, len3available))
                    //TODO something bugged if you use pivot here.

                    //var alternative = Pivot(lengthMinimum, len3available);
                    //var alts = new List<int>();
                    //foreach (var el in alternative)
                    //{
                    //    alts.Add(el);
                    //}
                    //if (alts.Count != len3choices.Count)
                    //{
                    //    var bb = 3;
                    //}

                    //TODO it is rather bad that we pivot here. with low tweak generation this will often result in non-long tweak len3s.
                    //it's also remarkable that if for a greater len2 we already kne wthe returnable at a given x coordinate, then our returnable
                    //is clearly true
                    //TODO it's also inefficient that we go all the way to len3available but only may use part of it.
                    foreach (var len3 in Pivot(lengthMinimum, len3available))
                    //foreach (var len3 in len3choices)
                    {
                        var len3endcandidate = len3 + len1;

                        var returnableDistance = ReturnableCache[len3endcandidate];
                        if (returnableDistance < 0)
                        {
                            returnableDistance = GetReturnable(len3endcandidate, len2dir, knownLen2max, len4dir, segnode);
                            ReturnableCache[len3endcandidate] = returnableDistance;
                        }

                        //the return column (seg4) is len2 tall; it does not need to match the up column's maximum.
                        if (returnableDistance < len2)
                        {
                            continue;
                        }
                        //this is cacheable - GSL(2,1)=1+GSL(3,1) assuming GSL(2,1)>1

                        //st=st, v=1, len2=len2
                        //we need to check:
                        //eptiness from st+v for len2 sqs
                        //that plus one for hittable
                        //down down from there is valid (can shortcircuit from verticals)
                        //once you get to the end of len2 you are good.

                        //no longtweaks to the last tweak.
                        if (segnode.Next==null && len1 + len3 == seg.Len)
                        {
                            continue;
                        }
                        if (segnode.Previous == null && len1 == 0)
                        {
                            continue;
                        }

                        var tw = new Tweak(segnode, right, len1, len2, len3, len2dir);
                        res.Add(tw);

                        var added = res.Count - startCount;
                        if (LevelConfiguration.TweakPicker.TweakLim.HasValue && added >= LevelConfiguration.TweakPicker.TweakLim)
                        {
                            return;
                        }
                        //TODO this is also applicable to getverticals and returnables...
                        if (LevelConfiguration.OptimizationSetup.GlobalTweakLim.HasValue && added >= LevelConfiguration.OptimizationSetup.GlobalTweakLim)
                        {
                            return;
                        }
                    }
                }
            }
        }

        //How far can you go from start in dir, including not overwriting an existing path,
        //not replacing a square which is hit by an earlier path, and not trickling straight into an already occupied square?
        public int GetSafeLength(LinkedListNode<Seg> segnode, (int, int) start, Dir dir, ulong index, int? max = null)
        {
            var candidate = Add(start, dir);
            var res = 1;
            var previousSegIndex = segnode.Previous?.Value?.Index ?? 0;
            //res refers to candidate index. So if there's a failure, step back one square

            while (true)
            {
                if (!InBounds(candidate))
                {
                    res--;
                    break;
                }

                var canIndex = GetRowIndex(candidate);
                if (canIndex != 0)
                {
                    //hit some other path; pull back.
                    res--;
                    if (canIndex > index)
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
                    //first seg has no previous: then any hit at all blocks.
                    if (previousSegIndex == 0 ? Hits.GetCount(candidate) > 0 : Hits.CandidateIsHitByLessThan(candidate, previousSegIndex))
                    {
                        res--;
                        break;
                    }
                }
                else
                {
                    if (Hits.CandidateIsHitByLessThan(candidate, index - 1))
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

        //core logic. Order matters: new segs are linked into Segs and given their final (space-filled) indexes
        //BEFORE ApplySeg writes their squares, because SetRowValue caches the owner's index per square.
        private List<LinkedListNode<Seg>> ApplyTweak(Tweak tweak)
        {
            var segnode = tweak.SegNode;
            var seg = segnode.Value;
            var prevnode = segnode.Previous;
            var nextnode = segnode.Next;
            var made = MadeBuffer;
            made.Clear();
            if (tweak.ShortTweak && tweak.LongTweak)
            {
                made.Add(MakeSeg(tweak, TweakSection.Three, seg.Index));
            }
            else if (tweak.ShortTweak)
            {
                made.Add(MakeSeg(tweak, TweakSection.Three, seg.Index));
                made.Add(MakeSeg(tweak, TweakSection.Four, seg.Index + 1));
                made.Add(MakeSeg(tweak, TweakSection.Five, seg.Index + 2));
            }
            else if (tweak.LongTweak)
            {
                made.Add(MakeSeg(tweak, TweakSection.One, seg.Index));
                made.Add(MakeSeg(tweak, TweakSection.Two, seg.Index + 1));
                made.Add(MakeSeg(tweak, TweakSection.Three, seg.Index + 2));
            }
            else
            {
                made.Add(MakeSeg(tweak, TweakSection.One, seg.Index));
                made.Add(MakeSeg(tweak, TweakSection.Two, seg.Index + 1));
                made.Add(MakeSeg(tweak, TweakSection.Three, seg.Index + 2));
                made.Add(MakeSeg(tweak, TweakSection.Four, seg.Index + 3));
                made.Add(MakeSeg(tweak, TweakSection.Five, seg.Index + 4));
            }

            //link in, then fix indexes.
            var nodes = NodesBuffer;
            nodes.Clear();
            var after = segnode;
            foreach (var m in made)
            {
                after = Segs.AddAfter(after, m);
                nodes.Add(after);
            }
            Segs.Remove(segnode);
            if (made.Count > 1)
            {
                if (LevelConfiguration.OptimizationSetup.UseSpaceFillingIndexes)
                {
                    SpaceFillIndexes(nodes);
                }
                else
                {
                    AdjustIndexAfter(nodes[nodes.Count - 1], (ulong)made.Count - 1);
                }
            }

            //now write the board.
            UnapplySeg(seg);
            if (tweak.ShortTweak)
            {
                //remove hit, lengthen and dig, add hit
                Lengthen(prevnode.Value, tweak);
            }
            if (tweak.LongTweak)
            {
                //move start back, increase length, dig squares
                Unlengthen(nextnode.Value, tweak);
            }
            foreach (var m in made)
            {
                ApplySeg(m);
            }
            if (!tweak.LongTweak)
            {
                //a seg owns its first square but not its last; the last square of seg5 belongs to whatever follows.
                var seg5 = made[made.Count - 1];
                var seg5end = Add(seg5.Start, seg5.Dir, seg5.Len);
                SetRowValue(seg5end, nextnode != null ? nextnode.Value : seg5);
            }
            return nodes;
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
                SetRowValue(candidate, seg);
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
                SetRowValue(segEnd, seg);
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
                SetRowValue(candidate, null);
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
                SetRowValue(candidate, seg);
                len++;
                candidate = Add(candidate, seg.Dir);
            }
            var hit = Add(seg.Start, seg.Dir, seg.Len + 1);
            Hits.Add(hit, seg);
        }

        /// <summary>
        /// Somewhat convoluted way to create segs.
        /// </summary>
        public Seg MakeSeg(Tweak tweak, TweakSection section, ulong index)
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

        /// <summary>
        /// A path cell with a single open neighbour can only be the path's start or end, and solvers use that:
        /// two such cells fix the start to one of two cells, one such cell halves the start search on average.
        /// Only the start and end of the path can be such cells (every interior cell touches its predecessor and
        /// successor), so shorten the path from whichever end is a pocket until both ends have a second open
        /// neighbour. Trimming keeps the path valid: the vacated square becomes a wall, which is what stopped the
        /// slide into it anyway. Returns the number of squares removed.
        /// </summary>
        public int TrimDeadEnds()
        {
            var removed = 0;
            while (Segs.Count > 1 && OpenNeighborCount(Segs.First.Value.Start) <= 1)
            {
                var first = Segs.First.Value;
                var oldStart = first.Start;
                SetRowValue(oldStart, null);
                first.Start = Add(first.Start, first.Dir);
                first.Len--;
                if (first.Len == 0)
                {
                    Hits.Remove(first.GetHit(), first);
                    Segs.RemoveFirst();
                }
                removed++;
            }
            while (Segs.Count > 1 && OpenNeighborCount(Segs.Last.Value.GetEnd()) <= 1)
            {
                var last = Segs.Last.Value;
                var end = last.GetEnd();
                Hits.Remove(last.GetHit(), last);
                SetRowValue(end, null);
                last.Len--;
                if (last.Len == 0)
                {
                    Segs.RemoveLast();
                    //the new final square belongs to the seg that now ends there.
                    SetRowValue(Segs.Last.Value.GetEnd(), Segs.Last.Value);
                }
                else
                {
                    Hits.Add(last.GetHit(), last);
                    SetRowValue(last.GetEnd(), last);
                }
                removed++;
            }
            return removed;
        }

        private int OpenNeighborCount((int, int) pt)
        {
            var n = 0;
            foreach (var d in AllDirs)
            {
                if (GetRowValue(Add(pt, d)) != null)
                {
                    n++;
                }
            }
            return n;
        }

        public void AdjustIndexAfter(LinkedListNode<Seg> seg, ulong amount)
        {
            var el = seg.Next;
            while (el != null)
            {
                el.Value.Index += amount;
                el = el.Next;
            }
            RecomputeCachedIndexes();
        }
    }
}
