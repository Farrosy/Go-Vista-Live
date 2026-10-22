// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

// If these tests fail, then it's likely the code gen is broken.
// Take a look at README_BAKING in Coherence.Common.Tests to regenerate the code.

namespace Coherence.Serializer.Tests
{
    using System;
    using System.Collections.Generic;
    using Brook;
    using Brook.Octet;
    using Coherence.Common.Tests;
    using Coherence.Tests;
    using Common;
    using Entities;
    using Log;
    using Moq;
    using NUnit.Framework;
    using ProtocolDef;
    using SimulationFrame;

    public class SerializerTest : CoherenceTest
    {
        private Mock<Logger> loggerMock;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();

            loggerMock = new Mock<Logger>(null, null, null, null);
        }

        [TestCase(8, 0)]
        [TestCase(8, 1)]
        [TestCase(16, 0)]
        [TestCase(16, 5)]
        [TestCase(16, 16)]
        [TestCase(64, 0)]
        [TestCase(64, 5)]
        [TestCase(64, 64)]
        [TestCase(1024, 0)]
        [TestCase(1024, 20)]
        [TestCase(1024, 40)]
        [TestCase(1024, 1024)]
        public void SerializeUpdated_ShouldCorrectlyMeasureBitsTaken(int bufferSizeBits, int bufferHeaderSizeBits)
        {
            // Arrange
            var change = new EntityChange
            {
                Update = new OutgoingEntityUpdate
                {
                    Operation = EntityOperation.Create,
                    Components = DeltaComponents.New()
                }
            };

            change.Update.Components.UpdateComponent(GlobalComponent.Create(TestSchema.DataFactory));
            change.Update.Components.RemoveComponent(3);

            var octetWriter = new OutOctetStream(bufferSizeBits / 8);
            var bitStream = new OutBitStream(octetWriter);
            WriteBitsToBitStream(bitStream, bufferHeaderSizeBits);

            Entities.Index lastIndex = 0;

            // Act
            Serialize.SerializeUpdated(change, new AbsoluteSimulationFrame(),
                new SerializerContext<IOutBitStream>(bitStream, false, loggerMock.Object), ref lastIndex, out uint bitsTaken);

            // Assert
            var expectedRemainingSize = bufferSizeBits - bufferHeaderSizeBits - bitsTaken;

            if (expectedRemainingSize > 0)
            {
                Assert.AreEqual(0, bitStream.OverflowBitCount);
                Assert.AreEqual(expectedRemainingSize, bitStream.RemainingBitCount);
                Assert.IsFalse(bitStream.IsFull);
            }
            else
            {
                var expectedOverflowBitCount = bitsTaken + bufferHeaderSizeBits - bufferSizeBits;
                Assert.AreEqual(expectedOverflowBitCount, bitStream.OverflowBitCount);
                Assert.IsTrue(bitStream.IsFull);
            }
        }

        [Test]
        [Description("Tests that a component with no fields can be deserialized into an entity update.")]
        public void DeserializeComponentWithNoFields()
        {
            // Arrange
            var logger = Log.GetLogger<SerializerTest>();

            var change = new EntityChange
            {
                Update = new OutgoingEntityUpdate
                {
                    Operation = EntityOperation.Create,
                    Components = DeltaComponents.New()
                }
            };

            // add a component with no fields like the global component.
            change.Update.Components.UpdateComponent(GlobalComponent.Create(TestSchema.DataFactory));

            var stream = new Brook.Octet.OutOctetStream(ConnectionSettings.DEFAULT_MTU);
            var bitStream = new OutBitStream(stream);
            var lastIndex = (Entities.Index)0;
            Serialize.SerializeUpdated(change, 1, new SerializerContext<IOutBitStream>(bitStream, false, logger), ref lastIndex, out _);

            bitStream.Flush();

            var outBuffer = stream.Close().ToArray();
            var octetReader = new InOctetStream(outBuffer);
            var inBitStream = new InBitStream(octetReader, outBuffer.Length);

            // Act
            var readEntity = Deserialize.ReadEntity(inBitStream, 1, ref lastIndex, out EntityWithMeta _, out AbsoluteSimulationFrame entityRefSimFrame, logger);
            var entityUpdate = IncomingEntityUpdate.New();
            entityUpdate = Deserialize.UpdateComponents(TestSchema.Definition, TestSchema.DataFactory, entityUpdate, entityRefSimFrame, inBitStream, logger);

            // Assert
            Assert.That(readEntity, Is.True);
            Assert.That(entityUpdate.Components.Count, Is.EqualTo(1));
        }

        [Test]
        [Description("Verifies that if an entity change exceeds the preferred budget, " +
            "and there are no prior changes that it is still serialized.")]
        public void WriteEntityUpdates_PreferredBudget_NoPriorChanges()
        {
            // Arrange
            var entity1 = new Entity((Entities.Index)1, 0, false);
            var entity2 = new Entity((Entities.Index)2, 0, false);
            var entity3 = new Entity((Entities.Index)3, 0, false);

            List<EntityChange> changes = new();
            changes.Add(GetTestEntityChange(entity1));
            changes.Add(GetTestEntityChange(entity2));
            changes.Add(GetTestEntityChange(entity3));

            var stream = new OutOctetStream(10000);
            var bitStream = new OutBitStream(stream);
            var ctx = new SerializerContext<IOutBitStream>(bitStream, false, this.logger);
            ctx.PreferredMaxBitCount = 1;

            // Act
            List<Entity> writtenEntities = new();
            Serialize.WriteEntityUpdates(writtenEntities, changes, new AbsoluteSimulationFrame(), ctx);

            // Assert
            Assert.That(writtenEntities.Count, Is.EqualTo(1));
            Assert.That(writtenEntities[0], Is.EqualTo(entity1));
        }

        [Test]
        [Description("Verifies that if an entity change exceeds the preferred budget, " +
            "and there are prior changes that it is not serialized.")]
        public void WriteEntityUpdates_PreferredBudget_WithPriorChanges()
        {
            // Arrange
            var entity1 = new Entity((Entities.Index)1, 0, false);
            var entity2 = new Entity((Entities.Index)2, 0, false);
            var entity3 = new Entity((Entities.Index)3, 0, false);

            List<EntityChange> changes = new();
            changes.Add(GetTestEntityChange(entity1));
            changes.Add(GetTestEntityChange(entity2));
            changes.Add(GetTestEntityChange(entity3));

            var stream = new OutOctetStream(10000);
            var bitStream = new OutBitStream(stream);
            var ctx = new SerializerContext<IOutBitStream>(bitStream, false, this.logger);
            ctx.PreferredMaxBitCount = 1;
            ctx.HasSerializedChanges = true;

            // Act
            List<Entity> writtenEntities = new();
            Serialize.WriteEntityUpdates(writtenEntities, changes, new AbsoluteSimulationFrame(), ctx);

            // Assert
            Assert.That(writtenEntities.Count, Is.EqualTo(0));
        }

        private EntityChange GetTestEntityChange(Entity id)
        {
            var change = new EntityChange
            {
                ID = id,
                Update = new OutgoingEntityUpdate
                {
                    Operation = EntityOperation.Create,
                    Components = DeltaComponents.New()
                }
            };
            change.Update.Components.UpdateComponent(GlobalComponent.Create(TestSchema.DataFactory));

            return change;
        }

        private void WriteBitsToBitStream(IOutBitStream stream, int bitCount)
        {
            while (bitCount > 0)
            {
                stream.WriteBits(1, Math.Min(bitCount, 32));
                bitCount -= 32;
            }
        }
    }
}
