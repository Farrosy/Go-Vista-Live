// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Transport
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Sockets;
    using Brook;
    using Brook.Octet;
    using Common;
    using Common.Pooling;
    using Connection;
    using Debugging;
    using Flux;
    using Log;
    using Stats;

    /// <summary>
    /// Allows the standalone RSL to use new .NET socket overloads to avoid allocations.
    /// </summary>
    public interface IUDPSocketDriver
    {
        int SendTo(Socket socket, byte[] buffer, int offset, int size, SocketFlags socketFlags, EndPoint remoteEP);

        /// <summary>
        /// Starts a receive loop. Calls <paramref name="onReceived"/> on the receive loop thread for each packet.
        /// <paramref name="remoteEndPoint"/> is null in listen mode; non-null in client mode.
        /// Buffer ownership: the extender rents from <paramref name="rentBuffer"/> before each receive.
        /// After <paramref name="onReceived"/> is called the transport owns the buffer and must return it
        /// via <paramref name="returnBuffer"/> when done processing.
        /// </summary>
        void StartReceiveLoop(
            Socket socket,
            IPEndPoint remoteEndPoint,
            Func<byte[]> rentBuffer,
            Action<byte[]> returnBuffer,
            Action<ArraySegment<byte>, IPEndPoint> onReceived,
            Action<SocketError, IPEndPoint> onError,
            Logger logger);
    }

    public class DefaultUDPSocketDriver : IUDPSocketDriver
    {
        private static readonly IPEndPoint AnyEndpoint = new(IPAddress.Any, 0);

        public int SendTo(Socket socket, byte[] buffer, int offset, int size, SocketFlags socketFlags, EndPoint remoteEP)
        {
            return socket.SendTo(buffer, offset, size, socketFlags, remoteEP);
        }

        public void StartReceiveLoop(
            Socket socket,
            IPEndPoint remoteEndPoint,
            Func<byte[]> rentBuffer,
            Action<byte[]> returnBuffer,
            Action<ArraySegment<byte>, IPEndPoint> onReceived,
            Action<SocketError, IPEndPoint> onError,
            Logger logger)
        {
            var isInClientMode = remoteEndPoint != null;
            var isInListenMode = !isInClientMode;

            var socketEventArgs = new SocketAsyncEventArgs();
            socketEventArgs.Completed += (_, args) => DataReceived(args);

            BeginReceive(socketEventArgs);

            void BeginReceive(SocketAsyncEventArgs args)
            {
                var nextBuffer = rentBuffer();
                args.SetBuffer(nextBuffer, 0, nextBuffer.Length);
                args.RemoteEndPoint = isInClientMode ? remoteEndPoint : AnyEndpoint;

                try
                {
                    var isPending = isInClientMode
                        ? socket.ReceiveAsync(args)
                        : socket.ReceiveFromAsync(args);

                    if (!isPending)
                    {
                        DataReceived(args);
                    }
                }
                catch (ObjectDisposedException) // only catch this, bubble everything else up
                {
                    // do nothing, socket is closed now
                    // socket was closed while async receive was waiting

                    returnBuffer(args.Buffer);
                }
            }

            void DataReceived(SocketAsyncEventArgs args)
            {
                var from = args.RemoteEndPoint as IPEndPoint;

                if (args.SocketError != SocketError.Success)
                {
                    if (isInListenMode && args.SocketError == SocketError.ConnectionReset)
                    {
                        logger.Debug("Connection reset", ("from", from));
                    }
                    else
                    {
                        onError(args.SocketError, from);
                        returnBuffer(args.Buffer);

                        return;
                    }
                }

                var data = new ArraySegment<byte>(args.Buffer, args.Offset, args.BytesTransferred);

                logger.Trace("DataReceived",
                    ("mode", isInClientMode ? "Client" : "Listen"),
                    ("from", from),
                    ("data", data.Count));

                onReceived(data, from);

                BeginReceive(args);
            }
        }
    }

    public class UdpTransportV2 : IListenTransport, IBoundPortProvider
    {
        int IBoundPortProvider.BoundPort => (socket?.LocalEndPoint as IPEndPoint)?.Port ?? 0;

        struct ReceiveEvent
        {
            public ArraySegment<byte> Data;
            public ConnectionException Error;
            public IPEndPoint From;
        }

        public const int HeaderSizeBytes = Flux.roomByteCount + SessionID.Size;
        public event Action OnOpen;
        public event Action<ConnectionException> OnError;
        public event Action OnDataReady;

        public TransportState State { get; private set; }
        public bool IsReliable => false;
        public bool CanSend => State == TransportState.Open;
        public int HeaderSize => HeaderSizeBytes;
        public string Description => "UDPv2";

        private bool IsInListenMode => remoteEndPoint == null;
        private bool IsInClientMode => !IsInListenMode;

        private const int DefaultEventCount = 64;
        private const int DefaultStreamCount = 64;

        private Socket socket;
        private IPEndPoint remoteEndPoint;
        private readonly IUDPSocketDriver socketExtender;
        private readonly IStats stats;
        private readonly Logger logger;

        private SessionID sessionId;
        private ushort roomId;
        private ushort maxBufferSize;

        private readonly Timeout timeout;

        private readonly Pool<byte[]> bufferPool;
        private readonly Pool<PooledInOctetStream> streamPool;
        private readonly ConcurrentQueue<ReceiveEvent> receiveQueue;

        public UdpTransportV2(ushort maxBufferSize, IStats stats, Logger logger, IUDPSocketDriver socketExtender = null, IDateTimeProvider dateTimeProvider = null)
        {
            this.stats = stats;
            this.logger = logger.With<UdpTransportV2>();
            this.maxBufferSize = maxBufferSize;
            this.socketExtender = socketExtender ?? new DefaultUDPSocketDriver();

            bufferPool = Pool<byte[]>
                .Builder(_ => new byte[maxBufferSize])
                .Prefill(DefaultEventCount)
                .Concurrent()
                .Build();
            streamPool = Pool<PooledInOctetStream>
                .Builder(pool => new(pool, maxBufferSize))
                .Prefill(DefaultStreamCount)
                .Concurrent()
                .Build();
            receiveQueue = new ConcurrentQueue<ReceiveEvent>();
            timeout = new Timeout(dateTimeProvider ?? new SystemDateTimeProvider(), HandleTimeout);
        }

        public void Open(EndpointData endpoint, ConnectionSettings settings)
        {
            logger.Debug("Open",
                ("endpoint", endpoint),
                ("mtu", settings.Mtu),
                ("maxBufferSize", maxBufferSize));

            try
            {
                remoteEndPoint = GetIPEndPoint(endpoint.host, endpoint.port);
                roomId = endpoint.roomId;

#if UNITY_SWITCH || UNITY_PS5
                socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
#else
                socket = new Socket(SocketType.Dgram, ProtocolType.Udp);
#endif

                SocketUtils.DisableAddressReuse(socket, logger);
                SocketUtils.SetupUDPClientBuffers(settings.Mtu, socket);

                socket.Connect(remoteEndPoint);

                logger.Debug("Connected",
                    ("localEndpoint", socket.LocalEndPoint),
                    ("remoteEndpoint", remoteEndPoint));

                timeout.SetTimeout(settings.DisconnectTimeout);
                timeout.Check(true);

                StartReceiving();
            }
            catch (Exception exception)
            {
                OnError?.Invoke(new ConnectionException("Open failed", exception));
                return;
            }

            State = TransportState.Open;
            OnOpen?.Invoke();
        }

        public void Listen(string host, int port, ConnectionSettings settings)
        {
            logger.Debug("Listen",
                ("host", host),
                ("port", port),
                ("mtu", settings.Mtu),
                ("maxBufferSize", maxBufferSize));

            try
            {
                socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

                SocketUtils.SetupUDPClientBuffers(settings.Mtu, socket);

                socket.Bind(GetIPEndPoint(host, port));

                StartReceiving();
            }
            catch (Exception exception)
            {
                OnError?.Invoke(new ConnectionException("Listen failed", exception));
                return;
            }

            State = TransportState.Open;
            OnOpen?.Invoke();
        }

        public void Close()
        {
            Close(true);
        }

        private void Close(bool raiseError)
        {
            logger.Debug("Close");

            State = TransportState.Closed;

            try
            {
                socket?.Close();
                socket?.Dispose();
            }
            catch (Exception exception)
            {
                if (raiseError)
                {
                    OnError?.Invoke(new ConnectionException("Close failed", exception));
                }
            }
        }

        private void StartReceiving()
        {
            socketExtender.StartReceiveLoop(
                socket,
                IsInClientMode ? remoteEndPoint : null,
                bufferPool.Rent,
                bufferPool.Return,
                HandlePacket,
                HandleReceiveError,
                logger);
        }

        private void HandlePacket(ArraySegment<byte> data, IPEndPoint from)
        {
            DbgAssert.That(data.Array != null, "Expected non-null buffer");
            DbgAssert.That(data.Offset == 0, "Expected offset to be 0");

            stats?.TrackIncomingPacket((uint)data.Count);

            if (IsInListenMode || remoteEndPoint.Equals(from))
            {
                receiveQueue.Enqueue(new ReceiveEvent { Data = data, From = from });
                OnDataReady?.Invoke();
            }
            else
            {
                bufferPool.Return(data.Array);
                logger.Warning(
                    Warning.TransportReceivedFromInvalidIP,
                    ("expected", remoteEndPoint),
                    ("actual", from));
            }
        }

        private void HandleReceiveError(SocketError socketError, IPEndPoint from)
        {
            ConnectionException error = socketError switch
            {
                SocketError.ConnectionRefused or SocketError.ConnectionReset =>
                    new ConnectionException($"The remote is not running or not reachable at {remoteEndPoint}"),
                SocketError.NetworkUnreachable or SocketError.HostUnreachable or SocketError.NetworkDown =>
                    new ConnectionException($"The remote at {remoteEndPoint} is unreachable: {socketError}"),
                _ => new ConnectionException($"Receive error: {socketError}, From: {from}")
            };
            receiveQueue.Enqueue(new ReceiveEvent { Error = error });
        }

        public void Send(IOutOctetStream stream)
        {
            WriteHeader(stream);
            Send(stream, endpoint: null);
        }

        public void SendTo(IOutOctetStream stream, IPEndPoint endpoint)
            => Send(stream, endpoint);

        private void Send(IOutOctetStream stream, IPEndPoint endpoint = null)
        {
            logger.Trace("Send", ("mode", IsInClientMode ? "Client" : "Listen"),
                ("to", endpoint ?? remoteEndPoint), ("data", stream.Position));

            var data = stream.Close();
            stats?.TrackOutgoingPacket((uint)data.Count);

            if (IsInClientMode)
            {
                try
                {
                    _ = socket.Send(data);
                }
                catch (SocketException e)
                {
                    var msg = e.SocketErrorCode switch
                    {
                        SocketError.ConnectionRefused or SocketError.ConnectionReset =>
                            $"The remote is not running or not reachable at {remoteEndPoint}",
                        SocketError.NetworkUnreachable or SocketError.HostUnreachable or SocketError.NetworkDown =>
                            $"The remote at {remoteEndPoint} is unreachable: {e.SocketErrorCode}",
                        _ => $"Send failed ({e.SocketErrorCode}) to {remoteEndPoint}"
                    };
                    stream.ReturnIfPoolable();
                    throw new ConnectionException(msg, e);
                }
            }
            else // Listen mode
            {
                _ = socketExtender.SendTo(socket, data.Array, data.Offset, data.Count, SocketFlags.None, endpoint);
            }

            stream.ReturnIfPoolable();
        }

        private void WriteHeader(IOutOctetStream stream)
        {
            var streamEnd = stream.Position;
            stream.Seek(0);

            stream.WriteUint16(roomId);
            SessionID.Write(sessionId, stream);

            DbgAssert.ThatFmt(stream.Position == HeaderSize,
                "Header size mismatch, was: {}, expected: {}",
                stream.Position, HeaderSize);

            stream.Seek(streamEnd);
        }

        public void Receive(List<(IInOctetStream, IPEndPoint)> buffer)
        {
            if (State == TransportState.Closed)
            {
                logger.Debug("DBG_ERROR: Receive in the closed state");
                return;
            }

            var anyValidPacketReceived = false;
            while (receiveQueue.TryDequeue(out var receiveEvent))
            {
                if (receiveEvent.Error != null)
                {
                    OnError?.Invoke(receiveEvent.Error);

                    return;
                }

                var stream = streamPool.Rent();
                stream.Reset(receiveEvent.Data);
                bufferPool.Return(receiveEvent.Data.Array);

                try
                {
                    // In Listen mode it is the responsibility of the caller to handle the roomID
                    if (IsInClientMode && !HandleRoomId(stream))
                    {
                        stream.Return();

                        continue;
                    }

                    // In Listen mode it is the responsibility of the caller to handle the sessionID
                    if (IsInClientMode && !HandleSessionId(stream))
                    {
                        stream.Return();

                        continue;
                    }
                }
                catch (Exception exception)
                {
                    stream.Return();

                    OnError?.Invoke(new ConnectionException("Failed to read roomID/sessionID", exception));

                    return;
                }

                anyValidPacketReceived = true;
                buffer.Add((stream, receiveEvent.From));
            }

            if (IsInClientMode)
            {
                timeout.Check(anyValidPacketReceived);
            }
        }

        private bool HandleRoomId(InOctetStream stream)
        {
            if (stream.RemainingOctetCount < Flux.roomByteCount)
            {
                return false;
            }

            var packetRoomId = stream.ReadUint16();
            if (packetRoomId == this.roomId)
            {
                return true;
            }

            logger.Warning(
                Warning.TransportWrongRoomID,
                ("expected", this.roomId),
                ("actual", packetRoomId));
            return false;
        }

        private bool HandleSessionId(InOctetStream stream)
        {
            if (stream.RemainingOctetCount < SessionID.Size)
            {
                return false;
            }

            var packetSessionId = SessionID.Read(stream);
            if (sessionId == SessionID.None)
            {
                sessionId = packetSessionId;
                logger.Debug("SessionID set", ("sessionID", sessionId));
                return true;
            }

            if (sessionId == packetSessionId)
            {
                return true;
            }

            logger.Debug(
                "Packet with wrong sessionID",
                ("expected", sessionId),
                ("actual", packetSessionId));
            return false;
        }

        private void HandleTimeout(ConnectionTimeoutException timeoutException)
        {
            OnError?.Invoke(timeoutException);
        }

        private static IPEndPoint GetIPEndPoint(string host, int port)
        {
            return new IPEndPoint(IPAddress.Parse(host), port);
        }
    }
}
