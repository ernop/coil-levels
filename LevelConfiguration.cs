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
            return $"{TweakPicker.GetStr()}-{OptimizationSetup.GetStr()}";
            //-{SegPicker.GetStr()}-{InitialWanderSetup.GetStr()}
        }
    }
}
