// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Transport
{
    using System;
    using System.Net.Sockets;
    using Log;

    internal static class SocketUtils
    {
        /// <summary>
        /// Disables SO_REUSEADDR on the socket to prevent multiple sockets from being assigned
        /// the same ephemeral port. Without this, the OS can assign the same local port to two
        /// UDP sockets connecting to the same remote endpoint, causing the replication server to
        /// see combined traffic from both sockets as a single connection and trigger
        /// recv-frequency throttling.
        /// </summary>
        internal static void DisableAddressReuse(Socket socket, Logger logger)
        {
            try
            {
                socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, false);
            }
            catch (Exception exception)
            {
                logger.Debug("Failed to disable address reuse", ("exception", exception));
            }
        }

        internal static void SetupUDPClientBuffers(ushort maxBufferSize, Socket socket)
        {
            // IP + UDP headers are up to 68 bytes plus we want to be able to hold at least one
            // packet in the buffer in case there's a delay sending or revceiving.
            const int IP_OVERHEAD = 68;
            const int NUM_BUFFERED_SEND_PACKETS = 3;
            const int NUM_BUFFERED_RECV_PACKETS = 10;

            var minSendBufferSize = (maxBufferSize + IP_OVERHEAD) * NUM_BUFFERED_SEND_PACKETS;
            if (socket.SendBufferSize < minSendBufferSize)
            {
                socket.SendBufferSize = minSendBufferSize;
            }

            var minRecvBufferSize = (maxBufferSize + IP_OVERHEAD) * NUM_BUFFERED_RECV_PACKETS;
            if (socket.ReceiveBufferSize < minRecvBufferSize)
            {
                socket.ReceiveBufferSize = minRecvBufferSize;
            }
        }
    }
}
