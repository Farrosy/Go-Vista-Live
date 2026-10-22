// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Entities.Tests
{
    using Coherence.Tests;
    using Common;
    using Common.Tests;
    using NUnit.Framework;
    using ProtocolDef;

    public class SortedValueMapTests
    {
        [Description("Verifies that sorting the map does not allocate any memory.")]
        [Test]
        public void Sort_NoAllocations()
        {
            // Arrange
            var iterations = 500;

            var componentData = WorldPositionComponent.Create(TestSchema.DataFactory, Vector3d.zero, 0);

            var map = new SortedValueMap<uint, ComponentData>(ComponentDataComparer.Cached, capacity: iterations);

            AllocCounter.AssertNoAllocs(() =>
            {
                for (var i = 0; i < iterations; i++)
                {
                    map.Add((uint)i, componentData);
                    var sorted = map.Sorted;
                }

                map.Clear();
            });
        }
    }
}
