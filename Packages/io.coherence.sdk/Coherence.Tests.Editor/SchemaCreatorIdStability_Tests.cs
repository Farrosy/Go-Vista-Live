// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Tests.Editor
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Coherence;
    using Coherence.Editor;
    using Coherence.Schema;
    using NUnit.Framework;

    public class SchemaCreatorIdStability_Tests
    {
        private static readonly IdOffsets EmptyOffsets = new();
        private static readonly SchemaCreator.PreviousIds NoPrevious = new();

        [Test]
        public void Components_Commands_Inputs_Receive_Ids_On_First_Bake()
        {
            var schema = new SchemaDefinition();
            schema.ComponentDefinitions.Add(new ComponentDefinition("Comp_A"));
            schema.CommandDefinitions.Add(new CommandDefinition("Cmd_A"));
            schema.InputDefinitions.Add(new InputDefinition("Input_A", new List<FieldDefinition>()));

            SchemaCreator.SetDefinitionIds(EmptyOffsets, NoPrevious, schema);

            Assert.That(schema.ComponentDefinitions[0].id, Is.GreaterThanOrEqualTo(0));
            Assert.That(schema.CommandDefinitions[0].id, Is.GreaterThanOrEqualTo(0));
            Assert.That(schema.InputDefinitions[0].id, Is.GreaterThanOrEqualTo(0));
        }

        [Test]
        public void Ids_Are_Stable_Across_Rebakes_With_Same_Definitions()
        {
            var schema1 = MakeSchema(components: new[] { "Comp_A", "Comp_B" },
                commands: new[] { "Cmd_A" },
                inputs: new[] { "Input_A" });

            SchemaCreator.SetDefinitionIds(EmptyOffsets, NoPrevious, schema1);

            var schema2 = MakeSchema(components: new[] { "Comp_A", "Comp_B" },
                commands: new[] { "Cmd_A" },
                inputs: new[] { "Input_A" });

            SchemaCreator.SetDefinitionIds(EmptyOffsets, BuildPreviousIds(schema1), schema2);

            Assert.That(Id(schema2.ComponentDefinitions, "Comp_A"), Is.EqualTo(Id(schema1.ComponentDefinitions, "Comp_A")));
            Assert.That(Id(schema2.ComponentDefinitions, "Comp_B"), Is.EqualTo(Id(schema1.ComponentDefinitions, "Comp_B")));
            Assert.That(Id(schema2.CommandDefinitions, "Cmd_A"), Is.EqualTo(Id(schema1.CommandDefinitions, "Cmd_A")));
            Assert.That(Id(schema2.InputDefinitions, "Input_A"), Is.EqualTo(Id(schema1.InputDefinitions, "Input_A")));
        }

        [Test]
        public void Adding_A_Definition_Does_Not_Change_Existing_Ids()
        {
            var schema1 = MakeSchema(components: new[] { "Comp_A", "Comp_B" });
            SchemaCreator.SetDefinitionIds(EmptyOffsets, NoPrevious, schema1);

            // Comp_New sorts before Comp_A alphabetically, which is exactly the scenario that used to shift IDs.
            var schema2 = MakeSchema(components: new[] { "Comp_A", "Comp_B", "Comp_New" });
            SchemaCreator.SetDefinitionIds(EmptyOffsets, BuildPreviousIds(schema1), schema2);

            Assert.That(Id(schema2.ComponentDefinitions, "Comp_A"), Is.EqualTo(Id(schema1.ComponentDefinitions, "Comp_A")));
            Assert.That(Id(schema2.ComponentDefinitions, "Comp_B"), Is.EqualTo(Id(schema1.ComponentDefinitions, "Comp_B")));

            var maxPreviousId = schema1.ComponentDefinitions.Max(c => c.id);
            Assert.That(Id(schema2.ComponentDefinitions, "Comp_New"), Is.GreaterThan(maxPreviousId));
        }

        [Test]
        public void Removing_A_Definition_Does_Not_Change_Remaining_Ids()
        {
            var schema1 = MakeSchema(components: new[] { "Comp_A", "Comp_B", "Comp_C" });
            SchemaCreator.SetDefinitionIds(EmptyOffsets, NoPrevious, schema1);

            var schema2 = MakeSchema(components: new[] { "Comp_A", "Comp_C" });
            SchemaCreator.SetDefinitionIds(EmptyOffsets, BuildPreviousIds(schema1), schema2);

            Assert.That(Id(schema2.ComponentDefinitions, "Comp_A"), Is.EqualTo(Id(schema1.ComponentDefinitions, "Comp_A")));
            Assert.That(Id(schema2.ComponentDefinitions, "Comp_C"), Is.EqualTo(Id(schema1.ComponentDefinitions, "Comp_C")));
        }

        [Test]
        public void Archetype_LodItem_Ids_Are_Stable_Across_Rebakes()
        {
            var schema1 = MakeSchemaWithArchetype(
                componentName: "Comp_A",
                archetypeName: "Arch_A",
                lodLevel: 0);

            SchemaCreator.SetDefinitionIds(EmptyOffsets, NoPrevious, schema1);

            var schema2 = MakeSchemaWithArchetype(
                componentName: "Comp_A",
                archetypeName: "Arch_A",
                lodLevel: 0);

            SchemaCreator.SetDefinitionIds(EmptyOffsets, BuildPreviousIds(schema1), schema2);

            var item1 = schema1.ArchetypeDefinitions[0].lods[0].items[0];
            var item2 = schema2.ArchetypeDefinitions[0].lods[0].items[0];
            Assert.That(item2.id, Is.EqualTo(item1.id));
        }

        // --- helpers ---

        private static SchemaDefinition MakeSchema(
            string[] components = null,
            string[] commands = null,
            string[] inputs = null)
        {
            var schema = new SchemaDefinition();
            foreach (var name in components ?? Array.Empty<string>())
                schema.ComponentDefinitions.Add(new ComponentDefinition(name));
            foreach (var name in commands ?? Array.Empty<string>())
                schema.CommandDefinitions.Add(new CommandDefinition(name));
            foreach (var name in inputs ?? Array.Empty<string>())
                schema.InputDefinitions.Add(new InputDefinition(name, new List<FieldDefinition>()));
            return schema;
        }

        private static SchemaDefinition MakeSchemaWithArchetype(string componentName, string archetypeName, int lodLevel)
        {
            var schema = new SchemaDefinition();
            schema.ComponentDefinitions.Add(new ComponentDefinition(componentName));

            var lod = new ArchetypeLOD(lodLevel, 0f);
            lod.items.Add(new ArchetypeLODItem(componentName));
            schema.ArchetypeDefinitions.Add(new ArchetypeDefinition(archetypeName, new List<ArchetypeLOD> { lod }));
            return schema;
        }

        private static SchemaCreator.PreviousIds BuildPreviousIds(SchemaDefinition schema)
        {
            var prev = new SchemaCreator.PreviousIds();
            foreach (var c in schema.ComponentDefinitions)
            {
                prev.ComponentIds[c.name] = c.id;
                prev.MaxComponentId = Math.Max(prev.MaxComponentId, c.id);
            }
            foreach (var cmd in schema.CommandDefinitions)
            {
                prev.CommandIds[cmd.name] = cmd.id;
                prev.MaxCommandId = Math.Max(prev.MaxCommandId, cmd.id);
            }
            foreach (var arch in schema.ArchetypeDefinitions)
            {
                prev.ArchetypeIds[arch.name] = arch.id;
                prev.MaxArchetypeId = Math.Max(prev.MaxArchetypeId, arch.id);
                foreach (var lod in arch.lods)
                    foreach (var item in lod.items)
                    {
                        prev.LodItemIds[$"{arch.name}:{lod.level}:{item.name}"] = item.id;
                        prev.MaxComponentId = Math.Max(prev.MaxComponentId, item.id);
                    }
            }
            foreach (var inp in schema.InputDefinitions)
            {
                prev.InputIds[inp.name] = inp.id;
                prev.MaxInputId = Math.Max(prev.MaxInputId, inp.id);
            }
            return prev;
        }

        private static int Id(IEnumerable<BaseDefinition> defs, string name)
            => defs.First(d => d.name == name).id;
    }
}
