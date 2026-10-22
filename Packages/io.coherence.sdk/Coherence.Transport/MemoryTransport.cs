// Copyright (c) coherence ApS.
// See the license file in the project root for more information.

namespace Coherence.Transport
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Net;
    using System.Threading;
    using Brook;
    using Brook.Octet;
    using Common;
    using Common.Pooling;
    using Connection;
    using Flux;
    using Log;
    using Stats;

    /// <summary>
    /// In-memory transport for direct connection with the RS. Uses ConcurrentQueues instead of sockets.
    /// Can also be used as a drop-in replacement for UdpTransportV2 for testing purposes.
    /// </summary>
    public class MemoryTransport : IListenTransport
    {
        private struct TransportEvent
        {
            public IOutOctetStream Stream;
            public IPEndPoint From;
            public ConnectionException Error;
        }

        public const int HeaderSizeBytes = Flux.roomByteCount + SessionID.Size;

        public event Action OnOpen;
        public event Action<ConnectionException> OnError;

        // This isn't actually invoked in the MemoryTransport,
        // this is because the memory transport is used only when the RSL is run
        // in the Unity and in that case the RSL is using the SequentialServerRunner
        // which doesn't rely on the OnDataReady event to trigger room ticks
#pragma warning disable CS0067
        public event Action OnDataReady;
#pragma warning restore CS0067

        public TransportState State { get; private set; }
        public bool IsReliable => false;
        public bool CanSend => State == TransportState.Open;
        public int HeaderSize => HeaderSizeBytes;
        public string Description => "Memory";

        private const int DefaultStreamCount = 64;

        private bool IsInListenMode => remoteEndPoint == null;
        private bool IsInClientMode => !IsInListenMode;

        private IPEndPoint remoteEndPoint;
        private IPEndPoint localEndPoint;
        private readonly IStats stats;
        private readonly Logger logger;

        private SessionID sessionId;
        private ushort roomId;
        private ushort maxBufferSize;

        private readonly Timeout timeout;
        private ConnectionException deferredReceiveError;

        private readonly Pool<PooledInOctetStream> streamPool;
        private readonly Func<int> generateLocalPort;

        // Shared queues for communication between transports, keyed by port only.
        // IP addresses are irrelevant in an in-memory context — a server bound to 0.0.0.0
        // and a client connecting to 127.0.0.1 must share the same queue, just as a real
        // OS socket would route them to the same listener.
        private static readonly ConcurrentDictionary<int, ConcurrentQueue<TransportEvent>> endpointQueues = new();

        public MemoryTransport(ushort maxBufferSize, IStats stats, Logger logger, IDateTimeProvider dateTimeProvider = null)
            : this(maxBufferSize, stats, logger, dateTimeProvider, null) { }

        internal MemoryTransport(ushort maxBufferSize, IStats stats, Logger logger, IDateTimeProvider dateTimeProvider, Func<int> generateLocalPort)
        {
            this.stats = stats;
            this.logger = logger.With<MemoryTransport>();
            this.maxBufferSize = maxBufferSize;
            this.generateLocalPort = generateLocalPort ?? GenerateRandomPort;

            streamPool = Pool<PooledInOctetStream>
                .Builder(pool => new(pool, maxBufferSize))
                .Prefill(DefaultStreamCount)
                .Build();

            timeout = new Timeout(dateTimeProvider ?? new SystemDateTimeProvider(), HandleTimeout);
        }

        public void Open(EndpointData endpoint, ConnectionSettings settings)
        {
            logger.Debug("Open",
                ("endpoint", endpoint),
                ("maxBufferSize", maxBufferSize));

            if (!IsLocalhostAddress(endpoint.host))
            {
                throw new ArgumentException(
                    $"MemoryTransport can only connect to localhost (127.0.0.1), got: {endpoint.host}",
                    nameof(endpoint));
            }

            try
            {
                remoteEndPoint = GetIPEndPoint(endpoint.host, endpoint.port);
                roomId = endpoint.roomId;

                // The remote queue must already exist — Listen() registers it at startup.
                if (!endpointQueues.ContainsKey(remoteEndPoint.Port))
                {
                    throw new InvalidOperationException(
                        $"No MemoryTransport listener on port {remoteEndPoint.Port}. Ensure Listen() is called before Open().");
                }

                localEndPoint = ClaimFreePort();

                deferredReceiveError = null;
                timeout.SetTimeout(settings.DisconnectTimeout);
                timeout.Check(true);
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
                ("maxBufferSize", maxBufferSize));

            if (!IsValidListenAddress(host))
            {
                throw new ArgumentException(
                    $"MemoryTransport can only listen on localhost (127.0.0.1) or any IP (0.0.0.0), got: {host}",
                    nameof(host));
            }

            try
            {
                localEndPoint = GetIPEndPoint(host, port);

                if (!endpointQueues.TryAdd(localEndPoint.Port, new ConcurrentQueue<TransportEvent>()))
                {
                    throw new InvalidOperationException(
                        $"Port {localEndPoint.Port} is already in use by another MemoryTransport listener.");
                }
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
            logger.Debug("Close");
            State = TransportState.Closed;

            if (remoteEndPoint != null)
            {
                Close(remoteEndPoint);
            }

            // Clean up the queue for this port
            if (localEndPoint != null)
            {
                if (endpointQueues.TryRemove(localEndPoint.Port, out var queue))
                {
                    foreach (var entry in queue)
                    {
                        var stream = entry.Stream;
                        stream.ReturnIfPoolable();
                    }
                }
            }
        }

        public void Close(IPEndPoint endPoint)
        {
            logger.Debug("Close", ("endpoint", endPoint));

            // send null data to close the connection.
            if (localEndPoint != null && endpointQueues.TryGetValue(endPoint.Port, out var targetQueue))
            {
                var transportEvent = new TransportEvent
                {
                    Stream = null,
                    From = localEndPoint,
                    Error = null
                };

                targetQueue.Enqueue(transportEvent);
            }
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
            var targetEndpoint = endpoint ?? remoteEndPoint;

            logger.Trace("Send", ("mode", IsInClientMode ? "Client" : "Listen"),
                ("to", targetEndpoint), ("data", stream.Position));

            // Send to the target endpoint's queue
            var transportEvent = new TransportEvent
            {
                Stream = stream,
                From = localEndPoint,
                Error = null
            };

            if (!endpointQueues.TryGetValue(targetEndpoint.Port, out var targetQueue))
            {
                stream.ReturnIfPoolable();
                return;
            }

            targetQueue.Enqueue(transportEvent);
        }

        private void WriteHeader(IOutOctetStream stream)
        {
            var streamEnd = stream.Position;
            stream.Seek(0);

            stream.WriteUint16(roomId);
            SessionID.Write(sessionId, stream);

            stream.Seek(streamEnd);
        }

        public void Receive(List<(IInOctetStream, IPEndPoint)> buffer)
        {
            if (State == TransportState.Closed)
            {
                logger.Debug("DBG_ERROR: Receive in the closed state");
                return;
            }

            if (localEndPoint == null)
            {
                return;
            }

            // Fire deferred error from a previous Receive that had data packets ahead of the close signal.
            if (deferredReceiveError != null)
            {
                var error = deferredReceiveError;
                deferredReceiveError = null;
                OnError?.Invoke(error);
                return;
            }

            if (!endpointQueues.TryGetValue(localEndPoint.Port, out var queue))
            {
                return;
            }

            var anyValidPacketReceived = false;
            while (queue.TryDequeue(out var transportEvent))
            {
                var receivedStream = transportEvent.Stream;
                var from = transportEvent.From;

                if (!IsInListenMode && remoteEndPoint.Port != from.Port)
                {
                    receivedStream?.ReturnIfPoolable();

                    logger.Warning(
                        Warning.TransportReceivedFromInvalidIP,
                        ("expected", remoteEndPoint),
                        ("actual", from));

                    continue;
                }

                if (receivedStream == null)
                {
                    deferredReceiveError = new ConnectionClosedException(from, "peer closed", default);
                    break;
                }

                logger.Trace("DataReceived", ("mode", IsInClientMode ? "Client" : "Listen"),
                    ("from", from),
                    ("data", receivedStream.Position));

                var stream = streamPool.Rent();
                stream.Reset(transportEvent.Stream.Octets);
                receivedStream.ReturnIfPoolable();

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
                buffer.Add((stream, transportEvent.From));
            }

            if (deferredReceiveError != null && buffer.Count == 0)
            {
                // There's an error, but no pending packets, we can fire immediately
                var pendingError = deferredReceiveError;
                deferredReceiveError = null;
                OnError?.Invoke(pendingError);
                return;
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

        private static IPEndPoint GetIPEndPoint(string host, int port) => new IPEndPoint(IPAddress.Parse(host), port);

        private static bool IsValidListenAddress(string host) =>
            host == "0.0.0.0" || host == "::" || host == "127.0.0.1";

        private static bool IsLocalhostAddress(string host) =>
            host == "127.0.0.1" || host == "::1";

        private const int StartPort = 49152;
        private const int EndPort = 65536;
        private static int nextPort = new Random().Next(StartPort, EndPort);

        /// <summary>
        /// Atomically claims a local port.
        /// </summary>
        private IPEndPoint ClaimFreePort()
        {
            var localPort = generateLocalPort();
            var startingPort = localPort;

            // TryAdd fails if the port is already owned by a listener or another client, so we retry until we get a free one.
            while (!endpointQueues.TryAdd(localPort, new ConcurrentQueue<TransportEvent>()))
            {
                localPort = generateLocalPort();

                if (localPort == startingPort)
                {
                    throw new Exception("Failed to find a free port");
                }
            }

            return new IPEndPoint(IPAddress.Loopback, localPort);
        }

        private static int GenerateRandomPort()
        {
            int current, next;
            do
            {
                current = nextPort;
                next = current + 1 >= EndPort ? StartPort : current + 1;
            } while (Interlocked.CompareExchange(ref nextPort, next, current) != current);

            return next;
        }
    }
}
