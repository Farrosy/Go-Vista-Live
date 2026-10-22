// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.ProtocolDef
{
    public struct LocalUserComponent
    {
        public const uint ComponentID = 2;

        public ComponentData Data { get; private set; }

        public int LocalIndex
        {
            get => Data.GetField(0).GetInt();
            set => Data.GetField(0).SetInt(value);
        }

        private LocalUserComponent(ComponentData componentData)
        {
            if (componentData.ComponentID != ComponentID)
            {
                throw new System.InvalidOperationException($"Invalid component ID: {componentData.ComponentID}. Expected: {ComponentID}");
            }

            this.Data = componentData;
        }

        public static LocalUserComponent From(ComponentData componentData) => new(componentData);
    }
}
