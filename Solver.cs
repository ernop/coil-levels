using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace coil
{
    /// <summary>
    /// A reference coil solver used as the hardness oracle: depth-first search over slide moves with the standard
    /// prunings a competent bot has (start must be a dead end when one exists, forced moves are not branch points,
    /// unvisited cells must stay connected and reachable, at most one unvisited cell may be a future dead end).
    /// "Effort" is the number of search nodes (moves actually made) before the first solution; a level that needs
    /// many nodes here is hard for this class of solver. Deterministic: same board, same effort.
    /// </summary>
    public class Solver
    {
        public readonly int W;
        public readonly int H;
        private readonly bool[] Wall;
        private readonly bool[] Visited;
        //number of unvisited open neighbours of each open cell
        private readonly int[] Degree;
        private readonly int Open;
        private readonly int[] FloodStack;
        private readonly int[] FloodMark;
        private int FloodGen;

        public long Nodes;
        public long NodeBudget = long.MaxValue;
        public bool BudgetExceeded;
        public long SolutionsFound;
        public long MaxSolutions = 1;
        public int StartsTried;
        public int CandidateStarts;
        public int DeadEndsInBoard;
        public string FirstSolution;
        public long ForcedMoves;
        public long BranchNodes;

        private static readonly int[] DX = { 0, 1, 0, -1 };
        private static readonly int[] DY = { -1, 0, 1, 0 };
        private static readonly char[] DC = { 'U', 'R', 'D', 'L' };

        private readonly StringBuilder Path = new StringBuilder();

        public Solver(string boardString)
        {
            var (w, h, wall) = CoilFormat.ParseBoard(boardString);
            W = w;
            H = h;
            Wall = wall;
            Visited = new bool[w * h];
            Degree = new int[w * h];
            FloodStack = new int[w * h];
            FloodMark = new int[w * h];
            for (var i = 0; i < w * h; i++)
            {
                if (!Wall[i])
                {
                    Open++;
                    Degree[i] = CountOpenNeighbors(i);
                }
            }
        }

        private int CountOpenNeighbors(int i)
        {
            var x = i % W;
            var y = i / W;
            var c = 0;
            for (var d = 0; d < 4; d++)
            {
                var nx = x + DX[d];
                var ny = y + DY[d];
                if (nx >= 0 && ny >= 0 && nx < W && ny < H && !Wall[ny * W + nx] && !Visited[ny * W + nx])
                {
                    c++;
                }
            }
            return c;
        }

        /// <summary>Cells that can only be the path's start or end.</summary>
        public List<int> DeadEnds()
        {
            var res = new List<int>();
            for (var i = 0; i < W * H; i++)
            {
                if (!Wall[i] && Degree[i] == 1)
                {
                    res.Add(i);
                }
            }
            return res;
        }

        public bool Solve()
        {
            if (MaxSolutions < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(MaxSolutions), "solution limit must be positive");
            }
            var deadEnds = DeadEnds();
            DeadEndsInBoard = deadEnds.Count;
            if (deadEnds.Count > 2)
            {
                CandidateStarts = 0;
                return false;
            }
            //two dead ends: the path runs between them, so the start is one of the two. One dead end: it is either
            //the start or the end, so try it first but every other cell stays a candidate. None: any cell.
            List<int> starts;
            if (deadEnds.Count == 2)
            {
                starts = deadEnds;
            }
            else
            {
                starts = new List<int>(deadEnds);
                for (var i = 0; i < W * H; i++)
                {
                    if (!Wall[i] && Degree[i] != 1)
                    {
                        starts.Add(i);
                    }
                }
            }
            CandidateStarts = starts.Count;
            if (Open == 1)
            {
                SolutionsFound = 1;
                FirstSolution = $"x={starts[0] % W}&y={starts[0] / W}&path=";
                return true;
            }

            foreach (var s in starts)
            {
                StartsTried++;
                Visit(s);
                Path.Clear();
                var found = Dfs(s, Open - 1, s);
                Unvisit(s);
                if (found && SolutionsFound >= MaxSolutions)
                {
                    return true;
                }
                if (BudgetExceeded)
                {
                    return SolutionsFound > 0;
                }
            }
            return SolutionsFound > 0;
        }

        private void Visit(int i)
        {
            Visited[i] = true;
            var x = i % W;
            var y = i / W;
            for (var d = 0; d < 4; d++)
            {
                var nx = x + DX[d];
                var ny = y + DY[d];
                if (nx >= 0 && ny >= 0 && nx < W && ny < H)
                {
                    Degree[ny * W + nx]--;
                }
            }
        }

        private void Unvisit(int i)
        {
            Visited[i] = false;
            var x = i % W;
            var y = i / W;
            for (var d = 0; d < 4; d++)
            {
                var nx = x + DX[d];
                var ny = y + DY[d];
                if (nx >= 0 && ny >= 0 && nx < W && ny < H)
                {
                    Degree[ny * W + nx]++;
                }
            }
        }

        /// <summary>
        /// Slide from pos in dir, visiting cells; returns the number of cells visited and the new position via out.
        /// </summary>
        private int Slide(int pos, int d, out int end)
        {
            var x = pos % W;
            var y = pos / W;
            var n = 0;
            while (true)
            {
                var nx = x + DX[d];
                var ny = y + DY[d];
                if (nx < 0 || ny < 0 || nx >= W || ny >= H)
                {
                    break;
                }
                var ni = ny * W + nx;
                if (Wall[ni] || Visited[ni])
                {
                    break;
                }
                Visit(ni);
                n++;
                x = nx;
                y = ny;
            }
            end = y * W + x;
            return n;
        }

        private void UnslideTo(int end, int d, int n)
        {
            //walk back n cells from end opposite to d, unvisiting
            var x = end % W;
            var y = end / W;
            for (var k = 0; k < n; k++)
            {
                Unvisit(y * W + x);
                x -= DX[d];
                y -= DY[d];
            }
        }

        /// <summary>
        /// Prune check after a move. False means this position cannot be completed:
        /// - an unvisited cell with no unvisited neighbour is unreachable unless it is adjacent to pos and the last cell;
        /// - more than one unvisited cell with a single unvisited neighbour (and not adjacent to pos) means two dead ends
        ///   but only one path end remains;
        /// - the unvisited cells must be one connected region touching pos.
        /// </summary>
        private bool Feasible(int pos, int remaining)
        {
            var px = pos % W;
            var py = pos / W;
            //degree-based checks over neighbours of pos are cheap; the global low-degree count needs a scan,
            //which the flood fill below does anyway.
            FloodGen++;
            var sp = 0;
            var reached = 0;
            var lowNotAdjacent = 0;
            for (var d = 0; d < 4; d++)
            {
                var nx = px + DX[d];
                var ny = py + DY[d];
                if (nx < 0 || ny < 0 || nx >= W || ny >= H)
                {
                    continue;
                }
                var ni = ny * W + nx;
                if (!Wall[ni] && !Visited[ni] && FloodMark[ni] != FloodGen)
                {
                    FloodMark[ni] = FloodGen;
                    FloodStack[sp++] = ni;
                }
            }
            if (sp == 0)
            {
                return remaining == 0;
            }
            while (sp > 0)
            {
                var c = FloodStack[--sp];
                reached++;
                var deg = Degree[c];
                if (deg <= 1)
                {
                    var cx = c % W;
                    var cy = c / W;
                    var adjacentToPos = Math.Abs(cx - px) + Math.Abs(cy - py) == 1;
                    if (!adjacentToPos)
                    {
                        if (deg == 0)
                        {
                            return false;
                        }
                        lowNotAdjacent++;
                        if (lowNotAdjacent > 1)
                        {
                            return false;
                        }
                    }
                }
                for (var d = 0; d < 4; d++)
                {
                    var nx = c % W + DX[d];
                    var ny = c / W + DY[d];
                    if (nx < 0 || ny < 0 || nx >= W || ny >= H)
                    {
                        continue;
                    }
                    var ni = ny * W + nx;
                    if (!Wall[ni] && !Visited[ni] && FloodMark[ni] != FloodGen)
                    {
                        FloodMark[ni] = FloodGen;
                        FloodStack[sp++] = ni;
                    }
                }
            }
            return reached == remaining;
        }

        private bool Dfs(int pos, int remaining, int start)
        {
            if (remaining == 0)
            {
                SolutionsFound++;
                if (FirstSolution == null)
                {
                    FirstSolution = $"x={start % W}&y={start / W}&path={Path}";
                }
                return true;
            }
            if (Nodes >= NodeBudget)
            {
                BudgetExceeded = true;
                return false;
            }

            //enumerate legal moves; a single legal move is forced and not counted as a branch.
            var legal = 0;
            var lastDir = -1;
            var px = pos % W;
            var py = pos / W;
            for (var d = 0; d < 4; d++)
            {
                var nx = px + DX[d];
                var ny = py + DY[d];
                if (nx >= 0 && ny >= 0 && nx < W && ny < H && !Wall[ny * W + nx] && !Visited[ny * W + nx])
                {
                    legal++;
                    lastDir = d;
                }
            }
            if (legal == 0)
            {
                return false;
            }
            if (legal == 1)
            {
                ForcedMoves++;
            }
            else
            {
                BranchNodes++;
            }

            var anyFound = false;
            for (var d = 0; d < 4; d++)
            {
                if (legal == 1 && d != lastDir)
                {
                    continue;
                }
                var n = Slide(pos, d, out var end);
                if (n == 0)
                {
                    continue;
                }
                Nodes++;
                Path.Append(DC[d]);
                if (Feasible(end, remaining - n))
                {
                    if (Dfs(end, remaining - n, start))
                    {
                        anyFound = true;
                        if (SolutionsFound >= MaxSolutions || BudgetExceeded)
                        {
                            Path.Length--;
                            UnslideTo(end, d, n);
                            return anyFound;
                        }
                    }
                    else if (BudgetExceeded)
                    {
                        Path.Length--;
                        UnslideTo(end, d, n);
                        return anyFound;
                    }
                }
                Path.Length--;
                UnslideTo(end, d, n);
            }
            return anyFound;
        }

        /// <summary>Structural facts about a board that a solver can exploit, independent of any search.</summary>
        public static string BoardStats(string boardString)
        {
            var (w, h, open, deg, isolatedWallPct) = BoardFacts(boardString);
            return $"{w}x{h} open={100.0 * open / (w * h):0.0}% deg1={deg[1]} deg2={deg[2]} deg3={deg[3]} deg4={deg[4]} " +
                $"deg2%={100.0 * deg[2] / open:0.0} deg4%={100.0 * deg[4] / open:0.0} isolatedWalls={isolatedWallPct:0.0}%";
        }

        public static (int w, int h, int open, int[] degreeHistogram, double isolatedWallPct) BoardFacts(string boardString)
        {
            var s = new Solver(boardString);
            var deg = new int[5];
            for (var i = 0; i < s.W * s.H; i++)
            {
                if (!s.Wall[i])
                {
                    deg[s.Degree[i]]++;
                }
            }
            //wall cells that touch no other wall: isolated pillars, the signature of short tweak gaps
            var isolatedWalls = 0;
            var walls = 0;
            for (var i = 0; i < s.W * s.H; i++)
            {
                if (!s.Wall[i])
                {
                    continue;
                }
                walls++;
                var x = i % s.W;
                var y = i / s.W;
                var touching = 0;
                for (var d = 0; d < 4; d++)
                {
                    var nx = x + DX[d];
                    var ny = y + DY[d];
                    if (nx >= 0 && ny >= 0 && nx < s.W && ny < s.H && s.Wall[ny * s.W + nx])
                    {
                        touching++;
                    }
                }
                if (touching == 0)
                {
                    isolatedWalls++;
                }
            }
            return (s.W, s.H, s.Open, deg, 100.0 * isolatedWalls / Math.Max(1, walls));
        }
    }
}
