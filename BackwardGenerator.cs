using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace coil
{
    public sealed class BackwardRecipe
    {
        public required int Width { get; init; }
        public required int Height { get; init; }
        public required int FinishX { get; init; }
        public required int FinishY { get; init; }
        // Directions in which the START grows, not forward play commands.
        public required string Growth { get; init; }
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public string SeedHex { get; set; }
    }

    public sealed class BackwardGenerator
    {
        private static readonly int[] Dx = { 0, 1, 0, -1 };
        private static readonly int[] Dy = { -1, 0, 1, 0 };
        private const string Directions = "URDL";
        private readonly bool[] open;
        private readonly List<int> reversePath = new List<int>();
        private readonly StringBuilder growth = new StringBuilder();
        public int Width { get; }
        public int Height { get; }
        public int Count => reversePath.Count;
        // Finish-to-start construction order; playing in this order may be illegal.
        public ReadOnlyCollection<int> ReversePath { get; }

        public BackwardGenerator(int width, int height, int finishX, int finishY)
        {
            if (width < 1 || height < 1 || (long)width * height > int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(width), "Dimensions must be positive and fit an array.");
            if (finishX < 0 || finishX >= width || finishY < 0 || finishY >= height)
                throw new ArgumentOutOfRangeException(nameof(finishX), "Finish must be inside the board.");
            Width = width;
            Height = height;
            open = new bool[checked(width * height)];
            var finish = finishY * width + finishX;
            open[finish] = true;
            reversePath.Add(finish);
            ReversePath = reversePath.AsReadOnly();
        }

        public bool IsOpen(int cell) => open[cell];

        public bool CanPrepend(Dir direction)
        {
            var d = (int)direction;
            if (d < 0 || d >= 4) throw new ArgumentOutOfRangeException(nameof(direction));
            var start = reversePath[Count - 1];
            var x = start % Width;
            var y = start / Width;
            var nx = x + Dx[d];
            var ny = y + Dy[d];
            if (nx < 0 || ny < 0 || nx >= Width || ny >= Height || open[ny * Width + nx])
                return false;
            if (Count == 1) return true;
            var ax = x - Dx[d];
            var ay = y - Dy[d];
            // A turn at the old start must stop against a wall or boundary.
            // The added square is visited before the suffix, so its other
            // stopping dependencies are unchanged.
            return ax < 0 || ay < 0 || ax >= Width || ay >= Height
                || !open[ay * Width + ax] || reversePath[Count - 2] == ay * Width + ax;
        }

        public void Prepend(Dir direction)
        {
            if (!CanPrepend(direction))
                throw new InvalidOperationException($"Illegal backward extension {direction} at growth step {growth.Length}.");
            var start = reversePath[Count - 1];
            var d = (int)direction;
            var next = start + Dx[d] + Dy[d] * Width;
            open[next] = true;
            reversePath.Add(next);
            growth.Append(Directions[d]);
        }

        public BackwardRecipe Recipe() => new BackwardRecipe
        {
            Width = Width, Height = Height,
            FinishX = reversePath[0] % Width, FinishY = reversePath[0] / Width,
            Growth = growth.ToString()
        };

        public static BackwardGenerator Replay(BackwardRecipe recipe)
        {
            ArgumentNullException.ThrowIfNull(recipe);
            ArgumentNullException.ThrowIfNull(recipe.Growth);
            var result = new BackwardGenerator(recipe.Width, recipe.Height, recipe.FinishX, recipe.FinishY);
            foreach (var letter in recipe.Growth)
            {
                var direction = Directions.IndexOf(letter);
                if (direction < 0) throw new FormatException($"Invalid growth direction '{letter}'. Expected URDL.");
                result.Prepend((Dir)direction);
            }
            return result;
        }

        public static BackwardGenerator Generate(int width, int height, Func<int, int> choose = null)
        {
            if (width < 1 || height < 1 || (long)width * height > int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(width));
            choose ??= RandomNumberGenerator.GetInt32;
            int Pick(int bound)
            {
                var value = choose(bound);
                if (value < 0 || value >= bound)
                    throw new InvalidOperationException($"Choice {value} outside [0, {bound}).");
                return value;
            }
            var capacity = checked(width * height);
            var finish = Pick(capacity);
            // Every length, including one and non-maximal lengths, is possible.
            // A fixed stopping probability per step would heavily favor tiny boards.
            var target = Pick(capacity) + 1;
            var result = new BackwardGenerator(width, height, finish % width, finish / width);
            var legal = new Dir[4];
            while (result.Count < target)
            {
                var count = 0;
                for (var d = 0; d < 4; d++)
                    if (result.CanPrepend((Dir)d)) legal[count++] = (Dir)d;
                if (count == 0) break;
                result.Prepend(legal[Pick(count)]);
            }
            return result;
        }

        public string BoardString()
        {
            var result = new StringBuilder($"x={Width}&y={Height}&board=");
            foreach (var cell in open) result.Append(cell ? '.' : 'X');
            return result.ToString();
        }

        public string SolutionString()
        {
            var start = reversePath[Count - 1];
            var result = new StringBuilder($"x={start % Width}&y={start / Width}&path=");
            var previousDirection = -1;
            for (var i = Count - 1; i > 0; i--)
            {
                var from = reversePath[i];
                var to = reversePath[i - 1];
                var direction = to / Width < from / Width ? 0
                    : to / Width > from / Width ? 2 : to > from ? 1 : 3;
                if (direction != previousDirection) result.Append(Directions[direction]);
                previousDirection = direction;
            }
            return result.ToString();
        }

        public void Validate()
        {
            Debug.DoDebug(this);
            CoilFormat.Validate(BoardString(), SolutionString());
        }

        public string RecipeJson(string seedHex = null)
        {
            var recipe = Recipe();
            recipe.SeedHex = seedHex;
            return JsonSerializer.Serialize(recipe, new JsonSerializerOptions { WriteIndented = true });
        }
    }
}
