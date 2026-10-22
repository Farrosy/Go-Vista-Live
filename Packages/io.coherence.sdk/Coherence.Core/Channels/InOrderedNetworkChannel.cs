// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Core.Channels
{
    using System;
    using System.Collections.Generic;
    using Brook;
    using Coherence.Serializer;
    using Common;
    using Entities;
    using Log;
    using ProtocolDef;
    using SimulationFrame;
    using Stats;

    internal class InOrderedNetworkChannel : IInNetworkChannel
    {
        internal static readonly int SequenceBufferSize = 512;
        internal const int MillisecondsMessageTTL = 5000;
        private static readonly TimeSpan MessageTTL = TimeSpan.FromMilliseconds(MillisecondsMessageTTL);

        public event Action<IncomingEntityUpdate> OnEntityUpdate
        {
            add { }
            remove { }
        }
        public event Action<CommandData> OnCommand;
        public event Action<InputData> OnInput
        {
            add { }
            remove { }
        }

        private readonly IMessageDeserializer messageDeserializer;
        private readonly MessageResolver messageResolver;
        private readonly IEntityRegistry entityRegistry;
        private readonly Stats stats;
        private readonly Logger logger;

        private readonly ReceiveSequenceBuffer sequenceBuffer = new(SequenceBufferSize);
        private readonly Queue<ExpirableMessage<CommandData>> receivedCommands = new(32);

        private readonly IDateTimeProvider dateTimeProvider = new SystemDateTimeProvider();

        // Caches received messages each tick.
        private readonly CacheList<CommandData> receivedMessages = new(32);
        private readonly CacheList<(MessageID, CommandData)> deserializedMessages = new(32);
        private readonly Queue<CommandData> messageQueue = new(32);

        public InOrderedNetworkChannel(
            ISchemaDataFactory schemaDataFactory,
            IEntityRegistry entityRegistry,
            Stats stats,
            Logger logger,
            IMessageDeserializer messageDeserializer = null)
        {
            this.messageResolver = new MessageResolver(entityRegistry);
            this.entityRegistry = entityRegistry;
            this.stats = stats;
            this.logger = logger.With<InOrderedNetworkChannel>();
            this.messageDeserializer = messageDeserializer ?? new MessageDeserializer(schemaDataFactory, this.logger);
        }

        public bool Deserialize(IInBitStream stream, AbsoluteSimulationFrame packetSimulationFrame, Vector3d packetFloatingOrigin)
        {
            using var _ = deserializedMessages;
            messageDeserializer.ReadOrderedCommands(stream, deserializedMessages);

            foreach (var (id, message) in deserializedMessages)
            {
                message.TakeOwnership("InOrderedNetworkChannel.Deserialize");
                sequenceBuffer.InsertMessage(id, message.ReleaseOwnership());
            }

            return false;
        }

        public List<RefsInfo> GetRefsInfos() => default;

        public bool FlushBuffer(IReadOnlyCollection<Entity> _)
        {
            var now = dateTimeProvider.UtcNow;

            FlushSequenceBuffer();

            messageQueue.Clear();

            // Execute commands from queue, already in the right sequence.
            while (receivedCommands.Count > 0)
            {
                var next = receivedCommands.Peek();

                if (messageResolver.IsResolvable(next.Message))
                {
                    messageQueue.Enqueue(next.Message);
                    receivedCommands.Dequeue();
                    continue;
                }

                if (next.HasExpired(now))
                {
                    logger.Debug("Ordered message expired",
                        ("entity", next.Message.Entity),
                        ("type", next.Message.GetType()),
                        ("sender", next.Message.SenderParticipant),
                        ("messageTarget", next.Message.MessageTarget));

                    next.Message.Dispose();
                    receivedCommands.Dequeue();
                    continue;
                }

                break; // Stop, cannot continue with the sequence.
            }

            while (messageQueue.Count > 0)
            {
                var message = messageQueue.Dequeue();
                OnCommand?.Invoke(message);
            }

            return receivedMessages.Count > 0;
        }

        public void Clear()
        {
            // A command could cause a disconnect, so we must dispose of the remaining buffered commands just in case.
            foreach (var command in messageQueue)
            {
                command.Dispose();
            }

            foreach (var command in receivedCommands)
            {
                command.Message.Dispose();
            }
            receivedCommands.Clear();
            sequenceBuffer.Clear();
        }

        private void FlushSequenceBuffer()
        {
            var now = dateTimeProvider.UtcNow;

            using var _ = receivedMessages;

            sequenceBuffer.FlushMessages(receivedMessages);

            foreach (var message in receivedMessages)
            {
                receivedCommands.Enqueue(new ExpirableMessage<CommandData>(message, now + MessageTTL));
            }
        }
    }
}
