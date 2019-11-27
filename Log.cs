using System;
using System.IO;
using static coil.Util;

namespace coil
{
    public class Log
    {
        public LevelConfiguration LevelConfiguration { get; set; }
        private string LogName;

        public Log(LevelConfiguration lc)
        {
            LevelConfiguration = lc;
            var logdir = "../../../logs";
            if (!System.IO.Directory.Exists(logdir))
            {
                System.IO.Directory.CreateDirectory(logdir);
            }
            LogName = $"{logdir}/{lc.GetStr()}.log";
            WL($"Log created at: {LogName}");
        }

        public void Info(string logMessage)
        {
            using (StreamWriter w = File.AppendText(LogName))
            {
                w.WriteLine($"{DateTime.Now.ToLongTimeString()} {logMessage}");
            }
        }
    }
}
