using System;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;

using static coil.Util;
using static coil.Debug;
using static coil.Coilutil;
namespace coil
{
    class Program
    {
        static void Main(string[] args)
        {
            var seed = 154;
            var x = 50;
            var y = 40;
            var count = 1;
            
            var segPickerName = "Longest";
            //segPickerName = "Next";
            //segPickerName = "BRand";
            var tweakPickerName = "shortrnd";
            //tweakPickerName = "";
            //CreateLevel(seed, x, y, false, segPickerName, tweakPickerName);
            CreateMultiple(seed, count, x, y, true, segPickerName, tweakPickerName);

            var minx = 3;
            var miny = 2;
            var xincrement = 1;
            var yincrement = 1;
            var maxx = 200;
            var maxy = 200;
            var countper = 2;
            var mass = true;

            //CreateLots(minx, miny, xincrement, yincrement, maxx, maxy, countper, mass);
        }

        static void CreateLots(int minx, int miny, int xincrement, int yincrement, int maxx, int maxy, int countper, bool mass)
        {
            var x = minx;
            var y = miny;
            var seed = 0;
            while (x < maxx && y < maxy) {
                CreateMultiple(seed, countper, x, y, mass);
                x += xincrement;
                y += yincrement;
            }
        }

        static void CreateMultiple(int seed, int count, int x, int y, bool mass, string segPickerName = null, string tweakPickerName=null) {
            var max = seed + count;
            while (seed < max)
            {
                CreateLevel(seed, x, y, mass, segPickerName: segPickerName, tweakPickerName:tweakPickerName);
                seed++;
            }
        }

        static void CreateLevel(int seed, int x, int y, bool mass = false, string segPickerName = null, string tweakPickerName = null)
        {
            
            //target = "rand99";
            //re-validate the board at every step
            var debug = false;

            var stem = "../../..";
            var levelstem = $"../../../output/{x}x{y}";

            if (!System.IO.Directory.Exists(levelstem))
            {
                System.IO.Directory.CreateDirectory($"{levelstem}");
            }

            var runCount = 0;
            //var lc2hash = new Dictionary<LevelConfiguration, string>();
            //var ws = new InitialWanderSetup(steplimit:20, startPoint:(1,1), gomax:true);
            var ws = new InitialWanderSetup();
            //not used

            //problems with the whole validation thing: 
            //hmm, there should be no randomness in tweak generation.

            //segpickers unused
            var pickers = TweakPickers.GetPickers();
            foreach (var tweakPicker in pickers)
            {
                if (!String.IsNullOrEmpty(tweakPickerName) && tweakPicker.Name!= tweakPickerName)
                {
                    continue;
                }

                foreach (var el in new List<int?>() {3, }) // null, 1, 100, 10000
                {
                    var cs = new OptimizationSetup();
                    cs.GlobalTweakLim = el;

                    foreach (var segPicker in SegPickers.GetSegPickers())
                    {
                        
                        if (!string.IsNullOrEmpty(segPickerName) && segPicker.Name != segPickerName) 
                        { 
                            continue;
                        }
                        var rnd = new System.Random(seed);
                        var lc = new LevelConfiguration(tweakPicker, segPicker, cs, ws);
                        runCount++;
                        var log = new Log(lc);
                        var counter = new Counter(lc);
                        var level = new Level(lc, log, x, y, rnd, seed, counter);

                        level.InitialWander();
                        //Show(level);
                        if (lc.OptimizationSetup.UseSpaceFillingIndexes)
                        {
                            level.RedoAllIndexesSpaceFillndexes();
                        }

                        //bit awkward to do it here - it needs a better guarantee of finding the best seg.
                        segPicker.Init(seed, level);
                        var st = Stopwatch.StartNew();
                        level.RepeatedlyTweak(true, 1, st);
                        //counter.Show();
                        var rep = Report(level, st.Elapsed, true);

                        if (runCount == 1 && !mass)
                        {
                            //Util.SaveEmpty(l, $"{stem}/e-{ii}.png");
                            SaveWithPath(level, $"{levelstem}/p-{seed}.png");
                        }
                        //leave this in for one final sense check.
                        var dst = Stopwatch.StartNew();
                        log.Info(rep.Replace("\n", ""));

                        
                        //WL($"Dodebug done: {dst.Elapsed}");
                        
                        var ist = Stopwatch.StartNew();
                        //it would be nice to take two lines.
                        if (true)
                        {
                            var pointTexts = GetAveragePoints(level, 3000);
                            SaveEmpty(level, $"{levelstem}/{lc.GetStr()}-empty-{seed}-arrows.png", subtitle: rep, quiet: true, pointTexts: pointTexts, arrows: true);
                        }
                        SaveEmpty(level, $"{levelstem}/{lc.GetStr()}-empty-{seed}.png", subtitle: rep, quiet: true);
                        //WL($"Saving image. {ist.Elapsed}");
                        SaveWithPath(level, $"{levelstem}/{lc.GetStr()}-path-{seed}.png", subtitle: rep, quiet: true);
                        //WL($"Saving pathimage. {ist.Elapsed}");
                        //Show(level);

                        //lc2hash[lc] = l.GetHash();

                        DoDebug(level, false, true);
                        SaveLevelAsText(level, seed);

                        if (false)
                        {
                            SaveArrowVersions(level, seed, levelstem);
                        }
                    }
                }
            }
            // TODO: compare hashes generated by all the cache usage combinations tested above and alert if different.
            //foreach (var k in lc2hash.Keys)
            //{
            //    WL($"{k} = {lc2hash[k].Length} {lc2hash[k]}");
            //}
        }
    }
}
