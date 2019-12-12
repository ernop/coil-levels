using System;
using System.Linq;
using SparkNet;
using static coil.Coilutil;

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
            public double decisionPercent;
            public double tweakSuccessPercent;
            public double coveragePercent;
            public string lcStr;
            public double totalSeconds;
            public int segCount;
            public string levelSize;
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
                $"dec{data.decisionPercent.ToString("0.0")}% {linebreak}" +
                $"{data.levelSize} div={data.divergence} {data.tweakSuccessPercent.ToString("##0.0")}%";
        }

        public static ReportData GetReport(Level level, TimeSpan ts, TweakStats tweakStats = null)
        {
            var res = new ReportData();
            res.lcStr = level.LevelConfiguration.GetStr();
            res.totalSeconds = ts.TotalSeconds;
            res.segCount = level.Segs.Count;
            res.levelSize = $"{level.Width - 2}x{level.Height - 2}";

            var sqs = (level.Height - 2) * (level.Width - 2);
            res.sqs = sqs;
            var sum = 1;
            var decisions = GetDecisions(level);

            //hard decisions not done yet. doable now.
            var easyDecisions = decisions.Item1;
            var hardDecisions = decisions.Item2;
            
            var decisionCount = easyDecisions.Count + hardDecisions.Count;
            res.decisionCount = decisionCount;
            
            foreach (var seg in level.Segs)
            {
                sum += seg.Len;
            }

            var coveragePercent = 100.0 * sum / sqs;
            res.coveragePercent = coveragePercent;

            var decisionPercent = decisionCount * 1.0 / sqs * 100;
            res.decisionPercent = decisionPercent;

            //Divergence = per seg, how distant other segs does it see?
            //problem - this prioritizes long paths.
            var divergence = GetDivergence(level);
            res.divergence = divergence;

            var tweakTrySum = (tweakStats.SuccessCt + tweakStats.NoTweaks + tweakStats.NoTweaksQualify);
            var tweakSuccessPercent = tweakStats.SuccessCt * 100.0 / tweakTrySum;
            res.tweakSuccessPercent = tweakSuccessPercent;

            //TODO it would be cool to sparkline render various things like periodic segment length.
            return res;
        }

        //TODO it would be nice to have a sparkline of block size/pathsize/neighbor size

        public static double GetDivergence(Level level)
        {
            return 0;
        }
    }
}
