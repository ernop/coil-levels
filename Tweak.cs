using System.Collections.Generic;

namespace coil
{
    public class Tweak
    {
        //tweak right or left
        public bool Right { get; set; }

        public LinkedListNode<Seg> SegNode { get; set; }
        
        //steps into the segment to start
        public int Len1 { get; set; }

        //distance from the seg to go
        public int Len2 { get; set; }

        //distance to go parallel to the tweak
        public int Len3 { get; set; }

        public Dir Len2dir { get; set; }

        //start==0
        public bool ShortTweak{ get; set; }

        //seg4==null
        public bool LongTweak { get; set; }

        public Tweak(LinkedListNode<Seg> segnode, bool right, int len1, int len2, int len3, Dir len2dir, bool shortTweak, bool longTweak)
        {
            SegNode = segnode;
            Right = right;
            Len1 = len1;
            Len2 = len2;
            Len3 = len3;
            Len2dir = len2dir;
            ShortTweak = shortTweak;
            LongTweak = longTweak;
        }

        public override string ToString()
        {
            var rstr = Right ? "R" : "L";
            var sstr = ShortTweak ? "S" : "";
            var lstr = LongTweak? "L" : "";
            return $"{rstr} {Len1},{Len2},{Len3} {sstr}{lstr}";
        }
    }
}