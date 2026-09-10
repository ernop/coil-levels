using System;
using System.Collections.Generic;
using System.Text;

namespace coil
{
    // Fair independent wall/open bits followed by a complete solvability decision.
    // Conditioning on solvability gives independent uniform BOARDS, with no
    // multiplicity from the number of solutions. Unknown decisions abort.
    public sealed class UniformRejectionSampler
    {
        public int Width { get; }
        public int Height { get; }
        public long Attempts { get; private set; }
        public long Disconnected { get; private set; }
        public long Unsolvable { get; private set; }
        public long Accepted { get; private set; }
        public long SolverNodes { get; private set; }
        private readonly int[] queue;
        private readonly bool[] opened, seen;

        public UniformRejectionSampler(int width, int height)
        {
            // The recursive reference solver is not a million-cell oracle.
            if (width < 1 || height < 1 || (long)width * height > 64)
                throw new ArgumentException("Uniform rejection reference supports rectangles of at most 64 cells.");
            Width = width; Height = height;
            opened = new bool[width * height]; seen = new bool[opened.Length]; queue = new int[opened.Length];
        }

        public (string board, string solution)? TryDraw(Func<int, int> choose, long nodeBudget = long.MaxValue)
        {
            Attempts++;
            var first = -1; var count = 0;
            for (var i = 0; i < opened.Length; i++)
            {
                var bit = choose(2);
                if (bit != 0 && bit != 1) throw new ArgumentException("Choice must be a fair bit, 0 or 1.");
                opened[i] = bit == 1;
                if (opened[i]) { first = i; count++; }
            }
            if (count == 0) { Unsolvable++; return null; }
            Array.Clear(seen); queue[0] = first; seen[first] = true; var tail = 1;
            void Enqueue(int cell) { if (opened[cell] && !seen[cell]) { seen[cell] = true; queue[tail++] = cell; } }
            for (var head = 0; head < tail; head++)
            {
                var cell = queue[head]; var x = cell % Width; var y = cell / Width;
                if (x > 0) Enqueue(cell - 1); if (x + 1 < Width) Enqueue(cell + 1);
                if (y > 0) Enqueue(cell - Width); if (y + 1 < Height) Enqueue(cell + Width);
            }
            if (tail != count) { Disconnected++; return null; }
            var text = new StringBuilder($"x={Width}&y={Height}&board=");
            foreach (var cell in opened) text.Append(cell ? '.' : 'X');
            var board = text.ToString();
            var solver = new Solver(board) { NodeBudget = nodeBudget, DepthLimit = opened.Length + 1 };
            var solved = solver.Solve(); SolverNodes += solver.Nodes;
            if (solver.BudgetExceeded || solver.TimedOut || solver.DepthExceeded)
                throw new InvalidOperationException("Uniform sampling aborted: incomplete solvability decision. An unknown board cannot be rejected as unsolvable.");
            if (!solved) { Unsolvable++; return null; }
            CoilFormat.Validate(board, solver.FirstSolution); Accepted++;
            return (board, solver.FirstSolution);
        }
    }
}
