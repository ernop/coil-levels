namespace coil
{
    /// <summary>
    /// Goal of this: to validate that the caches don't actually produce different outputs.  Very likely to have off by one errors in there.
    /// ALSO: this will have to happen a lot
    /// </summary>
    public class OptimizationSetup
    {
        //rather than increment indexes in order, go from 0..uint.max and fill the space between them.
        //this works but the speed improvement is unclear
        //seems clear that this is proven to work...
        public bool UseSpaceFillingIndexes { get; set; } = true;

        /// <summary>
        /// Validate these with a tweakpicker which uses them!
        /// </summary>
        public bool UseSTVCache { get; set; } = true;
        public bool UseTweakLen1Rule { get; set; } = true;
        public bool UseTweakLen2RuleInGetVerticals { get; set; } = true;
        public bool UseTweakLen2RuleInGetTweaks { get; set; } = true;
        public bool UseTweakLen3Rule { get; set; } = true;

        /// <summary>
        /// always just run the tweakpicker on these first N tweaks
        /// </summary>
        public int? GlobalTweakLim { get; set; } = null;

        public string GetStr()
        {
            var res = "";
            //res += UseSTVCache ? "t" : "f";
            //res += UseTweakLen1Rule ? "t" : "f";
            //res += UseTweakLen2RuleInGetVerticals ? "t" : "f";
            //res += UseTweakLen2RuleInGetTweaks ? "t" : "f";
            //res += UseTweakLen3Rule ? "t" : "f";
            res += UseSpaceFillingIndexes ? "t" : "f";
            res += GlobalTweakLim.HasValue ? $"lim{GlobalTweakLim}" : "nolim";
            return res;
        }
    }
}
