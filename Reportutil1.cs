using System;
using System.Linq;
using SparkNet;
using static coil.Coilutil;

namespace coil
{
    public static class Reportutil 
    { 
        public static string Report(Level level, TimeSpan ts, bool multiLine=false, TweakStats stats = null)
        {
            var sqs = (level.Height - 2) * (level.Width - 2);
            var sum = 1;
            var decisions = GetDecisions(level);
            var easyDecisions = decisions.Item1;
            var hardDecisions = decisions.Item2;
            var decisionCount = easyDecisions.Count + hardDecisions.Count;
            foreach (var seg in level.Segs)
            {
                sum += seg.Len;
            }
            var perc = 100.0 * sum / sqs;
            var decisionPercent = 100.0 * decisionCount / level.Segs.Count;
            //TODO determine how many "decisions" have to be made. More decisions == better!

            var linebreak = multiLine ? "\n" : "";

            //Divergence = per seg, how distant other segs does it see?
            //problem - this prioritizes long paths.
            var divergence = GetDivergence(level);

            var decp = decisionCount * 1.0 / sqs*100;

            var tweakTrySum = (stats.SuccessCt + stats.NoTweaks + stats.NoTweaksQualify);
            var succpercentage = stats.SuccessCt * 100.0 / tweakTrySum;
            var failratio = (stats.NoTweaks + stats.NoTweaksQualify)*100.0 / tweakTrySum;
            var statreport = $"succ={succpercentage.ToString("##0.0")}% failratio={failratio.ToString("##0.0")}%";

            //TODO it would be cool to sparkline render various things like periodic segment length.
            var sp = Spark.Render(0, 30, 55, 80, 33, 150);

            return $"{level.LevelConfiguration.GetStr()} {ts.TotalSeconds.ToString("0.0")}s {linebreak}" +
                $"segs{level.Segs.Count} cov{perc.ToString("##0.0")}% " +
                $"dec{decp.ToString("0.0")}% {sp} {linebreak}"+
                $"{level.Width - 2}x{level.Height - 2} div={divergence} {statreport}";
        }

        //TODO it would be nice to have a sparkline of block size/pathsize/neighbor size

        public static float GetDivergence(Level level)
        {
            return 0;
        }
    }
}
