using System;

namespace coil
{
    public static partial class Debug
    {
        // Independent reconstruction for the cell-path model, including the
        // zero-move singleton. The segment-model validator remains unchanged.
        public static void DoDebug(BackwardGenerator board)
        {
            var path = board.ReversePath;
            if (path.Count == 0) throw new InvalidOperationException("Empty backward path.");
            var order = new int[checked(board.Width * board.Height)];
            Array.Fill(order, -1);
            for (var i = 0; i < path.Count; i++)
            {
                var cell = path[path.Count - 1 - i];
                if (cell < 0 || cell >= order.Length || order[cell] != -1)
                    throw new InvalidOperationException($"Invalid/repeated backward path cell {cell}.");
                order[cell] = i;
            }
            for (var cell = 0; cell < order.Length; cell++)
                if (board.IsOpen(cell) != (order[cell] >= 0))
                    throw new InvalidOperationException($"Backward occupancy mismatch at {cell}.");

            for (var i = path.Count - 1; i > 0; i--)
            {
                var from = path[i];
                var to = path[i - 1];
                var dx = to % board.Width - from % board.Width;
                var dy = to / board.Width - from / board.Width;
                if (Math.Abs(dx) + Math.Abs(dy) != 1)
                    throw new InvalidOperationException("Non-adjacent backward path cells.");
                var ax = to % board.Width + dx;
                var ay = to / board.Width + dy;
                var inside = ax >= 0 && ay >= 0 && ax < board.Width && ay < board.Height;
                var ahead = inside ? ay * board.Width + ax : -1;
                if (i > 1 && ahead == path[i - 2]) continue;
                if (inside && order[ahead] > order[to])
                    throw new InvalidOperationException($"Unblocked turn at {to}: {ahead} is unvisited.");
            }
        }
    }
}
