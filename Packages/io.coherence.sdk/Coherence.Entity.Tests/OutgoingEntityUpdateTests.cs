// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Entities.Tests
{
    using Coherence.SimulationFrame;
    using NUnit.Framework;
    using Coherence.Tests;
    using Coherence.Serializer;
    using Coherence.ProtocolDef;
    using System.Numerics;
    using Coherence.Common.Tests;

    public class OutgoingEntityUpdateTests : CoherenceTest
    {
        [Description("Verifies that EntityState.Merge outputs correct entity operation in various configurations")]
        [TestCase(EntityOperation.Update, EntityOperation.Create, EntityOperation.Create)]      // Create should be preserved
        [TestCase(EntityOperation.Update, EntityOperation.Update, EntityOperation.Update)]      // Update + Update = Update
        [TestCase(EntityOperation.Create, EntityOperation.Update, EntityOperation.Create)]      // This is possible when dropped create is followed by dropped updates
        [TestCase(EntityOperation.Destroy, EntityOperation.Create, EntityOperation.Destroy)]    // Destroy always takes precedence
        [TestCase(EntityOperation.Destroy, EntityOperation.Update, EntityOperation.Destroy)]    // Destroy always takes precedence
        public void Merge_Works(EntityOperation bufferOperation, EntityOperation droppedOperation, EntityOperation expectedOperation)
        {
            // Arrange
            var inBufferState = OutgoingEntityUpdate.New();
            inBufferState.Operation = bufferOperation;

            var droppedState = OutgoingEntityUpdate.New();
            droppedState.Operation = droppedOperation;

            // Act
            droppedState.Add(inBufferState);

            // Assert
            Assert.That(droppedState.Operation, Is.EqualTo(expectedOperation));
        }

        [Test]
        [Description("Tests that clone OutgoingEntityUpdate has exact same values as the original. " +
            "From Operation and Priority, all the way down to field values.")]
        public void Clone_ReturnsExactValues()
        {
            // Arrange
            var operation = EntityOperation.Update;
            var priority = 5533;
            var position = new Vector3(1, 1, 1);
            var intVal = 44;
            var intSimFrame = (AbsoluteSimulationFrame)46;
            var floatVal = 35.35f;

            var original = new OutgoingEntityUpdate()
            {
                Operation = operation,
                Priority = priority,
                Components = new DeltaComponentsBuilder(TestSchema.DataFactory)
                    .AddPosition(position)
                    .AddSimFramesComponent(intVal, intSimFrame, floatVal)
                    .RemoveGlobal()
                    .Build()
            };

            // Act
            var clone = original.Clone(TestSchema.DataFactory);

            // Assert
            Assert.That(clone.Operation, Is.EqualTo(operation));
            Assert.That(clone.Priority, Is.EqualTo(priority));
            Assert.That(clone.Components.Updates.Count, Is.EqualTo(2));

            Assert.True(clone.Components.TryGetComponent(WorldPositionComponent.ComponentID, out var cloneWorldPosition));
            Assert.That(WorldPositionComponent.From(cloneWorldPosition).Value.ToCoreVector3(), Is.EqualTo(position));

            Assert.True(clone.Components.TryGetComponent(SimFramesComponent.ComponentID, out var cloneSimFrames));
            Assert.That(SimFramesComponent.From(cloneSimFrames).SimFrameIntValue, Is.EqualTo(intVal));
            Assert.That(SimFramesComponent.From(cloneSimFrames).SimFrameIntValueSimFrame, Is.EqualTo(intSimFrame));
            Assert.That(SimFramesComponent.From(cloneSimFrames).FloatValue, Is.EqualTo(floatVal));

            Assert.That(clone.Components.Destroys.Count, Is.EqualTo(1));
            Assert.True(clone.Components.Destroys.Contains(GlobalComponent.ComponentID));
        }

        [Test]
        [Description("Tests that the cloned OutgoingEntityUpdate doesn't share underlying memory in any layer. " +
            "From Operation and Priority, all the way down to field values.")]
        public void Clone_EditingClone_ShouldNotChangeOriginal()
        {
            // Arrange
            var operation = EntityOperation.Update;
            var priority = 5533;
            var position = new Vector3(12, 13, 14);
            var intVal = 44;
            var intSimFrame = (AbsoluteSimulationFrame)46;
            var floatVal = 35.35f;

            var original = new OutgoingEntityUpdate()
            {
                Operation = operation,
                Priority = priority,
                Components = new DeltaComponentsBuilder(TestSchema.DataFactory)
                    .AddPosition(position)
                    .AddSimFramesComponent(intVal, intSimFrame, floatVal)
                    .RemoveGlobal()
                    .Build()
            };

            // Act - clone and heavily edit it in many ways
            var clone = original.Clone(TestSchema.DataFactory);

            clone.Operation = EntityOperation.Create;
            clone.Priority = 1111;
            clone.Components.UpdateComponent(WorldPositionComponent.Create(TestSchema.DataFactory, Vector3.One, 0));
            clone.Components.UpdateComponent(SimFramesComponent.Create(TestSchema.DataFactory,
                simFrameIntValue: 1,
                simFrameFloatValue: 1f,
                floatValue: 1.1f,
                simFrameFloatValueSimFrame: 11,
                simFrameIntValueSimFrame: 111
            ));
            clone.Components.UpdateComponent(WorldOrientationComponent.Create(TestSchema.DataFactory, Quaternion.Identity, 0));
            clone.Components.Destroys.Add(GlobalQueryComponent.ComponentID);

            // Assert - that the original stays unchanged
            Assert.That(original.Operation, Is.EqualTo(operation));
            Assert.That(original.Priority, Is.EqualTo(priority));
            Assert.That(original.Components.Updates.Count, Is.EqualTo(2));

            Assert.True(original.Components.TryGetComponent(WorldPositionComponent.ComponentID, out var originalWorldPosition));
            Assert.That(WorldPositionComponent.From(originalWorldPosition).Value.ToCoreVector3(), Is.EqualTo(position));

            Assert.True(original.Components.TryGetComponent(SimFramesComponent.ComponentID, out var originalSimFrames));
            Assert.That(SimFramesComponent.From(originalSimFrames).SimFrameIntValue, Is.EqualTo(intVal));
            Assert.That(SimFramesComponent.From(originalSimFrames).SimFrameIntValueSimFrame, Is.EqualTo(intSimFrame));
            Assert.That(SimFramesComponent.From(originalSimFrames).FloatValue, Is.EqualTo(floatVal));

            Assert.That(original.Components.Destroys.Count, Is.EqualTo(1));
            Assert.True(original.Components.Destroys.Contains(GlobalComponent.ComponentID));
        }
    }
}
