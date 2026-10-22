// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Core
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using Brisk;
    using Brisk.Models;
    using Coherence.Brook.Octet;
    using Coherence.Core.Channels;
    using Coherence.Serializer.Fragmentation;
    using Common;
    using Connection;
    using Debugging;
    using Entities;
    using Log;
    using ProtocolDef;
    using Serializer;
    using SimulationFrame;
    using Stats;
    using Tend;
    using Transport;

    public static class Core
    {
        public static IClient GetNewClient(
            ProtocolDefinition schemaDefinition,
            Logger logger,
            HashSet<Entity> activeEntities = null,
            TransportType transportType = TransportType.UDPWithTCPFallback,
            BriskServices briskServices = null
        )
        {
            var transportFactory = new DefaultTransportFactory(transportType);
            return GetNewClient(schemaDefinition, logger, activeEntities, transportFactory, briskServices);
        }

        public static IClient GetNewClient(
            ProtocolDefinition schemaDefinition,
            Logger logger,
            HashSet<Entity> activeEntities = null,
            ITransportFactory transportFactory = null,
            BriskServices briskServices = null
        )
        {
            return new ClientCore(schemaDefinition, logger, activeEntities, briskServices, transportFactory);
        }
    }

#pragma warning disable 618
    internal class ClientCore : IClient, IEntityRegistry
#pragma warning restore 618
    {
        private const int ServerParticipantID = 0;

        public event Action<ClientID> OnConnected;

        public event Action<ConnectionCloseReason> OnDisconnected;
        public event Action<ConnectionException> OnConnectionError;
        public event Action<EndpointData> OnConnectedEndpoint;
        public event Action<ConnectionValidationRequest> OnValidateConnectionRequest;
        public event Action<ShutdownNoticeInfo> OnShutdownNotice;

        public event Action<Entity, IncomingEntityUpdate> OnEntityCreated;
        public event Action<Entity, IncomingEntityUpdate> OnEntityUpdated;
        public event Action<Entity, DestroyReason> OnEntityDestroyed;
        public event Action<Entity> OnEntityAcked;

        public event Action<CommandData> OnCommand;
        public event Action<InputData> OnInput;

        public event Action<PacketSentDebugInfo> DebugOnPacketSent;
        public event Action<int> DebugOnPacketReceived;
        [Obsolete("Use OnEntityAcked.")]
        [Coherence.Deprecated("07/2026", 2, 2, 0, Reason = "Use " + nameof(OnEntityAcked) + ".")]
        public event Action<Entity> DebugOnEntityAcked
        {
            add => OnEntityAcked += value;
            remove => OnEntityAcked -= value;
        }

        public event Action<AuthorityRequest> OnAuthorityRequested;
        public event Action<AuthorityRequestRejection> OnAuthorityRequestRejected;
        public event Action<AuthorityChange> OnAuthorityChange;
        public event Action<Entity> OnAuthorityTransferred;

        public event Action<SceneIndexChanged> OnSceneIndexChanged;

        private NetworkTime networkTime;
        public INetworkTime NetworkTime => networkTime;

        public ConnectionType ConnectionType { get; private set; }
        public ClientID ClientID => connection.ClientID;

        public string Hostname => hostname;
        public Stats Stats { get; private set; }
        public ConnectionState ConnectionState => IsResolvingHost ? ConnectionState.Opening : connection.State;
        public Ping Ping => connection.Ping;
        public EndpointData LastEndpointData => lastConnectData;
        public ConnectionSettings ConnectionSettings => lastConnectionSettings;
        public uint InitialScene { get => connection.InitialScene; set => connection.InitialScene = value; }

        /// <inheritdoc/>
        public ShutdownNoticeInfo? ScheduledShutdown { get; private set; }

        /// <inheritdoc/>
        public CustomPayload ValidatedHostPayload => connection.ValidatedHostPayload;

        /// <inheritdoc/>
        public CustomPayload ConnectionValidationPayload { get => connection.ConnectionValidationPayload; set => connection.ConnectionValidationPayload = value; }

        public ISchemaDataFactory SchemaDataFactory => schemaDataFactory;
        public ProtocolDefinition SchemaDefinition => schemaDefinition;

        public byte SendFrequency => connection.SendFrequency;
        private readonly HashSet<Entity> knownEntities = new HashSet<Entity>(Entity.DefaultComparer);
        private readonly HashSet<Entity> ackedEntities = new HashSet<Entity>(Entity.DefaultComparer);
        private readonly Dictionary<Entity, AuthorityType> authorityByEntity = new Dictionary<Entity, AuthorityType>(Entity.DefaultComparer);
        private readonly Dictionary<Entity, ChannelID> channelIDByEntity = new Dictionary<Entity, ChannelID>(Entity.DefaultComparer);

        private readonly IConnection connection;
        private readonly InConnection inConnection;
        private readonly OutConnection outConnection;
        private readonly ProtocolDefinition schemaDefinition;
        private readonly PooledSchemaDataFactory schemaDataFactory;
        private readonly EntityIDGenerator entityIdGenerator;
        private string hostname;
        private readonly Logger logger;
        private readonly Queue<InPacket> receiveBuffer = new();

        private ITransportFactory transportFactory;
        private TransportConditioner networkConditioner;
        private readonly TransportConditioner.Configuration networkConditionerConfig = new TransportConditioner.Configuration();

        private IDomainNameResolver domainNameResolver;
        private CancellationTokenSource dnsResolveCancellationSource;
        private bool IsResolvingHost => dnsResolveCancellationSource != null;

        private EndpointData lastConnectData;
        private ConnectionType lastConnectionType = ConnectionType.Client;
        private ConnectionSettings lastConnectionSettings;

        private Vector3d floatingOrigin;

        public ClientCore(ProtocolDefinition schemaDefinition,
            Logger logger,
            HashSet<Entity> activeEntities = null,
            BriskServices briskServices = null,
            ITransportFactory transportFactory = null,
            IDomainNameResolver domainNameResolver = null)
        {
            this.logger = logger.With<ClientCore>();
            this.Stats = new Stats();
            this.networkTime = new NetworkTime(logger);

            this.schemaDefinition = schemaDefinition;
            // TODO: input lifecycle in the SDK is currently very complex due to the InputBuffer
            // because of this, inputs aren't pooled yet on the client side
            this.schemaDataFactory = new PooledSchemaDataFactory(schemaDefinition, threadSafe: false, poolInputs: false);
            entityIdGenerator = new EntityIDGenerator(Entity.ClientInitialIndex, Entity.MaxRelativeID, Entity.Relative, isThreadSafe: false, this.logger);

            this.transportFactory = transportFactory ?? new DefaultTransportFactory();
            this.domainNameResolver = domainNameResolver ?? new DomainNameResolver();

            briskServices ??= BriskServices.Default;
            briskServices.TransportFactory = SetUpTransport;

            var brisk = new Brisk(logger, briskServices);
            connection = brisk;

            inConnection = new InConnection(
                this,
                new Dictionary<ChannelID, IInNetworkChannel>() {
                    { ChannelID.Voice, new InFragmentedNetworkChannel(
                        new InNetworkChannel(schemaDefinition, schemaDataFactory, this, ChannelID.Voice, this.Stats, this.logger),
                        new FragmentationSerializer(),
                        this.logger) },
                    { ChannelID.Default, new InNetworkChannel(schemaDefinition, schemaDataFactory, this, ChannelID.Default, this.Stats, this.logger) },
                    { ChannelID.Ordered, new InOrderedNetworkChannel(schemaDataFactory, this, this.Stats, this.logger) },
                    { ChannelID.Fragmented, new InFragmentedNetworkChannel(
                        new InNetworkChannel(schemaDefinition, schemaDataFactory, this, ChannelID.Fragmented, this.Stats, this.logger),
                        new FragmentationSerializer(),
                        this.logger) },
                    { ChannelID.FragmentedOrdered, new InFragmentedNetworkChannel(
                        new InOrderedNetworkChannel(schemaDataFactory, this, this.Stats, this.logger),
                        new FragmentationSerializer(),
                        this.logger) }
                },
                this.logger
            );
            inConnection.OnPacketReceived += octetsReceived => DebugOnPacketReceived?.Invoke(octetsReceived);

            outConnection = new OutConnection(
                connection,
                new Dictionary<ChannelID, IOutNetworkChannel>()
                {
                    { ChannelID.Voice, new OutFragmentedNetworkChannel(
                        new OutNetworkChannel(schemaDefinition, schemaDataFactory, this.Stats, this.logger),
                        new FragmentationSerializer(),
                        this.logger) },
                    { ChannelID.Default, new OutNetworkChannel(schemaDefinition, schemaDataFactory, this.Stats, this.logger) },
                    { ChannelID.Ordered, new OutOrderedNetworkChannel(this.Stats, this.logger) },
                    { ChannelID.Fragmented, new OutFragmentedNetworkChannel(
                        new OutNetworkChannel(schemaDefinition, schemaDataFactory, this.Stats, this.logger),
                        new FragmentationSerializer(),
                        this.logger) },
                    { ChannelID.FragmentedOrdered, new OutFragmentedNetworkChannel(
                        new OutOrderedNetworkChannel(this.Stats, this.logger),
                        new FragmentationSerializer(),
                        this.logger) }
                },
                ackedEntities,
                this.logger
            );
            outConnection.OnPacketSent += packetSentDebugInfo => DebugOnPacketSent?.Invoke(packetSentDebugInfo);
            outConnection.OnEntityAcked += entity => OnEntityAcked?.Invoke(entity);
            outConnection.OnAuthorityTransferred += RaiseOnAuthorityTransferred;

            connection.OnConnect += OnConnect;
            connection.OnDisconnect += OnDisconnect;
            connection.OnError += HandleError;
            connection.OnValidateConnectionRequest += HandleOnValidateConnectionRequest;
            connection.OnShutdownNotice += HandleOnShutdownNotice;
        }

        private ITransport SetUpTransport(Logger transportLogger)
        {
            var transport = transportFactory.Create(Brisk.MaxMTU, Stats, transportLogger);
            networkConditioner = new TransportConditioner(transport, SystemDateTimeProvider.Instance, transportLogger);
            networkConditioner.SetConfiguration(networkConditionerConfig);
            return networkConditioner;
        }

        public void Connect(EndpointData data, ConnectionSettings connectionSettings, ConnectionType connectionType = ConnectionType.Client)
        {
            if (data.region == EndpointData.LocalRegion && !data.customLocalToken)
            {
                data.authToken = AuthToken.ForLocalDevelopment(connectionType);
            }
            var validation = data.ValidateLocalAddress();
            if (!validation.HasFlag(EndpointData.ValidationResult.ValidEndpoint))
            {
                logger.Warning(Warning.CoreClientConnectFailed, ("message", data.GetErrorMessage(validation)));
                return;
            }

            if (ConnectionState != ConnectionState.Disconnected)
            {
                logger.Warning(Warning.CoreClientConnectFailed, ("message", "already connected"));
                return;
            }

            logger.Debug(nameof(Connect),
                ("host", data.host),
                ("port", data.port),
                ("region", data.region),
                ("roomId", data.roomId),
                ("roomUid", data.uniqueRoomId),
                ("worldId", data.worldId),
                ("schemaId", data.schemaId));

            connection.OnDeliveryInfo += HandleDeliveryInfo;

            inConnection.OnEntityUpdate += OnEntityUpdate;
            inConnection.OnCommand += HandleCommand;
            inConnection.OnInput += HandleInput;
            inConnection.OnServerSimulationFrameReceived += OnServerSimulationFrameReceived;
            inConnection.SetMaximumTransmissionUnit(connectionSettings?.Mtu ?? Brisk.DefaultMTU);

            ConnectionType = connectionType;
            hostname = data.GetHostAndPort();

            lastConnectData = data;
            lastConnectionType = connectionType;
            lastConnectionSettings = connectionSettings;

            if (UseDnsResolution())
            {
                DnsResolveHostAndStartConnection(data, connectionSettings, connectionType);
            }
            else
            {
                StartConnection(data, connectionSettings, connectionType);
            }
        }

        private void DnsResolveHostAndStartConnection(
            EndpointData data,
            ConnectionSettings connectionSettings,
            ConnectionType connectionType)
        {
            dnsResolveCancellationSource = new CancellationTokenSource();

            domainNameResolver.Resolve(data.host, dnsResolveCancellationSource.Token, logger, ip =>
            {
                ClearDnsResolve();

                data.host = ip.ToString();

                StartConnection(data, connectionSettings, connectionType);
            }, Disconnect);
        }

        private void ClearDnsResolve()
        {
            dnsResolveCancellationSource?.Dispose();
            dnsResolveCancellationSource = null;
        }

        private void StartConnection(
            EndpointData data,
            ConnectionSettings connectionSettings,
            ConnectionType connectionType)
        {
            connection.Connect(data, connectionType, connectionSettings);

            RaiseOnConnectedEndpoint();
        }

        public void Reconnect()
        {
            Connect(lastConnectData, lastConnectionSettings, lastConnectionType);
        }

        public void KickConnection(ClientID clientID, CustomPayload hostPayload)
        {
            connection.SendKickRequest(clientID, hostPayload);
        }

        private void Reset()
        {
            logger.Trace("Reset");

            ScheduledShutdown = null;

            connection.OnDeliveryInfo -= HandleDeliveryInfo;

            inConnection.OnEntityUpdate -= OnEntityUpdate;
            inConnection.OnCommand -= HandleCommand;
            inConnection.OnInput -= HandleInput;
            inConnection.OnServerSimulationFrameReceived -= OnServerSimulationFrameReceived;
            inConnection.Clear();

            if (dnsResolveCancellationSource != null)
            {
                try
                {
                    dnsResolveCancellationSource.Cancel();
                }
                catch (Exception e)
                {
                    logger.Error(Error.CoreClientDNSResolveException, ("exception", e));
                }

                ClearDnsResolve();
            }

            NetworkTime.Reset(notify: false);

            foreach (var kv in authorityByEntity)
            {
                if (kv.Value.Contains(AuthorityType.State))
                {
                    var channelID = GetChannelIDForEntity(kv.Key);
                    outConnection.DestroyEntity(kv.Key, channelID);
                    outConnection.ClearAllChangesForEntity(kv.Key);
                }
            }

            outConnection.Reset();
            entityIdGenerator.Reset();

            authorityByEntity.Clear();
            channelIDByEntity.Clear();
            knownEntities.Clear();
            ackedEntities.Clear();
        }

        private void OnConnect(ConnectResponse connectResponse)
        {
            logger.Trace(nameof(OnConnect), ("state", ConnectionState), ("clientID", ClientID));

            var simulationFrame = new AbsoluteSimulationFrame { Frame = (long)connectResponse.SimulationFrame };
            networkTime.SetServerSimulationFrame(simulationFrame, connection.Ping);

            // Update the connection settings with the negotiated MTU.
            lastConnectionSettings.Mtu = connectResponse.MTU;

            try
            {
                OnConnected?.Invoke(ClientID);
            }
            catch (Exception handlerException)
            {
                logger.Error(Error.CoreClientHandlerException,
                    ("caller", nameof(OnConnect)),
                    ("exception", handlerException));
            }
        }

        private void OnDisconnect(ConnectionCloseReason reason)
        {
            logger.Trace(nameof(OnDisconnect), ("state", ConnectionState), ("clientID", ClientID));

            ScheduledShutdown = null;

            try
            {
                OnDisconnected?.Invoke(reason);
            }
            catch (Exception handlerException)
            {
                logger.Error(Error.CoreClientHandlerException,
                    ("caller", nameof(OnDisconnect)),
                    ("exception", handlerException));
            }
        }

        private void HandleOnShutdownNotice(ShutdownNoticeInfo notice)
        {
            ScheduledShutdown = notice;

            logger.Debug(nameof(HandleOnShutdownNotice),
                ("shutdownAt", notice.ShutdownTimeUtc),
                ("hasPayload", !notice.Payload.IsEmpty));

            try
            {
                OnShutdownNotice?.Invoke(notice);
            }
            catch (Exception handlerException)
            {
                logger.Error(Error.CoreClientHandlerException,
                    ("caller", nameof(HandleOnShutdownNotice)),
                    ("exception", handlerException));
            }
        }

        private void HandleOnValidateConnectionRequest(ConnectionValidationRequest request)
        {
            if (OnValidateConnectionRequest != null)
            {
                OnValidateConnectionRequest.Invoke(request);
            }
            else
            {
                // In case there's no handler, we accept the connection by default.
                request.Respond(new ConnectionValidationResponse(true));
            }
        }

        private void HandleError(ConnectionException exception)
        {
            logger.Debug(nameof(HandleError), ("state", ConnectionState), ("exception", exception));

            // Log connection errors unless explicitly suppressed in settings
            var shouldLogConnectionErrors = ConnectionSettings is { SuppressConnectionErrors: false };
            if (shouldLogConnectionErrors)
            {
                var isGracefulClose = exception is ConnectionDeniedException { CloseReason: ConnectionCloseReason.GracefulClose };
                if (isGracefulClose)
                {
                    logger.Error(Error.CoreConnectionError, ("exception", exception));
                }
            }

            try
            {
                OnConnectionError?.Invoke(exception);
            }
            catch (Exception handlerException)
            {
                logger.Error(Error.CoreClientHandlerException,
                    ("caller", nameof(HandleError)),
                    ("exception", handlerException));

            }

            ConnectionCloseReason disconnectReason = ConnectionCloseReason.Unknown;
            bool serverInitiated = false;
            if (exception is ConnectionDeniedException deniedException)
            {
                disconnectReason = deniedException.CloseReason;
                serverInitiated = true;
            }
            else if (exception is ConnectionTimeoutException)
            {
                disconnectReason = ConnectionCloseReason.Timeout;
            }
            else if (exception is ConnectionClosedException)
            {
                disconnectReason = ConnectionCloseReason.SocketClosedByPeer;
            }
            else
            {
                disconnectReason = ConnectionCloseReason.InvalidData;
            }

            Disconnect(disconnectReason, serverInitiated);
        }

        private void HandleDeliveryInfo(DeliveryInfo deliveryInfo)
        {
            outConnection.OnDeliveryInfo(deliveryInfo);
        }

        public void Disconnect()
        {
            Disconnect(ConnectionCloseReason.GracefulClose, false);
        }

        internal void Disconnect(ConnectionCloseReason reason, bool serverInitiated)
        {
            logger.Debug($"{nameof(Disconnect)}", ("state", ConnectionState), ("serverInitiated", serverInitiated));

            Reset();

            try
            {
                connection.Disconnect(reason, serverInitiated);
            }
            catch (Exception handlerException)
            {
                logger.Error(Error.CoreClientHandlerException,
                    ("caller", nameof(Disconnect)),
                    ("exception", handlerException));
            }

            schemaDataFactory.AssertNoLeaks();

            logger.Trace($"{nameof(Disconnect)} (end)", ("state", ConnectionState), ("serverInitiated", serverInitiated));
        }

        public bool IsConnected()
        {
            return ConnectionState == ConnectionState.Connected;
        }

        public bool IsConnecting()
        {
            return ConnectionState == ConnectionState.Connecting;
        }

        public bool IsDisconnected()
        {
            return ConnectionState == ConnectionState.Disconnected;
        }

        public void UpdateReceiving()
        {
            if (connection == null)
            {
                return;
            }

            ReceiveAndProcessPackets();
        }

        private void ReceiveAndProcessPackets()
        {
            receiveBuffer.Clear();
            connection.Receive(receiveBuffer);

            while (receiveBuffer.Count > 0)
            {
                // If we got disconnected while processing packets, we want to stop processing
                // immediately to avoid raising invalid events after disconnection.
                if (ConnectionState == ConnectionState.Disconnected)
                {
                    break;
                }

                var packet = receiveBuffer.Dequeue();

                try
                {
                    inConnection.ProcessIncomingPacket(packet.Stream);
                }
                catch (Exception e)
                {
                    logger.Error(Error.CoreClientHandlerException,
                        ("caller", nameof(InConnection.ProcessIncomingPacket)),
                        ("from", packet.From),
                        ("position", packet.Stream.Position),
                        ("header", $"Seq: {packet.SequenceID} Reliable: {packet.IsReliable} OOB: {packet.IsOob}"),
                        ("contents", Convert.ToBase64String(packet.Stream.GetBuffer())),
                        ("exception", e));

                    Disconnect(ConnectionCloseReason.InvalidData, false);
                    break;
                }
            }

            // If we disconnect early due to an error or due to a disconnect OOB,
            // we want to be sure to return all the packets in the buffer to avoid leaks, even if we don't process them.
            while (receiveBuffer.Count > 0)
            {
                var packet = receiveBuffer.Dequeue();
                packet.Stream.ReturnIfPoolable();
            }
        }

        public void UpdateSending()
        {
            if (connection == null)
            {
                return;
            }

            outConnection.Update(NetworkTime.ClientSimulationFrame);
            connection.Update();
        }

        public Entity CreateEntity(IReadOnlyList<ComponentData> data, bool orphan, ChannelID channelID)
        {
            data.TakeOwnershipAll("ClientCore.CreateEntity");

            var err = entityIdGenerator.GetEntity(out var id);
            if (err != EntityIDGenerator.Error.None)
            {
                throw new Exception($"out of entities");
            }

            logger.Trace("Create Entity", ("entity", id), ("comp", data.ToStringEx()));

            knownEntities.Add(id);
            authorityByEntity.Add(id, AuthorityType.Full);

            outConnection.CreateEntity(id, data.ReleaseOwnershipAll(), channelID);

            channelIDByEntity[id] = channelID;

            if (orphan)
            {
                SendAuthorityTransfer(id, ClientID.Server, true, AuthorityType.Full);
            }

            return id;
        }

        public bool IsEntityInAuthTransfer(Entity id)
        {
            return outConnection.IsEntityInAuthTransfer(id);
        }

        public bool CanSendUpdates(Entity id)
        {
            DbgAssert.ThatFmt(HasAuthorityOverEntity(id, AuthorityType.State),
                "Trying to check if can send updates for entity {0} without state authority.", id);

            return outConnection.CanSendUpdates(id);
        }

        public void UpdateComponents(Entity id, IReadOnlyList<ComponentData> data)
        {
            data.TakeOwnershipAll("ClientCore.UpdateComponents");

            if (!HasAuthorityOverEntity(id, AuthorityType.State))
            {
                data.DisposeAll();

                return;
            }

            if (data.Count < 1)
            {
                return;
            }

            var channelID = GetChannelIDForEntity(id);

            outConnection.UpdateEntity(id, data.ReleaseOwnershipAll(), channelID);
        }

        public void RemoveComponents(Entity id, uint[] components)
        {
            if (!HasAuthorityOverEntity(id, AuthorityType.State))
            {
                return;
            }

            var channelID = GetChannelIDForEntity(id);

            outConnection.RemoveComponent(id, components, channelID);
        }

        public void DestroyEntity(Entity id)
        {
            logger.Trace("Destroy Entity", ("entity", id));

            if (id == Entity.InvalidRelative)
            {
                throw new Exception("Trying to destroy an invalid entity.");
            }

            if (!EntityExists(id))
            {
                // This can be caused by a network destroy disabling and not destroying an entity
                // which causes it to then try to destroy the client entity but that has already
                // been destroyed.  Particularly, if the entity is a child of another entity that
                // was disabled because of duplicate destroy, this entity will also be disabled
                // but the client core doesn't know about this entity any more.  Nested prefabs
                // leads to all kinds of races and orders of operation.
                return;
            }

            var disconnected = ConnectionState == ConnectionState.Disconnected;
            var entityOwned = HasAuthorityOverEntity(id, AuthorityType.State);
            if (!entityOwned && !disconnected)
            {
                throw new Exception("Trying to destroy an entity without state authority.");
            }

            knownEntities.Remove(id);
            authorityByEntity.Remove(id);

            if (id.IsClientCreated() && !disconnected)
            {
                // It's a local entity ID, so we can recycle it.
                entityIdGenerator.ReleaseEntity(id);
            }

            var channelID = GetChannelIDForEntity(id);

            outConnection.DestroyEntity(id, channelID);
        }

        public bool EntityExists(Entity entity)
        {
            return knownEntities.Contains(entity);
        }

        public bool HasAuthorityOverEntity(Entity entity, AuthorityType authorityType)
        {
            return authorityByEntity.TryGetValue(entity, out var authType) && authType.Contains(authorityType);
        }

        public void SendCommand(CommandData command, ChannelID channelID)
        {
            command.TakeOwnership("ClientCore.SendCommand");

            if (!EntityExists(command.Entity))
            {
                command.Dispose();
                return;
            }

            if (command.MessageTarget == MessageTarget.StateAuthorityOnly && HasAuthorityOverEntity(command.Entity, AuthorityType.State))
            {
                command.Dispose();
                return;
            }

            outConnection.PushCommand(command.ReleaseOwnership(), channelID);
        }

        public void SendInput(InputData input)
        {
            input.TakeOwnership("ClientCore.SendInput");

            var channelID = GetChannelIDForEntity(input.Entity);

            outConnection.PushInput(input.ReleaseOwnership(), channelID);
        }

        public void SendAuthorityRequest(Entity id, AuthorityType authorityType = AuthorityType.Full)
        {
            var command = AuthorityRequestCommand.Create(schemaDataFactory, id, MessageTarget.StateAuthorityOnly, ClientID, authorityType);
            SendCommand(command, ChannelID.Default);
        }

        public void SendAdoptOrphanRequest(Entity id)
        {
            var command = AdoptOrphanCommand.Create(schemaDataFactory, id, MessageTarget.StateAuthorityOnly);
            SendCommand(command, ChannelID.Default);
        }

        public bool SendAuthorityTransfer(Entity id, ClientID newAuthority, bool authorized, AuthorityType transferredAuthorityType = AuthorityType.Full)
        {
            logger.Debug("Authority Transfer",
                ("entity", id),
                ("newAuthority", newAuthority),
                ("authorized", authorized),
                ("transferredAuthorityType", transferredAuthorityType));

            if (transferredAuthorityType == AuthorityType.None)
            {
                return false;
            }

            if (authorized)
            {
                if (!authorityByEntity.TryGetValue(id, out AuthorityType currentAuthorityType))
                {
                    logger.Warning(Warning.CoreClientAuthTransferNoAuthority,
                        ("entity", id),
                        ("transferredAuthorityType", transferredAuthorityType),
                        ("newAuthority", newAuthority));
                    return false;
                }

                if (!currentAuthorityType.CanTransfer(transferredAuthorityType))
                {
                    logger.Warning(Warning.CoreClientAuthInsufficient,
                        ("entity", id),
                        ("currentAuthorityType", currentAuthorityType),
                        ("transferredAuthorityType", transferredAuthorityType),
                        ("newAuthority", newAuthority));
                    return false;
                }

                if (transferredAuthorityType == AuthorityType.Input && newAuthority == ClientID.Server)
                {
                    logger.Warning(Warning.CoreClientAuthCantOrphanInput,
                        ("entity", id),
                        ("currentAuthorityType", currentAuthorityType),
                        ("transferredAuthorityType", transferredAuthorityType));
                    return false;
                }

                AuthorityType authorityTypeLeft = currentAuthorityType.Subtract(transferredAuthorityType);
                if (authorityTypeLeft == AuthorityType.None)
                {
                    authorityByEntity.Remove(id);
                }
                else
                {
                    authorityByEntity[id] = authorityTypeLeft;
                }

                if (transferredAuthorityType.Contains(AuthorityType.State))
                {
                    outConnection.HoldChangesForEntity(id);
                }

                if (authorityTypeLeft != currentAuthorityType)
                {
                    RaiseOnAuthorityChange(id, authorityTypeLeft);
                }
            }

            var channelID = GetChannelIDForEntity(id);

            var command = AuthorityTransferCommand.Create(schemaDataFactory, id, MessageTarget.Other, newAuthority, authorized, transferredAuthorityType);
            SendCommand(command, channelID);

            return true;
        }

        public bool SendAuthorityTransfer(Entity id, ClientID newAuthority, bool authorized, AuthorityType transferredAuthorityType, out SendAuthorityTransferResult result)
        {
            logger.Debug("Authority Transfer",
                ("entity", id),
                ("newAuthority", newAuthority),
                ("authorized", authorized),
                ("transferredAuthorityType", transferredAuthorityType));

            if (transferredAuthorityType == AuthorityType.None)
            {
                result = new(SendAuthorityTransferResultType.InvalidAuthorityTypeError, Warning.CoreClientAuthTransferNoAuthority, "Transferred authority type cannot be None.");
                return false;
            }

            if (authorized)
            {
                if (!authorityByEntity.TryGetValue(id, out AuthorityType currentAuthorityType))
                {
                    result = new(SendAuthorityTransferResultType.InvalidAuthorityTypeError, Warning.CoreClientAuthTransferNoAuthority,
                        $"Tried to transfer authority of an entity with no authority.\nentity: {id}\ntransferredAuthorityType: {transferredAuthorityType}\nnewAuthority: {newAuthority}");
                    return false;
                }

                if (!currentAuthorityType.CanTransfer(transferredAuthorityType))
                {
                    result = new(SendAuthorityTransferResultType.InvalidAuthorityTypeError, Warning.CoreClientAuthInsufficient,
                        $"Insufficient authority for transfer\nentity: {id}\ncurrentAuthorityType: {currentAuthorityType}\ntransferredAuthorityType: {transferredAuthorityType}\nnewAuthority: {newAuthority}");
                    return false;
                }

                if (transferredAuthorityType == AuthorityType.Input && newAuthority == ClientID.Server)
                {
                    result = new(SendAuthorityTransferResultType.InvalidAuthorityTypeError, Warning.CoreClientAuthCantOrphanInput,
                        $"Cannot orphan input authority.\nentity: {id}\ncurrentAuthorityType: {currentAuthorityType}\ntransferredAuthorityType: {transferredAuthorityType}");
                    return false;
                }

                var authorityTypeLeft = currentAuthorityType.Subtract(transferredAuthorityType);
                if (authorityTypeLeft is AuthorityType.None)
                {
                    authorityByEntity.Remove(id);
                }
                else
                {
                    authorityByEntity[id] = authorityTypeLeft;
                }

                if (transferredAuthorityType.Contains(AuthorityType.State))
                {
                    outConnection.HoldChangesForEntity(id);
                }

                if (authorityTypeLeft != currentAuthorityType)
                {
                    RaiseOnAuthorityChange(id, authorityTypeLeft);
                }
            }

            var channelID = GetChannelIDForEntity(id);

            var req = AuthorityTransferCommand.Create(schemaDataFactory, id, MessageTarget.Other, newAuthority, authorized, transferredAuthorityType);
            SendCommand(req, channelID);
            result = SendAuthorityTransferResult.Success;
            return true;
        }

        public void SetFloatingOrigin(Vector3d newFloatingOrigin)
        {
            this.floatingOrigin = newFloatingOrigin;
            outConnection.SetFloatingOrigin(newFloatingOrigin);
        }

        public Vector3d GetFloatingOrigin()
        {
            return this.floatingOrigin;
        }

        public void SetTransportType(TransportType transportType)
        {
            this.transportFactory = new DefaultTransportFactory(transportType);
        }

        public void SetTransportFactory(ITransportFactory transportFactory)
        {
            this.transportFactory = transportFactory ?? throw new ArgumentNullException(nameof(transportFactory));
        }

        private void RaiseOnAuthorityTransferred(Entity id)
        {
            OnAuthorityTransferred?.Invoke(id);
        }

        private void OnEntityUpdate(IncomingEntityUpdate update)
        {
            update.TakeOwnershipOfComponents("ClientCore.OnEntityUpdate");

            EntityWithMeta meta = update.Meta;
            bool known = EntityExists(meta.EntityId);

            logger.Trace("OnEntityUpdate",
                ("entity", meta.EntityId.ToString()),
                ("operation", meta.Operation.ToString()),
                ("known", known));

            try
            {
                switch (meta.Operation)
                {
                    case EntityOperation.Create:
                        if (known)
                        {
                            HandleReceivedUpdate(update.ReleaseOwnershipOfComponents());
                            break;
                        }

                        HandleReceivedCreate(update.ReleaseOwnershipOfComponents());
                        break;

                    case EntityOperation.Update:
                        HandleReceivedUpdate(update.ReleaseOwnershipOfComponents());
                        break;

                    case EntityOperation.Destroy:
                        update.Dispose();
                        HandleReceivedDestroy(meta, known);
                        break;

                    default:
                        logger.Error(Error.CoreClientEntityUpdateUnknownOperation,
                            ("EntityID", meta.EntityId),
                            ("operation", meta.Operation));
                        update.Dispose();
                        break;
                }
            }
            catch (Exception exception)
            {
                logger.Error(Error.CoreClientEntityUpdateFailed,
                    ("EntityID", meta.EntityId),
                    ("operation", meta.Operation),
                    ("exception", exception));
            }
        }

        private void HandleReceivedCreate(in IncomingEntityUpdate update)
        {
            update.TakeOwnershipOfComponents("ClientCore.HandleReceivedCreate");

            EntityWithMeta meta = update.Meta;

            logger.Debug($"OnEntityUpdate New", ("EntityId", meta.EntityId), ("Comps", update));

            knownEntities.Add(meta.EntityId);
            AuthorityType authorityType = meta.Authority();
            if (authorityType != AuthorityType.None)
            {
                authorityByEntity.Add(meta.EntityId, authorityType);
                ackedEntities.Add(meta.EntityId);
            }

            channelIDByEntity[meta.EntityId] = update.ChannelID;

            try
            {
                OnEntityCreated?.Invoke(meta.EntityId, update.ReleaseOwnershipOfComponents());
            }
            catch (Exception handlerException)
            {
                logger.Error(Error.CoreClientHandlerException,
                    ("caller", nameof(OnEntityCreated)),
                    ("exception", handlerException));
            }
        }

        private void HandleReceivedUpdate(in IncomingEntityUpdate update)
        {
            update.TakeOwnershipOfComponents("ClientCore.HandleReceivedUpdate");

            EntityWithMeta meta = update.Meta;

            logger.Trace($"OnEntityUpdate Update ", ("EntityId", meta.EntityId), ("Update", update));

            try
            {
                OnEntityUpdated?.Invoke(meta.EntityId, update.ReleaseOwnershipOfComponents());
            }
            catch (Exception handlerException)
            {
                logger.Error(Error.CoreClientHandlerException,
                    ("caller", nameof(OnEntityUpdated)),
                    ("exception", handlerException));
            }

            bool changedOwnership = ProcessAuthorityChange(meta, out AuthorityType newAuthorityType);
            if (changedOwnership)
            {
                RaiseOnAuthorityChange(meta.EntityId, newAuthorityType);

                if (!meta.HasStateAuthority)
                {
                    outConnection.ClearAllChangesForEntity(meta.EntityId);
                }
            }
        }

        private void HandleReceivedDestroy(in EntityWithMeta meta, bool known)
        {
            logger.Debug("OnEntityUpdate destroy",
                ("EntityId", meta.EntityId),
                ("DestroyReason", meta.DestroyReason),
                ("known", known));

            if (!known)
            {
                return;
            }

            try
            {
                OnEntityDestroyed?.Invoke(meta.EntityId, meta.DestroyReason);
            }
            catch (Exception handlerException)
            {
                logger.Error(Error.CoreClientHandlerException,
                    ("caller", nameof(OnEntityDestroyed)),
                    ("exception", handlerException));
            }

            if (!knownEntities.Contains(meta.EntityId))
            {
                // this probably already deleted itself from the core
                // by disabling because it was a duplicate.
                return;
            }

            knownEntities.Remove(meta.EntityId);
            authorityByEntity.Remove(meta.EntityId);

            if (meta.EntityId.IsClientCreated())
            {
                // It's a local entity ID, its authority was transferred.
                entityIdGenerator.ReleaseEntity(meta.EntityId);
            }
        }

        private bool ProcessAuthorityChange(EntityWithMeta meta, out AuthorityType newAuthorityType)
        {
            Entity entityId = meta.EntityId;
            newAuthorityType = meta.Authority();

            if (newAuthorityType == AuthorityType.None)
            {
                bool authorityChanged = authorityByEntity.Remove(entityId);
                if (authorityChanged)
                {
                    logger.Debug($"{nameof(ProcessAuthorityChange)} - authority lost",
                        ("entity", entityId));
                }

                return authorityChanged;
            }

            bool hasSomeAuthority = authorityByEntity.TryGetValue(entityId, out AuthorityType currentAuthority);
            if (hasSomeAuthority)
            {
                if (newAuthorityType != currentAuthority)
                {
                    logger.Debug($"{nameof(ProcessAuthorityChange)} - authority changed",
                        ("entity", entityId),
                        ("newAuthority", newAuthorityType),
                        ("currentAuthority", currentAuthority));

                    authorityByEntity[entityId] = newAuthorityType;
                    return true;
                }

                return false;
            }

            if (newAuthorityType != AuthorityType.None)
            {
                logger.Debug($"{nameof(ProcessAuthorityChange)} - authority gained",
                    ("entity", entityId),
                    ("newAuthority", newAuthorityType));

                authorityByEntity.Add(entityId, newAuthorityType);
                ackedEntities.Add(entityId);

                return true;
            }

            return false;
        }

        private void HandleCommand(CommandData command)
        {
            switch (command.CommandID)
            {
                case AuthorityRequestCommand.CommandID:
                    var authorityRequestCommand = AuthorityRequestCommand.From(command);
                    if (!HasAuthorityOverEntity(command.Entity, AuthorityType.State))
                    {
                        logger.Debug($"Received authority request command for non-owned entity",
                            ("EntityId", command.Entity),
                            ("Requester", authorityRequestCommand.Requester),
                            ("AuthType", authorityRequestCommand.AuthorityType));
                        command.Dispose();
                        return;
                    }

                    RaiseOnAuthorityRequested(command.Entity, authorityRequestCommand.Requester, authorityRequestCommand.AuthorityType);
                    command.Dispose();
                    break;
                case AuthorityTransferCommand.CommandID:
                    var authorityTransferCommand = AuthorityTransferCommand.From(command);
                    if (authorityTransferCommand.Accepted)
                    {
                        //Intentionally throw away a command with the accepted auth since the RS will have already applied it.
                        command.Dispose();
                        return;
                    }

                    RaiseOnAuthorityRequestRejected(command.Entity, authorityTransferCommand.AuthorityType);
                    command.Dispose();
                    break;
                case SceneIndexChangedCommand.CommandID:
                    var sceneIndexChangedCommand = SceneIndexChangedCommand.From(command);
                    RaiseOnSceneIndexChanged(command.Entity, sceneIndexChangedCommand.SceneIndex);
                    command.Dispose();
                    break;
                default:
                    if (command.MessageTarget == MessageTarget.StateAuthorityOnly && !HasAuthorityOverEntity(command.Entity, AuthorityType.State))
                    {
                        logger.Warning(Warning.CoreClientCommandUnownedEntity,
                            ("target", command.MessageTarget),
                            ("EntityId", command.Entity),
                            ("Command", command.CommandID));
                        command.Dispose();
                        return;
                    }

                    try
                    {
                        logger.Trace($"INVOKE {command.Entity} - {command.CommandID}");
                        OnCommand?.Invoke(command);
                    }
                    catch (Exception handlerException)
                    {
                        logger.Error(Error.CoreClientHandlerException,
                            ("caller", nameof(OnCommand)),
                            ("exception", handlerException));
                    }
                    break;
            }
        }

        private void HandleInput(InputData input)
        {
            try
            {
                OnInput?.Invoke(input);
            }
            catch (Exception handlerException)
            {
                logger.Error(Error.CoreClientHandlerException,
                    ("caller", nameof(OnInput)),
                    ("exception", handlerException));
            }
        }

        public void Dispose()
        {
            Disconnect(ConnectionCloseReason.GracefulClose, false);
        }

        private bool UseDnsResolution()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return false;
#else
            return true;
#endif
        }

        private void RaiseOnAuthorityRequested(Entity id, ClientID requester, AuthorityType authType)
        {
            try
            {
                OnAuthorityRequested?.Invoke(new AuthorityRequest(id, requester, authType));
            }
            catch (Exception handlerException)
            {
                logger.Error(Error.CoreClientHandlerException,
                    ("caller", nameof(OnAuthorityRequested)),
                    ("exception", handlerException));
            }
        }

        private void RaiseOnAuthorityRequestRejected(Entity id, AuthorityType authType)
        {
            try
            {
                OnAuthorityRequestRejected?.Invoke(new AuthorityRequestRejection(id, authType));
            }
            catch (Exception handlerException)
            {
                logger.Error(Error.CoreClientHandlerException,
                    ("caller", nameof(OnAuthorityRequestRejected)),
                    ("exception", handlerException));
            }
        }

        private void RaiseOnAuthorityChange(Entity id, AuthorityType authorityType)
        {
            try
            {
                OnAuthorityChange?.Invoke(new AuthorityChange(id, authorityType));
            }
            catch (Exception handlerException)
            {
                logger.Error(Error.CoreClientHandlerException,
                    ("caller", nameof(OnAuthorityChange)),
                    ("exception", handlerException));
            }
        }

        private void RaiseOnConnectedEndpoint()
        {
            try
            {
                OnConnectedEndpoint?.Invoke(lastConnectData);
            }
            catch (Exception handlerException)
            {
                logger.Error(Error.CoreClientHandlerException,
                    ("caller", nameof(OnConnectedEndpoint)),
                    ("exception", handlerException));
            }
        }

        private void RaiseOnSceneIndexChanged(Entity id, int sceneIndex)
        {
            try
            {
                OnSceneIndexChanged?.Invoke(new SceneIndexChanged(id, sceneIndex));
            }
            catch (Exception handlerException)
            {
                logger.Error(Error.CoreClientHandlerException,
                    ("caller", nameof(OnSceneIndexChanged)),
                    ("exception", handlerException));
            }
        }

        // For debug testing only.
        public string DebugGetTransportDescription() => connection.TransportDescription;

        public void DebugHoldAllPackets(bool drop)
        {
            networkConditionerConfig.HoldOutgoingPackets = drop;
        }

        public void DebugReleaseAllHeldPackets()
        {
            networkConditioner.ReleaseAllHeldOutgoingPackets();
        }

        public void DebugSetNetworkCondition(Condition condition)
        {
            networkConditionerConfig.Conditions = condition;
        }

        public void DebugStopSerializingUpdates(bool stop)
        {
            networkConditionerConfig.CanSend = !stop;
        }

        public void DebugDropNextOutPacket(Action callback)
        {
            networkConditionerConfig.DropNextOutPacket = true;
            networkConditionerConfig.OnNextOutPacketDropped = callback;
        }

        public void DebugOnNextPacketSentOneShot(Action callback)
        {
            networkConditionerConfig.OnNextPacketSentOneShot += callback;
        }

        private void OnServerSimulationFrameReceived(AbsoluteSimulationFrame serverFrame)
        {
            networkTime.SetServerSimulationFrame(serverFrame, connection.Ping);
        }

        private ChannelID GetChannelIDForEntity(Entity entity)
        {
            if (!channelIDByEntity.TryGetValue(entity, out var channelID))
            {
                throw new Exception($"Couldn't find the channel for entity {entity}");
            }

            return channelID;
        }
    }
}
