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
            config.x = 14;
            config.y = 10;
            config.saveTweaks = false;
            config.saveEvery = 1;
            config.segPickerName = "Longest";
            config.segPickerName = "";
            config.tweakPickerName = "rnd100";
            config.tweakPickerName = "";
            config.saveEmpty = false;
            config.saveWithPath = true;
            config.saveArrows = true;
            config.arrowLengthMin = 400;
            config.genLimits = new List<int?>() { 3, };
            config.saveCsv = true;
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
            var levelstem = $"../../../output/{config.x}x{config.y}";
            var csvpath = levelstem + "/results.csv";
            var csv = new CsvWriter(csvpath);

            if (!System.IO.Directory.Exists(levelstem))
            {
                System.IO.Directory.CreateDirectory($"{levelstem}");
            }

            var runCount = 0;
            //var ws = new InitialWanderSetup(steplimit:2, startPoint:(1,1), gomax:true);
            var ws = new InitialWanderSetup();
           
            foreach (var tweakPicker in TweakPickers.GetPickers(config.tweakPickerName))
            {
                foreach (var el in config.genLimits) // null, 1, 100, 10000
                {
                    var os = new OptimizationSetup();
                    os.GlobalTweakLim = el;
                    foreach (var b in new List<bool>() { true, false })
                    {
                        os.UseSpaceFillingIndexes = b;

                        foreach (var segPicker in SegPickers.GetSegPickers(config.segPickerName))
                        {
                            runCount++;

                            var rnd = new System.Random(config.seed);
                            var lc = new LevelConfiguration(tweakPicker, segPicker, os, ws);
                            var log = new Log(lc);
                            var level = new Level(lc, config.x, config.y, rnd, config.seed);

                            level.InitialWander(lc);

                            //bit awkward to do it here - it needs a better guarantee of finding the best seg.
                            segPicker.Init(config.seed, level);
                            var st = Stopwatch.StartNew();
                            var tweakStats = level.RepeatedlyTweak(config.saveTweaks, config.saveEvery.Value, st);
                            var elapsed = st.Elapsed;

                            //before doing any outputting, validate the level.
                            DoDebug(level, show: false, validateBoard: true);
                            AfterLevelGenerated(level, config, levelstem, lc, tweakStats, elapsed, csv, log);
                        }
                    }
                }
            }
        }

        public static void AfterLevelGenerated(Level level, LevelGenerationConfig config, string levelstem, LevelConfiguration lc, TweakStats tweakStats, TimeSpan ts, CsvWriter csv, Log log)
        {
            var repdata = GetReport(level, ts, tweakStats);
            var rep = Report(repdata, multiline: true);

            log.Info(Report(repdata, multiline: false));
            SaveLevelAsText(level, config.seed);
            if (config.saveCsv)
            {
                csv.Write(repdata);
            }

            if (config.saveEmpty)
            {
                SaveEmpty(level, $"{levelstem}/{lc.GetStr()}-empty-{config.seed}.png", subtitle: rep, quiet: true);
            }
            if (config.saveWithPath)
            {
                SaveWithPath(level, $"{levelstem}/{lc.GetStr()}-path-{config.seed}.png", subtitle: rep, quiet: true);
            }
            if (config.saveArrows)
            {
                SaveArrowVersions(level, config.seed, levelstem, config.arrowLengthMin);
            }
        }
    }
}
