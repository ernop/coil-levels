using System.Collections.Generic;
namespace coil
{
    public class LevelGenerationConfig
    {
        public int seed;
        public int x;
        public int y;
        public bool mass = false;
        public string segPickerName = null;
        public string tweakPickerName = null;
        public bool saveTweaks = false;
        public int? saveEvery = null;
        public bool saveArrows = false;
        public int arrowLengthMin = 50;
        public bool saveEmpty = false;
        public bool saveWithPath = false;
        
        /// <summary>
        /// write a log for this run.
        /// </summary>
        public bool saveCsv = false;
        public List<int?> genLimits = new List<int?>() { null, 1, 3, 100 };
    }
}
