using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;

namespace coil
{
    public class TweakPicker
    {
        public TweakPicker(Func<List<Tweak>, Tweak> picker, string name, int? maxLen1 = null, int? maxLen2 = null, int? maxLen3 = null, int? tweaklim = null)
        {
            Picker = picker;
            Name = name;
            MaxLen1 = maxLen1;
            MaxLen2 = maxLen2;
            MaxLen3 = maxLen3;
            TweakLim = tweaklim;
        }

        public TweakPicker(Func<Tweak, int> scoringFunction, string name, int? maxLen1 = null, int? maxLen2 = null, int? maxLen3 = null, int? tweaklim = null)
        {
            Picker = (List<Tweak> tweaks) => tweaks.OrderByDescending(tt => scoringFunction(tt)).FirstOrDefault();
            Name = name;
            MaxLen1 = maxLen1;
            MaxLen2 = maxLen2;
            MaxLen3 = maxLen3;
            TweakLim = tweaklim;
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
