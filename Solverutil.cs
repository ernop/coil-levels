using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using static coil.Navigation;

namespace coil
{
    /// <summary>
    /// not writing a solver, but some analytics require solver-like things.
    /// </summary>
    public static class Solverutil
    {
        public static List<(int, int)> GetNeighbors((int, int) pt)
        {
            return new List<(int, int)>() { (pt.Item1, pt.Item2-1),
                (pt.Item1+1, pt.Item2),
                (pt.Item1, pt.Item2+1),
                (pt.Item1-1, pt.Item2),};

        }

        public static List<(int, int)> GetNeighbors(Seg seg)
        {
            var res = new List<(int, int)>();
            
            res.Add(Add(seg.Start, Rot(Rot(seg.Dir))));

            var candidate = seg.Start;
            var dirs = new List<Dir>() { Rot(seg.Dir), ARot(seg.Dir) };
            var ii = 0;
            while (ii <= seg.Len)
            {
                foreach (var dir in dirs) {
                    res.Add(Add(candidate, dir));
                }
                ii++;
                candidate = Add(candidate, seg.Dir);
            }
            res.Add(candidate);
            return res;
        }

        public static HashSet<Seg> GetNeighboringSegs(Level level, Seg seg)
        {
            var res = new HashSet<Seg>(new SegComparer());
            var neighbors = GetNeighbors(seg);
            
            foreach (var nei in neighbors)
            {
                var neiseg = level.GetRowValue(nei);
                if (neiseg != null)
                {
                    res.Add(neiseg);
                }
            }
            return res;
        }

        public class SegComparer : IEqualityComparer<Seg>
        {
            public bool Equals([AllowNull] Seg x, [AllowNull] Seg y)
            {
                return x.Index == y.Index;
            }

            public int GetHashCode([DisallowNull] Seg obj)
            {
                return obj.Index.GetHashCode();
            }
        }
    }
}
