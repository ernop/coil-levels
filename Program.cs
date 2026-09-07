using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

using static coil.Debug;
using static coil.Coilutil;
using static coil.Reportutil;
using static coil.Util;
namespace coil
{
    partial class Program
    {
        const string Usage = @"usage:
  gen   <width> <height> [seed] [--picker NAME] [--segpicker NAME] [--lim N] [--loops N] [--keep-deadends] [--path] [--quiet]
        generate one level into output/<w>x<h>/ (png corner, .coil, .board, .solution). Defaults: 5000 5000 0 rnd99 Weighted4 lim 20.
        Path ends that are one-exit pockets are trimmed unless --keep-deadends. --path also saves the solution png.
  solve <file.board> [--budget NODES] [--all MAX]
        run the reference solver on a coilbench board; prints effort. --all counts solutions up to MAX.
  evaluate [--budget NODES] [--timeout-ms MS] [--max-cells N] [--depth-limit N] [--directions URDL] [--starts natural|reverse|low-degree|high-degree] [--pruning on|off]
        read one coilbench board on stdin; emit a JSON search result and replay-validated solution.
  stats <file.board>...
        structural stats of boards (open %, degree histogram, isolated walls).
  bench <width> <height> <seedFrom> <seedCount> [--picker NAME] [--segpicker NAME] [--lim N] [--loops N] [--keep-deadends] [--budget NODES] [--all]
        generate seedCount levels in memory and solve each; one CSV line per level on stdout. --all also exhausts the search tree.
  hardest <width> <height> <seedFrom> <seedCount> [--keep N] [--score exact|proxy] [--budget NODES] [--threads N] [gen options]
        generate seedCount levels, score each for solver hardness, keep the N hardest (default 10) as
        output/hardest/<w>x<h>/rankNN-seedS.board/.solution plus a summary. Defaults: --picker last --lim none,
        --score exact up to 34x34 (full-tree reference-solver effort, budget 20M nodes) else proxy. See HARDNESS.md.
  refresh-details <gallery-directory>
        rebuild detail PNGs from full maps with visible crop labels and coordinates.
  verify-collection <gallery-directory>
        replay saved gzip solutions, check hashes, recompute geometry, compare map/crop cells, and check visible crop captions.
  specimen <dir> <side> <seed> [gen options]
        export validated gzip board/solution, maps, and exact geometry stats; directory must be new.
  gallery <dir> <count> <minSide> <maxSide> [--seed S] [--threads N] [gen options]
        generate count square levels with sides spaced geometrically from minSide to maxSide (seed S+i), write a
        whole-board png per level (open white, wall black; 2 px/cell up to 1500 a side, 1 px above) and manifest.csv
        into <dir>. No .coil/.board files: a level is reproduced exactly by `gen w h seed` with the same options.
  pickers
        list tweak picker and seg picker names.
No subcommand: `<width> <height> [seed]` behaves as gen.";

        static int Main(string[] args)
        {
            if (args.Length == 0 || char.IsDigit(args[0][0]))
            {
                return Gen(args);
            }
            switch (args[0])
            {
                case "gen": return Gen(args.Skip(1).ToArray());
                case "solve": return Solve(args.Skip(1).ToArray());
                case "evaluate": return Evaluate(args.Skip(1).ToArray());
                case "stats": return Stats(args.Skip(1).ToArray());
                case "bench": return Bench(args.Skip(1).ToArray());
                case "hardest": return Hardest(args.Skip(1).ToArray());
                case "refresh-details": return RefreshDetails(args.Skip(1).ToArray());
                case "verify-collection": return VerifyCollection(args.Skip(1).ToArray());
                case "specimen": return Specimen(args.Skip(1).ToArray());
                case "gallery": return Gallery(args.Skip(1).ToArray());
                case "pickers":
                    WL("tweak pickers: " + string.Join(" ", TweakPickers.GetPickers(null).Select(p => p.Name).Distinct()));
                    WL("seg pickers:   " + string.Join(" ", SegPickers.GetSegPickers(null).Select(p => p.Name)));
                    return 0;
                default:
                    Console.Error.WriteLine(Usage);
                    return 2;
            }
        }

        /// <summary>--key value options; positional args returned in order.</summary>
        static (List<string> positional, Dictionary<string, string> opts) ParseArgs(string[] args)
        {
            var pos = new List<string>();
            var opts = new Dictionary<string, string>();
            for (var i = 0; i < args.Length; i++)
            {
                if (args[i].StartsWith("--"))
                {
                    var key = args[i].Substring(2);
                    if (i + 1 < args.Length && !args[i + 1].StartsWith("--"))
                    {
                        opts[key] = args[++i];
                    }
                    else
                    {
                        opts[key] = "true";
                    }
                }
                else
                {
                    pos.Add(args[i]);
                }
            }
            return (pos, opts);
        }

        static LevelConfiguration MakeConfiguration(Dictionary<string, string> opts)
        {
            var pickerName = opts.GetValueOrDefault("picker", "rnd99");
            var segPickerName = opts.GetValueOrDefault("segpicker", "Weighted4");
            var tweakPicker = TweakPickers.GetPickers(pickerName).FirstOrDefault()
                ?? throw new ArgumentException($"unknown tweak picker '{pickerName}' (see `pickers`)");
            var segPicker = SegPickers.GetSegPickers(segPickerName).FirstOrDefault()
                ?? throw new ArgumentException($"unknown seg picker '{segPickerName}' (see `pickers`)");
            var os = new OptimizationSetup();
            var lim = opts.GetValueOrDefault("lim", "20");
            os.GlobalTweakLim = lim == "none" ? (int?)null : int.Parse(lim);
            if (segPicker is ConfigurableSegPicker csp && opts.TryGetValue("loops", out var loops))
            {
                csp.MaxLoops = int.Parse(loops);
            }
            var wander = new InitialWanderSetup();
            if (opts.TryGetValue("wander-max", out var wanderMax)) wander.MaxLen = int.Parse(wanderMax);
            if (opts.TryGetValue("wander-steps", out var wanderSteps)) wander.StepLimit = int.Parse(wanderSteps);
            wander.GoMax = opts.ContainsKey("wander-full");
            if (wander.MaxLen < 2 || wander.StepLimit < 1) throw new ArgumentException("wander-max must be >=2; wander-steps >=1");
            return new LevelConfiguration(tweakPicker, segPicker, os, wander);
        }

        /// <summary>Generate one level: wander, tweak to exhaustion (MaxLoops passes), validate. Deterministic in seed.</summary>
        public static (Level level, TweakStats stats, TimeSpan tweakTime) GenerateLevel(LevelConfiguration lc, int width, int height, int seed, bool quiet, bool trimDeadEnds = true)
        {
            var phase = Stopwatch.StartNew();
            var rnd = new System.Random(seed);
            var level = new Level(lc, width, height, rnd, seed);
            level.InitialWander(lc);
            if (!quiet)
            {
                WL($"phase wander     {phase.Elapsed.TotalSeconds,7:0.000}s segs={level.Segs.Count}");
            }
            phase.Restart();

            lc.SegPicker.Init(seed, level);
            lc.TweakPicker.Init(seed);
            var st = Stopwatch.StartNew();
            var tweakStats = level.RepeatedlyTweak(false, 1, st, quiet);
            var elapsed = st.Elapsed;
            if (!quiet)
            {
                WL($"phase tweak      {phase.Elapsed.TotalSeconds,7:0.000}s segs={level.Segs.Count} redoAll={level.RedoAllCount} gc0/1/2={GC.CollectionCount(0)}/{GC.CollectionCount(1)}/{GC.CollectionCount(2)} alloc={GC.GetTotalAllocatedBytes() / (1 << 20)}MB");
            }
            phase.Restart();
            if (trimDeadEnds)
            {
                var trimmed = level.TrimDeadEnds();
                if (!quiet && trimmed > 0)
                {
                    WL($"trimmed {trimmed} dead-end square(s) from the path ends");
                }
            }

            //before doing any outputting, validate the level against the segs, then against the game rules.
            DoDebug(level, show: false, validateBoard: true);
            CoilFormat.Validate(CoilFormat.BoardString(level), CoilFormat.SolutionString(level));
            GenerationQuality.Validate(level);
            if (!quiet)
            {
                WL($"phase validate   {phase.Elapsed.TotalSeconds,7:0.000}s");
            }
            return (level, tweakStats, elapsed);
        }

        static int Gen(string[] args)
        {
            var (pos, opts) = ParseArgs(args);
            var width = pos.Count >= 2 ? int.Parse(pos[0]) : 5000;
            var height = pos.Count >= 2 ? int.Parse(pos[1]) : 5000;
            var seed = pos.Count >= 3 ? int.Parse(pos[2]) : 0;
            var quiet = opts.ContainsKey("quiet");
            var lc = MakeConfiguration(opts);

            var levelstem = $"{Paths.Root}/output/{width}x{height}";
            Directory.CreateDirectory(levelstem);
            var csv = new CsvWriter(levelstem + "/results.csv");
            var log = new Log(lc);

            var (level, tweakStats, elapsed) = GenerateLevel(lc, width, height, seed, quiet, !opts.ContainsKey("keep-deadends"));

            var phase = Stopwatch.StartNew();
            var repdata = GetReport(level, elapsed, tweakStats);
            var rep = Report(repdata, multiline: true);
            log.Info(Report(repdata, multiline: false));
            SaveLevelAsText(level, seed);
            var stem = $"{levelstem}/{lc.GetStr()}-{seed}";
            //coilbench interchange files (already validated in GenerateLevel).
            File.WriteAllText(stem + ".board", CoilFormat.BoardString(level) + "\n");
            File.WriteAllText(stem + ".solution", CoilFormat.SolutionString(level) + "\n");
            csv.Write(repdata);
            SaveEmpty(level, $"{stem}-corner.png", subtitle: rep, quiet: true, corner: true);
            if (opts.ContainsKey("path"))
            {
                SaveWithPath(level, $"{stem}-path.png", subtitle: rep, quiet: true);
            }
            if (!quiet)
            {
                WL($"phase output     {phase.Elapsed.TotalSeconds,7:0.000}s -> {stem}.board");
            }
            return 0;
        }

        static int Solve(string[] args)
        {
            var (pos, opts) = ParseArgs(args);
            if (pos.Count != 1)
            {
                Console.Error.WriteLine(Usage);
                return 2;
            }
            var board = File.ReadAllText(pos[0]).Trim();
            var solver = new Solver(board);
            if (opts.TryGetValue("budget", out var b))
            {
                solver.NodeBudget = long.Parse(b);
            }
            if (opts.TryGetValue("all", out var all))
            {
                solver.MaxSolutions = long.Parse(all);
            }
            var sw = Stopwatch.StartNew();
            var solved = solver.Solve();
            sw.Stop();
            WL($"{Path.GetFileName(pos[0])} {solver.W}x{solver.H} solved={solved} nodes={solver.Nodes} branchNodes={solver.BranchNodes} forced={solver.ForcedMoves} " +
               $"startsTried={solver.StartsTried}/{solver.CandidateStarts} deadEnds={solver.DeadEndsInBoard} solutions={solver.SolutionsFound} budgetExceeded={solver.BudgetExceeded} {sw.Elapsed.TotalSeconds:0.000}s");
            if (solved && solver.MaxSolutions == 1)
            {
                CoilFormat.Validate(board, solver.FirstSolution);
                WL(solver.FirstSolution);
            }
            return solved ? 0 : 1;
        }

        static int Stats(string[] args)
        {
            foreach (var f in args)
            {
                WL($"{Path.GetFileName(f),-40} {Solver.BoardStats(File.ReadAllText(f).Trim())}");
            }
            return 0;
        }

        static int Bench(string[] args)
        {
            var (pos, opts) = ParseArgs(args);
            if (pos.Count != 4)
            {
                Console.Error.WriteLine(Usage);
                return 2;
            }
            var width = int.Parse(pos[0]);
            var height = int.Parse(pos[1]);
            var seedFrom = int.Parse(pos[2]);
            var seedCount = int.Parse(pos[3]);
            var budget = long.Parse(opts.GetValueOrDefault("budget", "2000000"));
            //--all: also exhaust the whole search tree (all starts, all solutions) under the same budget, which
            //measures the level rather than the luck of the start ordering.
            var all = opts.ContainsKey("all");
            WL("config,size,seed,open%,segs,deadEnds,candidateStarts,solved,nodes,branchNodes,forced,startsTried,budgetExceeded,genSeconds,solveSeconds,nodesAll,solutions,allExceeded,avgSegLen,easyDec,hardDec,deg2%,deg3%,deg4%,isolatedWall%");
            for (var seed = seedFrom; seed < seedFrom + seedCount; seed++)
            {
                var lc = MakeConfiguration(opts);
                var gsw = Stopwatch.StartNew();
                var (level, _, _) = GenerateLevel(lc, width, height, seed, quiet: true, trimDeadEnds: !opts.ContainsKey("keep-deadends"));
                gsw.Stop();
                var board = CoilFormat.BoardString(level);
                var solver = new Solver(board) { NodeBudget = budget };
                var ssw = Stopwatch.StartNew();
                var solved = solver.Solve();
                ssw.Stop();
                var open = 100.0 * (level.TotalLength()) / (width * height);
                var allStr = ",,,";
                if (all)
                {
                    var full = new Solver(board) { NodeBudget = budget, MaxSolutions = long.MaxValue };
                    full.Solve();
                    allStr = $",{full.Nodes},{full.SolutionsFound},{full.BudgetExceeded}";
                }
                var decisions = GetDecisions(level);
                var (_, _, openCells, deg, isolatedWallPct) = Solver.BoardFacts(board);
                var structure = $",{GetAvgSegLen(level):0.00},{decisions.Item1.Count},{decisions.Item2.Count},{100.0 * deg[2] / openCells:0.0},{100.0 * deg[3] / openCells:0.0},{100.0 * deg[4] / openCells:0.0},{isolatedWallPct:0.0}";
                WL($"{lc.GetStr().Replace(' ', '_')},{width}x{height},{seed},{open:0.0},{level.Segs.Count},{solver.DeadEndsInBoard},{solver.CandidateStarts},{solved},{solver.Nodes},{solver.BranchNodes},{solver.ForcedMoves},{solver.StartsTried},{solver.BudgetExceeded},{gsw.Elapsed.TotalSeconds:0.000},{ssw.Elapsed.TotalSeconds:0.000}{allStr}{structure}");
            }
            return 0;
        }

        static int Hardest(string[] args)
        {
            var (pos, opts) = ParseArgs(args);
            if (pos.Count != 4)
            {
                Console.Error.WriteLine(Usage);
                return 2;
            }
            var width = int.Parse(pos[0]);
            var height = int.Parse(pos[1]);
            var seedFrom = int.Parse(pos[2]);
            var seedCount = int.Parse(pos[3]);
            var keep = int.Parse(opts.GetValueOrDefault("keep", "10"));
            var budget = long.Parse(opts.GetValueOrDefault("budget", "20000000"));
            var threads = int.Parse(opts.GetValueOrDefault("threads", Environment.ProcessorCount.ToString()));
            //the sweep winners (HARDNESS.md): tweak picker `last`, no per-seg tweak limit.
            opts.TryAdd("picker", "last");
            opts.TryAdd("lim", "none");
            var scoreMode = opts.GetValueOrDefault("score", width * height <= 34 * 34 ? "exact" : "proxy");
            var exact = scoreMode switch
            {
                "exact" => true,
                "proxy" => false,
                _ => throw new ArgumentException($"--score must be exact or proxy, got '{scoreMode}'"),
            };
            var trim = !opts.ContainsKey("keep-deadends");

            var sw = Stopwatch.StartNew();
            var scores = new Hardness.Score[seedCount];
            var done = 0;
            var po = new System.Threading.Tasks.ParallelOptions { MaxDegreeOfParallelism = threads };
            System.Threading.Tasks.Parallel.For(0, seedCount, po, i =>
            {
                var seed = seedFrom + i;
                var lc = MakeConfiguration(opts);
                var (level, _, _) = GenerateLevel(lc, width, height, seed, quiet: true, trimDeadEnds: trim);
                scores[i] = Hardness.Measure(level, seed, exact, budget);
                var n = System.Threading.Interlocked.Increment(ref done);
                if (n % Math.Max(1, seedCount / 20) == 0)
                {
                    Console.Error.WriteLine($"{n}/{seedCount} scored {sw.Elapsed.TotalSeconds:0}s");
                }
            });

            var ranked = scores.OrderByDescending(s => s.Rank(exact)).ToList();
            var outDir = $"{Paths.Root}/output/hardest/{width}x{height}";
            Directory.CreateDirectory(outDir);
            var lc0 = MakeConfiguration(opts);
            var summary = new List<string>
            {
                $"# hardest {width}x{height} seeds {seedFrom}..{seedFrom + seedCount - 1} config={lc0.GetStr()} score={scoreMode} budget={budget} {sw.Elapsed.TotalSeconds:0.0}s",
                "rank,seed,nodesAll,solutions,exceeded,proxy,open%,hardDec,isolatedWall%,deg2%",
            };
            for (var r = 0; r < Math.Min(keep, ranked.Count); r++)
            {
                var s = ranked[r];
                var stem = $"{outDir}/rank{r + 1:00}-seed{s.Seed}";
                File.WriteAllText(stem + ".board", s.Board + "\n");
                File.WriteAllText(stem + ".solution", s.Solution + "\n");
                summary.Add($"{r + 1},{s.Seed},{s.NodesAll},{s.Solutions},{s.BudgetExceeded},{s.Proxy:0.00},{s.OpenPct:0.0},{s.HardDecisions},{s.IsolatedWallPct:0.0},{s.Deg2Pct:0.0}");
            }
            //distribution of the whole batch, so the kept ones can be read against it.
            if (exact)
            {
                var all = scores.Where(s => !s.BudgetExceeded).Select(s => (double)s.NodesAll).OrderBy(v => v).ToList();
                if (all.Count > 0)
                {
                    var gm = Math.Exp(all.Average(v => Math.Log(Math.Max(1, v))));
                    summary.Add($"# batch nodesAll: gm={gm:0} p50={all[all.Count / 2]:0} p90={all[(int)(all.Count * 0.9)]:0} max={all[all.Count - 1]:0} exceeded={scores.Count(s => s.BudgetExceeded)}");
                }
            }
            File.WriteAllLines(outDir + "/summary.csv", summary);
            //every seed's scores, for checking the proxy against the exact score or picking by another criterion.
            File.WriteAllLines(outDir + "/scores.csv", new[] { "seed,nodesAll,solutions,exceeded,proxy,open%,hardDec,isolatedWall%,deg2%" }
                .Concat(scores.Select(s => $"{s.Seed},{s.NodesAll},{s.Solutions},{s.BudgetExceeded},{s.Proxy:0.00},{s.OpenPct:0.0},{s.HardDecisions},{s.IsolatedWallPct:0.0},{s.Deg2Pct:0.0}")));
            foreach (var line in summary)
            {
                WL(line);
            }
            WL($"-> {outDir}");
            return 0;
        }

        static int Gallery(string[] args)
        {
            var (pos, opts) = ParseArgs(args);
            if (pos.Count != 4)
            {
                Console.Error.WriteLine(Usage);
                return 2;
            }
            var dir = Path.IsPathRooted(pos[0]) ? pos[0] : Path.Combine(Paths.Root, pos[0]);
            var count = int.Parse(pos[1]);
            var minSide = int.Parse(pos[2]);
            var maxSide = int.Parse(pos[3]);
            var seed0 = int.Parse(opts.GetValueOrDefault("seed", "1"));
            //big boards take gigabytes each while tweaking, so the default parallelism is low.
            var threads = int.Parse(opts.GetValueOrDefault("threads", "4"));
            Directory.CreateDirectory(dir);

            var sides = new int[count];
            for (var i = 0; i < count; i++)
            {
                var t = count == 1 ? 1.0 : (double)i / (count - 1);
                sides[i] = (int)Math.Round(minSide * Math.Pow((double)maxSide / minSide, t));
            }
            var lc0 = MakeConfiguration(opts);
            var rows = new string[count];
            var sw = Stopwatch.StartNew();
            var done = 0;
            //largest first so the long ones are not left for the end.
            var order = Enumerable.Range(0, count).OrderByDescending(i => sides[i]).ToArray();
            var po = new System.Threading.Tasks.ParallelOptions { MaxDegreeOfParallelism = threads };
            System.Threading.Tasks.Parallel.ForEach(order, po, i =>
            {
                var side = sides[i];
                var seed = seed0 + i;
                var lc = MakeConfiguration(opts);
                var gsw = Stopwatch.StartNew();
                var (level, _, _) = GenerateLevel(lc, side, side, seed, quiet: true, trimDeadEnds: !opts.ContainsKey("keep-deadends"));
                var genSeconds = gsw.Elapsed.TotalSeconds;
                var name = $"{side}x{side}-seed{seed}.png";
                ImageUtil.SaveMap(level, Path.Combine(dir, name), side <= 1500 ? 2 : 1);
                var (_, _, openCells, deg, isolatedWallPct) = Solver.BoardFacts(CoilFormat.BoardString(level));
                var (easy, hard) = GetDecisions(level);
                rows[i] = $"{name},{side},{side},{seed},{lc.GetStr()},{100.0 * openCells / (side * side):0.0},{level.Segs.Count},{GetAvgSegLen(level):0.00},{easy.Count},{hard.Count},{isolatedWallPct:0.0},{100.0 * deg[2] / openCells:0.0},{genSeconds:0.0}";
                var n = System.Threading.Interlocked.Increment(ref done);
                Console.Error.WriteLine($"{n}/{count} {name} {genSeconds:0.0}s (total {sw.Elapsed.TotalSeconds:0}s)");
            });
            var manifest = new List<string> { "file,width,height,seed,config,open%,segs,avgSegLen,easyDec,hardDec,isolatedWall%,deg2%,genSeconds" };
            manifest.AddRange(rows);
            File.WriteAllLines(Path.Combine(dir, "manifest.csv"), manifest);
            WL($"{count} levels, config={lc0.GetStr()}, sides {minSide}..{maxSide}, {sw.Elapsed.TotalSeconds:0}s -> {dir}");
            return 0;
        }
    }
}
