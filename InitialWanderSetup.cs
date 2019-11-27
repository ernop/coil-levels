namespace coil
{
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
}
