using System;

using static coil.Util;

namespace coil
{
    class Program
    {
        static void Main(string[] args)
        {
            var ii = 6;
            var mm = 7;
            var x = 310;
            var y = 220;
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
                var rnd = new System.Random(ii);
                var l = new Level(x, y, rnd);
                Console.WriteLine($"rnd seed: {ii}");
                // Show(l);
                // foreach (var seg in l.Segs){
                //     Console.WriteLine(seg);
                // }

                Util.SaveEmpty(l,$"{stem}/{ii}-empty.png");
                Util.SaveWithPath(l, $"{stem}/{ii}-path.png");

                l.Tweak(true, 40);
                Util.SaveEmpty(l, $"{stem}/{ii}-tweaked.png");
                Util.SaveWithPath(l, $"{stem}/{ii}-path-tweaked.png");
                Report(l);
                ii++;
            }
        }

        public static void Report(Level level)
        {
            var sqs = (level.Height - 2) * (level.Width - 2);
            var sum = 1;
            foreach (var seg in level.Segs)
            {
                sum += seg.Len;
            }
            var perc = 100.0 * sum / sqs;
            WL($"Fill: sqs={sqs}, sum={sum}, perc={perc}");
        }
    }
}
