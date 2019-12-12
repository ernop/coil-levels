using System;
using System.Collections.Generic;
using System.Text;
using System;
using System.Linq;
using System.Collections.Generic;


namespace coil
{
    public static class TweakPickers
    {
        public static int OneFraction(int n, Random rnd)
        {
            if (rnd.Next(n) == 0)
            {
                return 1;
            }
            return 0;
        }

        //TODO I need segpickers too which would grab the longest segs / long*index order
        public static IEnumerable<TweakPicker> GetPickers(string name)
        {
            var globalRand = new System.Random(0);
            var pickers = new List<TweakPicker>() {
                //new TweakPicker((List<Tweak> tweaks) =>
                //    {
                //        var ordered = tweaks.OrderByDescending(tw=>tw.SegNode.Value.Dir==Dir.Right ? -1*tw.Len2 : tw.Len2+tw.Len3);
                //        return ordered.First();
                //    } , "dir-right-sz"),
                new TweakPicker((List<Tweak> tweaks) =>
                    {
                        var ordered = tweaks.OrderByDescending(tw=>tw.Len2<11 ? 100+tw.Len2+tw.Len3 : tw.Len2+tw.Len3);
                        return ordered.First();
                    } , "2lim10"),
                new TweakPicker((List<Tweak> tweaks) =>
                    {
                        var ordered = tweaks.OrderByDescending(tw=>tw.Len2<6 ? 100+tw.Len2+tw.Len3 : tw.Len2+tw.Len3);
                        return ordered.First();
                    } , "2lim5"),
                new TweakPicker((List<Tweak> tweaks) =>
                    {
                        var ordered = tweaks.OrderByDescending(tw=>tw.Len2<26 ? 1000+tw.Len2+tw.Len3 : tw.Len2+tw.Len3);
                        return ordered.First();
                    } , "2lim25"),
                new TweakPicker((List<Tweak> tweaks) =>
                    {
                        var ordered = tweaks.OrderByDescending(tw=>tw.Len2<26 ? 1000+tw.Len2+tw.Len3 : tw.Len2+tw.Len3);
                        return ordered.First();
                    } , "2lim25"),
                new TweakPicker((List<Tweak> tweaks) =>
                    {
                        var ordered = tweaks.OrderByDescending(tw=>tw.Len2<101 ? 1000+tw.Len2 : tw.Len2+tw.Len3);
                        return ordered.First();
                    } , "2lim100"),
                new TweakPicker((List<Tweak> tweaks) =>
                    {
                        var ordered = tweaks.OrderByDescending(tw=>tw.Len2<51 ? 1000+tw.Len2+tw.Len3 : tw.Len2+tw.Len3);
                        return ordered.First();
                    } , "2lim50"),
                new TweakPicker((List<Tweak> tweaks) =>
                    {
                        var ordered = tweaks.OrderByDescending(tw=>tw.Len3<51 ? 1000+tw.Len2+tw.Len3 : tw.Len2+tw.Len3);
                        return ordered.First();
                    } , "3lim50"),
                new TweakPicker((List<Tweak> tweaks) =>
                    {
                        var ordered = tweaks.OrderByDescending(tw=>tw.Len3<11 ? 1000+tw.Len2+tw.Len3 : tw.Len2+tw.Len3);
                        return ordered.First();
                    } , "3lim10"),
                 new TweakPicker((List<Tweak> tweaks) =>
                    {
                        var ordered = tweaks.OrderByDescending(tw=>tw.Len3<11 && tw.Len2<11 ? 1000+tw.Len2+tw.Len3 : tw.Len2+tw.Len3);
                        return ordered.First();
                    } , "23lim10"),
                  new TweakPicker((List<Tweak> tweaks) =>
                    {
                        return tweaks.OrderByDescending(tw=>tw.Len3<26 && tw.Len2<26 ? 1000+tw.Len2+tw.Len3 : tw.Len2+tw.Len3).First();
                    } , "23lim25"),
                   new TweakPicker((List<Tweak> tweaks) =>
                    {
                        return tweaks.OrderByDescending(tw=>tw.Len3<61 && tw.Len2<61 ? 1000+tw.Len2+tw.Len3+globalRand.Next(25) : tw.Len2+tw.Len3)
                        .First();
                    } , "23lim60"),
                   new TweakPicker((List<Tweak> tweaks) =>
                    {
                        return tweaks.OrderByDescending(tw=>tw.Len3<31 && tw.Len2<31 ? 1000+tw.Len2+tw.Len3+globalRand.Next(25) : tw.Len2+tw.Len3)
                        .First();
                    } , "23lim30"),
                   new TweakPicker((List<Tweak> tweaks) =>
                    {
                        return tweaks
                            .OrderByDescending(tw=>tw.Len2+tw.Len3+globalRand.Next(5)).FirstOrDefault();
                    } , "shortrnd", null, 5, 5),
                   new TweakPicker((List<Tweak> tweaks) =>
                    {

                        var subtweaks = tweaks.Where(tw=>tw.Len2<=5 && tw.Len3 <=5);
                        if (!subtweaks.Any())
                        {
                            return null;
                        }
                        var best = subtweaks.First();
                        var score = best.Len2+best.Len3+globalRand.Next(5);
                        foreach (var tweak in subtweaks)
                        {
                            var candidateScore = tweak.Len2+tweak.Len3+globalRand.Next(5);
                            if (candidateScore > score)
                            {
                                best=tweak;
                                score=candidateScore;
                            }
                        }
                        return best;
                    } , "sz23-opt", 5, 5, 5),

                new TweakPicker((List<Tweak> tweaks) =>
                    {
                        return tweaks.OrderByDescending(tw=>tw.Len2<51 ? 1000+tw.Len2+tw.Len3+OneFraction(10, globalRand) : tw.Len2)
                        .First();
                    } , "len2lim50rnd"),
                new TweakPicker((List<Tweak> tweaks) =>
                    {
                        var ordered = tweaks.OrderByDescending(tw=>tw.Len2==tw.Len3 ? 1000+tw.Len2+tw.Len3+OneFraction(10, globalRand) : tw.Len2);
                        return ordered.First();
                    } , "equal23"),
                new TweakPicker((List<Tweak> tweaks) =>
                    {
                        var ordered = tweaks.OrderByDescending(tw=>
                        tw.Len2==tw.Len3 && tw.Len2 <=5 && tw.Len3 < 5
                            ? 1000+tw.Len2+tw.Len3
                            : tw.Len2+tw.Len3);
                        return ordered.First();
                    } , "equal23short", 5, 5, 5),
                new TweakPicker((List<Tweak> tweaks) =>
                    {
                        var ordered = tweaks.OrderByDescending(tw=>tw.Len1==tw.SegNode.Value.Len-tw.Len1-tw.Len2? 1000+tw.Len2+tw.Len3+OneFraction(10, globalRand) : tw.Len2);
                        return ordered.First();
                    } , "equalrem"),
                //new TweakPicker((List<Tweak> tweaks) =>
                //    {
                //        if (globalRand.Next(10) == 0)
                //        {
                //            return tweaks[globalRand.Next(tweaks.Count)];
                //        }
                //        var ordered = tweaks.OrderByDescending(tw=>tw.Len2<51 ? 1000+tw.Len2+tw.Len3 : tw.Len2);
                //        return ordered.First();
                //    } , "rand-50-tenthrandom"),
                new TweakPicker((List<Tweak> tweaks) => tweaks.First(), "first"),
                new TweakPicker((List<Tweak> tweaks) => tweaks.Last(), "last"),

                new TweakPicker((Tweak tw) => tw.Len1, "len1"),
                //new TweakPicker((Tweak tw) => tw.Len2, "len2"),
                //new TweakPicker((Tweak tw) => tw.Len3, "len3"),
                //new TweakPicker((Tweak tw) => tw.Len1 + tw.Len3, "len13"),
                //new TweakPicker((Tweak tw) => tw.Len1 + tw.Len2, name:"len12"),
                new TweakPicker((Tweak tw) => tw.Len2 + tw.Len3, "len23"),
                new TweakPicker((Tweak tw) => tw.Len2-tw.Len1, "sz2-1"),
                new TweakPicker((Tweak tw) => tw.Len2-tw.Len3, "sz2-3"),
                new TweakPicker((Tweak tw) => tw.Len3-tw.Len1, "sz3-1"),
                new TweakPicker((Tweak tw) => tw.Len3-tw.Len2, "sz3-2"),
                //new TweakPicker((Tweak tw) => tw.Len1-tw.Len2, "sz1-2"),
                new TweakPicker((Tweak tw) => tw.Len1-tw.Len2-tw.Len3, "sz1-23"),
                new TweakPicker((Tweak tw) => tw.Len2-tw.Len1-tw.Len3, "sz2-13"),

                new TweakPicker((Tweak tw) => tw.Len2 + tw.Len3 - tw.Len1, "sz23-1"),
                new TweakPicker((Tweak tw) => tw.Len2 + tw.Len3 + globalRand.Next(2), "sz23rnd2"),
                new TweakPicker((Tweak tw) => tw.Len2 + tw.Len3 + globalRand.Next(3), "sz23rnd3"),
                new TweakPicker((Tweak tw) => tw.Len2 + tw.Len3 + OneFraction(2, globalRand), "len23-half"),
                new TweakPicker((Tweak tw) => tw.Len2 + tw.Len3 + OneFraction(3, globalRand), "len23-third"),
                new TweakPicker((Tweak tw) => tw.Len2 + tw.Len3 + OneFraction(5, globalRand), "len23-10th"),
                new TweakPicker((Tweak tw) => tw.Len2 + tw.Len3 + OneFraction(10, globalRand), "len23-10th"),
                new TweakPicker((Tweak tw) => tw.Len2 + tw.Len3 + OneFraction(20, globalRand), "len23-20th"),
                //new TweakPicker((Tweak tw) => tw.Right ? 1000+tw.Len2 + tw.Len3 : -1*(1000+tw.Len2 + tw.Len3), "turndir-right-sz"),
                //new TweakPicker((Tweak tw) => tw.Right ? -1*(1000 + tw.Len2 + tw.Len3) : 1000 + tw.Len2 + tw.Len3, "turndir-left-sz"),
                new TweakPicker((Tweak tw) => globalRand.Next(100), "rnd100lim5", null, 5, 5),
                new TweakPicker((Tweak tw) => globalRand.Next(100), "rnd100lim20", null, 20, 20),
                new TweakPicker((Tweak tw) => globalRand.Next(100), "rnd100"),
                new TweakPicker((Tweak tw) => tw.Len2+tw.Len3+globalRand.Next(100), "len23rnd100"),
                new TweakPicker((Tweak tw) => globalRand.Next(99), "rnd99"),
                new TweakPicker((Tweak tw) => globalRand.Next(3), "rnd3"),
                new TweakPicker((Tweak tw) => tw.Len1 + globalRand.Next(3), "len1rnd3"),
                new TweakPicker((Tweak tw) => tw.Len1 +tw.Len2 + globalRand.Next(3), "len12rnd3"),
                new TweakPicker((Tweak tw) => tw.Len2+ tw.Len3 + globalRand.Next(3), "len23rnd3"),
                //new TweakPicker((Tweak tw) => (tw.SegNode.Value.Start.Item1 % 50<25 ? 10 : 0) + globalRand.Next(3), "partition-rand3"),
                new TweakPicker((Tweak tw) => globalRand.Next(5), "rnd5"),
                new TweakPicker((Tweak tw) => globalRand.Next(2), "rnd2"),
                new TweakPicker((Tweak tw) => tw.Len2%2==0?tw.Len2+tw.Len3 : -1*(tw.Len2+tw.Len3), "even"),
                new TweakPicker((Tweak tw) => tw.Len2%2==1?tw.Len2+tw.Len3 : -1 * (tw.Len2 + tw.Len3), "odd"),

                new TweakPicker((List<Tweak> tweaks) =>
                    {
                        var ordered = tweaks.OrderByDescending(tw=>tw.Len2+tw.Len3);
                        if (globalRand.Next(10) == 0)
                        {
                            return ordered.Last();
                        }
                        return ordered.First();
                    } , "1stlast10"),
                new TweakPicker((List<Tweak> tweaks) =>
                    {
                        var ordered = tweaks.OrderByDescending(tw=>tw.Len2+tw.Len3);
                        if (globalRand.Next(2) == 0)
                        {
                            return ordered.Last();
                        }
                        return ordered.First();
                    } , "1stlast2"),
                new TweakPicker((List<Tweak> tweaks) =>
                    {
                        var ordered = tweaks.OrderByDescending(tw=>tw.Len2+tw.Len3);
                        var index = Math.Min(tweaks.Count-1, globalRand.Next(4));
                        return ordered.Skip(index).First();
                    } , "len23-1stfour-sz"),
                new TweakPicker((List<Tweak> tweaks) =>
                    {
                        var ordered = tweaks.OrderByDescending(tw=>tw.Len2+tw.Len3);
                        var mid = ordered.Count()/2;
                        return ordered.Skip(mid).First();
                    } , "len23mid"),
                new TweakPicker((List<Tweak> tweaks) =>
                    {
                        var ordered = tweaks.OrderByDescending(tw=>tw.Len2+tw.Len3);
                        var mid = ordered.Count()/3;
                        return ordered.Skip(mid).First();
                    } , "len23-3rd"),
                new TweakPicker((List<Tweak> tweaks) =>
                    {
                        var ordered = tweaks.OrderByDescending(tw=>tw.Len2+tw.Len3);
                        var mid = ordered.Count()/5;
                        return ordered.Skip(mid).First();
                    } , "len23-5th"),
                //new TweakPicker((List<Tweak> tweaks) =>
                //    {
                //        var ordered = tweaks.OrderByDescending(tw=>tw.Len2+tw.Len3);
                //        var mid = ordered.Count()/6;
                //        return ordered.Skip(mid).First();
                //    } , "order-sixth-sz"),
                //new TweakPicker((List<Tweak> tweaks) =>
                //    {
                //        var ordered = tweaks.OrderByDescending(tw=>tw.Len2+tw.Len3+OneFraction(10, globalRand));
                //        var mid = ordered.Count()/6;
                //        return ordered.Skip(mid).First();
                //    } , "order-sixth-rndtenth-sz"),
                //new TweakPicker((List<Tweak> tweaks) =>
                //    {
                //        var ordered = tweaks.OrderByDescending(tw=>tw.Len2-tw.Len1);
                //        var mid = ordered.Count()/6;
                //        return ordered.Skip(mid).First();
                //    } , "slice-sixthize2-1"),
                //new TweakPicker((List<Tweak> tweaks) =>
                //    {
                //        var ordered = tweaks.OrderByDescending(tw=>tw.Len2+tw.Len3);
                //        var mid = ordered.Count()/2;
                //        return ordered.Skip(mid).First();
                //    } , "order-quarter-sz"),
            };

            if (string.IsNullOrEmpty(name))
            {
                return pickers.OrderBy(p => p.Name);
            }
            return pickers.Where(p => p.Name == name);
        }
    }
}
