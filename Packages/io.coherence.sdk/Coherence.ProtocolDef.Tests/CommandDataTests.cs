// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.ProtocolDef.Tests
{
    using System.Collections.Generic;
    using Coherence.Entities;
    using Coherence.Schema;
    using Coherence.Tests;
    using NUnit.Framework;

    public class CommandDataTests
    {
        private ProtocolCommandDefinition simpleCommandDefinition;
        private ProtocolCommandDefinition stringsCommandDefinition;
        private ProtocolCommandDefinition byteArraysCommandDefinition;
        private ProtocolCommandDefinition complexCommandDefinition;
        private ProtocolCommandDefinition simFramesCommandDefinition;
        private ProtocolCommandDefinition entityRefsCommandDefinition;
        private ManagedTypesTables managedTypesTables;

        [SetUp]
        public void SetUp()
        {
            simpleCommandDefinition = new ProtocolCommandDefinition(1, "SimpleCommand", new List<ProtocolFieldDefinition>
            {
                new("IntField", SchemaType.Int, default),
                new("FloatField", SchemaType.Float, default),
            });
            stringsCommandDefinition = new ProtocolCommandDefinition(2, "StringsCommand", new List<ProtocolFieldDefinition>
            {
                new("StringField1", SchemaType.String, default),
                new("StringField2", SchemaType.String, default),
            });
            byteArraysCommandDefinition = new ProtocolCommandDefinition(3, "ByteArraysCommand", new List<ProtocolFieldDefinition>
            {
                new("ByteArrayField1", SchemaType.Bytes, default),
                new("ByteArrayField2", SchemaType.Bytes, default),
            });
            complexCommandDefinition = new ProtocolCommandDefinition(4, "ComplexCommand", new List<ProtocolFieldDefinition>
            {
                new("ByteArrayField", SchemaType.Bytes, default),
                new("ClientIDField", SchemaType.ClientID, default),
                new("Vector3Field", SchemaType.Vector3, default),
                new("EntityField", SchemaType.Entity, default),
                new("StringField", SchemaType.String, default),
                new("BoolField", SchemaType.Bool, default),
                new("CharField", SchemaType.Char, default)
            });
            simFramesCommandDefinition = new ProtocolCommandDefinition(5, "SimFramesCommand", new List<ProtocolFieldDefinition>
            {
                new("Field1", SchemaType.Int, default, hasSimFrames: true),
                new("Field2", SchemaType.Float, default, hasSimFrames: true),
            });
            entityRefsCommandDefinition = new ProtocolCommandDefinition(6, "EntityRefsCommand", new List<ProtocolFieldDefinition>
            {
                new("EntityField1", SchemaType.Entity, default),
                new("EntityField2", SchemaType.Entity, default),
                new("EntityField3", SchemaType.Entity, default),
                new("NotEntityField", SchemaType.Int, default),
            });

            managedTypesTables = new ManagedTypesTables();
        }

        private CommandData CreateCommandData(ProtocolCommandDefinition commandDefinition)
        {
            return new CommandData(
                commandDefinition,
                managedTypesTables,
                new byte[commandDefinition.DataSize],
                Entity.InvalidRelative,
                MessageTarget.All);
        }


        [Test]
        public void TestFields()
        {
            var commandData = CreateCommandData(simpleCommandDefinition);

            var count = 0;

            foreach (var field in commandData.Fields)
            {
                if (count == 0)
                {
                    Assert.That(field.DefinitionName, Is.EqualTo("IntField"));
                }
                else if (count == 1)
                {
                    Assert.That(field.DefinitionName, Is.EqualTo("FloatField"));
                }
                count++;
            }

            Assert.That(count, Is.EqualTo(2));
        }

        [Test]
        public void TestFields_NoAllocations()
        {
            var commandData = CreateCommandData(complexCommandDefinition);

            AllocCounter.AssertNoAllocs(() =>
            {
                foreach (var field in commandData.Fields)
                {
                }
            });
        }

        [Test]
        public void TestEntityFields()
        {
            var commandData = CreateCommandData(entityRefsCommandDefinition);
            var entity1 = new Entity((Index)1, 1, false);
            var entity2 = new Entity((Index)2, 1, false);
            var entity3 = new Entity((Index)3, 1, false);

            commandData.GetField(0).SetEntity(entity1);
            commandData.GetField(1).SetEntity(entity2);
            commandData.GetField(2).SetEntity(entity3);

            var count = 0;

            foreach (var field in commandData.EntityFields)
            {
                if (count == 0)
                {
                    Assert.That(field.GetEntity(), Is.EqualTo(entity1));
                }
                else if (count == 1)
                {
                    Assert.That(field.GetEntity(), Is.EqualTo(entity2));
                }
                else if (count == 2)
                {
                    Assert.That(field.GetEntity(), Is.EqualTo(entity3));
                }

                count++;
            }

            Assert.That(count, Is.EqualTo(3));
        }

        [Test]
        public void TestEntityFields_NoAllocations()
        {
            var commandData = CreateCommandData(entityRefsCommandDefinition);

            AllocCounter.AssertNoAllocs(() =>
            {
                foreach (var field in commandData.EntityFields)
                {
                }
            });
        }

        [Test]
        public void NullEntityRefs_NoAllocations()
        {
            var commandData = CreateCommandData(entityRefsCommandDefinition);

            AllocCounter.AssertNoAllocs(() =>
            {
                commandData.NullEntityRefs(Entity.InvalidRelative);
            });
        }
    }
}

