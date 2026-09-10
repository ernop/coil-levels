using System;
using System.Buffers.Binary;
using System.Security.Cryptography;

namespace coil
{
    // Versioned reproducible stream: SHA-256(seed || uint64-BE counter),
    // uint32-LE words, rejection sampling to avoid modulo bias.
    public sealed class SeededChoices
    {
        public const string Algorithm = "sha256-counter-v1";
        private readonly byte[] input;
        private byte[] block = Array.Empty<byte>();
        private int offset;
        private ulong counter;
        public string SeedHex { get; }

        public SeededChoices(string seedHex)
        {
            var seed = Convert.FromHexString(seedHex);
            if (seed.Length < 32) throw new ArgumentException("seed-hex requires at least 32 bytes (64 hex characters).");
            SeedHex = Convert.ToHexString(seed).ToLowerInvariant();
            input = new byte[seed.Length + 8];
            seed.CopyTo(input, 0);
        }

        public static SeededChoices Create() => new SeededChoices(Convert.ToHexString(RandomNumberGenerator.GetBytes(64)));

        public int Next(int bound)
        {
            if (bound < 1) throw new ArgumentOutOfRangeException(nameof(bound));
            var limit = (1UL << 32) / (uint)bound * (uint)bound;
            while (true)
            {
                if (offset == block.Length)
                {
                    BinaryPrimitives.WriteUInt64BigEndian(input.AsSpan(input.Length - 8), counter);
                    counter = checked(counter + 1);
                    block = SHA256.HashData(input);
                    offset = 0;
                }
                var value = BinaryPrimitives.ReadUInt32LittleEndian(block.AsSpan(offset, 4));
                offset += 4;
                if (value < limit) return (int)(value % (uint)bound);
            }
        }
    }
}
