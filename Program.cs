using System;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;

using static coil.Util;
using static coil.Debug;
using static coil.Coilutil;
using static coil.Reportutil;
namespace coil
{
    partial class Program
    {
        static void Main(string[] args)
        {
            var config = new LevelGenerationConfig();
            config.seed = 0;
            config.x = 140*2*2;
            config.y = 80*2*2;
            config.saveTweaks = false;
            config.saveEvery = 1;
            config.segPickerName = "Longest";
            config.segPickerName = "";
            config.tweakPickerName = "rnd100";
            config.tweakPickerName = "";
            config.saveEmpty = true;
            config.saveWithPath = true;
            config.saveArrows = true;
            config.arrowLengthMin = 700;
            config.genLimits = new List<int?>() { 3, };

            CreateLevel(config);
        }

        static void CreateLots(LevelGenerationConfig config, int minx, int miny, int maxx, int maxy, int xincrement, int yincrement, int countper)
        {
            var x = minx;
            var y = miny;
            var seed = 0;
            while (x < maxx && y < maxy) {
                CreateMultiple(config, countper);
                x += xincrement;
                y += yincrement;
            }
        }

        static void CreateMultiple(LevelGenerationConfig config, int count) {
            var max = config.seed + count;
            while (config.seed < max)
            {
                
                CreateLevel(config);
                config.seed++;
            }
        }

        static void CreateLevel(LevelGenerationConfig config)
        {
            
            //target = "rand99";
            //re-validate the board at every step
            
            var levelstem = $"../../../output/{config.x}x{config.y}";

            if (!System.IO.Directory.Exists(levelstem))
            {
                System.IO.Directory.CreateDirectory($"{levelstem}");
            }

            var runCount = 0;
            //var lc2hash = new Dictionary<LevelConfiguration, string>();
            //var ws = new InitialWanderSetup(steplimit:2, startPoint:(1,1), gomax:true);
            var ws = new InitialWanderSetup();
            //not used

            //problems with the whole validation thing: 
            //hmm, there should be no randomness in tweak generation.

            //segpickers unused
            var pickers = TweakPickers.GetPickers();
            foreach (var tweakPicker in pickers)
            {
                if (!String.IsNullOrEmpty(config.tweakPickerName) && tweakPicker.Name!= config.tweakPickerName)
                {
                    continue;
                }

                foreach (var el in config.genLimits) // null, 1, 100, 10000
                {
                    var cs = new OptimizationSetup();
                    cs.GlobalTweakLim = el;

                    foreach (var segPicker in SegPickers.GetSegPickers())
                    {
                        
                        if (!string.IsNullOrEmpty(config.segPickerName) && segPicker.Name != config.segPickerName) 
                        { 
                            continue;
                        }
                        var rnd = new System.Random(config.seed);
                        var lc = new LevelConfiguration(tweakPicker, segPicker, cs, ws);
                        runCount++;
                        var log = new Log(lc);
                        var counter = new Counter(lc);
                        var level = new Level(lc, log, config.x, config.y, rnd, config.seed, counter);

                        level.InitialWander();
                        //Show(level);
                        if (lc.OptimizationSetup.UseSpaceFillingIndexes)
                        {
                            level.RedoAllIndexesSpaceFillndexes();
                        }

                        //bit awkward to do it here - it needs a better guarantee of finding the best seg.
                        segPicker.Init(config.seed, level);
                        var st = Stopwatch.StartNew();
                        var tweakStats = level.RepeatedlyTweak(config.saveTweaks, config.saveEvery.Value, st);
                        //counter.Show();
                        var rep = Report(level, st.Elapsed, multiLine:true, tweakStats);

                        if (runCount == 1 && !config.mass)
                        {
                            //Util.SaveEmpty(l, $"{stem}/e-{ii}.png");
                            SaveWithPath(level, $"{levelstem}/p-{config.seed}.png");
                        }
                        //leave this in for one final sense check.
                        var dst = Stopwatch.StartNew();
                        log.Info(rep.Replace("\n", ""));

                        
                        //WL($"Dodebug done: {dst.Elapsed}");
                        
                        var ist = Stopwatch.StartNew();
                        //it would be nice to take two lines.
                       
                        if (config.saveEmpty)
                        {
                            SaveEmpty(level, $"{levelstem}/{lc.GetStr()}-empty-{config.seed}.png", subtitle: rep, quiet: true);
                        }
                        if (config.saveWithPath)
                        {
                            //WL($"Saving image. {ist.Elapsed}");
                            SaveWithPath(level, $"{levelstem}/{lc.GetStr()}-path-{config.seed}.png", subtitle: rep, quiet: true);
                        }
                        //WL($"Saving pathimage. {ist.Elapsed}");
                        //Show(level);

                        //lc2hash[lc] = l.GetHash();

                        DoDebug(level, false, true);
                        SaveLevelAsText(level, config.seed);

                        if (config.saveArrows)
                        {
                            SaveArrowVersions(level, config.seed, levelstem, config.arrowLengthMin);
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
