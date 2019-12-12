using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.IO;
using static coil.Reportutil;

namespace coil
{
    public class CsvWriter
    {
        public string Path { get; set; }
        public bool seen = false;
        public List<System.Reflection.FieldInfo> Fields;

        public CsvWriter(string path) {
            Path = path;
            if (System.IO.File.Exists(path))
            {
                System.IO.File.Delete(path);
            }
            System.Console.WriteLine($"Writing to: {path}");
        }

        private void init(ReportData data)
        {
            
            Fields = data.GetType().GetFields()
                .OrderBy(el=>el.ToString())
                .ToList();
        }

        public void Write(ReportData data)
        {
            if (!seen)
            {
                init(data);
                var header = string.Join(",", Fields.Select(f => f.Name))+"\n";
                File.AppendAllText(Path, header);
                seen = true;
            }
            
            var res = new List<string>();
            foreach (var prop in Fields)
            {
                res.Add(prop.GetValue(data).ToString());
            }

            var line = string.Join(",", res) + "\n";

            File.AppendAllText(Path, line);
        }
    }
}
