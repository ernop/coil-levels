using System;
using System.Collections.Generic;
using System.Text;

namespace coil
{
    public class LevelConfiguration
    {
        public TweakPicker TweakPicker { get; set; }
        public ISegPicker SegPicker { get; set; }
        public OptimizationSetup OptimizationSetup { get; set; }
        public InitialWanderSetup InitialWanderSetup { get; set; }
        public LevelConfiguration(TweakPicker tp, ISegPicker sp, OptimizationSetup optimizationSetup, InitialWanderSetup iws)
        {
            TweakPicker = tp;
            SegPicker = sp;
            OptimizationSetup = optimizationSetup;
            InitialWanderSetup = iws;
        }

        public string GetStr()
        {
            return $"{TweakPicker.GetStr()}";
            //-{SegPicker.GetStr()}-{OptimizationSetup.GetStr()}-{InitialWanderSetup.GetStr()}
        }
    }

    public class InitialWanderSetup
    {
        public int? MaxLen { get; set; }
        public int? StepLimit { get; set; }
        public (int,int)? StartPoint { get; set; }
        public bool GoMax { get; set; }

        public InitialWanderSetup(int? maxlen = null, int? steplimit = null, (int,int)? startPoint = null, bool gomax = false)
        {
            MaxLen = maxlen;
            StepLimit = steplimit;
            StartPoint = startPoint;
            GoMax = gomax;
        }

        public string GetStr()
        {
            return "w";
        }
    }

    /// <summary>
    /// Goal of this: to validate that the caches don't actually produce different outputs.  Very likely to have off by one errors in there.
    /// ALSO: this will have to happen a lot
    /// </summary>
    public class OptimizationSetup
    {
        /// <summary>
        /// Validate these with a tweakpicker which uses them!
        /// </summary>
        public bool UseSTVCache { get; set; } = false;
        public bool UseTweakLen1Rule { get; set; } = false;
        public bool UseTweakLen2RuleInGetVerticals { get; set; } = false;
        public bool UseTweakLen2RuleInGetTweaks { get; set; } = false;
        public bool UseTweakLen3Rule { get; set; } = false;
        public string GetStr()
        {
            var res = "";
            res += UseSTVCache ? "t" : "f";
            res += UseTweakLen1Rule? "t" : "f";
            res += UseTweakLen2RuleInGetVerticals ? "t" : "f";
            res += UseTweakLen2RuleInGetTweaks? "t" : "f";
            res += UseTweakLen3Rule ? "t" : "f";
            return res;
        }
    }
}
