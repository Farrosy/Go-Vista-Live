// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Runtime.Tests
{
    using System;
    using System.Threading.Tasks;
    using Coherence.Tests;
    using NUnit.Framework;
    using Utils;

    /// <summary>
    /// Unit tests for <see cref="TimeSpanExtensions"/>.
    /// </summary>
    public sealed class TimeSpanExtensionsTests : CoherenceTest
    {
        [TestCase(0d), TestCase(0.01d)]
        public async Task Await_TimeSpan_Completes(double seconds) => await TimeSpan.FromSeconds(seconds);

        [TestCase(60d, 60)]
        [TestCase(59.9d, 60, Description = "A request never lasts less than asked for.")]
        [TestCase(0.5d, 1)]
        [TestCase(0d, 0)]
        [TestCase(-5d, 0)]
        public void ToWholeSeconds_RoundsUpAndClamps_ForTheGivenSpan(double seconds, int expected)
            => Assert.That(TimeSpan.FromSeconds(seconds).ToWholeSeconds(), Is.EqualTo(expected));

        [Test]
        public void ToWholeSeconds_ReturnsIntMaxValue_WhenTheSpanIsTooLong()
            => Assert.That(TimeSpan.MaxValue.ToWholeSeconds(), Is.EqualTo(int.MaxValue));
    }
}
