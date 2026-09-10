using System;
using System.Buffers.Binary;
using System.Security.Cryptography;

namespace coil
{
    // Fetch fresh system-random bytes in blocks instead of expanding a saved
    // fixed-width seed. Final construction codes supply deterministic replay.
    public sealed class FreshChoices
    {
        private readonly byte[] buffer = new byte[65536];
        private int offset = 65536;
        public long Draws { get; private set; }

        public int Next(int bound)
        {
            if (bound < 1) throw new ArgumentOutOfRangeException(nameof(bound));
            Draws++;
            var limit = (1UL << 32) / (uint)bound * (uint)bound;
            while (true)
            {
                if (offset == buffer.Length) { RandomNumberGenerator.Fill(buffer); offset = 0; }
                var value = BinaryPrimitives.ReadUInt32LittleEndian(buffer.AsSpan(offset, 4));
                offset += 4;
                if (value < limit) return (int)(value % (uint)bound);
            }
        }
    }
}
