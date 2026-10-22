// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.ProtocolDef
{
    using System;
    using Coherence.Common;

    [Serializable]
    public struct FieldMeta
    {
        public FloatCompression Compression;
        public int BitCount;
        public long Minimum;
        public long Maximum;
        public double Precision;

        public static void EnsureValidTruncatedBitCount(int bits)
        {
            const int minBits = 10; // sign (1) + exponent (8) + at least 1 mantissa bit
            const int maxBits = 32; // full float

            if (bits < minBits) throw new ArgumentOutOfRangeException(nameof(bits), $"Number of bits must be greater than or equal to {minBits}, was {bits}");
            if (bits > maxBits) throw new ArgumentOutOfRangeException(nameof(bits), $"Number of bits must be less than or equal to {maxBits}, was {bits}");
        }

        public static FieldMeta ForInt(int minValue = int.MinValue, int maxValue = int.MaxValue)
        {
            return new FieldMeta()
            {
                BitCount = BitMath.BitsNeededForRange(minValue, maxValue),
                Minimum = minValue,
                Maximum = maxValue
            };
        }

        public static FieldMeta ForUInt(uint minValue = uint.MinValue, uint maxValue = uint.MaxValue)
        {
            return new FieldMeta()
            {
                BitCount = BitMath.BitsNeededForRange(minValue, maxValue),
                Minimum = minValue,
                Maximum = maxValue
            };
        }

        public static FieldMeta ForBitCount(int bitCount)
        {
            return new FieldMeta()
            {
                BitCount = bitCount
            };
        }

        public static FieldMeta ForUncompressedFloat()
        {
            return new FieldMeta()
            {
                Compression = FloatCompression.None,
                BitCount = sizeof(float) * 8,
            };
        }

        public static FieldMeta ForTruncatedFloat(int bits)
        {
            EnsureValidTruncatedBitCount(bits);

            return new FieldMeta()
            {
                Compression = FloatCompression.Truncated,
                BitCount = bits,
            };
        }

        public static FieldMeta ForFixedPointFloat(long minRange, long maxRange, double precision)
        {
            return new FieldMeta()
            {
                Compression = FloatCompression.FixedPoint,
                Minimum = minRange,
                Maximum = maxRange,
                Precision = precision
            };
        }

        public override string ToString()
        {
            return $"FieldMeta(Compression={Compression}, BitCount={BitCount}, Minimum={Minimum}, Maximum={Maximum}, Precision={Precision})";
        }

    }
}
