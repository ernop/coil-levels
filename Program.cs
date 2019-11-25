using System;

using static coil.Util;

namespace coil
{
    class Program
    {
        static void Main(string[] args)
        {
            var ii = 0;
            var mm = 100;
            var x = 190;
            var y = 170;
            var test = false;

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
                var rnd = new System.Random(ii);
                var l = new Level(x, y, rnd, test, debug);
                Console.WriteLine($"rnd seed: {ii}");
   
                //Util.SaveEmpty(l, $"{stem}/{ii}-empty.png");
                //Util.SaveWithPath(l, $"{stem}/{ii}-path.png");

                l.Tweak2(true, 1000);

                //l.Tweak(true, 50);
                Util.SaveEmpty(l, $"{stem}/{ii}-tweaked.png");
                Util.SaveWithPath(l, $"{stem}/{ii}-path-tweaked.png");
                WL(Report(l));
                ii++;
            }
        }
    }
}
