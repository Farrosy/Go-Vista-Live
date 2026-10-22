// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.ProtocolDef
{
    using System.Collections.Generic;
    using System.Linq;

    public class ProtocolArchetypeLODDefinition
    {
        public uint Level;
        public float Distance;
        public Dictionary<uint, uint> ComponentReplacement;
        public uint[] ComponentsExcluded;

        public ProtocolArchetypeLODDefinition(uint level, float distance, Dictionary<uint, uint> componentReplacement, uint[] componentsExcluded)
        {
            Level = level;
            Distance = distance;
            ComponentReplacement = componentReplacement;
            ComponentsExcluded = componentsExcluded;
        }

        public uint[] RemovedComponentsAtLevel()
        {
            return ComponentsExcluded;
        }

        public bool IsExcludedComponentType(uint comp)
        {
            return ComponentsExcluded.Contains(comp);
        }

        public bool SpecializedComponentType(uint baseComponentType, out uint mapped)
        {
            return ComponentReplacement.TryGetValue(baseComponentType, out mapped);
        }
    }

    public struct ProtocolArchetypeDefinition
    {
        public uint ArchetypeID;
        public string Name;
        public ProtocolArchetypeLODDefinition[] LODs;

        public ProtocolArchetypeDefinition(uint archetypeID, string name, ProtocolArchetypeLODDefinition[] lods)
        {
            ArchetypeID = archetypeID;
            Name = name;
            LODs = lods;
        }

        public bool LODForDistance(double distance, out ProtocolArchetypeLODDefinition lod)
        {
            for (int i = LODs.Length - 1; i >= 0; i--)
            {
                lod = LODs[i];

                if (distance >= (double)lod.Distance)
                {
                    return true;
                }
            }

            lod = null;

            return false;
        }
    }
}
