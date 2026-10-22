// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Entities.Tests
{
    using Coherence.ProtocolDef;

    public static class UtilExtensions
    {
        public static bool HasComponent(this DeltaComponents components, uint componentID)
        {
            return components.Updates.Store.ContainsKey(componentID);
        }

        public static bool HasDestroy(this DeltaComponents components, uint componentID)
        {
            return components.Destroys.Contains(componentID);
        }

        public static bool TryGetComponent(this DeltaComponents components, uint componentID, out ComponentData component)
        {
            if (components.Updates.Store.TryGetValue(componentID, out component))
            {
                return true;
            }

            return false;
        }
    }
}
