// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Entities.Tests
{
    using System;
    using Coherence.SimulationFrame;
    using NUnit.Framework;
    using Coherence.Tests;
    using Coherence.Common;
    using Coherence.ProtocolDef;
    using System.Numerics;
    using Coherence.Common.Tests;

    public class DeltaComponentsTests : CoherenceTest
    {
        private PooledSchemaDataFactory schemaDataFactory;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();
            
            schemaDataFactory = new PooledSchemaDataFactory(TestSchema.Definition, threadSafe: false, poolInputs: true);
        }

        [TearDown]
        public override void TearDown()
        {
            schemaDataFactory.AssertNoLeaks();

            base.TearDown();
        }

        [Test]
        [Description("Verifies that component is removed when remove is merged")]
        public void Merge_RemovesComponent()
        {
            // Arrange
            DeltaComponents components = new DeltaComponentsBuilder(schemaDataFactory)
                .AddGlobal()
                .Build();

            DeltaComponents remove = new DeltaComponentsBuilder(schemaDataFactory)
                .RemoveGlobal()
                .Build();

            // Act
            components.Merge(remove);

            // Assert
            Assert.That(components.HasComponent(GlobalComponent.ComponentID), Is.False);
        }

        [Test]
        [Description("Verifies that component is appended when update is merged")]
        public void Merge_AddsComponent()
        {
            // Arrange
            DeltaComponents components = new DeltaComponentsBuilder(schemaDataFactory)
                .AddGlobal()
                .Build();

            DeltaComponents update = new DeltaComponentsBuilder(schemaDataFactory)
                .AddPosition()
                .Build();

            // Act
            components.Merge(update);

            // Assert
            Assert.That(components.HasComponent(GlobalComponent.ComponentID), Is.True);
            Assert.That(components.HasComponent(WorldPositionComponent.ComponentID), Is.True);

            // Clean up
            components.Reset();
        }

        [Test]
        [Description("Verifies that component's value is updated when update is merged")]
        public void Merge_UpdatesValue()
        {
            // Arrange
            DeltaComponents components = new DeltaComponentsBuilder(schemaDataFactory)
                .AddPosition(Vector3.Zero)
                .Build();

            Vector3 updatedPosition = Vector3.One;

            DeltaComponents update = new DeltaComponentsBuilder(schemaDataFactory)
                .AddPosition(updatedPosition)
                .Build();

            // Act
            components.Merge(update);

            // Assert
            Assert.That(components.TryGetComponent(WorldPositionComponent.ComponentID, out var comp), Is.True);
            Assert.That(WorldPositionComponent.From(comp).Value.ToCoreVector3(), Is.EqualTo(updatedPosition));

            // Clean up
            components.Reset();
        }

        [Test]
        [Description("Verifies that pending remove is cleared when update for that component is merged")]
        public void Merge_UpdateClearsRemove()
        {
            // Arrange
            DeltaComponents components = new DeltaComponentsBuilder(schemaDataFactory)
                .RemoveGlobal()
                .Build();

            DeltaComponents update = new DeltaComponentsBuilder(schemaDataFactory)
                .AddGlobal()
                .Build();

            // Act
            components.Merge(update);

            // Assert
            Assert.That(components.HasDestroy(GlobalComponent.ComponentID), Is.False);
            Assert.That(components.HasComponent(GlobalComponent.ComponentID));

            // Clean up
            components.Reset();
        }

        [Test]
        [Description("Verifies that component's simFrames is updated when update is merged")]
        public void Merge_UpdatesSimFrames()
        {
            // Arrange
            DeltaComponents components = new DeltaComponentsBuilder(schemaDataFactory)
                .AddSimFramesComponent(
                    intValue: 3, intSimulationFrame: 5,
                    float0Value: 2.2f,
                    float1Value: 3.3f, floatSimulationFrame: 6)
                .Build();

            DeltaComponents update = new DeltaComponentsBuilder(schemaDataFactory)
                .AddSimFramesComponent(
                    float1Value: 4.4f, floatSimulationFrame: 8)
                .Build();

            // Act
            components.Merge(update);

            // Assert
            Assert.That(components.TryGetComponent(SimFramesComponent.ComponentID, out var component), Is.True);
            var simFramesComp = SimFramesComponent.From(component);
            Assert.That(simFramesComp.SimFrameIntValue, Is.EqualTo(3));
            Assert.That(simFramesComp.SimFrameIntValueSimFrame, Is.EqualTo((AbsoluteSimulationFrame)5));
            Assert.That(simFramesComp.FloatValue, Is.EqualTo(2.2f));
            Assert.That(simFramesComp.SimFrameFloatValue, Is.EqualTo(4.4f));
            Assert.That(simFramesComp.SimFrameFloatValueSimFrame, Is.EqualTo((AbsoluteSimulationFrame)8));

            // Clean up
            components.Reset();
        }

        [Test]
        [Description("Verifies that the floating origin isn't updated if the updates don't contain WorldPosition component.")]
        public void Merge_NoWorldPosition_DoesntUpdateFloatingOrigin()
        {
            // Arrange
            var initialFloatingOrigin = new Vector3d(1000, 0, 0);
            var updatedFloatingOrigin = new Vector3d(2000, 0, 0);

            var components = new DeltaComponentsBuilder(schemaDataFactory)
                .AddPosition()
                .WithFloatingOrigin(initialFloatingOrigin)
                .Build();

            var update = new DeltaComponentsBuilder(schemaDataFactory)
                .AddGlobal()
                .WithFloatingOrigin(updatedFloatingOrigin)
                .Build();

            // Act
            components.Merge(update);

            // Assert
            Assert.That(components.Updates.FloatingOrigin, Is.EqualTo(initialFloatingOrigin));

            // Clean up
            components.Reset();
        }

        [Test]
        [Description("Verifies that the floating origin is updated if the updates contain WorldPosition component.")]
        public void Merge_WithWorldPosition_UpdatesFloatingOrigin()
        {
            // Arrange
            var initialFloatingOrigin = new Vector3d(1000, 0, 0);
            var updatedFloatingOrigin = new Vector3d(2000, 0, 0);

            var components = new DeltaComponentsBuilder(schemaDataFactory)
                .AddPosition()
                .WithFloatingOrigin(initialFloatingOrigin)
                .Build();

            var update = new DeltaComponentsBuilder(schemaDataFactory)
                .AddPosition()
                .WithFloatingOrigin(updatedFloatingOrigin)
                .Build();

            // Act
            components.Merge(update);

            // Assert
            Assert.That(components.Updates.FloatingOrigin, Is.EqualTo(updatedFloatingOrigin));

            // Clean up
            components.Reset();
        }

        [Test]
        [Description("Verifies that Dispose returns the pooled ComponentMap and ComponentDestroyMap to the factory pool")]
        public void Dispose_ReturnsPooledMapsToPool()
        {
            var deltaComponents = DeltaComponents.New(schemaDataFactory);

            deltaComponents.Dispose();

            // AssertNoLeaks() in TearDown verifies both maps were returned
        }

        [Test]
        [Description("Verifies that Dispose propagates through pooled maps that contain components, returning all to the pool")]
        public void Dispose_WithComponents_ReturnsAllToPool()
        {
            var deltaComponents = DeltaComponents.New(schemaDataFactory);
            var component = GlobalComponent.Create(schemaDataFactory);
            component.FieldsMask = component.InitialFieldsMask();
            deltaComponents.UpdateComponent(component);
            deltaComponents.RemoveComponent(WorldPositionComponent.ComponentID);

            deltaComponents.Dispose();

            // AssertNoLeaks() in TearDown verifies all rented objects were returned
        }

        [Test]
        [Description("Verifies that Dispose clears OrderedUpdateTime")]
        public void Dispose_ClearsOrderedUpdateTime()
        {
            var deltaComponents = DeltaComponents.New(schemaDataFactory);
            deltaComponents.OrderedUpdateTime = DateTime.UtcNow;

            deltaComponents.Dispose();

            Assert.That(deltaComponents.OrderedUpdateTime, Is.Null);
        }

        [Test]
        [Description("Verifies that Merge disposes the other DeltaComponents, returning its pooled maps to the factory pool")]
        public void Merge_DisposesOther_ReturnsToPool()
        {
            var components = DeltaComponents.New(schemaDataFactory);
            var other = DeltaComponents.New(schemaDataFactory);

            components.Merge(other);
            components.Dispose();

            // AssertNoLeaks() in TearDown verifies all four pooled maps were returned
        }

        [Test]
        public void GetMinSimulationFrame_Works()
        {
            var component = SimFramesComponent.Create(schemaDataFactory,
                simFrameIntValue: default,
                floatValue: default,
                simFrameFloatValue: default,
                simFrameIntValueSimFrame: 3,
                simFrameFloatValueSimFrame: 2 // skipped because mask is 0
            );
            var simFramesComponent = SimFramesComponent.From(component);
            simFramesComponent.IsSimFrameIntValueSet = true;
            simFramesComponent.IsFloatValueSet = true;
            simFramesComponent.IsSimFrameFloatValueSet = false;

            Assert.That(simFramesComponent.Data.GetMinSimulationFrame(), Is.EqualTo((AbsoluteSimulationFrame)3));
            component.Dispose();

            component = SimFramesComponent.Create(schemaDataFactory,
                simFrameIntValue: default,
                floatValue: default,
                simFrameFloatValue: default,
                simFrameIntValueSimFrame: 3,
                simFrameFloatValueSimFrame: 2
            );
            simFramesComponent = SimFramesComponent.From(component);
            simFramesComponent.IsSimFrameIntValueSet = true;
            simFramesComponent.IsFloatValueSet = true;
            simFramesComponent.IsSimFrameFloatValueSet = true;

            Assert.That(simFramesComponent.Data.GetMinSimulationFrame(), Is.EqualTo((AbsoluteSimulationFrame)2));
            component.Dispose();

            component = SimFramesComponent.Create(schemaDataFactory,
                simFrameIntValue: default,
                floatValue: default,
                simFrameFloatValue: default,
                simFrameIntValueSimFrame: 3, // skipped because mask is 0
                simFrameFloatValueSimFrame: 2 // skipped because mask is 0
            );
            simFramesComponent = SimFramesComponent.From(component);
            simFramesComponent.IsSimFrameIntValueSet = false;
            simFramesComponent.IsFloatValueSet = true;
            simFramesComponent.IsSimFrameFloatValueSet = false;

            Assert.That(simFramesComponent.Data.GetMinSimulationFrame(), Is.EqualTo(null));
            component.Dispose();
        }
    }
}
