using System;
using System.Numerics;
using System.Text.Json;

namespace coil
{
    public sealed class ConstructionCode
    {
        public required string Format { get; init; }
        public required int Width { get; init; }
        public required int Height { get; init; }
        public required string CodeHex { get; init; }
    }

    // Every nonnegative integer decodes to a legal board. Every ordered legal
    // solution has an integer that reconstructs it, with no fixed seed width.
    public static class UniversalConstruction
    {
        public const string Format = "coil-construction-v1";
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions {
            WriteIndented = true,
            UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow
        };

        private static int Area(int width, int height)
        {
            if (width < 1 || height < 1 || (long)width * height > int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(width), "Dimensions must be positive and fit an array.");
            return checked(width * height);
        }

        public static ConstructionCode Read(string json)
        {
            var code = JsonSerializer.Deserialize<ConstructionCode>(json, JsonOptions);
            if (code == null) throw new FormatException("Construction code must be a JSON object.");
            return code;
        }

        public static string Json(ConstructionCode code) => JsonSerializer.Serialize(code, JsonOptions);

        public static BackwardGenerator Decode(ConstructionCode code)
        {
            ArgumentNullException.ThrowIfNull(code);
            if (code.Format != Format) throw new FormatException($"Unknown construction format '{code.Format}'.");
            return Decode(code.Width, code.Height, code.CodeHex);
        }

        public static BackwardGenerator Decode(int width, int height, string codeHex)
        {
            var area = Area(width, height);
            ArgumentNullException.ThrowIfNull(codeHex);
            if (codeHex.Length == 0) throw new FormatException("CodeHex must contain hexadecimal digits.");
            var bytes = Convert.FromHexString(codeHex.Length % 2 == 0 ? codeHex : "0" + codeHex);
            var value = new BigInteger(bytes, isUnsigned: true, isBigEndian: true);
            var rest = BigInteger.DivRem(value, area, out var targetMinusOne);
            var choices = BigInteger.DivRem(rest, area, out var finish);
            var end = (int)finish;
            var board = new BackwardGenerator(width, height, end % width, end / width);
            // Extract the integer's base-4 digits once; dividing a million-cell
            // code on every step would make replay quadratic in code length.
            var digits = choices.ToByteArray(isUnsigned: true, isBigEndian: false);
            var legal = new Dir[4];
            for (var step = 0; step < (int)targetMinusOne; step++)
            {
                var count = 0;
                for (var d = 0; d < 4; d++)
                    if (board.CanPrepend((Dir)d)) legal[count++] = (Dir)d;
                if (count == 0) break;
                // Leading digits of an integer are zero. Redundant digits and
                // codes that become trapped are allowed; this is not a ranking.
                var digit = step / 4 < digits.Length ? (digits[step / 4] >> (2 * (step % 4))) & 3 : 0;
                board.Prepend(legal[digit % count]);
            }
            board.Validate();
            return board;
        }

        public static ConstructionCode Encode(BackwardGenerator board)
        {
            ArgumentNullException.ThrowIfNull(board);
            board.Validate();
            var area = Area(board.Width, board.Height);
            var recipe = board.Recipe();
            var replay = new BackwardGenerator(board.Width, board.Height, recipe.FinishX, recipe.FinishY);
            var digits = new byte[(recipe.Growth.Length + 3L) / 4];
            for (var step = 0; step < recipe.Growth.Length; step++)
            {
                var direction = "URDL".IndexOf(recipe.Growth[step]);
                var index = 0;
                for (var d = 0; d < direction; d++) if (replay.CanPrepend((Dir)d)) index++;
                digits[step / 4] |= (byte)(index << (2 * (step % 4)));
                replay.Prepend((Dir)direction);
            }
            var choices = new BigInteger(digits, isUnsigned: true, isBigEndian: false);
            var finish = recipe.FinishY * board.Width + recipe.FinishX;
            var value = (choices * area + finish) * area + board.Count - 1;
            var hex = Convert.ToHexString(value.ToByteArray(isUnsigned: true, isBigEndian: true)).TrimStart('0');
            return new ConstructionCode { Format = Format, Width = board.Width, Height = board.Height,
                CodeHex = hex.Length == 0 ? "0" : hex };
        }
    }
}
