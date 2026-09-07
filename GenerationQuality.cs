using System;

namespace coil
{
    public static class GenerationQuality
    {
        public const int FullWalkDefaultSteps = 8;
        public const string Policy = "bounded-initial-walk-v1";

        // A filled square larger than one fifth of the board side is a degenerate
        // open region for this collection. Small puzzles retain an eight-cell allowance.
        public static int MaximumOpenSquareSide(int width, int height) => Math.Max(8, Math.Min(width, height) / 5);

        public static void Validate(BaseLevel level)
        {
            int width = level.Width - 2, height = level.Height - 2;
            int limit = MaximumOpenSquareSide(width, height);
            var above = new int[width + 1];
            for (int y = 1; y <= height; y++)
            {
                int diagonal = 0;
                for (int x = 1; x <= width; x++)
                {
                    int old = above[x];
                    above[x] = level.GetRowValue((x, y)) == null ? 0 : 1 + Math.Min(diagonal, Math.Min(above[x - 1], above[x]));
                    diagonal = old;
                    if (above[x] > limit)
                        throw new InvalidOperationException($"Generation quality failed: all-open square exceeds {limit} cells on {width}x{height}. Reduce initial walk steps or change the generation configuration.");
                }
            }
        }
    }
}
