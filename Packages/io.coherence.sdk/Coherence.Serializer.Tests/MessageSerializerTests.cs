// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Serializer.Tests
{
    using System.Collections.Generic;
    using Coherence.Brook;
    using Coherence.Brook.Octet;
    using Coherence.Common;
    using Coherence.Common.Tests;
    using Coherence.Connection;
    using Coherence.Entities;
    using Coherence.ProtocolDef;
    using Coherence.Tests;
    using NUnit.Framework;

    public class MessageSerializerTests : CoherenceTest
    {
        private MessageSerializer messageSerializer;
        private MessageDeserializer messageDeserializer;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();

            messageSerializer = new MessageSerializer(this.logger);
            messageDeserializer = new MessageDeserializer(TestSchema.DataFactory, this.logger);
        }

        [Test(Description = "Serialization and deserialization of a list of ordered commands")]
        public void SerializeOrderedCommands()
        {
            var commands = new List<(MessageID, SerializedEntityMessage)>()
            {
                (new MessageID(5), CreateTestCommand()),
                (new MessageID(6), CreateTestCommand()),
                (new MessageID(9), CreateTestCommand()),
                (new MessageID(10), CreateTestCommand())
            };

            var stream = new OutOctetStream(ConnectionSettings.DEFAULT_MTU);

            // Serialize
            {
                var ctx = new SerializerContext<IOutBitStream>(new OutBitStream(stream), false, logger);

                var res = messageSerializer.WriteOrderedCommands(commands, ctx);

                ctx.BitStream.Flush();

                Assert.That(res.Count, Is.EqualTo(commands.Count));
                for (var i = 0; i < res.Count; ++i)
                {
                    Assert.That(res[i], Is.EqualTo(commands[i].Item1));
                }
            }

            // Deserialize
            {
                var octetReader = new InOctetStream(stream.Close().ToArray());
                var inBitStream = new InBitStream(octetReader, (int)octetReader.Length);
                var res = new List<(MessageID, CommandData)>();
                messageDeserializer.ReadOrderedCommands(inBitStream, res);

                Assert.That(res.Count, Is.EqualTo(commands.Count));
                for (var i = 0; i < res.Count; ++i)
                {
                    Assert.That(res[i].Item1, Is.EqualTo(commands[i].Item1));
                    Assert.That(res[i].Item2.CommandID, Is.EqualTo(AuthorityRequestCommand.CommandID));
                }
            }
        }

        [Test]
        [Description("Verifies that if an ordered command exceeds the preferred budget, and there are no prior changes " +
            "that it is still serialized.")]
        public void OrderedCommands_PreferredSize_NoPriorChanges()
        {
            // Arrange
            var commands = new List<(MessageID, SerializedEntityMessage)>()
            {
                (new MessageID(5), CreateTestCommand()),
                (new MessageID(6), CreateTestCommand()),
                (new MessageID(9), CreateTestCommand()),
                (new MessageID(10), CreateTestCommand())
            };

            var stream = new OutOctetStream(10000);
            var ctx = new SerializerContext<IOutBitStream>(new OutBitStream(stream), false, logger);
            ctx.PreferredMaxBitCount = 1;

            // Act
            var res = messageSerializer.WriteOrderedCommands(commands, ctx);

            // Assert
            Assert.That(res.Count, Is.EqualTo(1));
        }

        [Test]
        [Description("Verifies that if an ordered command exceeds the preferred budget, and there are prior changes " +
            "that it is not serialized.")]
        public void OrderedCommands_PreferredSize_WithPriorChanges()
        {
            // Arrange
            var commands = new List<(MessageID, SerializedEntityMessage)>()
            {
                (new MessageID(5), CreateTestCommand()),
                (new MessageID(6), CreateTestCommand()),
                (new MessageID(9), CreateTestCommand()),
                (new MessageID(10), CreateTestCommand())
            };

            var stream = new OutOctetStream(10000);
            var ctx = new SerializerContext<IOutBitStream>(new OutBitStream(stream), false, logger);
            ctx.PreferredMaxBitCount = 1;
            ctx.HasSerializedChanges = true;

            // Act
            var res = messageSerializer.WriteOrderedCommands(commands, ctx);

            // Assert
            Assert.That(res.Count, Is.EqualTo(0));
        }

        [TestCase(MessageTarget.StateAuthorityOnly)]
        [TestCase(MessageTarget.InputAuthorityOnly)]
        [TestCase(MessageTarget.Other)]
        [TestCase(MessageTarget.All)]
        [Description("Verifies that the message target is serialized and deserialized correctly, and that there are sufficient bits for it.")]
        public void Test_SerializingMessageTarget(MessageTarget target)
        {
            _ = CreateTestCommand(target);
        }

        [Test]
        [Description("Verifies that we can serialize and deserialize a command containing big data.")]
        public void Test_SerializingBigDataCommand()
        {
            // Arrange
            var entityID = new Entity((Index)1, 0, false);
            var command = ByteArraysCommand.Create(TestSchema.DataFactory, entityID, MessageTarget.All,
                GenerateByteArray(500),
                GenerateByteArray(454),
                GenerateByteArray(450),
                GenerateByteArray(455),
                GenerateByteArray(504),
                GenerateByteArray(505));
            var originalCommand = ByteArraysCommand.From(command);

            // Act
            var serializedCommand = messageSerializer.SerializeCommand(command, false);

            var inStream = new InOctetStream(serializedCommand.OutOctetStream.Close());
            var inBitStream = new InBitStream(inStream, (int)inStream.Length);

            var deserializedCommand = messageDeserializer.ReadCommand(inBitStream);

            // Assert
            Assert.That(deserializedCommand.CommandID, Is.EqualTo(ByteArraysCommand.CommandID));
            var byteArraysCommand = ByteArraysCommand.From(deserializedCommand);

            Assert.That(deserializedCommand.Entity, Is.EqualTo(command.Entity));
            Assert.That(byteArraysCommand.Bytes1, Is.EqualTo(originalCommand.Bytes1));
            Assert.That(byteArraysCommand.Bytes2, Is.EqualTo(originalCommand.Bytes2));
            Assert.That(byteArraysCommand.Bytes3, Is.EqualTo(originalCommand.Bytes3));
            Assert.That(byteArraysCommand.Bytes4, Is.EqualTo(originalCommand.Bytes4));
            Assert.That(byteArraysCommand.Bytes5, Is.EqualTo(originalCommand.Bytes5));
            Assert.That(byteArraysCommand.Bytes6, Is.EqualTo(originalCommand.Bytes6));
        }

        [Test]
        [Description("Verifies that we can serialize and deserialize an input containing big data.")]
        public void Test_SerializingBigDataInput()
        {
            // Arrange
            var entityID = new Entity((Index)1, 0, false);
            var input = ByteArraysInput.Create(TestSchema.DataFactory, entityID, MessageTarget.StateAuthorityOnly, 0,
                GenerateByteArray(500),
                GenerateByteArray(454),
                GenerateByteArray(450),
                GenerateByteArray(455),
                GenerateByteArray(504),
                GenerateByteArray(505));
            var originalInput = ByteArraysInput.From(input);

            // Act
            var serializedInput = messageSerializer.SerializeInput(input, false);

            var inStream = new InOctetStream(serializedInput.OutOctetStream.Close());
            var inBitStream = new InBitStream(inStream, (int)inStream.Length);

            var deserializedInput = messageDeserializer.ReadInput(inBitStream);

            // Assert
            Assert.That(deserializedInput.InputID, Is.EqualTo(ByteArraysInput.InputID));
            var byteArraysInput = ByteArraysInput.From(deserializedInput);

            Assert.That(deserializedInput.Entity, Is.EqualTo(input.Entity));
            Assert.That(byteArraysInput.Bytes1, Is.EqualTo(originalInput.Bytes1));
            Assert.That(byteArraysInput.Bytes2, Is.EqualTo(originalInput.Bytes2));
            Assert.That(byteArraysInput.Bytes3, Is.EqualTo(originalInput.Bytes3));
            Assert.That(byteArraysInput.Bytes4, Is.EqualTo(originalInput.Bytes4));
            Assert.That(byteArraysInput.Bytes5, Is.EqualTo(originalInput.Bytes5));
            Assert.That(byteArraysInput.Bytes6, Is.EqualTo(originalInput.Bytes6));
        }

        [Test]
        [Description("Verifies that a warning is logged when a command is too big to fit in a packet.")]
        public void Test_GetCountFromBudget_CommandTooBig()
        {
            var command = CreateTestCommand();
            var queue = new Queue<SerializedEntityMessage>();
            queue.Enqueue(command);

            _ = messageSerializer.GetCountFromBudget(queue, 10000, 10000, false, false, command.BitCount - 1);

            Assert.That(this.logger.GetCountForWarningID(Log.Warning.SerializeMessageTooBig), Is.EqualTo(1));
        }

        [Test]
        [Description("Verifies that if a command exceeds the preferred budget, and there are no prior changes " +
            "that it is still serialized.")]
        public void Test_GetCountFromBudget_PreferredBudget_NoPriorChanges()
        {
            var command = CreateTestCommand();
            var queue = new Queue<SerializedEntityMessage>();
            queue.Enqueue(command);
            queue.Enqueue(command);
            queue.Enqueue(command);

            var (count, _) = messageSerializer.GetCountFromBudget(queue, 10000, 1, false, false, 10000);

            Assert.That(count, Is.EqualTo(1));
        }

        [Test]
        [Description("Verifies that if a command exceeds the preferred budget, and there are prior changes " +
            "that it is not serialized.")]
        public void Test_GetCountFromBudget_PreferredBudget_WithPriorChanges()
        {
            var command = CreateTestCommand();
            var queue = new Queue<SerializedEntityMessage>();
            queue.Enqueue(command);
            queue.Enqueue(command);
            queue.Enqueue(command);

            var (count, _) = messageSerializer.GetCountFromBudget(queue, 10000, 1, true, false, 10000);

            Assert.That(count, Is.EqualTo(0));
        }

        private SerializedEntityMessage CreateTestCommand()
        {
            var entityID = new Entity((Index)1, 0, false);
            var cmd = AuthorityRequestCommand.Create(TestSchema.DataFactory,
                entityID,
                MessageTarget.StateAuthorityOnly,
                new ClientID(1),
                AuthorityType.State
            );

            return messageSerializer.SerializeCommand(cmd, false);
        }

        private SerializedEntityMessage CreateTestCommand(MessageTarget messageTarget)
        {
            var entityID = new Entity((Index)1, 0, false);
            var cmd = AuthorityRequestCommand.Create(TestSchema.DataFactory,
                entityID,
                messageTarget,
                new ClientID(1),
                AuthorityType.State
            );

            return messageSerializer.SerializeCommand(cmd, false);
        }

        private byte[] GenerateByteArray(int length)
        {
            var byteArray = new byte[length];
            for (var i = 0; i < length; i++)
            {
                byteArray[i] = (byte)(i % 256);
            }
            return byteArray;
        }
    }
}
