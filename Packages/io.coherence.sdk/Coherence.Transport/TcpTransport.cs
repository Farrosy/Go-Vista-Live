// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Transport
{
    using Brook;
    using Common;
    using Connection;
    using Log;
    using Stats;
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Sockets;
    using System.Threading;
    using System.Threading.Tasks;
    using Common.Pooling;

    internal class TcpTransport : ITransport
    {
        public event Action OnOpen;
        public event Action<ConnectionException> OnError;
        public event Action OnClose;

        public TransportState State { get; private set; }
        public bool CanSend => State == TransportState.Open;
        public bool IsReliable => true;
        public int HeaderSize => (int)TcpTransportLoop.HEADER_SIZE;
        public string Description => "TCP";

        private readonly Logger logger;
        private readonly IStats stats;
        private ConnectionSettings settings;

        /// <summary>Optional callback invoked on the receive loop thread whenever a complete
        /// TCP packet arrives. Used by the parallel server mode to schedule room ticks.</summary>
        public Action OnDataReady { get; set; }

        private bool isServerSideConnection;

        private IPEndPoint originalRemoteEndpoint;
        private CancellationTokenSource cancellationSource;
        private Socket socket;
        private TcpTransportLoop transportLoop;

        private ConcurrentQueue<(byte[], ConnectionException)> receiveQueue;
        private AsyncQueue<IOutOctetStream> sendQueue;
        private readonly Pool<PooledInOctetStream> streamPool;
        private ConnectionException deferredReceiveError;

        public TcpTransport(IStats stats, Logger logger)
        {
            this.stats = stats;
            this.logger = logger.With(typeof(TcpTransport));

            streamPool = Pool<PooledInOctetStream>
                .Builder(pool => new PooledInOctetStream(pool))
                .Build();
        }

        public void Open(EndpointData endpoint, ConnectionSettings settings)
        {
            logger.Debug($"Opening", ("host", endpoint.host), ("port", endpoint.port));

            if (State != TransportState.Closed)
            {
                logger.Warning(Warning.TCPTranportInvalidState, ("currentState", State));
                return;
            }

            originalRemoteEndpoint = GetEndpoint(endpoint);
            this.settings = settings;

            var client = new TcpClient(new IPEndPoint(0, 0));
            client.ReceiveTimeout = (int)settings.DisconnectTimeout.TotalMilliseconds;
            client.NoDelay = true;

            receiveQueue = new();
            sendQueue = new();
            cancellationSource = new();
            deferredReceiveError = null;
            transportLoop = null;

            State = TransportState.Opening;

            ConnectClient(client, endpoint).Then(task =>
            {
                if (task.IsFaulted && !cancellationSource.Token.IsCancellationRequested)
                {
                    var exception = task.Exception?.InnerException ?? task.Exception;
                    logger.Debug($"Connection failed", ("exception", exception));
                    OnError?.Invoke(GetConnectionException("TCP connection failure", exception));
                }

                client.Dispose();
            });
        }

        public void Open(TcpClient client, ConnectionSettings settings)
        {
            var endpoint = (IPEndPoint)client.Client.RemoteEndPoint;

            logger.Debug($"Opening", ("host", endpoint.Address), ("port", endpoint.Port));

            if (State != TransportState.Closed)
            {
                logger.Warning(Warning.TCPTranportInvalidState, ("currentState", State));
                return;
            }

            this.originalRemoteEndpoint = endpoint;
            this.settings = settings;
            this.socket = client.Client;
            this.isServerSideConnection = true;

            client.ReceiveTimeout = (int)settings.DisconnectTimeout.TotalMilliseconds;
            client.NoDelay = true;

            receiveQueue = new();
            sendQueue = new();
            cancellationSource = new();
            deferredReceiveError = null;
            transportLoop = null;

            State = TransportState.Opening;

            StartClient(client, null).Then(task =>
            {
                if (task.IsFaulted && !cancellationSource.Token.IsCancellationRequested)
                {
                    var exception = task.Exception?.InnerException ?? task.Exception;
                    logger.Debug($"Connection failed", ("exception", exception));
                    OnError?.Invoke(GetConnectionException("TCP connection failure", exception));
                }

                client.Dispose();
            });
        }

        public void Close()
        {
            logger.Debug("Closing", ("state", State));

            State = TransportState.Closed;

            var socketToShutdown = socket;
            socket = null;

            if (isServerSideConnection && socketToShutdown != null)
            {
                if (transportLoop != null)
                {
                    transportLoop.StopSendLoop(() => GracefulShutdown(socketToShutdown));
                }
                else
                {
                    GracefulShutdown(socketToShutdown);
                }
            }
            else
            {
                cancellationSource?.Cancel();
            }

            OnClose?.Invoke();
        }

        /// <summary>
        /// When closing a server-side TCP connection, we must prevent the OS from sending
        /// RST which would discard pending send data (e.g. a DisconnectRequest).
        /// Setting Linger ensures the socket waits for data delivery, and shutting down
        /// the send side signals FIN to the peer instead of RST.
        /// </summary>
        private void GracefulShutdown(Socket socketToShutdown)
        {
            if (socketToShutdown == null)
            {
                cancellationSource?.Cancel();
                return;
            }

            try
            {
                logger.Debug("Initiating graceful TCP shutdown", ("endpoint", originalRemoteEndpoint));
                socketToShutdown.LingerState = new LingerOption(true, 1);
                socketToShutdown.Shutdown(SocketShutdown.Send);
            }
            catch (Exception ex)
            {
                // Not error as it can fail when socket is already closed/closing.
                logger.Debug("Graceful TCP shutdown failed", ("exception", ex));
            }
            finally
            {
                cancellationSource?.Cancel();
            }
        }

        private async Task ConnectClient(TcpClient client, EndpointData endpoint)
        {
            await client.ConnectAsync(endpoint.host, endpoint.port);

            // NOTE: This continuation runs on a main thread

            if (cancellationSource.Token.IsCancellationRequested)
            {
                return;
            }

            await StartClient(client, endpoint.uniqueRoomId);
        }

        private async Task StartClient(TcpClient client, ulong? uniqueRoomId)
        {
            if (!client.Connected)
            {
                var exception = new Exception("Client not connected");
                logger.Debug($"Connection failed", ("exception", exception));
                OnError?.Invoke(GetConnectionException("TCP connection failure", exception));
                return;
            }

            logger.Debug("Open", ("localEp", client.Client.LocalEndPoint));

            socket = client.Client;

            transportLoop = new TcpTransportLoop(
                client.GetStream(),
                uniqueRoomId,
                receiveQueue,
                sendQueue,
                stats,
                logger,
                cancellationSource.Token,
                OnDataReady);

            State = TransportState.Open;
            OnOpen?.Invoke();
            await transportLoop.Run();
        }

        public void Send(IOutOctetStream data)
        {
            if (State != TransportState.Open)
            {
                return;
            }

            sendQueue.Enqueue(data);
        }

        public void Receive(List<(IInOctetStream, IPEndPoint)> buffer)
        {
            if (State != TransportState.Open)
            {
                return;
            }

            // Fire any error that was deferred from the previous Receive() call.
            // This gives the caller one tick to process data packets (e.g. a DisconnectRequest
            // OOB that carries the real close reason) before the transport error disconnects.
            if (deferredReceiveError != null)
            {
                var error = deferredReceiveError;
                deferredReceiveError = null;
                HandleReceiveException(error);
                return;
            }

            while (receiveQueue.TryDequeue(out (byte[] data, ConnectionException exception) tuple))
            {
                if (tuple.exception != null)
                {
                    deferredReceiveError = tuple.exception;
                    break;
                }

                var packet = streamPool.Rent();
                packet.Reset(tuple.data);

                buffer.Add((packet, originalRemoteEndpoint));
            }

            if (deferredReceiveError != null && buffer.Count == 0)
            {
                // There's an error, but no pending packets, we can fire immediately
                var pendingError = deferredReceiveError;
                deferredReceiveError = null;
                HandleReceiveException(pendingError);
            }
        }

        private bool HandleReceiveException(ConnectionException exception)
        {
            if (exception == null)
            {
                return false;
            }

            exception = GetConnectionException(exception.Message, exception.InnerException);

            logger.Debug($"Encountered exception", ("exception", exception));
            OnError?.Invoke(exception);
            return true;
        }

        private ConnectionException GetConnectionException(string message, Exception innerException)
        {
            // The exception can be an IO.Exception but the socket was connection reset or shutdown.
            // in these cases, we want the innerException.InnerException.
            var socketException = innerException as SocketException ?? innerException?.InnerException as SocketException;
            if (socketException != null)
            {
                if (socketException.SocketErrorCode == SocketError.TimedOut)
                {
                    return new ConnectionTimeoutException(settings.DisconnectTimeout, message, socketException);
                }
                else if (socketException.SocketErrorCode == SocketError.ConnectionReset ||
                      socketException.SocketErrorCode == SocketError.Shutdown ||
                      socketException.SocketErrorCode == SocketError.ConnectionAborted)
                {
                    return new ConnectionClosedException(originalRemoteEndpoint, message, socketException);
                }
                else if (socketException.SocketErrorCode == SocketError.ConnectionRefused)
                {
                    return new ConnectionException(
                        $"The remote is not running or not reachable at {originalRemoteEndpoint}", socketException);
                }
                else if (socketException.SocketErrorCode == SocketError.NetworkUnreachable ||
                         socketException.SocketErrorCode == SocketError.HostUnreachable ||
                         socketException.SocketErrorCode == SocketError.NetworkDown)
                {
                    return new ConnectionException(
                        $"The remote at {originalRemoteEndpoint} is unreachable: {socketException.SocketErrorCode}", socketException);
                }
            }

            // EndOfStreamException means the remote host closed the TCP connection cleanly (read returned 0 bytes).
            // This is semantically a ConnectionClosedException, same as a SocketError.ConnectionReset.
            if (innerException is System.IO.EndOfStreamException)
            {
                return new ConnectionClosedException(originalRemoteEndpoint, message, innerException);
            }

            return new ConnectionException(message, innerException);
        }

        private IPEndPoint GetEndpoint(EndpointData endpointData)
        {
            if (IPAddress.TryParse(endpointData.host, out IPAddress address))
            {
                return new IPEndPoint(address, endpointData.port);
            }

            throw new Exception($"Failed to parse host: {endpointData.host}");
        }
    }
}
