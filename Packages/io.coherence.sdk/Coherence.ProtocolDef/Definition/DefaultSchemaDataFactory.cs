// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.ProtocolDef
{
    using Coherence.Connection;
    using Coherence.Entities;
    using Coherence.SimulationFrame;

    public class DefaultSchemaDataFactory : ISchemaDataFactory
    {
        private ProtocolDefinition schemaDefinition;
        private ManagedTypesTables managedTypesTables;

        public DefaultSchemaDataFactory(ProtocolDefinition schemaDefinition)
        {
            this.schemaDefinition = schemaDefinition;
            this.managedTypesTables = new ManagedTypesTables();
        }

        public ComponentData CreateComponent(uint componentID)
        {
            var componentDef = schemaDefinition.GetComponentDefinition(componentID);

            var data = new byte[componentDef.DataSize];
            var simFrames = new AbsoluteSimulationFrame[ProtocolComponentDefinition.MaxFields];

            return new ComponentData(componentDef, managedTypesTables, data, simFrames);
        }

        public CommandData CreateCommand(uint commandID, Entity entity, MessageTarget messageTarget,
            bool usesMeta = default, ClientID senderClientID = default, long frame = default)
        {
            var commandDef = schemaDefinition.GetCommandDefinition(commandID);

            var data = new byte[commandDef.DataSize];

            return new CommandData(commandDef, managedTypesTables, data, entity, messageTarget, usesMeta, senderClientID, frame);
        }

        public InputData CreateInput(uint inputID, Entity entity, MessageTarget messageTarget, AbsoluteSimulationFrame frame)
        {
            var inputDef = schemaDefinition.GetInputDefinition(inputID);

            var data = new byte[inputDef.DataSize];

            return new InputData(inputDef, managedTypesTables, data, entity, messageTarget, frame);
        }

        public ComponentMap CreateComponentMap() => new ComponentMap();

        public ComponentDestroyMap CreateComponentDestroyMap() => new ComponentDestroyMap();
    }
}
