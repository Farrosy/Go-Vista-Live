// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence
{
    using System;
    using System.Collections.Generic;
    using Brisk;
    using Coherence.Core;
    using Common;
    using Connection;
    using Entities;
    using Log;
    using ProtocolDef;
    using Serializer;
    using Transport;

    public interface IClient : IDisposable
    {
        event Action<ClientID> OnConnected;
        event Action<ConnectionCloseReason> OnDisconnected;
        event Action<ConnectionException> OnConnectionError;
        event Action<EndpointData> OnConnectedEndpoint;
        event Action<ConnectionValidationRequest> OnValidateConnectionRequest;

        /// <inheritdoc cref="IConnection.OnShutdownNotice"/>
        event Action<ShutdownNoticeInfo> OnShutdownNotice;

        event Action<Entity, IncomingEntityUpdate> OnEntityCreated;
        event Action<Entity, IncomingEntityUpdate> OnEntityUpdated;
        event Action<Entity, DestroyReason> OnEntityDestroyed;
        event Action<Entity> OnEntityAcked;

        event Action<CommandData> OnCommand;
        event Action<InputData> OnInput;

        event Action<AuthorityRequest> OnAuthorityRequested;
        event Action<AuthorityRequestRejection> OnAuthorityRequestRejected;
        event Action<AuthorityChange> OnAuthorityChange;
        event Action<Entity> OnAuthorityTransferred;

        event Action<SceneIndexChanged> OnSceneIndexChanged;

        ISchemaDataFactory SchemaDataFactory { get; }
        ProtocolDef.ProtocolDefinition SchemaDefinition { get; }

        ClientID ClientID { get; }
        INetworkTime NetworkTime { get; }
        ConnectionType ConnectionType { get; }
        string Hostname { get; }
        Stats.Stats Stats { get; }
        ConnectionState ConnectionState { get; }
        Ping Ping { get; }
        EndpointData LastEndpointData { get; }
        ConnectionSettings ConnectionSettings { get; }
        uint InitialScene { get; set; }

        /// <summary>
        /// Scheduled shutdown reported by the replication server, or null when
        /// no shutdown has been announced. Cleared on disconnect.
        /// </summary>
        ShutdownNoticeInfo? ScheduledShutdown { get; }

        /// <inheritdoc cref="IConnection.ValidatedHostPayload"/>
        CustomPayload ValidatedHostPayload { get; }

        /// <inheritdoc cref="IConnection.ConnectionValidationPayload"/>
        CustomPayload ConnectionValidationPayload { get; set; }

        byte SendFrequency { get; }

        void Connect(EndpointData data, ConnectionSettings connectionSettings, ConnectionType connectionType = ConnectionType.Client);

        bool IsConnected();
        bool IsDisconnected();
        bool IsConnecting();
        void UpdateReceiving();
        void UpdateSending();
        void Disconnect();
        void Reconnect();

        void KickConnection(ClientID clientID, CustomPayload hostPayload);

        Entity CreateEntity(IReadOnlyList<ComponentData> data, bool orphan, ChannelID channelID);
        bool CanSendUpdates(Entity id);
        void UpdateComponents(Entity id, IReadOnlyList<ComponentData> data);
        void RemoveComponents(Entity id, uint[] data);
        void DestroyEntity(Entity id);

        bool EntityExists(Entity id);

        public bool HasAuthorityOverEntity(Entity entity, AuthorityType authorityType);
        public bool IsEntityInAuthTransfer(Entity id);

        void SendCommand(CommandData command, ChannelID channelID);
        void SendInput(InputData input);

        void SendAuthorityRequest(Entity id, AuthorityType authorityMode = AuthorityType.Full);

        void SendAdoptOrphanRequest(Entity id);

        bool SendAuthorityTransfer(Entity id, ClientID newAuthority, bool authorized, AuthorityType authorityType = AuthorityType.Full);

        bool SendAuthorityTransfer(Entity id, ClientID newAuthority, bool authorized, AuthorityType authorityType, out SendAuthorityTransferResult result)
        {
            if(SendAuthorityTransfer(id, newAuthority, authorized, authorityType))
            {
                result = SendAuthorityTransferResult.Success;
                return true;
            }

            result = new(SendAuthorityTransferResultType.InvalidAuthorityTypeError, Warning.CoreClientAuthTransferFailed, "SendAuthorityTransfer failed.");
            return false;
        }

        void SetFloatingOrigin(Vector3d newFloatingOrigin);
        Vector3d GetFloatingOrigin();

        void SetTransportType(TransportType transportType);
        void SetTransportFactory(ITransportFactory transportFactory);

        // For debug testing only.
        string DebugGetTransportDescription();
        void DebugHoldAllPackets(bool hold);
        void DebugReleaseAllHeldPackets();
        void DebugSetNetworkCondition(Condition condition);
        void DebugStopSerializingUpdates(bool stop);
        void DebugDropNextOutPacket(Action callback);
        void DebugOnNextPacketSentOneShot(Action callback);
        [Obsolete("Use OnEntityAcked.")]
        [Deprecated("07/2026", 2, 2, 0, Reason = "Use " + nameof(OnEntityAcked) + ".")]
        event Action<Entity> DebugOnEntityAcked;
        event Action<PacketSentDebugInfo> DebugOnPacketSent;
        event Action<int> DebugOnPacketReceived;
    }
}
