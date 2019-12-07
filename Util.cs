using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using System.IO;

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






        /// <summary>
        /// Return int values from min to max in order of increasing distance from the midpoint.
        /// </summary>
        /// <param name="min"></param>
        /// <param name="max"></param>
        /// <returns></returns>
        public static IEnumerable<int> Pivot(int min, int max)
        {
            var adder = 1;
            var now = (max + min) / 2;
            yield return now;
            while (true)
            {
                var success = false;
                now += adder;

                if (now <= max)
                {
                    yield return now;
                    success = true;
                }
                adder++;
                now -= adder;
                adder++;
                if (now >= min)
                {
                    yield return now;
                    success = true;
                }
                if (!success)
                {
                    break;
                }
            }
        }



        public class PointText
        {
            public string Text;
            public (int, int) Point;
            public PointText(string text, (int, int) point)
            {
                Text = text;
                Point = point;
            }
        }




        public static int GridDist((int, int) a, (int, int) b)
        {
            return Math.Abs(a.Item1 - b.Item1) + Math.Abs(a.Item2 - b.Item2);
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

       
        public static void W(object s)
        {
            Console.Write(s.ToString());
        }

        public static void WL(object s)
        {
            Console.WriteLine(s.ToString());
        }


        //public void ApplySaveAndUndoTweak(Tweak tweak, string fn)
        //{
        //    var fakeLevel = new Level(Width - 2, Height - 2, Rnd, false, 0, TweakPicker);
        //    Seg lastSeg = null;
        //    LinkedListNode<Seg> tweakSegNode = null;
        //    foreach (var seg in Segs)
        //    {
        //        lastSeg = seg;
        //        var fakeSeg = new Seg(seg.Start, seg.Dir, seg.Len);
        //        fakeSeg.Index = seg.Index;
        //        fakeLevel.ApplySeg(fakeSeg);
        //        fakeLevel.Segs.AddLast(fakeSeg);
        //        if (seg.Index == tweak.SegNode.Value.Index)
        //        {
        //            tweakSegNode = fakeLevel.Segs.Last;
        //        }
        //    }
        //    fakeLevel.Rows[lastSeg.GetEnd()] = lastSeg;

        //    //gotta copy the tweak too.
        //    var fakeTweak = new Tweak(tweakSegNode, tweak.Right, tweak.Len1, tweak.Len2, tweak.Len3, tweak.Len2dir, tweak.ShortTweak, tweak.LongTweak);

        //    fakeLevel.ApplyTweak(fakeTweak);
        //    SaveWithPath(fakeLevel, fn);
        //    //SaveEmpty(fakeLevel, fn.Replace(".png","-empty.png"));
        //}


        //we have to validate that the full possible set of tweaks for this seg is really being generated.
        //create an image of each tweak.
        //public void PossiblySaveAvailableTweaks(List<Tweak> tweaks, int tweakct)
        //{
        //    if (false)
        //    {
        //        var ii = 0;
        //        foreach (var testtweak in tweaks)
        //        {
        //            var fn = $"../../../output/{Width - 2}x{Height - 2}/Tweaks-{Index}-{tweakct}-{ii}.png";
        //            ApplySaveAndUndoTweak(testtweak, fn);
        //            ii++;
        //        }
        //    }
        //}

        
    }
}
