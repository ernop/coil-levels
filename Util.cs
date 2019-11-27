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

        public static void SaveEmpty(Level level, string fn, string subtitle="", bool quiet = false)
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

            ImageUtil.Save(_Images, level, baseMap, fn, subtitle, quiet);
        }

        public static void SaveWithPath(BaseLevel level, string fn, string subtitle = "", bool quiet = false)
        {
            var baseMap = GetOutputMap(level);
            var path = GetInOutStrings(level);
            var decisions = GetDecisions(level);
            var easyDecisions = decisions.Item1;
            var hardDecisions = decisions.Item2;
            var ins = path.Item1;
            var outs = path.Item2;
            //TODO add in decision markers.
            //throw new Exception();
            for (var y = 0; y < baseMap.Count; y++)
            {
                for (var x = 0; x < baseMap[0].Count; x++)
                {
                    if (ins[y][x] != "")
                    {
                        
                        baseMap[y][x] = ins[y][x] + outs[y][x];
                        if (hardDecisions.Contains((x,y)))
                        {
                            if (baseMap[y][x].Length != 2)
                            {
                                WL("Bad basemap len");
                            }
                            baseMap[y][x] += "-hard";
                        }
                    }
                }
            }

            ImageUtil.Save(_Images, level, baseMap, fn, subtitle, quiet);
        }

        //for image creation - just get the path parts - rest ""
        public static Tuple<List<List<string>>, List<List<string>>> GetInOutStrings(BaseLevel l)
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
                    if (l.Hits.Contains((xx, yy)))
                    {
                        var hits = l.Hits.Get((xx, yy));
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

        public static List<List<string>> GetOutputMap(BaseLevel l)
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

                    var hit = l.Hits.Get(point);
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

        public static void W(object s)
        {
            Console.Write(s.ToString());
        }

        public static void WL(object s)
        {
            Console.WriteLine(s.ToString());
        }

        public static (HashSet<(int,int)>,HashSet<(int, int)>) GetDecisions(BaseLevel level)
        {
            //one side is an obvious dead end.
            var easyDecisions = new List<(int,int)>();
            
            //those where one side isn't an obvious dead end.
            var hardDecisions = new List<(int, int)>();
            foreach (var seg in level.Segs)
            {
                var end = seg.GetEnd();
                var right = Add(end, Rot(seg.Dir));
                var left = Add(end, ARot(seg.Dir));

                //decision if left and right are empty, and the seg that fills them has index greater than current.
                if (level.Rows[right] != null && level.Rows[left] != null
                    && level.Rows[right].Index > seg.Index && level.Rows[left].Index > seg.Index)
                {
                    //we have a decision to make!
                    //TODO hmm how to implement this with an untouched board?
                    hardDecisions.Add(end);
                }
            }
            return (new HashSet<(int,int)>(easyDecisions), new HashSet<(int, int)>(hardDecisions));
        }

        public static string Report(Level level, TimeSpan ts)
        {
            var sqs = (level.Height - 2) * (level.Width - 2);
            var sum = 1;
            var decisions = GetDecisions(level);
            var easyDecisions = decisions.Item1;
            var hardDecisions = decisions.Item2;
            var decisionCount = easyDecisions.Count + hardDecisions.Count;
            foreach (var seg in level.Segs)
            {
                sum += seg.Len;
            }
            var perc = 100.0 * sum / sqs;
            var decisionPercent = 100.0 * decisionCount / level.Segs.Count;
            //TODO determine how many "decisions" have to be made. More decisions == better!

            return $"{level.Width}x{level.Height} {level.LevelConfiguration.GetStr()} {ts.TotalSeconds.ToString("0.0")}s " +
                $"segs={level.Segs.Count} cov={perc.ToString("##0.0")}% " +
                $"dec={decisionPercent.ToString("0.0")}% ({decisionCount})";
        }
    }
}
