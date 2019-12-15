using System;
using System.Collections.Generic;
using System.Text;

namespace coil
{
    public class LevelConfiguration
    {
        public TweakPicker TweakPicker { get; set; }
        public SegPicker SegPicker { get; set; }
        public OptimizationSetup OptimizationSetup { get; set; }
        public InitialWanderSetup InitialWanderSetup { get; set; }
        public LevelConfiguration(TweakPicker tp, SegPicker sp, OptimizationSetup optimizationSetup, InitialWanderSetup iws)
        {
            TweakPicker = tp;
            SegPicker = sp;
            OptimizationSetup = optimizationSetup;
            InitialWanderSetup = iws;
        }

        public string GetStr()
        {
            var os = OptimizationSetup.GetStr();
            if (string.IsNullOrWhiteSpace(os))
            {
                os = "";
            }
            else
            {
                os = " " + os;
            }
            return $"{TweakPicker.GetStr()} {SegPicker.GetName()}{os}";
        }
    } 
}
