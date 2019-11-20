using System;
using System.Collections.Generic;

using SixLabors.ImageSharp;

using static coil.Navigation;

namespace coil
{
    public static class Util
    {
        public static List<Dir> HDirs = new List<Dir>() { Dir.Right, Dir.Left };
        public static List<Dir> VDirs = new List<Dir>() { Dir.Up, Dir.Down };
        public static List<Dir> AllDirs = new List<Dir>() { Dir.Up, Dir.Right, Dir.Down, Dir.Left };
        public static List<bool> Bools = new List<bool>() { true, false };
        public static List<bool> Bools2 = new List<bool>() { false, true };

        private static Dictionary<string, Image> _Images = ImageUtil.GetImages();

        //for cmdline
        public static void Show(Level l)
        {
            WL("SHOW:");
            var om = GetOutputMap(l);
            for (var y = 0; y < om.Count; y++)
            {
                for (var x = 0; x < om[0].Count; x++)
                {
                    W(om[y][x]);
                }

                W("\n");
            }
        }

        public static void SaveEmpty(Level level, string fn)
        {
            var baseMap = GetOutputMap(level);

            //remove h, s, e
            for (var y = 0; y < baseMap.Count; y++)
            {
                for (var x = 0; x < baseMap[0].Count; x++)
                {
                    if (baseMap[y][x] == "h")
                    {
                        baseMap[y][x] = "x";
                    }

                    if (baseMap[y][x] == "s")
                    {
                        baseMap[y][x] = ".";
                    }

                    if (baseMap[y][x] == "e")
                    {
                        baseMap[y][x] = ".";
                    }
                }
            }

            ImageUtil.Save(_Images, level, baseMap, fn);
        }

        public static void SaveWithPath(Level level, string fn)
        {
            var baseMap = GetOutputMap(level);
            var path = GetInOutStrings(level);
            var ins = path.Item1;
            var outs = path.Item2;
            for (var y = 0; y < baseMap.Count; y++)
            {
                for (var x = 0; x < baseMap[0].Count; x++)
                {
                    if (ins[y][x] != "")
                    {
                        baseMap[y][x] = ins[y][x] + outs[y][x];
                    }
                }
            }

            ImageUtil.Save(_Images, level, baseMap, fn);
        }

        //for image creation - just get the path parts - rest ""
        public static Tuple<List<List<string>>, List<List<string>>> GetInOutStrings(Level l)
        {
            var ins = new List<List<string>>();
            var outs = new List<List<string>>();
            for (var yy = 0; yy < l.Height; yy++)
            {
                ins.Add(new List<string>());
                outs.Add(new List<string>());
                for (var xx = 0; xx < l.Width; xx++)
                {
                    ins[yy].Add("");
                    outs[yy].Add("");
                }
            }

            foreach (var seg in l.Segs)
            {
                var len = 0;
                var target = seg.Start;
                var dirstring = GetDString(seg.Dir);
                while (len < seg.Len)
                {
                    //you go out to target
                    outs[target.Item2][target.Item1] = dirstring;
                    target = Add(target, seg.Dir);
                    ins[target.Item2][target.Item1] = dirstring;
                    len++;
                }
            }

            var start = l.Segs.First.Value.Start;
            ins[start.Item2][start.Item1] = "s";

            var end = GetEnd(l.Segs.Last.Value);
            outs[end.Item2][end.Item1] = "e";

            return new Tuple<List<List<string>>, List<List<string>>>(ins, outs);
        }

        private static string GetDString(Dir dir)
        {
            switch (dir)
            {
                case Dir.Up:
                    return "u";

                case Dir.Right:
                    return "r";

                case Dir.Down:
                    return "d";

                case Dir.Left:
                    return "l";

                default:
                    throw new Exception("Bad dstring");
            }
        }

        public static T PopFirst<T>(IList<T> l)
        {
            var el = l[0];
            l.Remove(el);
            return el;
        }

        public static void Shuffle<T>(this IList<T> list, Random rnd)
        {
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = rnd.Next(n + 1);
                T value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
        }

        public static void ShowHit(Level l)
        {
            WL("HITS:");
            List<Seg> h = null;
            List<Seg> t = null;
            for (var yy = 0; yy < l.Height; yy++)
            {
                for (var xx = 0; xx < l.Width; xx++)
                {
                    if (l.Hits.ContainsKey((xx, yy)))
                    {
                        var hits = l.Hits[(xx, yy)];
                        if (hits.Count == 0)
                        {
                            W(".");
                        }
                        else if (hits.Count == 1)
                        {
                            Console.Write(hits[0].Index % 10);
                        }
                        else if (hits.Count == 2)
                        {
                            W("T");
                            t = hits;
                        }
                        else if (hits.Count == 3)
                        {
                            W("H");
                            h = hits;
                        }
                    }
                    else
                    {
                        W(".");
                    }
                }

                W("\n");
            }

            if (h != null)
            {
                WL("H:");
                foreach (var el in h)
                {
                    WL(el.ToString());
                }
            }

            if (t != null)
            {
                WL("t:");
                foreach (var el in t)
                {
                    WL(el.ToString());
                }
            }
        }

        public static void ShowSeg(Level l)
        {
            WL("SEG");
            for (var yy = 0; yy < l.Height; yy++)
            {
                for (var xx = 0; xx < l.Width; xx++)
                {
                    if (l.Rows.ContainsKey((xx, yy))
                        && l.Rows[(xx, yy)] != null)
                    {
                        Console.Write(l.Rows[(xx, yy)].Index % 10);
                    }
                    else
                    {
                        Console.Write(".");
                    }
                }

                Console.Write("\n");
            }
        }

        public static List<List<string>> GetOutputMap(Level l)
        {
            var hasStart = false;
            (int, int) start = (0, 0);
            (int, int) end = (0, 0);
            if (l.Segs.Count > 0)
            {
                start = l.Segs.First.Value.Start;
                end = GetEnd(l.Segs.Last.Value);
                hasStart = true;
            }

            var outputMap = new List<List<string>>();

            for (var yy = 0; yy < l.Height; yy++)
            {
                var row = new List<string>();
                for (var xx = 0; xx < l.Width; xx++)
                {
                    var point = (xx, yy);
                    bool edge = xx == 0 || xx == l.Width - 1 || yy == 0 || yy == l.Height - 1;

                    if (hasStart)
                    {
                        if (yy == start.Item2 && xx == start.Item1)
                        {
                            row.Add("s");
                            continue;
                        }

                        if (yy == end.Item2 && xx == end.Item1)
                        {
                            row.Add("e");
                            continue;
                        }
                    }

                    var hit = l.Hits[point];
                    var val = l.Rows[point];
                    if (val != null)
                    {
                        row.Add(".");
                    }
                    else if (hit!.Count > 0)
                    {
                        row.Add("h");
                    }
                    else if (edge)
                    {
                        row.Add("b");
                    }

                    else if (val == null)
                    {
                        row.Add("x");
                    }
                }

                outputMap.Add(row);
            }

            return outputMap;
        }

        public static void W(string s)
        {
            Console.Write(s);
        }

        public static void WL(string s)
        {
            Console.WriteLine(s);
        }
    }
}
