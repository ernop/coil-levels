using System;
using System.Linq;
using SparkNet;
using static coil.Coilutil;
using static coil.Solverutil;

namespace coil
{
    public static class Reportutil
    {
        public class ReportData
        {
            public ReportData() { }
            public int sqs;
            public int decisionCount;
            public double divergence;
            public double easyDecisionPercent;
            public double hardDecisionPercent;
            public double tweakSuccessPercent;
            public double coveragePercent;
            public string lcStr;
            public double totalSeconds;
            public int segCount;
            public string levelSize;
            public double avgNeighborCount;
            public double avgSegLen;
        }

        public static string Report(ReportData data, bool multiline = false)
        {
            var linebreak = "";
            if (multiline)
            {
                linebreak = "\n";
            }
            return $"{data.lcStr} {data.totalSeconds.ToString("0.0")}s {linebreak}" +
                $"segs{data.segCount} cov{data.coveragePercent.ToString("##0.0")}% " +
                $"dec{data.easyDecisionPercent.ToString("0.0")}/{data.hardDecisionPercent.ToString("0.0")}% {linebreak}" +
                $"{data.levelSize} div={data.divergence} nei={data.avgNeighborCount.ToString("0.0")} asl{data.avgSegLen.ToString("0.0")} {linebreak}"+
                $"{data.tweakSuccessPercent.ToString("##0.0")}%";
        }

        public static double GetCoveragePercent(Level level, out int sqs)
        {
            sqs = (level.Height - 2) * (level.Width - 2);
            var sum = 1;
            foreach (var seg in level.Segs)
            {
                sum += seg.Len;
            }
            var coveragePercent = 100.0 * sum / sqs;
            return coveragePercent;
        }

        public static ReportData GetReport(Level level, TimeSpan ts, TweakStats tweakStats = null)
        {
            var res = new ReportData();
            res.lcStr = level.LevelConfiguration.GetStr();
            res.totalSeconds = ts.TotalSeconds;
            res.segCount = level.Segs.Count;
            res.levelSize = $"{level.Width - 2}x{level.Height - 2}";

            var decisions = GetDecisions(level);

            //hard decisions not done yet. doable now.
            var easyDecisions = decisions.Item1;
            var hardDecisions = decisions.Item2;
            
            res.decisionCount = easyDecisions.Count + hardDecisions.Count;

            res.coveragePercent = GetCoveragePercent(level, out int sqs);
            res.sqs = sqs;

            res.hardDecisionPercent = hardDecisions.Count * 1.0 / sqs * 100;

            res.easyDecisionPercent = easyDecisions.Count * 1.0 / sqs * 100;

            //Divergence = per seg, how distant other segs does it see?
            //problem - this prioritizes long paths.
            res.divergence = GetDivergence(level);

            res.avgNeighborCount = GetAvgNeighborCount(level);

            res.avgSegLen = GetAvgSegLen(level);

            var tweakTrySum = (tweakStats.SuccessCt + tweakStats.NoTweaks + tweakStats.NoTweaksQualify);
            var tweakSuccessPercent = tweakStats.SuccessCt * 100.0 / tweakTrySum;
            res.tweakSuccessPercent = tweakSuccessPercent;

            //TODO it would be cool to sparkline render various things like periodic segment length.
            return res;
        }

        /// <summary>
        /// for ever
        /// </summary>
        /// <param name="level"></param>
        /// <returns></returns>
        public static double GetDivergence(Level level)
        {
            return 0;
        }


        /// <summary>
        /// for ever
        /// </summary>
        /// <param name="level"></param>
        /// <returns></returns>
        public static double GetAvgNeighborCount(Level level)
        {
            var tot = 0;
            foreach (var seg in level.Segs)
            {
                var neighbors = GetNeighboringSegs(level, seg);
                tot += neighbors.Count;
            }
            return tot * 1.0 / level.Segs.Count;
        }

        public static double GetAvgSegLen(Level level)
        {
            var tot = 0;
            foreach (var seg in level.Segs)
            {
                tot += seg.Len;
            }
            return tot * 1.0 / level.Segs.Count;
        }
    }
}
