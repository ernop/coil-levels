using System.Collections.Generic;

namespace coil
{
    public class Seg
    {
        public (int, int) Start { get; private set; }
        public Dir Dir { get; private set; }

        //a seg covering 2 squars has a length of 1.  squares "covered" is len-1
        public int Len { get; set; }

        //seems quite bad to manually maintain an index but this is way way faster than constantly doing IndexOf in the list.
        //but, that suggests I can just use a linkedList for Segs...
        public int Index { get; set; }
        public Seg((int, int) start, Dir dir, int len)
        {
            Start = start;
            Dir = dir;
            Len = len;
        }

        public override string ToString()
        {
            return $"Seg{Index,5}: {Start} going {Dir,4} ({Len})";
        }
    }
}