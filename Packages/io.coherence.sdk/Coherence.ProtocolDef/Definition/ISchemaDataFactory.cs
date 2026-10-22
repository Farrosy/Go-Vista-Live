// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.ProtocolDef
{
    using Coherence.Connection;
    using Coherence.Entities;
    using Coherence.SimulationFrame;

    public interface ISchemaDataFactory
    {
        ComponentData CreateComponent(uint componentID);

        CommandData CreateCommand(uint commandID, Entity entity, MessageTarget messageTarget,
            bool usesMeta = default, ClientID senderClientID = default, long frame = default);

        InputData CreateInput(uint inputID, Entity entity, MessageTarget messageTarget, AbsoluteSimulationFrame frame);

        ComponentMap CreateComponentMap();

        ComponentDestroyMap CreateComponentDestroyMap();
    }

    public delegate void OnComponentDataDisposed(ComponentData componentData, byte[] data, AbsoluteSimulationFrame[] simFrames);
    public delegate void OnCommandDataDisposed(CommandData commandData, byte[] data);
    public delegate void OnInputDataDisposed(InputData inputData, byte[] data);
    public delegate void OnComponentMapDisposed(ComponentMap componentMap);
    public delegate void OnComponentDestroyMapDisposed(ComponentDestroyMap componentDestroyMap);

    public delegate void OnOwnershipChanged(object obj, string ownerIdentifier);
}
