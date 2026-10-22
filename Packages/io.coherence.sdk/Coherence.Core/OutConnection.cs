// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Core
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;
    using Brook;
    using Coherence.Common;
    using Coherence.Core.Channels;
    using Coherence.Utils;
    using Connection;
    using Entities;
    using Log;
    using ProtocolDef;
    using Serializer;
    using SimulationFrame;
    using Tend;

    public class OutConnection
    {
        private readonly SortedListEx<ChannelID, IOutNetworkChannel> channels = new();
        private readonly IOutConnection connection;

        private HashSet<Entity> ackedEntities;
        private Dictionary<Entity, HashSet<uint>> ackedComponentsPerEntity = new Dictionary<Entity, HashSet<uint>>();

        // When an entity is transferred from this client it is added to this
        // set until the RS acks the transfer.
        private HashSet<Entity> entitiesInAuthTransfer;

        private readonly Logger logger;

        private List<Entity> entitiesToRemove = new List<Entity>();

        private Vector3d floatingOrigin;

        // Cache for SerializeAndQueuePackets so we don't need to re-allocate the map every time
        private readonly Dictionary<ChannelID, Dictionary<Entity, OutgoingEntityUpdate>> allChannelUpdatesSent = new();

        private readonly SerializerContext<IOutBitStream> serializerCtxCache = new();

        internal event Action<PacketSentDebugInfo> OnPacketSent;
        internal event Action<Entity> OnEntityAcked;

        internal event Action<Entity> OnAuthorityTransferred;

        internal OutConnection(IOutConnection connection,
            Dictionary<ChannelID, IOutNetworkChannel> channels,
            HashSet<Entity> ackedEntities,
            Logger logger)
        {
            this.connection = connection;
            this.ackedEntities = ackedEntities;
            entitiesInAuthTransfer = new HashSet<Entity>(Entity.DefaultComparer);

            this.logger = logger.With<OutConnection>();

            foreach (var (channelID, channel) in channels)
            {
                AddChannel(channelID, channel);
            }
        }

        internal void AddChannel(ChannelID channelID, IOutNetworkChannel channel)
        {
            if (channel == null)
            {
                throw new ArgumentNullException(nameof(channel), "channel must not be null");
            }

            if (!channelID.IsValid())
            {
                throw new ArgumentException($"Invalid ChannelID {channelID}, only channels {ChannelID.MinValue}-{ChannelID.MaxValue} are supported");
            }

            if (!channels.TryAdd(channelID, channel))
            {
                throw new Exception($"Failed to add channel, duplicate ChannelID {channelID}");
            }

            channel.OnEntityAcked += e => OnEntityAcked?.Invoke(e);
        }

        public void Update(AbsoluteSimulationFrame clientSimulationFrame)
        {
            if (connection.CanSend)
            {
                SerializeAndQueuePackets(clientSimulationFrame);

                UpdateHeldEntities();
            }
        }

        public void OnDeliveryInfo(DeliveryInfo info)
        {
            logger.Trace("OnDeliveryInfo", ("info", info.ToString()));

            foreach (var (_ , channel) in channels)
            {
                channel.OnDeliveryInfo(info.PacketSequenceId.Value, info.WasDelivered, ref ackedEntities, ref ackedComponentsPerEntity);
            }
        }

        // Can be used for scene changes to know when all the transfers and orphaning is done.
        public bool IsEntityInAuthTransfer(Entity id)
        {
            return entitiesInAuthTransfer.Contains(id);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool CanSendUpdates(Entity id) => !IsEntityInAuthTransfer(id);

        public void CreateEntity(Entity id, IReadOnlyList<ComponentData> data, ChannelID channelID)
        {
            data.TakeOwnershipAll("OutConnection.CreateEntity");

            GetChannel(channelID).CreateEntity(id, data.ReleaseOwnershipAll());
        }

        public void UpdateEntity(Entity id, IReadOnlyList<ComponentData> data, ChannelID channelID)
        {
            data.TakeOwnershipAll("OutConnection.UpdateEntity");

            if (entitiesInAuthTransfer.Contains(id))
            {
                logger.Error(Error.CoreOutConnectionUpdateOnHeldEntity, ("entity", id));
                return;
            }

            GetChannel(channelID).UpdateComponents(id, data.ReleaseOwnershipAll());
        }

        public void RemoveComponent(Entity id, uint[] componentTypes, ChannelID channelID)
        {
            if (entitiesInAuthTransfer.Contains(id))
            {
                logger.Error(Error.CoreOutConnectionRemoveComponentHeldEntity, ("entity", id));
                return;
            }

            GetChannel(channelID).RemoveComponents(id, componentTypes, ackedComponentsPerEntity);
        }

        public void DestroyEntity(Entity id, ChannelID channelID) => GetChannel(channelID).DestroyEntity(id, ackedEntities);

        public void PushCommand(CommandData command, ChannelID channelID)
        {
            command.TakeOwnership("OutConnection.PushCommand");

            GetChannel(channelID).PushCommand(command.ReleaseOwnership(), connection.UseDebugStreams);
        }

        public void PushInput(InputData input, ChannelID channelID)
        {
            input.TakeOwnership("OutConnection.PushInput");

            GetChannel(channelID).PushInput(input.ReleaseOwnership(), connection.UseDebugStreams);
        }

        private void SerializeAndQueuePackets(AbsoluteSimulationFrame clientSimulationFrame)
        {
            try
            {
                var hasChanges = false;
                foreach (var (_, channel) in channels)
                {
                    if (channel.HasChanges(ackedEntities))
                    {
                        hasChanges = true;
                        break;
                    }
                }

                if (!hasChanges)
                {
                    return;
                }

                var useDebugStream = connection.UseDebugStreams;

                var packet = connection.CreatePacket(true);

                PacketHeaderWriter.WriteHeader(packet.Stream, useDebugStream, clientSimulationFrame);

                var bitStream = useDebugStream
                        ? new DebugOutBitStream(new OutBitStream(packet.Stream))
                        : (IOutBitStream)new OutBitStream(packet.Stream);

                serializerCtxCache.Reset(bitStream, useDebugStream, logger);

                Serialize.WriteFloatingOrigin(floatingOrigin, serializerCtxCache);

                using (_ = Serialize.NewEndOfChannelsReservationScope(serializerCtxCache))
                {
                    foreach (var (channelID, channel) in channels)
                    {
                        if (!channel.HasChanges(ackedEntities))
                        {
                            // Clear serialization results so that MarkAsSent will enqueue an empty sentCache element.
                            channel.ClearLastSerializationResult();
                            continue;
                        }

                        var rewindPosition = bitStream.Position;

                        Serialize.WriteChannelID(channelID, serializerCtxCache);

                        serializerCtxCache.SetBitsRemainingInEmptyPacket();

                        var anythingSerialized = channel.Serialize(serializerCtxCache, clientSimulationFrame, floatingOrigin, entitiesInAuthTransfer.Count > 0, ackedEntities);

                        if (!anythingSerialized)
                        {
                            bitStream.Seek(rewindPosition);
                        }
                    }

                    Serialize.WriteEndOfChannels(serializerCtxCache);
                }


                bitStream.Flush();

                var octetCount = packet.Stream.Position;

                ReliableSendToConnection(packet);

                allChannelUpdatesSent.Clear();
                var totalChanges = 0;
                foreach (var (channelID, channel) in channels)
                {
                    var updatesSent = channel.MarkAsSent(packet.SequenceID.Value);
                    allChannelUpdatesSent.Add(channelID, updatesSent);
                    totalChanges += updatesSent?.Count ?? 0;
                }

                OnPacketSent?.Invoke(new PacketSentDebugInfo
                {
                    ChangesSentPerChannel = allChannelUpdatesSent,
                    TotalChanges = totalChanges,
                    OctetCount = octetCount,
                });
            }
            catch (Exception e)
            {
                if (serializerCtxCache != null && !string.IsNullOrEmpty(serializerCtxCache.Section))
                {
                    throw new SerializerException(serializerCtxCache.Section, serializerCtxCache.EntityId,
                        serializerCtxCache.ComponentId, e);
                }

                throw;
            }
        }

        private void ReliableSendToConnection(OutPacket packet)
        {
            connection.Send(packet);
        }

        public void ClearAllChangesForEntity(Entity id)
        {
            foreach (var (_, channel) in channels)
            {
                channel.ClearAllChangesForEntity(id);
            }
        }

        /// <summary>
        ///     When changing state authority, we hold on to the command and cancel all other updates until all existing updates
        ///     are sent.  This prevents losing the complete state of the entity at the time of transfer.
        /// </summary>
        public void HoldChangesForEntity(Entity id)
        {
            _ = entitiesInAuthTransfer.Add(id);
        }

        public void Reset()
        {
            foreach (var (_, channel) in channels)
            {
                channel.Reset();
            }
            ackedEntities.Clear();
            ackedComponentsPerEntity.Clear();
            entitiesInAuthTransfer.Clear();
        }

        private void UpdateHeldEntities()
        {
            entitiesToRemove.Clear();

            foreach (var id in entitiesInAuthTransfer)
            {
                var hasChangesForEntity = false;
                foreach (var (_, channel) in channels)
                {
                    if (channel.HasChangesForEntity(id))
                    {
                        hasChangesForEntity = true;
                        break;
                    }
                }

                if (!hasChangesForEntity)
                {
                    entitiesToRemove.Add(id);
                }
            }

            foreach (var id in entitiesToRemove)
            {
                _ = entitiesInAuthTransfer.Remove(id);

                OnAuthorityTransferred?.Invoke(id);
            }
        }

        internal void SetFloatingOrigin(Vector3d floatingOrigin)
        {
            this.floatingOrigin = floatingOrigin;
        }

        private IOutNetworkChannel GetChannel(ChannelID channelID)
        {
            if (!channels.TryGetValue(channelID, out var channel))
            {
                throw new ArgumentException($"Channel with ID {channelID} doesn't exist.", nameof(channelID));
            }

            return channel;
        }
    }
}
