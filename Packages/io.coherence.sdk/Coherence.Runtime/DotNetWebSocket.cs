// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Runtime
{
    using System;
    using System.Net.WebSockets;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Log;
    using Utils;

    public class DotNetWebSocket : IWebSocket
    {
        /// <summary>
        /// Server's max frame length is 32 KB.
        /// </summary>
        /// <remarks>
        /// If this is set to any higher the web socket can get closed with MessageTooBig: Max frame length of 32768 has been exceeded.
        /// </remarks>
        internal const int MaxFrameLength = 32 * 1024;

        public bool PingWebSocket => false;

        private event Action OnConnect;
        private event Action OnDisconnect;
        private event Action<Error, string> OnWebSocketFail;
        private event Action<string> OnReceive;
        private event Action<int, string, Error, string> OnSendFail;

        private WebSocket ws;
        private ClientWebSocket ClientWs => ws as ClientWebSocket;
        private CancellationTokenSource abortTokenSource;
        private CancellationToken abortToken = CancellationToken.None;
        private Task receiveTask;
        private Task connectTask;
        private Task sendTask = Task.CompletedTask;
        private readonly Logger logger = Log.GetLogger<DotNetWebSocket>();

        public DotNetWebSocket() { }

        /// <remarks> Constructor enabling injecting of a test double for testing purposes. </remarks>
        internal DotNetWebSocket(WebSocket webSocket) => ws = webSocket;

        public bool IsConnected() => ws?.State == WebSocketState.Open;

        public void OpenSocket(string endpoint, Action onConnect, Action onDisconnect,
            Action<string> onReceive, Action<Error, string> onError, Action<int, string, Error, string> onSendFail)
        {
            OnConnect = onConnect;
            OnDisconnect = onDisconnect;
            OnWebSocketFail = onError;
            OnReceive = onReceive;
            OnSendFail = onSendFail;
            abortTokenSource = new CancellationTokenSource();
            abortToken = abortTokenSource.Token;

            ws = new ClientWebSocket();

            connectTask = OpenSocketAsync(endpoint);
        }

        public async void Send(int requestCounter, string requestId, string message) => await SendAsync(requestCounter, requestId, message);

        public void Update()
        {
        }

        public async Task CloseAsync()
        {
            var disposedWs = Interlocked.CompareExchange(ref ws, null,
                ws);

            if (disposedWs == null)
            {
                return;
            }

            if (connectTask != null)
            {
                await connectTask;
            }

            CancelAndDisposeToken();

            if (receiveTask != null)
            {
                await receiveTask;
            }

            if (sendTask != null)
            {
                await sendTask;
            }

            await CloseSocketAsync(disposedWs);

            try
            {
                disposedWs.Dispose();
            }
            catch (Exception exception)
            {
                logger.Debug("Disposing DotNet WebSocket failed",
                    ("exception", exception));
            }
        }

        private async Task OpenSocketAsync(string endpoint)
        {
            try
            {
                await ClientWs.ConnectAsync(new Uri(endpoint), abortToken);
            }
            catch (WebSocketException webSocketException)
            {
                if (webSocketException.InnerException is OperationCanceledException)
                {
                    // This particular exception is expected if the
                    // connection is closed while connecting, so it's not an error.
                    return;
                }

                OnWebSocketFail?.Invoke(Error.RuntimeWebsocketCloudFailed, webSocketException.Message);
                return;
            }
            catch (Exception ex)
            {
                OnWebSocketFail?.Invoke(Error.RuntimeWebsocketOpenFailed, ex.Message);
                return;
            }

            receiveTask = RunReceive();
            OnConnect?.Invoke();
        }

        private async Task RunReceive()
        {
            var buffer = new byte[1 * 1024 * 1024];
            int receivedBytes = 0;

            try
            {
                while (!abortToken.IsCancellationRequested)
                {
                    try
                    {
                        if (ws == null)
                        {
                            return;
                        }

                        var receiveBuffer = new ArraySegment<byte>(buffer, receivedBytes, buffer.Length - receivedBytes);
                        var received = await ws.ReceiveAsync(receiveBuffer.AsMemory(), abortToken);
                        receivedBytes += received.Count;

                        if (received.MessageType == WebSocketMessageType.Close)
                        {
                            if (ws.CloseStatus is { } and not WebSocketCloseStatus.NormalClosure and not WebSocketCloseStatus.Empty)
                            {
                                logger.Warning(Warning.RuntimeWebsocketClosedByServer, $"WebSocket was closed because of {ws.CloseStatus}.\nDescription: {ws.CloseStatusDescription}.");
                            }

                            await CloseAsync();
                            return;
                        }

                        if (!received.EndOfMessage)
                        {
                            if (receivedBytes >= buffer.Length)
                            {
                                throw new WebSocketException($"Response message size over the limit of {buffer.Length} bytes");
                            }

                            continue;
                        }
                    }
                    catch (NullReferenceException)
                    {
                        // Mono specific case - that's nullref from the ClientWebSocket internals
                        // which means that the connection was closed.
                        throw new WebSocketException(WebSocketError.ConnectionClosedPrematurely);
                    }

                    var message = Encoding.UTF8.GetString(buffer, 0, receivedBytes);
                    if (!string.IsNullOrEmpty(message))
                    {
                        OnReceive?.Invoke(message);
                    }

                    receivedBytes = 0;
                }
            }
            catch (Exception exception)
            {
                switch (exception)
                {
                    case OperationCanceledException _:
                        break;
                    case WebSocketException { WebSocketErrorCode: WebSocketError.ConnectionClosedPrematurely }:
                        logger.Warning(Warning.RuntimeWebsocketClosedPrematurely,
                            ("exception", exception.Message));
                        OnWebSocketFail?.Invoke(Error.RuntimeWebsocketReceiveException, exception.Message);
                        break;
                    default:
                        logger.Error(Error.RuntimeWebsocketReceiveException,
                            ("exception", exception.Message));
                        OnWebSocketFail?.Invoke(Error.RuntimeWebsocketReceiveException, exception.Message);
                        break;
                }
            }
        }

        private async Task CloseSocketAsync(WebSocket socket)
        {
            try
            {
                logger.Debug("Closing", ("Socket State", socket.State));

                if (socket.State == WebSocketState.Closed)
                {
                    return;
                }

                var wasOpen = socket.State != WebSocketState.Open;

                try
                {
                    await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
                }
                catch { /* Ignore */ }

                if (wasOpen)
                {
                    OnDisconnect?.Invoke();
                }
            }
            catch (Exception exception)
            {
                logger.Debug("Closing failed", ("exception", exception));
            }
        }

        private void CancelAndDisposeToken()
        {
            if (abortTokenSource == null)
            {
                return;
            }

            abortTokenSource.Cancel();
            abortTokenSource.Dispose();
            abortTokenSource = null;
        }

        internal Task SendAsync(int requestCounter, string requestId, string text) => sendTask = SendAsync(requestCounter, requestId, text, sendTask);

        private async Task SendAsync(int requestCounter, string requestId, string text, Task previousSendAsyncTasks)
        {
            try
            {
                await previousSendAsyncTasks;
            }
            catch
            {
                // Ignore exceptions from previous send tasks, as we're only awaiting them to ensure ordering of sends, not to handle their exceptions here.
            }

            while (!IsConnected() && !abortToken.IsCancellationRequested)
            {
                await TimeSpan.FromMilliseconds(50);
            }

            if (abortToken.IsCancellationRequested)
            {
                return;
            }

            try
            {
                var bytes = Encoding.UTF8.GetBytes(text);

                if (bytes.Length == 0)
                {
                    await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, abortToken);
                }
                else
                {
                    var offset = 0;

                    while (offset < bytes.Length)
                    {
                        var remaining = bytes.Length - offset;
                        var chunkSize = Math.Min(MaxFrameLength, remaining);
                        var endOfMessage = (offset + chunkSize) >= bytes.Length;
                        var chunk = new ArraySegment<byte>(bytes, offset, chunkSize);

                        await ws.SendAsync(chunk, WebSocketMessageType.Text, endOfMessage, abortToken);

                        offset += chunkSize;
                    }
                }
            }
            catch (Exception exception)
            {
                if (abortToken.IsCancellationRequested)
                {
                    return;
                }

                switch (exception)
                {
                    case OperationCanceledException _:
                    case ObjectDisposedException _: // Cancellation token was disposed
                    case WebSocketException { WebSocketErrorCode: WebSocketError.InvalidState }:
                        break;
                    case NullReferenceException _:
                    case WebSocketException { WebSocketErrorCode: WebSocketError.ConnectionClosedPrematurely }:
                        logger.Warning(Warning.RuntimeWebsocketClosedPrematurely, ("requestID", requestId));
                        break;
                    default:
                        logger.Error(Error.RuntimeWebsocketSendException,
                            ("requestID", requestId),
                            ("exception", exception));
                        break;
                }

                OnSendFail?.Invoke(requestCounter, requestId, Error.RuntimeWebsocketSendException, exception.Message);
            }
        }
    }
}
