using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace coil
{
    /// <summary>
    /// The coilbench / hacker.org interchange format, and an independent replay of the game rules.
    /// Board:    x=W&y=H&board=<W*H chars, '.' open, 'X' wall, row-major from top-left>
    /// Solution: x=SX&y=SY&path=<U/D/L/R...>  (0-based start cell; each letter is one slide until blocked)
    /// This file knows nothing about Segs or Rows beyond reading them once, so it can catch generator bugs
    /// that the generator's own bookkeeping would agree with.
    /// </summary>
    public static class CoilFormat
    {
        public static string BoardString(BaseLevel l)
        {
            var w = l.Width - 2;
            var h = l.Height - 2;
            var sb = new StringBuilder(w * h + 32);
            sb.Append($"x={w}&y={h}&board=");
            for (var yy = 1; yy <= h; yy++)
            {
                for (var xx = 1; xx <= w; xx++)
                {
                    sb.Append(l.GetRowValue((xx, yy)) == null ? 'X' : '.');
                }
            }
            return sb.ToString();
        }

        public static string SolutionString(BaseLevel l)
        {
            var first = l.Segs.First.Value;
            var sb = new StringBuilder(l.Segs.Count + 32);
            sb.Append($"x={first.Start.Item1 - 1}&y={first.Start.Item2 - 1}&path=");
            foreach (var seg in l.Segs)
            {
                sb.Append(DirChar(seg.Dir));
            }
            return sb.ToString();
        }

        public static char DirChar(Dir d)
        {
            switch (d)
            {
                case Dir.Up: return 'U';
                case Dir.Down: return 'D';
                case Dir.Left: return 'L';
                case Dir.Right: return 'R';
                default: throw new ArgumentOutOfRangeException(nameof(d), d, null);
            }
        }

        public static (int w, int h, bool[] wall) ParseBoard(string s)
        {
            var q = ParseQuery(s);
            var w = int.Parse(q["x"]);
            var h = int.Parse(q["y"]);
            var board = q["board"];
            if (w < 1 || h < 1 || (long)w * h > int.MaxValue) throw new FormatException("Board dimensions must be positive and fit an array");
            if (board.Length != w * h)
            {
                throw new FormatException($"board has {board.Length} chars, expected {w}x{h}={w * h}");
            }
            var wall = new bool[w * h];
            for (var i = 0; i < board.Length; i++)
            {
                switch (board[i])
                {
                    case 'X': wall[i] = true; break;
                    case '.': wall[i] = false; break;
                    default: throw new FormatException($"bad board char '{board[i]}' at {i}");
                }
            }
            return (w, h, wall);
        }

        public static (int x, int y, string path) ParseSolution(string s)
        {
            var q = ParseQuery(s);
            return (int.Parse(q["x"]), int.Parse(q["y"]), q["path"]);
        }

        private static Dictionary<string, string> ParseQuery(string s)
        {
            var d = new Dictionary<string, string>();
            foreach (var part in s.Trim().Split('&'))
            {
                var eq = part.IndexOf('=');
                if (eq < 0)
                {
                    throw new FormatException($"no '=' in '{part}'");
                }
                if (!d.TryAdd(part.Substring(0, eq), part.Substring(eq + 1)))
                    throw new FormatException("Duplicate query key");
            }
            return d;
        }

        /// <summary>
        /// Replay a solution under the game rules. Throws with a specific message on the first violation.
        /// </summary>
        public static void Validate(string boardString, string solutionString)
        {
            var (w, h, wall) = ParseBoard(boardString);
            var (sx, sy, path) = ParseSolution(solutionString);

            var open = 0;
            foreach (var isWall in wall)
            {
                if (!isWall) open++;
            }

            if (sx < 0 || sy < 0 || sx >= w || sy >= h)
            {
                throw new InvalidOperationException($"start ({sx},{sy}) outside {w}x{h}");
            }
            if (wall[sy * w + sx])
            {
                throw new InvalidOperationException($"start ({sx},{sy}) is a wall");
            }

            var visited = new bool[w * h];
            visited[sy * w + sx] = true;
            var visitedCount = 1;
            var x = sx;
            var y = sy;

            for (var i = 0; i < path.Length; i++)
            {
                int dx, dy;
                switch (path[i])
                {
                    case 'U': dx = 0; dy = -1; break;
                    case 'D': dx = 0; dy = 1; break;
                    case 'L': dx = -1; dy = 0; break;
                    case 'R': dx = 1; dy = 0; break;
                    default: throw new FormatException($"bad path char '{path[i]}' at move {i}");
                }
                var steps = 0;
                while (true)
                {
                    var nx = x + dx;
                    var ny = y + dy;
                    if (nx < 0 || ny < 0 || nx >= w || ny >= h || wall[ny * w + nx] || visited[ny * w + nx])
                    {
                        break;
                    }
                    x = nx;
                    y = ny;
                    visited[y * w + x] = true;
                    visitedCount++;
                    steps++;
                }
                if (steps == 0)
                {
                    throw new InvalidOperationException($"move {i} '{path[i]}' from ({x},{y}) is blocked immediately");
                }
            }

            if (visitedCount != open)
            {
                throw new InvalidOperationException($"path visits {visitedCount} of {open} open cells; ends at ({x},{y})");
            }
        }

        /// <summary>
        /// Write <stem>.board and <stem>.solution, validating first. The board file is exactly what coilbench's
        /// evaluate.py feeds a solver on stdin.
        /// </summary>
        public static void Export(BaseLevel l, string stem)
        {
            var board = BoardString(l);
            var solution = SolutionString(l);
            Validate(board, solution);
            File.WriteAllText(stem + ".board", board + "\n");
            File.WriteAllText(stem + ".solution", solution + "\n");
        }
    }
}
