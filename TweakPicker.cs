using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;

namespace coil
{
    public class TweakPicker
    {
        public TweakPicker(Func<List<Tweak>, System.Random, Tweak> picker, string name, int? maxLen1 = null, int? maxLen2 = null, int? maxLen3 = null, int? tweaklim = null)
        {
            //wrap in the tweakpicker's random
            Picker = (List<Tweak> tweaks) => picker(tweaks, Random);
            Init(name, maxLen1, maxLen2, maxLen3, tweaklim);
        }

        public TweakPicker(Func<List<Tweak>, Tweak> picker, string name, int? maxLen1 = null, int? maxLen2 = null, int? maxLen3 = null, int? tweaklim = null)
        {
            Picker = picker;
            Init(name, maxLen1, maxLen2, maxLen3, tweaklim);
        }

        public TweakPicker(Func<Tweak, int> scoringFunction, string name, int? maxLen1 = null, int? maxLen2 = null, int? maxLen3 = null, int? tweaklim = null)
        {
            Picker = (List<Tweak> tweaks) => ArgMax(tweaks, tt => scoringFunction(tt));
            Init(name, maxLen1, maxLen2, maxLen3, tweaklim);
        }

        public TweakPicker(Func<Tweak, Random, int> scoringFunction, string name, int? maxLen1 = null, int? maxLen2 = null, int? maxLen3 = null, int? tweaklim = null)
        {
            Picker = (List<Tweak> tweaks) => ArgMax(tweaks, tt => scoringFunction(tt, Random));
            Init(name, maxLen1, maxLen2, maxLen3, tweaklim);
        }

        /// <summary>
        /// First element with the maximum score, scoring each element once in list order - the same element
        /// (and the same sequence of Random calls) as OrderByDescending(score).First(), without the sort.
        /// </summary>
        public static Tweak ArgMax(List<Tweak> tweaks, Func<Tweak, int> score)
        {
            if (tweaks.Count == 0)
            {
                throw new ArgumentException("no tweaks to pick from");
            }
            var best = tweaks[0];
            var bestScore = score(best);
            for (var i = 1; i < tweaks.Count; i++)
            {
                var s = score(tweaks[i]);
                if (s > bestScore)
                {
                    best = tweaks[i];
                    bestScore = s;
                }
            }
            return best;
        }

        private void Init(string name, int? maxLen1 = null, int? maxLen2 = null, int? maxLen3 = null, int? tweaklim = null)
        {
            Name = name;
            MaxLen1 = maxLen1;
            MaxLen2 = maxLen2;
            MaxLen3 = maxLen3;
            TweakLim = tweaklim;
        }

        public Random Random { get; set; }
        
        public void Init(int seed)
        {
            Random = new System.Random(seed);
        }

        public override string ToString()
        {
            return $"TweakPicker:{Name} {MaxLen1},{MaxLen2},{MaxLen3},{TweakLim}";
        }

        public Func<List<Tweak>, Tweak> Picker;

        public string Name;

        //metadata to feed into getTweaks to limit the size of the generated list of choices.
        public int? MaxLen1;
        public int? MaxLen2;
        public int? MaxLen3;
        public int? TweakLim;

        public string GetStr()
        {
            return Name;
        }
    }
}
