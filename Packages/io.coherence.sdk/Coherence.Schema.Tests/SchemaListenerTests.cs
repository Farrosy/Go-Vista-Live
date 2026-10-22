// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Schema.Tests
{
    using System.Globalization;
    using System.Threading;
    using Coherence.Tests;
    using NUnit.Framework;

    public class SchemaListenerTests : CoherenceTest
    {
        // -----------------------------------------------------------------------
        // Components
        // -----------------------------------------------------------------------

        [Test]
        [Description("A component block is parsed into a single ComponentDefinition with the correct name and id.")]
        public void Component_ParsesNameAndId()
        {
            var comp = SchemaReader.Read("component MyComp [id \"42\"]\n").ComponentDefinitions[0];

            Assert.That(comp.name, Is.EqualTo("MyComp"));
            Assert.That(comp.id, Is.EqualTo(42));
        }

        [Test]
        [Description("A component with no field lines produces an empty members list.")]
        public void Component_NoFields_HasEmptyMembers() =>
            Assert.That(SchemaReader.Read("component Empty [id \"0\"]\n").ComponentDefinitions[0].members, Is.Empty);

        [Test]
        [Description("Multiple component blocks each produce their own entry in ComponentDefinitions.")]
        public void Component_MultipleBlocks_AllParsed() =>
            Assert.That(SchemaReader.Read("component A [id \"0\"]\ncomponent B [id \"1\"]\ncomponent C [id \"2\"]\n")
                .ComponentDefinitions, Has.Count.EqualTo(3));

        private static readonly object[][] KnownFieldTypes =
        {
            new object[] { "Int", SchemaType.Int },
            new object[] { "Float", SchemaType.Float },
            new object[] { "Bool", SchemaType.Bool },
            new object[] { "String", SchemaType.String },
            new object[] { "Vector2", SchemaType.Vector2 },
            new object[] { "Vector3", SchemaType.Vector3 },
            new object[] { "Quaternion", SchemaType.Quaternion },
            new object[] { "Entity", SchemaType.Entity },
            new object[] { "Bytes", SchemaType.Bytes },
            new object[] { "Color", SchemaType.Color },
        };

        [TestCaseSource(nameof(KnownFieldTypes))]
        [Description("A component field whose type matches a SchemaType name is parsed into that SchemaType.")]
        public void Component_KnownFieldType_ParsedCorrectly(string typeName, SchemaType expected)
        {
            var schema = $"component Test [id \"0\"]\n  value {typeName}\n";
            var field = SchemaReader.Read(schema).ComponentDefinitions[0].members[0];

            Assert.That(field.name, Is.EqualTo("value"));
            Assert.That(field.type, Is.EqualTo(expected));
        }

        [Test]
        [Description("A component field whose type is not a recognised SchemaType name is mapped to SchemaType.Enum, with the original name stored as enumName.")]
        public void Component_UnknownFieldType_FallsBackToEnum()
        {
            var field = SchemaReader.Read("component Test [id \"0\"]\n  state MoveState\n").ComponentDefinitions[0].members[0];

            Assert.That(field.type, Is.EqualTo(SchemaType.Enum));
            Assert.That(field.enumName, Is.EqualTo("MoveState"));
        }

        [Test]
        [Description("Field overrides such as compression and bits are stored in the field's overrides dictionary.")]
        public void Component_FieldOverrides_AreParsed()
        {
            const string schema = "component Test [id \"0\"]\n  pos Vector3 [compression \"None\", bits \"32\"]\n";
            var overrides = SchemaReader.Read(schema).ComponentDefinitions[0].members[0].overrides;

            Assert.That(overrides["compression"], Is.EqualTo("None"));
            Assert.That(overrides["bits"], Is.EqualTo("32"));
        }

        [Test]
        [Description("Non-id component overrides such as send-ordered are stored in the component's overrides dictionary.")]
        public void Component_CustomOverrides_AreParsed()
        {
            var comp = SchemaReader.Read("component Ordered [id \"0\", send-ordered \"true\"]\n").ComponentDefinitions[0];
            Assert.That(comp.overrides["send-ordered"], Is.EqualTo("true"));
        }

        // -----------------------------------------------------------------------
        // Commands
        // -----------------------------------------------------------------------

        [Test]
        [Description("A command block is parsed with the correct name and id.")]
        public void Command_ParsesNameAndId()
        {
            var cmd = SchemaReader.Read("command MyCmd [id \"7\"]\n").CommandDefinitions[0];

            Assert.That(cmd.name, Is.EqualTo("MyCmd"));
            Assert.That(cmd.id, Is.EqualTo(7));
        }

        [Test]
        [Description("A command with no routing override defaults to MessageTarget.All.")]
        public void Command_NoRoutingOverride_DefaultsToAll() =>
            Assert.That(SchemaReader.Read("command Ping [id \"0\"]\n").CommandDefinitions[0].routing,
                Is.EqualTo(MessageTarget.All));

        [Test]
        [Description("A command routing override of 'StateAuthorityOnly' is parsed to MessageTarget.StateAuthorityOnly.")]
        public void Command_RoutingStateAuthorityOnly_ParsedCorrectly() =>
            Assert.That(SchemaReader.Read("command Cmd [id \"0\", routing \"StateAuthorityOnly\"]\n").CommandDefinitions[0].routing,
                Is.EqualTo(MessageTarget.StateAuthorityOnly));

        [Test]
        [Description("A command routing override of 'InputAuthorityOnly' is parsed to MessageTarget.InputAuthorityOnly.")]
        public void Command_RoutingInputAuthorityOnly_ParsedCorrectly() =>
            Assert.That(SchemaReader.Read("command Cmd [id \"0\", routing \"InputAuthorityOnly\"]\n").CommandDefinitions[0].routing,
                Is.EqualTo(MessageTarget.InputAuthorityOnly));

        [Test]
        [Description("A command routing override of 'Other' is parsed to MessageTarget.Other.")]
        public void Command_RoutingOther_ParsedCorrectly() =>
            Assert.That(SchemaReader.Read("command Cmd [id \"0\", routing \"Other\"]\n").CommandDefinitions[0].routing,
                Is.EqualTo(MessageTarget.Other));

        [Test]
        [Description("Command fields are parsed into the members list with correct names and types.")]
        public void Command_WithFields_FieldsParsed()
        {
            const string schema = "command Hit [id \"0\"]\n  damage Float\n  lethal Bool\n";
            var members = SchemaReader.Read(schema).CommandDefinitions[0].members;

            Assert.That(members, Has.Count.EqualTo(2));
            Assert.That(members[0].name, Is.EqualTo("damage"));
            Assert.That(members[0].type, Is.EqualTo(SchemaType.Float));
            Assert.That(members[1].name, Is.EqualTo("lethal"));
            Assert.That(members[1].type, Is.EqualTo(SchemaType.Bool));
        }

        // -----------------------------------------------------------------------
        // Inputs
        // -----------------------------------------------------------------------

        [Test]
        [Description("An input block is parsed with the correct name, id, and fields.")]
        public void Input_ParsesNameIdAndFields()
        {
            const string schema = "input PlayerInput [id \"3\"]\n  horizontal Float\n  jump Bool\n";
            var input = SchemaReader.Read(schema).InputDefinitions[0];

            Assert.That(input.name, Is.EqualTo("PlayerInput"));
            Assert.That(input.id, Is.EqualTo(3));
            Assert.That(input.members, Has.Count.EqualTo(2));
        }

        // -----------------------------------------------------------------------
        // Enums
        // -----------------------------------------------------------------------

        [Test]
        [Description("An enum block is parsed with name, bits, range bounds, and member names and values.")]
        public void Enum_ParsesDefinitionFully()
        {
            const string schema = "enum State [bits \"4\", range-min \"0\", range-max \"3\"]\n  Idle 0\n  Walking 1\n  Running 2\n";
            var enumDef = SchemaReader.Read(schema).EnumDefinitions[0];

            Assert.That(enumDef.name, Is.EqualTo("State"));
            Assert.That(enumDef.bits, Is.EqualTo(4));
            Assert.That(enumDef.min, Is.EqualTo(0));
            Assert.That(enumDef.max, Is.EqualTo(3));
            Assert.That(enumDef.names, Is.EqualTo(new[] { "Idle", "Walking", "Running" }));
            Assert.That(enumDef.values, Is.EqualTo(new[] { "0", "1", "2" }));
        }

        // -----------------------------------------------------------------------
        // Archetypes
        // -----------------------------------------------------------------------

        [Test]
        [Description("An archetype block is parsed with the correct name and id.")]
        public void Archetype_ParsesNameAndId()
        {
            const string schema =
                "archetype MyArch [id \"5\"]\n" +
                "  lod 0\n" +
                "    CompA [id \"1\", base-id \"0\"]\n" +
                "      archetype_field value [compression \"None\"]\n";
            var arch = SchemaReader.Read(schema).ArchetypeDefinitions[0];

            Assert.That(arch.name, Is.EqualTo("MyArch"));
            Assert.That(arch.id, Is.EqualTo(5));
        }

        [Test]
        [Description("An archetype with a single LOD 0 produces no excluded components.")]
        public void Archetype_SingleLOD0_NoExcludedComponents()
        {
            const string schema =
                "archetype Obj [id \"0\"]\n" +
                "  lod 0\n" +
                "    CompA [id \"1\", base-id \"0\"]\n" +
                "      archetype_field value [compression \"None\"]\n";
            var lod = SchemaReader.Read(schema).ArchetypeDefinitions[0].lods[0];

            Assert.That(lod.excludedComponentNames, Is.Empty);
        }

        [Test]
        [Description("A component present in LOD 0 but absent from LOD 1 appears in LOD 1's excludedComponentNames.")]
        public void Archetype_ComponentAbsentInHigherLOD_IsExcluded()
        {
            const string schema =
                "archetype Obj [id \"0\"]\n" +
                "  lod 0\n" +
                "    CompA [id \"1\", base-id \"0\"]\n" +
                "      archetype_field value [compression \"None\"]\n" +
                "    CompB [id \"2\", base-id \"1\"]\n" +
                "      archetype_field value [compression \"None\"]\n" +
                "  lod 1 [distance \"50\"]\n" +
                "    CompA [id \"3\", base-id \"0\"]\n" +
                "      archetype_field value [compression \"None\"]\n";
            var lod1 = SchemaReader.Read(schema).ArchetypeDefinitions[0].lods[1];

            Assert.That(lod1.excludedComponentNames, Contains.Item("CompB"));
        }

        [Test]
        [Description("A fractional LOD distance (invariant-formatted, e.g. \"12.5\") is parsed with the invariant " +
            "culture, so it loads correctly even when the current culture uses a comma decimal separator " +
            "(reproduces the WebGL crash where the browser locale leaks into CurrentCulture).")]
        public void Archetype_FractionalLodDistance_UnderCommaDecimalCulture_ParsesValue()
        {
            const string schema =
                "archetype Obj [id \"0\"]\n" +
                "  lod 0\n" +
                "    CompA [id \"1\", base-id \"0\"]\n" +
                "      archetype_field value [compression \"None\"]\n" +
                "  lod 1 [distance \"12.5\"]\n" +
                "    CompA [id \"2\", base-id \"0\"]\n" +
                "      archetype_field value [compression \"None\"]\n";

            var originalCulture = Thread.CurrentThread.CurrentCulture;
            try
            {
                // de-DE uses a comma as the decimal separator.
                Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");

                var lod1 = SchemaReader.Read(schema).ArchetypeDefinitions[0].lods[1];

                Assert.That(lod1.distance, Is.EqualTo(12.5f));
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = originalCulture;
            }
        }

        [Test]
        [Description("A component present in all LODs does not appear in any excluded list.")]
        public void Archetype_ComponentPresentInAllLODs_NeverExcluded()
        {
            const string schema =
                "archetype Obj [id \"0\"]\n" +
                "  lod 0\n" +
                "    CompA [id \"1\", base-id \"0\"]\n" +
                "      archetype_field value [compression \"None\"]\n" +
                "  lod 1 [distance \"50\"]\n" +
                "    CompA [id \"2\", base-id \"0\"]\n" +
                "      archetype_field value [compression \"None\"]\n";
            var lod1 = SchemaReader.Read(schema).ArchetypeDefinitions[0].lods[1];

            Assert.That(lod1.excludedComponentNames, Is.Empty);
        }

        [Test]
        [Description("LOD 0 always has a distance of 0; LODs with a distance override have that value parsed.")]
        public void Archetype_LOD_ParsesDistance()
        {
            const string schema =
                "archetype Obj [id \"0\"]\n" +
                "  lod 0\n" +
                "    CompA [id \"1\", base-id \"0\"]\n" +
                "      archetype_field value [compression \"None\"]\n" +
                "  lod 1 [distance \"100\"]\n" +
                "    CompA [id \"2\", base-id \"0\"]\n" +
                "      archetype_field value [compression \"None\"]\n";
            var lods = SchemaReader.Read(schema).ArchetypeDefinitions[0].lods;

            Assert.That(lods[0].distance, Is.EqualTo(0f));
            Assert.That(lods[1].distance, Is.EqualTo(100f));
        }

        [Test]
        [Description("A component override within an archetype LOD has its id and base-id parsed.")]
        public void Archetype_ComponentOverride_ParsesIdAndBaseId()
        {
            const string schema =
                "archetype Obj [id \"0\"]\n" +
                "  lod 0\n" +
                "    WorldPos [id \"5\", base-id \"3\"]\n" +
                "      archetype_field value [compression \"None\"]\n";
            var item = SchemaReader.Read(schema).ArchetypeDefinitions[0].lods[0].items[0];

            Assert.That(item.name, Is.EqualTo("WorldPos"));
            Assert.That(item.id, Is.EqualTo(5));
            Assert.That(item.baseComponentId, Is.EqualTo(3));
        }
    }
}
