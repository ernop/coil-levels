﻿using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;

namespace coil
{
    public class TweakPicker
    {
        public TweakPicker(Func<List<Tweak>, Tweak> picker, string name, int? maxLen1 = null, int? maxLen2 = null, int? maxLen3 = null)
        {
            Picker = picker;
            Name = name;
            MaxLen1 = maxLen1;
            MaxLen2 = maxLen2;
            MaxLen3 = maxLen3;
        }

        public TweakPicker(Func<Tweak, int> scoringFunction, string name, int? maxLen1 = null, int? maxLen2 = null, int? maxLen3 = null)
        {
            Picker = (List<Tweak> tweaks) => tweaks.OrderByDescending(tt => scoringFunction(tt)).First();
            Name = name;
            MaxLen1 = maxLen1;
            MaxLen2 = maxLen2;
            MaxLen3 = maxLen3;
        }

        public Func<List<Tweak>, Tweak> Picker;

        public string Name;
        public int? MaxLen1;
        public int? MaxLen2;
        public int? MaxLen3;

        public string GetStr()
        {
            return Name;
        }
    }
}