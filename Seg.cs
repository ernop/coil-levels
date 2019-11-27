using System.Collections.Generic;
using static coil.Navigation;

namespace coil
{
    public class Seg
    {
        public (int, int) Start { get; set; }
        public Dir Dir { get; private set; }

        //a seg covering 2 squars has a length of 1.  squares "covered" is len-1
        public int Len { get; set; }

        //seems quite bad to manually maintain an index but this is way way faster than constantly doing IndexOf in the list.
        //but, that suggests I can just use a linkedList for Segs...
        public uint Index { get; set; }

        public short FailCount { get; set; } = 0;

        public List<(int,int)> GetSqs()
        {
            var res = new List<(int, int)>();
            for (var ii = 0; ii <= Len; ii++)
            {
                res.Add(Add(Start, Dir, ii));
            }
            return res;
        }
        
        //should use these rather than constantly re-calculate it.
        public (int,int) GetEnd()
        {
            return Add(Start, Dir, Len);
        }

        public (int,int) GetHit()
        {
            return Add(Start, Dir, Len + 1);
        }

        public Seg((int, int) start, Dir dir, int len)
        {
            Start = start;
            Dir = dir;
            Len = len;
        }

        public override string ToString()
        {
            return $"Seg{Index,5}: {Start} going {Dir,6} ({Len})";
        }
    }
}