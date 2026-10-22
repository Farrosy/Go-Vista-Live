// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Common
{
    using System;

    public static class BitMath
    {
        public static int BitsNeededForValue(long value) => Log2Ceil(value);

        public static int BitsNeededForRange(long minValue, long maxValue) =>
            BitsNeededForValue(maxValue - minValue + 1);

        private static int Log2Ceil(long value)
        {
            if (value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            var bits = 0;
            var v = value;
            while (v > 1)
            {
                v >>= 1;
                bits++;
            }

            if (!IsPowerOfTwo(value))
            {
                bits++;
            }

            return bits;
        }

        private static bool IsPowerOfTwo(long v) => (v & (v - 1)) == 0;
    }
}
