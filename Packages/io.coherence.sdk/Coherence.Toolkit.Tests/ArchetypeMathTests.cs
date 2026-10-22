// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Toolkit.Tests
{
    using System;
    using NUnit.Framework;
    using Coherence.Tests;

    public class ArchetypeMathTests : CoherenceTest
    {
        enum TestEnum
        {
            Zero,
            One,
            Two,
            Three,
        }

        public enum LargeEnum
        {
            A = 1231,
            B = 555,
            C = 23423424,
            D = 10,
        }

        [Flags]
        enum TestFlags
        {
            Zero = 0,
            One = 1,
            Two = 2,
            Three = One | Two,
            Four = 4,
            Eight = 8,
        }

        [Flags]
        enum SmallFlags
        {
            Zero = 0,
            One = 1,
        }

        [Flags]
        enum ExtraTestFlags
        {
            Zero = 0,
            One = 1,
            Two = 2,
            Three = One | Two,
            Four = 4,
            Eight = 8,
            Thirteen = Eight | Four | One,
        }

        enum HalfInt { Zero = 0, Max = int.MaxValue }
        enum AlmostFullInt { Min = -1, Max = int.MaxValue }
        enum FullInt { Min = int.MinValue, Max = int.MaxValue }

        [TestCase(typeof(TestEnum), 2, TestEnum.Zero, TestEnum.Three)]
        [TestCase(typeof(LargeEnum), 25, LargeEnum.D, LargeEnum.C)]
        [TestCase(typeof(TestFlags), 4, TestFlags.Zero, TestFlags.One | TestFlags.Two | TestFlags.Four | TestFlags.Eight)]
        [TestCase(typeof(SmallFlags), 1, TestFlags.Zero, TestFlags.One)]
        [TestCase(typeof(ExtraTestFlags), 4, TestFlags.Zero, TestFlags.One | TestFlags.Two | TestFlags.Four | TestFlags.Eight)]
        [TestCase(typeof(HalfInt), 31, HalfInt.Zero, HalfInt.Max)]
        [TestCase(typeof(AlmostFullInt), 32, AlmostFullInt.Min, AlmostFullInt.Max)]
        [TestCase(typeof(FullInt), 32, FullInt.Min, FullInt.Max)]
        public void GetRangeAndBitsForEnum_Works(Type testEnum, int expectedBits, int expectedMin, int expectedMax)
        {
            var (bits, min, max) = ArchetypeMath.GetRangeAndBitsForEnum(testEnum);
            Assert.That(bits, Is.EqualTo(expectedBits));
            Assert.That(min, Is.EqualTo(expectedMin));
            Assert.That(max, Is.EqualTo(expectedMax));
        }

        [TestCase(4, 0.1, 1u)]
        [TestCase(5, 0.1, 3u)]
        [TestCase(32, 0.001, 4294967u)]
        public void GetRangeByBitsAndPrecision_Works(int bits, double precision, ulong expectedRange)
        {
            ulong range = ArchetypeMath.GetTotalRangeByBitsAndPrecision(bits, precision);
            Assert.That(range, Is.EqualTo(expectedRange));
        }
    }
}
