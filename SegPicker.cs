using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

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
        public bool Quiet { get; set; }
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

    /// <summary>
    /// Picks the seg with the largest Key each time. Segs live in a min-heap on -Key; removal is lazy: a seg's
    /// HeapStamp is bumped when it is removed or modified, and stale entries are skipped when popped.
    /// One pass over the heap is a "loop"; after a loop with at least one successful tweak, every seg is re-queued.
    /// </summary>
    public class ConfigurableSegPicker : SegPicker
    {
        public Func<Seg, double> Key;

        public ConfigurableSegPicker(string name, Func<Seg, double> key)
        {
            Name = name;
            Key = key;
        }

        public override void Init(int seed, Level level)
        {
            BaseInit(seed, level);
            RedoHeap();
            LoopStats = new TweakStats();
        }

        public TweakStats LoopStats { get; set; }

        public bool Success = false;

        /// <summary>Stop after this many full passes; each pass after the first yields far less coverage per second.</summary>
        public int MaxLoops { get; set; } = 3;

        private readonly struct Entry
        {
            public readonly LinkedListNode<Seg> Node;
            public readonly int Stamp;
            public Entry(LinkedListNode<Seg> node, int stamp) { Node = node; Stamp = stamp; }
        }

        private PriorityQueue<Entry, double> Heap;

        public override LinkedListNode<Seg> PickSeg(List<LinkedListNode<Seg>> newSegs, List<LinkedListNode<Seg>> modifiedSegs, TweakStats stat, bool success)
        {
            if (newSegs != null)
            {
                foreach (var newSeg in newSegs)
                {
                    Enqueue(newSeg);
                }
            }
            if (modifiedSegs != null)
            {
                foreach (var modseg in modifiedSegs)
                {
                    //re-key: the tweak changed this seg's length. Stamp bump makes the old entry stale.
                    modseg.Value.HeapStamp++;
                    Enqueue(modseg);
                }
            }
            if (success)
            {
                LoopStats.SuccessCt++;
                Success = true;
            }
            else
            {
                LoopStats.NoTweaks++;
            }

            while (true)
            {
                if (Heap.Count == 0)
                {
                    if (!Success)
                    {
                        return null;
                    }
                    Success = false;
                    stat.loopct++;
                    var lastLoopSuccessPercentage = LoopStats.SuccessCt * 1.0 / (LoopStats.SuccessCt + LoopStats.NoTweaksQualify + LoopStats.NoTweaks);
                    if (!Quiet)
                    {
                        WL($"loop {stat.loopct} done: tweak success {lastLoopSuccessPercentage:0.0%}");
                    }
                    if (stat.loopct >= MaxLoops)
                    {
                        return null;
                    }
                    RedoHeap();
                }
                var el = Heap.Dequeue();
                if (el.Stamp == el.Node.Value.HeapStamp && el.Node.List != null)
                {
                    //popped: any later entry for this seg (there should be none) must not be honored.
                    el.Node.Value.HeapStamp++;
                    return el.Node;
                }
            }
        }

        private void RedoHeap()
        {
            Heap = new PriorityQueue<Entry, double>(Level.Segs.Count);
            var el = Level.Segs.First;
            while (el != null)
            {
                Enqueue(el);
                el = el.Next;
            }
        }

        private void Enqueue(LinkedListNode<Seg> node)
        {
            Heap.Enqueue(new Entry(node, node.Value.HeapStamp), -Key(node.Value));
        }
    }

    public static class SegPickers
    {
        private static double Order(Seg seg) => seg.Index / 4294967296.0;

        public static IEnumerable<SegPicker> GetSegPickers(string name)
        {
            var pickers = new List<SegPicker>() { 
                new ConfigurableSegPicker("Longest", seg => seg.Len),
                //Weighted*: later segs first, tempered by a root; Order() rescales the 64-bit index to the 32-bit
                //range these formulas were tuned in, so Len keeps the same relative weight.
                new ConfigurableSegPicker("Weighted", seg => Order(seg) + seg.Len),
                new ConfigurableSegPicker("Weighted2", seg => Math.Sqrt(Order(seg)) + seg.Len),
                new ConfigurableSegPicker("Weighted3", seg => Math.Sqrt(Math.Sqrt(Order(seg))) + seg.Len),
                new ConfigurableSegPicker("Weighted4", seg => Math.Sqrt(Math.Sqrt(Math.Sqrt(Order(seg)))) + seg.Len),
                new ConfigurableSegPicker("First", seg => -Order(seg)),
                new ConfigurableSegPicker("Last", seg => Order(seg)),
                //new NewSegPicker()
            };
            if (string.IsNullOrEmpty(name))
            {
                return pickers.OrderBy(p => p.Name);
            }
            return pickers.Where(p => p.Name == name);
        }
    }
}


