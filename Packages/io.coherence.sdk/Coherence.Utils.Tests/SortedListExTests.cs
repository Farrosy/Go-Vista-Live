// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Utils.Tests
{
    using System;
    using System.Collections.Generic;
    using Coherence.Tests;
    using NUnit.Framework;

    public class SortedListExTests
    {
        private SortedListEx<int, string> list;

        [SetUp]
        public void SetUp()
        {
            list = new SortedListEx<int, string>();
            list.Add(3, "three");
            list.Add(1, "one");
            list.Add(4, "four");
            list.Add(2, "two");
        }

        [Description("foreach over a typed SortedListEx variable must produce zero GC allocations.")]
        [Test]
        public void Foreach_NoAllocations()
        {
            var sink = 0;

            AllocCounter.AssertNoAllocs(() =>
            {
                foreach (var (key, _) in list)
                {
                    sink += key;
                }
            });
        }

        [Description("foreach over an empty SortedListEx must produce zero GC allocations.")]
        [Test]
        public void Foreach_EmptyList_NoAllocations()
        {
            var empty = new SortedListEx<int, string>();

            AllocCounter.AssertNoAllocs(() =>
            {
                foreach (var _ in empty)
                {
                    // nothing
                }
            });
        }

        [Description("foreach over a SortedListEx upcast to SortedList must allocate (validates the baseline).")]
        [Test]
        public void Foreach_Upcast_Allocates()
        {
            // When the compile-time type is SortedList, the base GetEnumerator() is called,
            // which returns IEnumerator<> and boxes the Enumerator struct.
            SortedList<int, string> upcast = list;

            var sink = 0;

            var allocations = AllocCounter.InstrumentWithWarmup(() =>
            {
                foreach (var (key, _) in upcast)
                {
                    sink += key;
                }
            });

            Assert.That(allocations, Is.GreaterThan(0));
        }

        [Description("SortedListEx must iterate entries in ascending key order.")]
        [Test]
        public void Foreach_IteratesInAscendingKeyOrder()
        {
            var keys = new List<int>();

            foreach (var (key, _) in list)
            {
                keys.Add(key);
            }

            Assert.That(keys, Is.EqualTo(new[] { 1, 2, 3, 4 }).AsCollection);
        }

        [Description("SortedListEx must visit every entry exactly once.")]
        [Test]
        public void Foreach_VisitsAllEntries()
        {
            var visited = new List<string>();

            foreach (var (_, value) in list)
            {
                visited.Add(value);
            }

            Assert.That(visited, Is.EquivalentTo(new[] { "one", "two", "three", "four" }));
        }

        [Description("Mutating the list during enumeration must throw InvalidOperationException.")]
        [Test]
        public void Foreach_ThrowsOnMutationDuringEnumeration()
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                foreach (var (key, _) in list)
                {
                    list.Add(99, "ninety-nine");
                }
            });
        }
    }
}
