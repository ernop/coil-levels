using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

using C5;
using static coil.Util;

namespace coil
{
    //TODO restore the smart functionality we had earlier
    //basically, we want to treat segs from the back so that they can push closer, and not have to increment indexes as much.
    //the former benefit will never go away.

    /// <summary>
    /// As you iterate, continuously tweaking segs, which seg should you actually tweak next?
    /// This depends in complex ways on the data structure used to store tweaks
    /// As of now using a linkedlist makes it a big pain to navigate around, especially since you're constantly removing segs.
    /// 
    /// TODO: seg ordering is a bit confusing.
    /// a,b,c=>
    ///   a is the first, c is the last
    ///   at b a is previous, c is next. This is conceptually opposite for the normal rule of "back to front"
    /// </summary>

    public abstract class SegPicker
    {
        public string Name { get; set; }
        public System.Random Random { get; set; }
        public Level Level { get; set; }

        public string GetName() { return Name; }

        public SegPicker()
        {
        }

        public abstract void Init(int seed, Level level);

        public void BaseInit(int seed, Level level)
        {
            Random = new System.Random(seed);
            Level = level;
        }

        public override string ToString()
        {
            return $"SegPicker:{Name}";
        }

        public abstract LinkedListNode<Seg> PickSeg(List<LinkedListNode<Seg>> newSegs, List<LinkedListNode<Seg>> modifiedSegs, TweakStats stats, bool success);
    }

    public class NewSegPicker : SegPicker
    {
        public NewSegPicker() : base() {
            Name = "New";
        }

        private bool Success = false;

        public override void Init(int seed, Level level)
        {
            BaseInit(seed, level);
        }

        LinkedListNode<Seg> LastReturnedSeg { get; set; }

        public override LinkedListNode<Seg> PickSeg(List<LinkedListNode<Seg>> newSegs, List<LinkedListNode<Seg>> modifiedSegs, TweakStats stats, bool success)
        {
            if (LastReturnedSeg == null)
            {
                LastReturnedSeg = Level.Segs.Last;
                return Level.Segs.Last;
            }
            if (newSegs!=null)
            {
                Success = true;
                var last = newSegs.Last();
                LastReturnedSeg = last;
                return last;
            }

            var val = LastReturnedSeg.Previous;
            if (val == null)
            {
                //we got all the way back to the start.
                if (Success)
                {
                    val = Level.Segs.Last;
                    LastReturnedSeg = val;
                    Success = false;
                    return val;
                }
                else
                {
                    return null;
                }
            }
            val = LastReturnedSeg.Previous;
            LastReturnedSeg = val;
            return val;
        }
    }

    public class ConfigurableSegPicker : SegPicker
    {
        public IComparer<LinkedListNode<Seg>> Comparer;


        public ConfigurableSegPicker(string name, IComparer<LinkedListNode<Seg>> comparer) {
            Name = name;
            Comparer = comparer;
        }

        public override void Init(int seed, Level level)
        {
            BaseInit(seed, level);
            RedoHeap();
        }

        public bool Success = false;

        public C5.IntervalHeap<LinkedListNode<Seg>> Heap { get; set; }

        private LinkedListNode<Seg> LastReturnedSeg { get; set; }

        private Dictionary<Seg, IPriorityQueueHandle<LinkedListNode<Seg>>> Handles;

        /// <summary>
        /// When a seg length changes, I need to know about it - SL tweaks can do this invisibly which is annoying.
        /// </summary>
        public override LinkedListNode<Seg> PickSeg(List<LinkedListNode<Seg>> newSegs, List<LinkedListNode<Seg>> modifiedSegs, TweakStats stat, bool success)
        {
            //put in the new segs.
            if (newSegs != null)
            {
                foreach (var newSeg in newSegs)
                {
                    AddSafely(newSeg);
                }
            }
            if (modifiedSegs != null)
            {
                foreach (var modseg in modifiedSegs)
                {
                    RemoveSafely(modseg);
                    AddSafely(modseg);
                }
            }
            if (success)
            {
                Success = true;
            }

            if (Heap.Count == 0)
            {
                if (Success)
                {
                    RedoHeap();
                    Success = false;
                    stat.Loops++;
                }
                else
                {
                    return null;
                }
            }

            //the item will still hang around in the handles dict.
            var el = Heap.DeleteMax();
            
            //WL($"Returning seg {el.Value.Index} of len {el.Value.Len}");
            return el;
        }

        private void RedoHeap()
        {
            Heap = new IntervalHeap<LinkedListNode<Seg>>(Comparer);
            Handles = new Dictionary<Seg, IPriorityQueueHandle<LinkedListNode<Seg>>>();
            var el = Level.Segs.First;
            while (el != null)
            {
                AddSafely(el);
                el = el.Next;
            }
        }

        private void RemoveSafely(LinkedListNode<Seg> seg)
        {
            var handle = Handles[seg.Value];
            //can this be null, and if so, why?
            //yes, since the heap is continuously being cut down, this seg may have already been removed, processed, and still be in the map 
            //(if there was no successful replacemet of it with a tweak.))
            if (!handle.ToString().Contains("-1"))
            {
                if (handle.ToString()=="[1]")
                {
                    var ae = 3;
                }
                try
                {
                    //can't delete the last item for some reason?
                    Heap.Delete(handle);
                }catch (Exception ex)
                {
                    
                    var aee = 3;
                }
            }
        }

        private void AddSafely(LinkedListNode<Seg> seg)
        {
            IPriorityQueueHandle<LinkedListNode<Seg>> handle = null;
            Heap.Add(ref handle, seg);
            Handles[seg.Value] = handle;
        }
    }

    public class LengthComparer : IComparer<LinkedListNode<Seg>>
    {
        public int Compare([AllowNull] LinkedListNode<Seg> x, [AllowNull] LinkedListNode<Seg> y)
        {
            return x.Value.Len.CompareTo(y.Value.Len);
        }
    }

    public class WeightedComparer : IComparer<LinkedListNode<Seg>>
    {
        public int Compare([AllowNull] LinkedListNode<Seg> x, [AllowNull] LinkedListNode<Seg> y)
        {
            return (x.Value.Index+x.Value.Len).CompareTo(y.Value.Index+y.Value.Len);
        }
    }

    public static class SegPickers
    {
        public static IEnumerable<SegPicker> GetSegPickers(string name)
        {
            var pickers = new List<SegPicker>() { new ConfigurableSegPicker("Longest", new LengthComparer()), new ConfigurableSegPicker("Weighted", new WeightedComparer()), new NewSegPicker() };
            if (string.IsNullOrEmpty(name))
            {
                return pickers;
            }
            return pickers.Where(p => p.Name == name);
        }
    }

    //public static class SegPickers
    //{
    //    public static List<SegPicker> GetSegPickers(int seed)
    //    {
    //        var pickers = new List<SegPicker>()
    //        {
    //            //randomly walk around the active area, generally moving forward.
    //            //this is messed up somehow.
    //            new SegPicker(seed: seed,
    //                name:"BRand",
    //                initialPicker: (LinkedList<Seg> segs)=>segs.Last,
    //                picker: (Random rnd, LinkedList<Seg> segs,  LinkedListNode<Seg> previous,  LinkedListNode<Seg> next, LinkedListNode<Seg> newseg, bool created) =>
    //                {
    //                    if (newseg!=null)
    //                    {
    //                        return newseg;
    //                    }
    //                    if (previous == null)
    //                    {
    //                        return next;
    //                    }

    //                    var candidate=previous;
    //                    if (rnd.Next(3) > 0) //going forward
    //                    {
    //                        var ct = rnd.Next(5);
    //                        for (var ii = 0;ii<ct;ii++)
    //                        {
    //                            if (candidate.Next == null)
    //                            {
    //                                return null;
    //                            }
    //                            candidate=candidate.Next;
    //                        }
    //                        return candidate;
    //                    }
    //                    else{
    //                        var ct = rnd.Next(2);
    //                        for (var ii = 0; ii < ct; ii++)
    //                        {
    //                            if (candidate.Previous == null)
    //                            {
    //                                return candidate;
    //                            }
    //                            candidate=candidate.Previous;
    //                        }
    //                        return candidate;
    //                    }
    //                }
    //            ),
    //            new SegPicker(seed: seed,
    //                name:"Prev",
    //                initialPicker: (LinkedList<Seg> segs)=>segs.Last,
    //                picker: (Random rnd, LinkedList<Seg> segs,  LinkedListNode<Seg> previous,  LinkedListNode<Seg> next, LinkedListNode<Seg> newseg, bool created)=>previous
    //            ),
    //            new SegPicker(seed: seed,
    //                name:"New",
    //                initialPicker: (LinkedList<Seg> segs)=>segs.Last,
    //                picker: (Random rnd, LinkedList<Seg> segs,  LinkedListNode<Seg> previous,  LinkedListNode<Seg> next, LinkedListNode<Seg> newseg, bool created) =>
    //                {
    //                    if (created)
    //                    {
    //                        return newseg;
    //                    }
    //                    return previous;
    //                }
    //            ),
    //            new SegPicker(seed: seed,
    //                name:"Next",
    //                initialPicker: (LinkedList<Seg> segs)=>segs.Last,
    //                picker: (Random rnd, LinkedList<Seg> segs,  LinkedListNode<Seg> previous,  LinkedListNode<Seg> next, LinkedListNode<Seg> newseg, bool created) =>
    //                {
    //                    //if we modified a seg, do the next seg cause room might have opened up. This is aiming to get a more complete pass the first time.
    //                    //you'll naturally fall through to the next.
    //                    if (created)
    //                    {
    //                        return next;
    //                    }
    //                    return previous;
    //                }
    //            ),
    //             new SegPicker(seed: seed,
    //                name:"NextR",
    //                initialPicker: (LinkedList<Seg> segs)=>segs.Last,
    //                picker: (Random rnd, LinkedList<Seg> segs,  LinkedListNode<Seg> previous,  LinkedListNode<Seg> next, LinkedListNode<Seg> newseg, bool created) =>
    //                {
    //                    //if we modified a seg, do the next seg cause room might have opened up. This is aiming to get a more complete pass the first time.
    //                    //you'll naturally fall through to the next.
    //                    if (created){
    //                        if (rnd.Next(5) == 0)
    //                        {
    //                            return newseg;
    //                        }
    //                        else
    //                        {
    //                            return next;
    //                        }
    //                    }

    //                    return previous;
    //                }
    //            ),

    //        new SegPicker(seed: seed,
    //                name:"NewR",
    //                initialPicker: (LinkedList<Seg> segs)=>segs.Last,
    //                picker: (Random rnd, LinkedList<Seg> segs,  LinkedListNode<Seg> previous,  LinkedListNode<Seg> next, LinkedListNode<Seg> newseg, bool created) =>
    //                { 
    //                    if (newseg != null && rnd.Next(2)==0)
    //                    {
    //                        return newseg;
    //                    }

    //                    //theory: this is doing a bunch of useless work.
    //                    return previous;
    //                }
    //            ),
    //            new SegPicker(seed: seed,
    //                name:"Big",
    //                initialPicker: (LinkedList<Seg> segs)=>segs.Last,
    //                picker: (Random rnd, LinkedList<Seg> segs,  LinkedListNode<Seg> previous,  LinkedListNode<Seg> next, LinkedListNode<Seg> newseg, bool created) =>
    //                {
    //                    var ii = 0;
    //                    var choices = new List<LinkedListNode<Seg>>();
    //                    while (ii < 10 && previous != null)
    //                    {
    //                        choices.Add(previous);
    //                        ii++;
    //                    }
    //                    if (!choices.Any())
    //                    {
    //                        return null;
    //                    }
    //                    return choices.OrderByDescending(el=>el.Value.Len).First();
    //                }
    //            ),
    //            new SegPicker(seed: seed,
    //                name:"PrevR",
    //                initialPicker: (LinkedList<Seg> segs)=>segs.Last,
    //                picker: (Random rnd, LinkedList<Seg> segs,  LinkedListNode<Seg> previous,  LinkedListNode<Seg> next, LinkedListNode<Seg> newseg, bool created) =>
    //                {
    //                    if (rnd.Next(3)==0){
    //                        return previous;
    //                    }
    //                    return previous?.Previous;
    //                }
    //            ),
    //        };
    //        return pickers.OrderBy(el => el.Name).ToList();
    //    }
}

