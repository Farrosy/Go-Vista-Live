// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Entities.Tests
{
    using System.Numerics;
    using Coherence.Common;
    using Coherence.Common.Tests;
    using Coherence.ProtocolDef;
    using Coherence.SimulationFrame;

    public class DeltaComponentsBuilder
    {
        private ISchemaDataFactory schemaDataFactory;
        private DeltaComponents components = DeltaComponents.New();

        public DeltaComponentsBuilder(ISchemaDataFactory schemaDataFactory)
        {
            this.schemaDataFactory = schemaDataFactory;
        }

        public DeltaComponents Build()
        {
            return components.ReleaseOwnershipOfComponents();
        }

        public DeltaComponentsBuilder AddConnectedEntity(Entity parent)
        {
            var connectedEntity = ConnectedEntityComponent.Create(schemaDataFactory, parent, Vector3.Zero, Quaternion.Identity, Vector3.One);
            return AddComponent(connectedEntity);
        }

        public DeltaComponentsBuilder AddSimFramesComponent(
            int? intValue = null, AbsoluteSimulationFrame intSimulationFrame = default,
            float? float0Value = null,
            float? float1Value = null, AbsoluteSimulationFrame floatSimulationFrame = default)
        {
            var component = SimFramesComponent.Create(schemaDataFactory,
                intValue ?? 0, intSimulationFrame,
                float0Value ?? 0,
                float1Value ?? 0, floatSimulationFrame);

            var simFramesComponent = SimFramesComponent.From(component);
            simFramesComponent.IsSimFrameIntValueSet = intValue != null;
            simFramesComponent.IsFloatValueSet = float0Value != null;
            simFramesComponent.IsSimFrameFloatValueSet = float1Value != null;

            components.UpdateComponent(simFramesComponent.Data);

            return this;
        }

        public DeltaComponentsBuilder AddGlobal()
        {
            return AddComponent(GlobalComponent.Create(schemaDataFactory));
        }

        public DeltaComponentsBuilder RemoveGlobal()
        {
            components.RemoveComponent(GlobalComponent.ComponentID);

            return this;
        }

        public DeltaComponentsBuilder AddPosition(Vector3 position = default)
        {
            var worldPosition = WorldPositionComponent.Create(schemaDataFactory, position, 0);
            return AddComponent(worldPosition);
        }

        public DeltaComponentsBuilder WithFloatingOrigin(Vector3d floatingOrigin)
        {
            components.Updates.FloatingOrigin = floatingOrigin;

            return this;
        }

        private DeltaComponentsBuilder AddComponent(ComponentData component)
        {
            component.FieldsMask = component.InitialFieldsMask();
            components.UpdateComponent(component);

            return this;
        }
    }
}
