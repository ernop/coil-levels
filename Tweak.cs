namespace coil
{
    public class Tweak
    {
        //tweak right or left
        public bool Right { get; set; }

        //steps into the segment to start
        public int Start { get; set; }

        //distance from the seg to go
        public int Len1 { get; set; }

        //distance to go parallel to the tweak
        public int Len2 { get; set; }

        public Dir Len1Dir { get; set; }

        public bool LongTweak { get; set; }

        public Tweak(bool right, int start, int len1, int len2, Dir len1dir, bool longTweak)
        {
            Right = right;
            Start = start;
            Len1 = len1;
            Len2 = len2;
            Len1Dir = len1dir;
            LongTweak = longTweak;
        }
    }
}