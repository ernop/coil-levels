using System.Collections.Generic;

namespace coil
{
    //A candidate modification of one seg. A struct: GetTweaks creates millions of these per level and most are
    //discarded unpicked.
    public readonly struct Tweak
    {
        //tweak right or left
        public bool Right { get; }

        public LinkedListNode<Seg> SegNode { get; }
        
        //steps into the segment to start
        public int Len1 { get; }

        //distance from the seg to go
        public int Len2 { get; }

        //distance to go parallel to the tweak
        public int Len3 { get; }

        public Dir Len2dir { get; }

        //start==0
        public bool ShortTweak { get; }

        //seg4==null
        public bool LongTweak { get; }

        public Tweak(LinkedListNode<Seg> segnode, bool right, int len1, int len2, int len3, Dir len2dir)
        {
            SegNode = segnode;
            Right = right;
            Len1 = len1;
            Len2 = len2;
            Len3 = len3;
            Len2dir = len2dir;
            ShortTweak = len1==0;
            LongTweak = len1+len3==segnode.Value.Len;
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