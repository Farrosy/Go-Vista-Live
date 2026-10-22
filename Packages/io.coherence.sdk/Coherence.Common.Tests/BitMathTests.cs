// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Common.Tests
{
    using NUnit.Framework;

    public class BitMathTests
    {
        [TestCase(0, 1, 1)]
        [TestCase(0, 2, 2)]
        [TestCase(0, 3, 2)]
        [TestCase(0, 4, 3)]
        [TestCase(0, ushort.MaxValue, 16)]
        [TestCase(-1, ushort.MaxValue, 17)]
        [TestCase(0, int.MaxValue, 31)]
        [TestCase(-1, int.MaxValue, 32)]
        [TestCase(0, uint.MaxValue, 32)]
        [TestCase(int.MinValue, int.MaxValue, 32)]
        public void BitsNeededForRange_UsesExpectedBitCount(long minValue, long maxValue, int expectedBitCount)
        {
            Assert.That(BitMath.BitsNeededForRange(minValue, maxValue), Is.EqualTo(expectedBitCount));
        }
    }
}
