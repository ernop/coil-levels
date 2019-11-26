using System;
using System.Linq;
using System.Collections.Generic;

using static coil.Util;

namespace coil
{
    class Program
    {

        public static int Round(double n)
        {
            if (n >= 0.5)
            {
                return 1;
            }
            return 0;
        }
        public Func<List<Tweak>,Tweak> GetTweakPicker(float rate)
        {
            return (List<Tweak> tweaks) => tweaks.First();
        }

        public static int OneFraction(int n, Random rnd)
        {
            if (rnd.Next(n) == 0)
            {
                return 1;
            }
            return 0;
        }

        //TODO I need segpickers too which would grab the longest segs / long*index order
        public static List<TweakPicker> GetPickers()
        {
            var globalRand = new System.Random(0);
            var pickers = new List<TweakPicker>() {
                new TweakPicker((List<Tweak> tweaks) =>
                    {
                        var seg = tweaks.First().SegNode.Value.Start;
                        var ordered = tweaks.OrderByDescending(tw=>tw.Len2+tw.Len3);
                        
                        if (seg.Item1 % 30<15)
                        {
                            return ordered.First();
                        }
                        else
                        {
                            return ordered.Last();
                        }
                        
                    } , "sectional-verticalstripes"),
                new TweakPicker((List<Tweak> tweaks) =>
                    {
                        var ordered = tweaks.OrderByDescending(tw=>tw.SegNode.Value.Dir==Dir.Right ? -1*tw.Len2 : tw.Len2+tw.Len3);
                        return ordered.First();
                    } , "dir-right-size"),
                new TweakPicker((List<Tweak> tweaks) =>
                    {
                        var ordered = tweaks.OrderByDescending(tw=>tw.Len2<11 ? 100+tw.Len2+tw.Len3 : tw.Len2+tw.Len3);
                        return ordered.First();
                    } , "size-2limit10"),
                new TweakPicker((List<Tweak> tweaks) =>
                    {
                        var ordered = tweaks.OrderByDescending(tw=>tw.Len2<6 ? 100+tw.Len2+tw.Len3 : tw.Len2+tw.Len3);
                        return ordered.First();
                    } , "size-2limit5"),
                new TweakPicker((List<Tweak> tweaks) =>
                    {
                        var ordered = tweaks.OrderByDescending(tw=>tw.Len2<26 ? 1000+tw.Len2+tw.Len3 : tw.Len2+tw.Len3);
                        return ordered.First();
                    } , "size-2limit25"),
                new TweakPicker((List<Tweak> tweaks) =>
                    {
                        var ordered = tweaks.OrderByDescending(tw=>tw.Len2<26 ? 1000+tw.Len2+tw.Len3 : tw.Len2+tw.Len3);
                        return ordered.First();
                    } , "size-2limit25"),
                new TweakPicker((List<Tweak> tweaks) =>
                    {
                        var ordered = tweaks.OrderByDescending(tw=>tw.Len2<101 ? 1000+tw.Len2 : tw.Len2+tw.Len3);
                        return ordered.First();
                    } , "size-2limit100"),
                new TweakPicker((List<Tweak> tweaks) =>
                    {
                        var ordered = tweaks.OrderByDescending(tw=>tw.Len2<51 ? 1000+tw.Len2+tw.Len3 : tw.Len2+tw.Len3);
                        return ordered.First();
                    } , "size-2limit50"),
                new TweakPicker((List<Tweak> tweaks) =>
                    {
                        var ordered = tweaks.OrderByDescending(tw=>tw.Len3<51 ? 1000+tw.Len2+tw.Len3 : tw.Len2+tw.Len3);
                        return ordered.First();
                    } , "size-3limit50"),
                new TweakPicker((List<Tweak> tweaks) =>
                    {
                        var ordered = tweaks.OrderByDescending(tw=>tw.Len3<11 ? 1000+tw.Len2+tw.Len3 : tw.Len2+tw.Len3);
                        return ordered.First();
                    } , "size-3limit10"),
                 new TweakPicker((List<Tweak> tweaks) =>
                    {
                        var ordered = tweaks.OrderByDescending(tw=>tw.Len3<11 && tw.Len2<11 ? 1000+tw.Len2+tw.Len3 : tw.Len2+tw.Len3);
                        return ordered.First();
                    } , "size-23limit10"),
                  new TweakPicker((List<Tweak> tweaks) =>
                    {
                        var ordered = tweaks.OrderByDescending(tw=>tw.Len3<26 && tw.Len2<26 ? 1000+tw.Len2+tw.Len3 : tw.Len2+tw.Len3);
                        return ordered.First();
                    } , "size-23limit25"),
                   new TweakPicker((List<Tweak> tweaks) =>
                    {
                        var ordered = tweaks.OrderByDescending(tw=>tw.Len3<61 && tw.Len2<61 ? 1000+tw.Len2+tw.Len3+globalRand.Next(25) : tw.Len2+tw.Len3);
                        return ordered.First();
                    } , "size-23limit60"),
                   new TweakPicker((List<Tweak> tweaks) =>
                    {
                        var ordered = tweaks.OrderByDescending(tw=>tw.Len3<31 && tw.Len2<31 ? 1000+tw.Len2+tw.Len3+globalRand.Next(25) : tw.Len2+tw.Len3);
                        return ordered.First();
                    } , "size-23limit30"),
                new TweakPicker((List<Tweak> tweaks) =>
                    {
                        var ordered = tweaks.OrderByDescending(tw=>tw.Len2<51 ? 1000+tw.Len2+tw.Len3+OneFraction(10, globalRand) : tw.Len2);
                        return ordered.First();
                    } , "size-2limit50-rnd-tenth"),
                new TweakPicker((List<Tweak> tweaks) =>
                    {
                        var ordered = tweaks.OrderByDescending(tw=>tw.Len2==tw.Len3 ? 1000+tw.Len2+tw.Len3+OneFraction(10, globalRand) : tw.Len2);
                        return ordered.First();
                    } , "equal-segs"),
                new TweakPicker((List<Tweak> tweaks) =>
                    {
                        var ordered = tweaks.OrderByDescending(tw=>tw.Len2==tw.Len3 ? 1000+tw.Len2+tw.Len3+globalRand.Next(25) : tw.Len2+tw.Len3);
                        return ordered.First();
                    } , "equal-segs-lim10", null, 10, 10),
                //10,10 5k in 4s
                //50,50
                new TweakPicker((List<Tweak> tweaks) =>
                    {
                        var ordered = tweaks.OrderByDescending(tw=>tw.Len1==tw.SegNode.Value.Len-tw.Len1-tw.Len2? 1000+tw.Len2+tw.Len3+OneFraction(10, globalRand) : tw.Len2);
                        return ordered.First();
                    } , "equal-remainders"),
                new TweakPicker((List<Tweak> tweaks) =>
                    {
                        if (globalRand.Next(10) == 0)
                        {
                            return tweaks[globalRand.Next(tweaks.Count)];
                        }
                        var ordered = tweaks.OrderByDescending(tw=>tw.Len2<51 ? 1000+tw.Len2+tw.Len3 : tw.Len2);
                        return ordered.First();
                    } , "size-limit50-tenthrandom"),
  
            
                //new TweakPicker((List<Tweak> tweaks) =>
                //    {
                //        var ordered = tweaks.OrderByDescending(tw=>(tw.Len2dir==Dir.Up) ?  -1*tw.Len2-tw.Len3 : tw.Len2+tw.Len3);
                //        return ordered.First();
                //    } , "dir-no-right-size"),
                //new TweakPicker((List<Tweak> tweaks) => tweaks.First(), "first"),
                //new TweakPicker((List<Tweak> tweaks) => tweaks.Last(), "last"),

                new TweakPicker((Tweak tw) => tw.Len1, "len1"),
                //new TweakPicker((Tweak tw) => tw.Len2, "len2"),
                new TweakPicker((Tweak tw) => tw.Len3, "len3"),
                new TweakPicker((Tweak tw) => tw.Len1 + tw.Len3, "len13"),
                new TweakPicker((Tweak tw) => tw.Len1 + tw.Len2, "len12"),
                new TweakPicker((Tweak tw) => tw.Len2 + tw.Len3, "len23"),
                new TweakPicker((Tweak tw) => tw.Len2-tw.Len1, "size2-1"),
                new TweakPicker((Tweak tw) => tw.Len2-tw.Len3, "size2-3"),
                new TweakPicker((Tweak tw) => tw.Len3-tw.Len1, "size3-1"),
                new TweakPicker((Tweak tw) => tw.Len3-tw.Len2, "size3-2"),
                new TweakPicker((Tweak tw) => tw.Len1-tw.Len2, "size1-2"),
                new TweakPicker((Tweak tw) => tw.Len1-tw.Len2-tw.Len3, "size1-23"),
                new TweakPicker((Tweak tw) => tw.Len2-tw.Len1-tw.Len3, "size2-13"),
                
                new TweakPicker((Tweak tw) => tw.Len2 + tw.Len3 - tw.Len1, "sizebig"),
                new TweakPicker((Tweak tw) => tw.Len2 + tw.Len3 + globalRand.Next(2), "sizerand"),
                new TweakPicker((Tweak tw) => tw.Len2 + tw.Len3 + globalRand.Next(3), "sizerand2"),
                new TweakPicker((Tweak tw) => tw.Len2 + tw.Len3 + OneFraction(3, globalRand), "rand-third"),
                new TweakPicker((Tweak tw) => tw.Len2 + tw.Len3 + OneFraction(2, globalRand), "rand-half"),
                new TweakPicker((Tweak tw) => tw.Len2 + tw.Len3 + OneFraction(5, globalRand), "rand-fifth"),
                new TweakPicker((Tweak tw) => tw.Len2 + tw.Len3 + OneFraction(10, globalRand), "rand-tenth"),
                new TweakPicker((Tweak tw) => tw.Len2 + tw.Len3 + OneFraction(20, globalRand), "rand-twentieth"),
                new TweakPicker((Tweak tw) => tw.Right ? 1000+tw.Len2 + tw.Len3 : -1*(1000+tw.Len2 + tw.Len3), "turndir-right-size"),
                new TweakPicker((Tweak tw) => tw.Right ? -1*(1000 + tw.Len2 + tw.Len3) : 1000 + tw.Len2 + tw.Len3, "turndir-left-size"),
                new TweakPicker((Tweak tw) => globalRand.Next(100), "rand100"),
                new TweakPicker((Tweak tw) => tw.Len2+tw.Len3+globalRand.Next(100), "rand100plus23"),
                new TweakPicker((Tweak tw) => globalRand.Next(99), "rand99"),
                new TweakPicker((Tweak tw) => globalRand.Next(3), "rand3"),
                new TweakPicker((Tweak tw) => tw.Len1 + globalRand.Next(3), "rand3len1"),
                new TweakPicker((Tweak tw) => tw.Len1 +tw.Len2 + globalRand.Next(3), "rand3len12"),
                new TweakPicker((Tweak tw) => tw.Len2+ tw.Len3 + globalRand.Next(3), "rand3len23"),
                new TweakPicker((Tweak tw) => (tw.SegNode.Value.Start.Item1 % 50<25 ? 10 : 0) + globalRand.Next(3), "partition-rand3"),
                new TweakPicker((Tweak tw) => globalRand.Next(5), "rand5"),
                new TweakPicker((Tweak tw) => globalRand.Next(2), "rand2"),
                //new TweakPicker((Tweak tw) => tw.Len2%2==0?tw.Len2+tw.Len3 : -1*(tw.Len2+tw.Len3), "parity-even-size"),
                //new TweakPicker((Tweak tw) => tw.Len2%2==1?tw.Len2+tw.Len3 : -1 * (tw.Len2 + tw.Len3), "parity-odd-size"),

                new TweakPicker((List<Tweak> tweaks) =>
                    {
                        var ordered = tweaks.OrderByDescending(tw=>tw.Len2+tw.Len3);
                        if (globalRand.Next(10) == 0)
                        {
                            return ordered.Last();
                        }
                        return ordered.First();
                    } , "order-mostlyfirst-orlast"),
                new TweakPicker((List<Tweak> tweaks) =>
                    {
                        var ordered = tweaks.OrderByDescending(tw=>tw.Len2+tw.Len3);
                        if (globalRand.Next(2) == 0)
                        {
                            return ordered.Last();
                        }
                        return ordered.First();
                    } , "order-firstlast"),
                new TweakPicker((List<Tweak> tweaks) =>
                    {
                        var ordered = tweaks.OrderByDescending(tw=>tw.Len2+tw.Len3);
                        var index = Math.Min(tweaks.Count-1, globalRand.Next(4));
                        return ordered.Skip(index).First();
                    } , "order-firstfour-size"),
                new TweakPicker((List<Tweak> tweaks) =>
                    {
                        var ordered = tweaks.OrderByDescending(tw=>tw.Len2+tw.Len3);
                        var mid = ordered.Count()/2;
                        return ordered.Skip(mid).First();
                    } , "order-half-size"),
                new TweakPicker((List<Tweak> tweaks) =>
                    {
                        var ordered = tweaks.OrderByDescending(tw=>tw.Len2+tw.Len3);
                        var mid = ordered.Count()/3;
                        return ordered.Skip(mid).First();
                    } , "order-third-size"),
                new TweakPicker((List<Tweak> tweaks) =>
                    {
                        var ordered = tweaks.OrderByDescending(tw=>tw.Len2+tw.Len3);
                        var mid = ordered.Count()/5;
                        return ordered.Skip(mid).First();
                    } , "order-fifth-size"),
                new TweakPicker((List<Tweak> tweaks) =>
                    {
                        var ordered = tweaks.OrderByDescending(tw=>tw.Len2+tw.Len3);
                        var mid = ordered.Count()/6;
                        return ordered.Skip(mid).First();
                    } , "order-sixth-size"),
                new TweakPicker((List<Tweak> tweaks) =>
                    {
                        var ordered = tweaks.OrderByDescending(tw=>tw.Len2+tw.Len3+OneFraction(10, globalRand));
                        var mid = ordered.Count()/6;
                        return ordered.Skip(mid).First();
                    } , "order-sixth-rndtenth-size"),
                new TweakPicker((List<Tweak> tweaks) =>
                    {
                        var ordered = tweaks.OrderByDescending(tw=>tw.Len2-tw.Len1);
                        var mid = ordered.Count()/6;
                        return ordered.Skip(mid).First();
                    } , "order-sixth-size2-1"),
                new TweakPicker((List<Tweak> tweaks) =>
                    {
                        var ordered = tweaks.OrderByDescending(tw=>tw.Len2+tw.Len3);
                        var mid = ordered.Count()/2;
                        return ordered.Skip(mid).First();
                    } , "order-quarter-size"),
                
            };
            
            return pickers.OrderBy(p=>p.Name).ToList();
        }

        static void Main(string[] args)
        {
            var pickers = GetPickers();
            var ii = 0;
            var mm = 1;
            var x = 70;
            var y = 50;
            var target = "size-23limit30";
            target = "equal-segs-lim10";
            //re-validate the board at every step
            var debug = false;

            var stem = $"../../../output/{x}x{y}";

            if (!System.IO.Directory.Exists(stem))
            {
                System.IO.Directory.CreateDirectory($"{stem}");
            }
            if (!System.IO.Directory.Exists($"../../../tweaks/"))
            {
                System.IO.Directory.CreateDirectory($"../../../tweaks/");
            }

            while (ii < mm)
            {
                var pickerCount =0 ;
                foreach (var picker in pickers)
                {
                    if (!string.IsNullOrWhiteSpace(target) && picker.Name != target)
                    {
                        continue;
                    }
                    WL($"Starting picker: {picker.Name}");
                    pickerCount++;
                    var rnd = new System.Random(ii);
                    var l = new Level(x, y, rnd, debug, ii, picker);
                    l.InitialWander();
                    Console.WriteLine($"rnd seed: {ii}");
                    if (pickerCount == 1)
                    {
                        //Util.SaveEmpty(l, $"{stem}/{ii}-empty.png");
                        //Util.SaveWithPath(l, $"{stem}/{ii}-path.png");
                    }

                    l.RepeatedlyTweak(false, 21000);
                    Util.SaveEmpty(l, $"{stem}/empty-{ii}-{picker.Name}.png");
                    Util.SaveWithPath(l, $"{stem}/path-{ii}-{picker.Name}.png");
                    WL(Report(l));
                }
                ii++;
            }
        }
    }
}
