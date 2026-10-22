// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Core.Tests
{
    using System;
    using System.Collections.Generic;
    using Coherence.Brook;
    using Coherence.Common;
    using Coherence.Common.Tests;
    using Coherence.Core.Channels;
    using Coherence.Entities;
    using Coherence.ProtocolDef;
    using Coherence.Serializer;
    using Coherence.Tests;
    using NUnit.Framework;

    public class InOrderedNetworkChannelTests : CoherenceTest
    {
        private MessageDeserializerMock messageDeserializerMock;
        private EntityRegistry entityRegistry;
        private InOrderedNetworkChannel channel;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();

            entityRegistry = new EntityRegistry(new HashSet<Entities.Entity>(), new Dictionary<Entities.Entity, AuthorityType>());
            messageDeserializerMock = new MessageDeserializerMock();

            channel = new InOrderedNetworkChannel(TestSchema.DataFactory, entityRegistry, null, logger, messageDeserializerMock);
        }

        [Test]
        [Description("Verifies that FlushBuffer method does not allocate.")]
        public void FlushBuffer_ShouldNotAllocate()
        {
            // Arrange
            var iterations = 100;

            messageDeserializerMock.orderedCommands = new List<(MessageID, CommandData)>()
            {
                (new MessageID(0), ByteArraysCommand.Create(TestSchema.DataFactory, Entity.InvalidRelative, MessageTarget.All, default, default, default, default, default, default)),
                (new MessageID(1), ByteArraysCommand.Create(TestSchema.DataFactory, Entity.InvalidRelative, MessageTarget.All, default, default, default, default, default, default)),
                (new MessageID(2), ByteArraysCommand.Create(TestSchema.DataFactory, Entity.InvalidRelative, MessageTarget.All, default, default, default, default, default, default)),
                (new MessageID(3), ByteArraysCommand.Create(TestSchema.DataFactory, Entity.InvalidRelative, MessageTarget.All, default, default, default, default, default, default)),
            };

            AllocCounter.AssertNoAllocs(() =>
            {
                for (var i = 0; i < iterations; i++)
                {
                    channel.Deserialize(null, 0, Vector3d.zero);
                    channel.FlushBuffer(null);
                    channel.Clear();
                }
            });
        }

        private class MessageDeserializerMock : IMessageDeserializer
        {
            public List<(MessageID, CommandData)> orderedCommands;

            public void ReadOrderedCommands(IInBitStream bitStream, List<(MessageID, CommandData)> deserializedMessages)
            {
                deserializedMessages.AddRange(orderedCommands);
            }

            public void ReadCommands(IInBitStream bitStream, List<CommandData> buffer) => throw new NotImplementedException();
            public void ReadInputs(IInBitStream bitStream, List<InputData> buffer) => throw new NotImplementedException();
        }
    }
}
