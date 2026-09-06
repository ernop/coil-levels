using System;
using System.IO;

namespace coil
{
    //All file IO is relative to the repo root (tiles/, output/, levels/, logs/), so the program
    //must not depend on the current working directory: `dotnet run` uses the project dir, Visual
    //Studio uses bin/Debug/<tfm>. Locate the root from the executable and refuse to guess.
    public static class Paths
    {
        public const string RootMarker = "coil-levels-csharp.csproj";

        public static readonly string Root = FindRoot();

        public static string In(string relative) => Path.Combine(Root, relative);

        private static string FindRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir.FullName, RootMarker)))
                {
                    return dir.FullName;
                }
                dir = dir.Parent;
            }
            throw new DirectoryNotFoundException($"No {RootMarker} found in or above {AppContext.BaseDirectory}");
        }
    }
}
