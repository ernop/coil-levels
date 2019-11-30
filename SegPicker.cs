using System.Collections.Generic;

namespace coil
{
    //TODO restore the smart functionality we had earlier
    //basically, we want to treat segs from the back so that they can push closer, and not have to increment indexes as much.
    //the former benefit will never go away.

    /// <summary>
    /// As you iterate, continuously tweaking segs, which seg should you actually tweak next?
    /// This depends in complex ways on the data structure used to store tweaks
    /// As of now using a linkedlist makes it a big pain to navigate around, especially since you're constantly removing segs.
    /// </summary>

    public abstract class SegPickerBase
    {
        public virtual string Name { get; set; }
        public LinkedListNode<Seg> PreviousSeg { get; set; }
        public LinkedListNode<Seg> NextSeg { get; set; }
        public LinkedList<Seg> Segs { get; set; }

        public string GetStr()
        {
            return Name;
        }
    }

    public class BackwardSegPicker : SegPickerBase, ISegPicker
    {
        public LinkedListNode<Seg> PickSeg(LinkedList<Seg> segs, LinkedListNode<Seg> previousSeg, LinkedListNode<Seg> nextSeg)
        {
            return previousSeg;
        }
    }

    public class ThresholdSegPicker : SegPickerBase, ISegPicker
    {
        private int threshold = 100;
        public string Name { get; set; } = "threshold";
        public LinkedListNode<Seg> PickSeg(LinkedList<Seg> segs, LinkedListNode<Seg> previousSeg, LinkedListNode<Seg> nextSeg)
        {
            while (previousSeg != null)
            {
                if (previousSeg.Value.Len < threshold)
                {
                    previousSeg = previousSeg.Previous;
                }
                return previousSeg;
            }
            threshold -= 10;
            return null;
        }
    }

    public static class SegPickers
    {
        public static List<ISegPicker> GetSegPickers(LinkedListNode<Seg> segs)
        {
            return new List<ISegPicker>()
            {
                new BackwardSegPicker(),
                new ThresholdSegPicker()
            };
        }
    }


    //we will create a segpicker for a run and continuously hit it to decide next seg.
    //possibilities: forward, backward, extend recent ones, decreasing threshold
    public interface ISegPicker
    {
        LinkedListNode<Seg> PickSeg(LinkedList<Seg> segs, LinkedListNode<Seg> previousSeg, LinkedListNode<Seg> nextSeg);
    }
}
