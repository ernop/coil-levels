using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using static coil.Log;

namespace coil
{
    public class Counter
    {
        public Log Log {get;set;}
        public Counter(LevelConfiguration lc)
        {
            Log = new Log(lc);
            foreach (var k in Keys)
            {
                Counts[k] = 0;
            }
        }

        private List<string> Keys = new List<string>() { "GetVerticalsAndReturnables", 
            "SpaceFillIndexes","Lengthen", "Unlengthen", "ApplyTweak", "MakeSeg","GetTweaks","GetSafeLength","ApplySeg","UnapplySeg"};

        public SortedDictionary<string, int> Counts = new SortedDictionary<string, int>();
        public void Inc(string key)
        {
            Counts[key]++;
        }

        public void Show()
        {
            var res = new List<(string, int)>();
            foreach (var key in Counts.Keys)
            {
                res.Add((key, Counts[key]));
                
            }
            foreach (var el in res.OrderByDescending(ee=>ee.Item2))
            {
                Log.Info($"{el.Item2,4}={el.Item1}");
            }
        }
    }
}
