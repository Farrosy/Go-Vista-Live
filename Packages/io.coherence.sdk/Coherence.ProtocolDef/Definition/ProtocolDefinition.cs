// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.ProtocolDef
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using Coherence.Schema;

    public class ProtocolDefinition
    {
        private Dictionary<uint, ProtocolComponentDefinition> components;
        private Dictionary<uint, ProtocolCommandDefinition> commands;
        private Dictionary<uint, ProtocolInputDefinition> inputs;
        private Dictionary<uint, ProtocolArchetypeDefinition> archetypes;
        private Dictionary<string, uint> archetypeIDByName;

        public IReadOnlyDictionary<uint, ProtocolComponentDefinition> Components => components;
        public IReadOnlyDictionary<uint, ProtocolCommandDefinition> Commands => commands;
        public IReadOnlyDictionary<uint, ProtocolInputDefinition> Inputs => inputs;
        public IReadOnlyDictionary<uint, ProtocolArchetypeDefinition> Archetypes => archetypes;

        public string SchemaHash { get; private set; }

        public static readonly ProtocolDefinition RSSchema = new ProtocolDefinition(
            "",
            new Dictionary<uint, ProtocolComponentDefinition>()
            {
                { 0, new ProtocolComponentDefinition(0, "WorldPosition", new List<ProtocolFieldDefinition>()
                    {
                        new("value", SchemaType.Vector3, FieldMeta.ForUncompressedFloat(), hasSimFrames: true)
                    },
                    new ComponentAttributes(priority: ComponentPriority.High))
                },
                { 1, new ProtocolComponentDefinition(1, "WorldOrientation", new List<ProtocolFieldDefinition>()
                    {
                        new("value", SchemaType.Quaternion, FieldMeta.ForBitCount(32), hasSimFrames: true)
                    },
                    new ComponentAttributes(priority: ComponentPriority.High))
                },
                { 3, new ProtocolComponentDefinition(3, "WorldPositionQuery", new List<ProtocolFieldDefinition>()
                    {
                        new("position", SchemaType.Vector3, FieldMeta.ForUncompressedFloat()),
                        new("radius", SchemaType.Float, FieldMeta.ForUncompressedFloat()),
                        new("buffer", SchemaType.Float, FieldMeta.ForUncompressedFloat())
                    },
                    new ComponentAttributes(priority: ComponentPriority.High))
                },
                { 4, new ProtocolComponentDefinition(4, "ArchetypeComponent", new List<ProtocolFieldDefinition>()
                    {
                        new("index", SchemaType.Int, FieldMeta.ForInt(-2147483648, 2147483647)),
                    })
                },
                { 5, new ProtocolComponentDefinition(5, "Persistence", new List<ProtocolFieldDefinition>()) },
                { 6, new ProtocolComponentDefinition(6, "ConnectedEntity", new List<ProtocolFieldDefinition>()
                    {
                        new("value", SchemaType.Entity, default),
                        new("pos", SchemaType.Vector3, FieldMeta.ForUncompressedFloat()),
                        new("rot", SchemaType.Quaternion, FieldMeta.ForBitCount(32)),
                        new("scale", SchemaType.Vector3, FieldMeta.ForUncompressedFloat()),
                    },
                    new ComponentAttributes(componentOrder: -1, sendOrdered: true))
                },
                { 7, new ProtocolComponentDefinition(7, "UniqueID", new List<ProtocolFieldDefinition>()
                    {
                        new("uuid", SchemaType.String, default),
                    })
                },
                { 8, new ProtocolComponentDefinition(8, "Connection", new List<ProtocolFieldDefinition>()
                    {
                        new("id", SchemaType.UInt, FieldMeta.ForUInt(0, 2147483647)),
                        new("type", SchemaType.Int, FieldMeta.ForInt(0, 7))
                    })
                },
                { 9, new ProtocolComponentDefinition(9, "ConnectionScene", new List<ProtocolFieldDefinition>()
                    {
                        new("value", SchemaType.UInt, FieldMeta.ForUInt(0, 4294967295)),
                    })
                },
                { 10, new ProtocolComponentDefinition(10, "Global", new List<ProtocolFieldDefinition>()) },
                { 11, new ProtocolComponentDefinition(11, "GlobalQuery", new List<ProtocolFieldDefinition>()) },
                { 12, new ProtocolComponentDefinition(12, "Tag", new List<ProtocolFieldDefinition>()
                    {
                        new("tag", SchemaType.String, default)
                    })
                },
                { 13, new ProtocolComponentDefinition(13, "TagQuery", new List<ProtocolFieldDefinition>()
                    {
                        new("tag", SchemaType.String, default)
                    })
                },
                { 14, new ProtocolComponentDefinition(14, "PreserveChildren", new List<ProtocolFieldDefinition>()) },
                { 15, new ProtocolComponentDefinition(15, "Scene", new List<ProtocolFieldDefinition>()
                    {
                        new("value", SchemaType.UInt, FieldMeta.ForUInt(0, 2147483647)),
                    })
                },
            },
            new Dictionary<uint, ProtocolCommandDefinition>()
            {
                { 0, new ProtocolCommandDefinition(0, "AuthorityRequest", new List<ProtocolFieldDefinition>()
                    {
                        new("requester", SchemaType.UInt, FieldMeta.ForUInt()),
                        new("authorityType", SchemaType.Int, FieldMeta.ForInt(0, 3)),
                    })
                },
                { 1, new ProtocolCommandDefinition(1, "AuthorityTransfer", new List<ProtocolFieldDefinition>()
                    {
                        new("newAuthority", SchemaType.UInt, FieldMeta.ForUInt()),
                        new("accepted", SchemaType.Bool, default),
                        new("authorityType", SchemaType.Int, FieldMeta.ForInt(0, 3)),
                    })
                },
                { 2, new ProtocolCommandDefinition(2, "QuerySynced", new List<ProtocolFieldDefinition>()
                    {
                        new("liveQuerySynced", SchemaType.Bool, default),
                        new("globalQuerySynced", SchemaType.Bool, default),
                    })
                },
                { 3, new ProtocolCommandDefinition(3, "AdoptOrphan", new List<ProtocolFieldDefinition>()) },
                { 4, new ProtocolCommandDefinition(4, "PersistenceReady", new List<ProtocolFieldDefinition>()) },
                { 5, new ProtocolCommandDefinition(5, "SceneIndexChanged", new List<ProtocolFieldDefinition>()
                    {
                        new("sceneIndex", SchemaType.Int, FieldMeta.ForInt()),
                    })
                },
            },
            new Dictionary<uint, ProtocolInputDefinition>(),
            new Dictionary<uint, ProtocolArchetypeDefinition>());

        public ProtocolDefinition(
            string schemaHash,
            Dictionary<uint, ProtocolComponentDefinition> components,
            Dictionary<uint, ProtocolCommandDefinition> commands,
            Dictionary<uint, ProtocolInputDefinition> inputs,
            Dictionary<uint, ProtocolArchetypeDefinition> archetypes)
        {
            SchemaHash = schemaHash;
            this.components = components;
            this.commands = commands;
            this.inputs = inputs;
            this.archetypes = archetypes;

            this.archetypeIDByName = new Dictionary<string, uint>();
            foreach (var (id, archetype) in archetypes)
            {
                this.archetypeIDByName[archetype.Name] = id;
            }
        }

        public ProtocolComponentDefinition GetComponentDefinition(uint componentID)
        {
            if (!components.TryGetValue(componentID, out var component))
            {
                throw new Exception($"Component ID {componentID} not found in schema.");
            }

            return component;
        }

        public ProtocolCommandDefinition GetCommandDefinition(uint commandID)
        {
            if (!commands.TryGetValue(commandID, out var command))
            {
                throw new Exception($"Command ID {commandID} not found in schema.");
            }

            return command;
        }

        public ProtocolInputDefinition GetInputDefinition(uint inputID)
        {
            if (!inputs.TryGetValue(inputID, out var input))
            {
                throw new Exception($"Input ID {inputID} not found in schema.");
            }

            return input;
        }

        public bool TryGetEntityArchetype(uint archetypeID, out ProtocolArchetypeDefinition entityArchetype)
        {
            if (!archetypes.TryGetValue(archetypeID, out entityArchetype))
            {
                return false;
            }

            return true;
        }

        public bool TryGetEntityArchetypeIDByName(string name, out uint archetypeID)
        {
            return archetypeIDByName.TryGetValue(name, out archetypeID);
        }

        public int? GetMaxDataSize()
        {
            int? maxSize = null;

            foreach (var component in components.Values)
            {
                var componentSize = component.DataSize;
                if (maxSize == null || componentSize > maxSize)
                {
                    maxSize = componentSize;
                }
            }

            foreach (var command in commands.Values)
            {
                var commandSize = command.DataSize;
                if (maxSize == null || commandSize > maxSize)
                {
                    maxSize = commandSize;
                }
            }

            foreach (var input in inputs.Values)
            {
                var inputSize = input.DataSize;
                if (maxSize == null || inputSize > maxSize)
                {
                    maxSize = inputSize;
                }
            }

            return maxSize;
        }

        public static ProtocolDefinition LoadFromText(string schemaText, bool includeRSSchema)
        {
            var schemaDefinition = SchemaReader.Read(schemaText.Replace("\r\n", "\n"));
            return ProtocolSchemaParser.Parse(schemaDefinition.SchemaId, new[] { schemaDefinition }, includeRSSchema);
        }

        public static ProtocolDefinition LoadFromPath(string path, bool includeRSSchema)
        {
            var schemaText = File.ReadAllText(path, Encoding.UTF8);
            return LoadFromText(schemaText, includeRSSchema);
        }

        public static ProtocolDefinition LoadFromTexts(IEnumerable<string> schemaTexts, bool includeRSSchema)
        {
            var combinedSchema = schemaTexts.Aggregate((a, b) => a + "\n" + b);
            return LoadFromText(combinedSchema, includeRSSchema);
        }

        public static ProtocolDefinition LoadFromPaths(string[] paths, bool includeRSSchema)
        {
            var schemaDefinitions = new List<string>();
            foreach (var path in paths)
            {
                var schemaText = File.ReadAllText(path, Encoding.UTF8);
                schemaDefinitions.Add(schemaText);
            }

            return LoadFromTexts(schemaDefinitions, includeRSSchema);
        }
    }
}
