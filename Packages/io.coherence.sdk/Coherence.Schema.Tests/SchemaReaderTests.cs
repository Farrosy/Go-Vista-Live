// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Schema.Tests
{
    using Coherence.Tests;
    using NUnit.Framework;

    public class SchemaReaderTests : CoherenceTest
    {
        [Test]
        [Description("An empty schema string returns a definition with all collections empty.")]
        public void Read_EmptyString_ReturnsEmptyCollections()
        {
            var def = SchemaReader.Read("");

            Assert.That(def.ComponentDefinitions, Is.Empty);
            Assert.That(def.CommandDefinitions, Is.Empty);
            Assert.That(def.InputDefinitions, Is.Empty);
            Assert.That(def.EnumDefinitions, Is.Empty);
            Assert.That(def.ArchetypeDefinitions, Is.Empty);
        }

        [Test]
        [Description("SchemaId is set to a non-empty string for any non-trivial input.")]
        public void Read_NonEmptyInput_SchemaIdIsNotEmpty() =>
            Assert.That(SchemaReader.Read("component Foo [id \"0\"]").SchemaId, Is.Not.Empty);

        [Test]
        [Description("Two identical schema strings produce the same SchemaId.")]
        public void Read_IdenticalInputs_ProduceSameSchemaId()
        {
            const string schema = "component Foo [id \"0\"]\n";
            Assert.That(SchemaReader.Read(schema).SchemaId, Is.EqualTo(SchemaReader.Read(schema).SchemaId));
        }

        [Test]
        [Description("Two different schema strings produce different SchemaIds.")]
        public void Read_DifferentInputs_ProduceDifferentSchemaIds()
        {
            var idA = SchemaReader.Read("component Foo [id \"0\"]\n").SchemaId;
            var idB = SchemaReader.Read("component Bar [id \"1\"]\n").SchemaId;
            Assert.That(idA, Is.Not.EqualTo(idB));
        }
    }
}
