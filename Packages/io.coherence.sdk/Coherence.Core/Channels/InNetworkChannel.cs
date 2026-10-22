// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Core.Channels
{
    using System;
    using System.Collections.Generic;
    using Coherence.Brook;
    using Coherence.Common;
    using Coherence.Entities;
    using Coherence.Log;
    using Coherence.ProtocolDef;
    using Coherence.Serializer;
    using Coherence.SimulationFrame;
    using Coherence.Stats;

    /// <summary>
    /// InNetworkChannel is responsible for deserializing and buffering incoming changes.
    /// While taking care of entity references and ordering.
    /// </summary>
    internal class InNetworkChannel : IInNetworkChannel
    {
        public event Action<IncomingEntityUpdate> OnEntityUpdate;
        public event Action<CommandData> OnCommand;
        public event Action<InputData> OnInput;

        private readonly ProtocolDefinition schemaDefinition;
        private readonly ISchemaDataFactory schemaDataFactory;
        private readonly IMessageDeserializer messageDeserializer;
        private readonly ChannelID channelID;

        private readonly ReceiveChangeBuffer changeBuffer;

        private readonly Stats stats;
        private readonly Logger logger;

        // Cache for HandleEntityUpdate() and FlushBuffer() so we don't need to re-allocate the list every time
        private readonly List<IncomingEntityUpdate> updatesCache = new(32);

        // Cache for FlushBuffer() so we don't need to re-allocate the list every time
        private readonly Queue<IncomingEntityUpdate> updatesBuffer = new(32);
        private readonly Queue<CommandData> commandsBuffer = new(32);
        private readonly Queue<InputData> inputsBuffer = new(32);

        private readonly CacheList<CommandData> deserializedCommandsCache = new(32);
        private readonly CacheList<InputData> deserializedInputsCache = new(32);

        public InNetworkChannel(
            ProtocolDefinition schemaDefinition,
            ISchemaDataFactory schemaDataFactory,
            IEntityRegistry entityRegistry,
            ChannelID channelID,
            Stats stats,
            Logger logger)
        {
            this.schemaDefinition = schemaDefinition;
            this.schemaDataFactory = schemaDataFactory;
            this.stats = stats;
            this.logger = logger.With<InNetworkChannel>();
            this.messageDeserializer = new MessageDeserializer(schemaDataFactory, this.logger);
            this.channelID = channelID;

            this.changeBuffer = new ReceiveChangeBuffer(entityRegistry, this.logger);
        }

        public bool Deserialize(IInBitStream stream, AbsoluteSimulationFrame packetSimulationFrame, Vector3d packetFloatingOrigin)
        {
            var gotEntityUpdate = false;

            while (DeserializeCommands.DeserializeCommand(stream, out var messageType))
            {
                if (messageType == MessageType.EcsWorldUpdate)
                {
                    gotEntityUpdate = true;
                }

                PerformMessage(messageType, packetSimulationFrame, stream, packetFloatingOrigin);
            }

            return gotEntityUpdate;
        }

        public List<RefsInfo> GetRefsInfos()
        {
            return changeBuffer.GetRefsInfos();
        }

        public bool FlushBuffer(IReadOnlyCollection<Entity> resolvableEntities)
        {
            var didFlushAnything = false;

            updatesCache.Clear();
            changeBuffer.TakeUpdates(updatesCache, resolvableEntities);
            updatesBuffer.Clear();
            foreach (var update in updatesCache)
            {
                updatesBuffer.Enqueue(update);
            }

            didFlushAnything |= updatesBuffer.Count > 0;
            while (updatesBuffer.Count > 0)
            {
                var update = updatesBuffer.Dequeue();
                OnEntityUpdate?.Invoke(update);
            }

            commandsBuffer.Clear();
            changeBuffer.TakeCommands(commandsBuffer);
            didFlushAnything |= commandsBuffer.Count > 0;
            while (commandsBuffer.Count > 0)
            {
                var command = commandsBuffer.Dequeue();
                OnCommand?.Invoke(command);
            }

            inputsBuffer.Clear();
            changeBuffer.TakeInputs(inputsBuffer);
            didFlushAnything |= inputsBuffer.Count > 0;
            while (inputsBuffer.Count > 0)
            {
                var input = inputsBuffer.Dequeue();
                OnInput?.Invoke(input);
            }

            return didFlushAnything;
        }

        public void Clear()
        {
            // An update/command/input could cause a disconnect, so we must dispose of the remaining buffered data just in case.
            foreach (var update in updatesBuffer)
            {
                update.Dispose();
            }
            updatesBuffer.Clear();

            foreach (var command in commandsBuffer)
            {
                command.Dispose();
            }
            commandsBuffer.Clear();

            foreach (var input in inputsBuffer)
            {
                input.Dispose();
            }
            inputsBuffer.Clear();

            changeBuffer.Clear();
        }

        private void PerformMessage(MessageType messageType, AbsoluteSimulationFrame packetSimulationFrame, IInBitStream bitStream, Vector3d packetFloatingOrigin)
        {
            switch (messageType)
            {
                case MessageType.EcsWorldUpdate:
                    HandleEntityUpdate(packetSimulationFrame, bitStream, packetFloatingOrigin);
                    break;
                case MessageType.Command:
                    HandleCommands(bitStream);
                    break;
                case MessageType.Input:
                    HandleInputs(bitStream);
                    break;
                default:
                    logger.Warning(Warning.CoreInNetworkChannelUnknownMessage,
                         ("MessageCode", messageType),
                         ("SimFrame", packetSimulationFrame.Frame),
                         ("RemainingBits", bitStream.RemainingBits()));
                    break;
            }
        }

        private void HandleCommands(IInBitStream bitStream)
        {
            using var _ = deserializedCommandsCache;
            messageDeserializer.ReadCommands(bitStream, deserializedCommandsCache);

            stats.TrackIncomingMessages(MessageType.Command, deserializedCommandsCache.Count);

            foreach (var command in deserializedCommandsCache)
            {
                changeBuffer.AddCommand(command);
            }
        }

        private void HandleInputs(IInBitStream bitStream)
        {
            using var _ = deserializedInputsCache;
            messageDeserializer.ReadInputs(bitStream, deserializedInputsCache);

            stats.TrackIncomingMessages(MessageType.Input, deserializedInputsCache.Count);

            foreach (var input in deserializedInputsCache)
            {
                changeBuffer.AddInput(input);
            }
        }

        private void HandleEntityUpdate(AbsoluteSimulationFrame packetSimulationFrame, IInBitStream bitStream,
            Vector3d floatingOrigin)
        {
            updatesCache.Clear();
            Serializer.Deserialize.ReadWorldUpdate(updatesCache, packetSimulationFrame, floatingOrigin, schemaDefinition, schemaDataFactory, bitStream,
                channelID, logger);
            stats.TrackIncomingMessages(MessageType.EcsWorldUpdate, updatesCache.Count);

            foreach (var update in updatesCache)
            {
                update.TakeOwnershipOfComponents("InNetworkChannel.HandleEntityUpdate");
                changeBuffer.AddChange(update.ReleaseOwnershipOfComponents());
            }
        }
    }
}
