using System;
using System.Collections.Generic;
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

    public class SegPicker
    {
        public string Name { get; }
        public LinkedListNode<Seg> PreviousSeg { get; set; }
        public LinkedListNode<Seg> NextSeg { get; set; }
        public LinkedList<Seg> Segs { get; set; }
        public System.Random Random { get; set; }

        public string GetName() { return Name; }

        public Func<LinkedList<Seg>, LinkedListNode<Seg>> InitialPick;
        private Func<Random, LinkedList<Seg>, LinkedListNode<Seg>, LinkedListNode<Seg>, LinkedListNode<Seg>, bool, LinkedListNode<Seg>> InnerPicker;

        public SegPicker(int seed,
            string name,
            
            Func<LinkedList<Seg>, LinkedListNode<Seg>> initialPicker,
            Func<Random, LinkedList<Seg>, LinkedListNode<Seg>, LinkedListNode<Seg>, LinkedListNode<Seg>, bool, LinkedListNode<Seg>> picker) //the context for the pick
        {
            Random = new System.Random(seed);
            Name = name;
            InitialPick = initialPicker;
            InnerPicker = picker;
        }

        public LinkedListNode<Seg> Pick(LinkedList<Seg> segs, LinkedListNode<Seg> prev, LinkedListNode<Seg> next, LinkedListNode<Seg> newseg, bool justCreated)
        {
            return InnerPicker.Invoke(Random, segs, prev, next, newseg, justCreated);
        }
    }

    public static class SegPickers
    {
        public static List<SegPicker> GetSegPickers(int seed)
        {
            var pickers = new List<SegPicker>()
            {
                new SegPicker(seed: seed,
                    name:"Previous",
                    initialPicker: (LinkedList<Seg> segs)=>segs.Last,
                    picker: (Random rnd, LinkedList<Seg> segs,  LinkedListNode<Seg> previous,  LinkedListNode<Seg> next, LinkedListNode<Seg> newseg, bool created)=>previous
                ),
                new SegPicker(seed: seed,
                    name:"New",
                    initialPicker: (LinkedList<Seg> segs)=>segs.Last,
                    picker: (Random rnd, LinkedList<Seg> segs,  LinkedListNode<Seg> previous,  LinkedListNode<Seg> next, LinkedListNode<Seg> newseg, bool created) =>
                    {
                        if (created)
                        {
                            return newseg;
                        }
                        return previous;
                    }
                ),
                new SegPicker(seed: seed,
                    name:"Next",
                    initialPicker: (LinkedList<Seg> segs)=>segs.Last,
                    picker: (Random rnd, LinkedList<Seg> segs,  LinkedListNode<Seg> previous,  LinkedListNode<Seg> next, LinkedListNode<Seg> newseg, bool created) =>
                    {
                        //if we modified a seg, do the next seg cause room might have opened up. This is aiming to get a more complete pass the first time.
                        //you'll naturally fall through to the next.
                        if (created)
                        {
                            return next;
                        }
                        return previous;
                    }
                ),
                 new SegPicker(seed: seed,
                    name:"NextR",
                    initialPicker: (LinkedList<Seg> segs)=>segs.Last,
                    picker: (Random rnd, LinkedList<Seg> segs,  LinkedListNode<Seg> previous,  LinkedListNode<Seg> next, LinkedListNode<Seg> newseg, bool created) =>
                    {
                        //if we modified a seg, do the next seg cause room might have opened up. This is aiming to get a more complete pass the first time.
                        //you'll naturally fall through to the next.
                        if (created){
                            if (rnd.Next(5) == 0)
                            {
                                return newseg;
                            }
                            else
                            {
                                return next;
                            }
                        }
                        
                        return previous;
                    }
                ),

            new SegPicker(seed: seed,
                    name:"NewR",
                    initialPicker: (LinkedList<Seg> segs)=>segs.Last,
                    picker: (Random rnd, LinkedList<Seg> segs,  LinkedListNode<Seg> previous,  LinkedListNode<Seg> next, LinkedListNode<Seg> newseg, bool created) =>
                    { 
                        if (newseg != null && rnd.Next(2)==0)
                        {
                            return newseg;
                        }
                        
                        //theory: this is doing a bunch of useless work.
                        return previous;
                    }
                ),
                new SegPicker(seed: seed,
                    name:"Bigger",
                    initialPicker: (LinkedList<Seg> segs)=>segs.Last,
                    picker: (Random rnd, LinkedList<Seg> segs,  LinkedListNode<Seg> previous,  LinkedListNode<Seg> next, LinkedListNode<Seg> newseg, bool created) =>
                    {
                        var ii = 0;
                        var choices = new List<LinkedListNode<Seg>>();
                        while (ii < 10 && previous != null)
                        {
                            choices.Add(previous);
                            ii++;
                        }
                        if (!choices.Any())
                        {
                            return null;
                        }
                        return choices.OrderByDescending(el=>el.Value.Len).First();
                    }
                ),
                new SegPicker(seed: seed,
                    name:"PreviousR",
                    initialPicker: (LinkedList<Seg> segs)=>segs.Last,
                    picker: (Random rnd, LinkedList<Seg> segs,  LinkedListNode<Seg> previous,  LinkedListNode<Seg> next, LinkedListNode<Seg> newseg, bool created) =>
                    {
                        if (rnd.Next(3)==0){
                            return previous;
                        }
                        return previous?.Previous;
                    }
                ),
            };
            return pickers.OrderBy(el => el.Name).ToList();
        }
    }
}
