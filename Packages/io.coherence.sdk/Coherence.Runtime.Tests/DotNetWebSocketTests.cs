// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Runtime.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Net.WebSockets;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Coherence.Tests;
    using Moq;
    using NUnit.Framework;

    /// <summary>
    /// Edit Mode unit tests for the <see cref="DotNetWebSocket"/>.
    /// </summary>
    public class DotNetWebSocketTests : CoherenceTest
    {
        private const int MaxFrameLength = DotNetWebSocket.MaxFrameLength;

        private Mock<WebSocket> mockWs;
        private DotNetWebSocket socket;
        private List<(byte[] data, WebSocketMessageType messageType, bool endOfMessage)> sendCalls;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();

            sendCalls = new();
            mockWs = new(MockBehavior.Loose);
            mockWs.Setup(ws => ws.State).Returns(WebSocketState.Open);

            mockWs.Setup(ws => ws.SendAsync
            (
                It.IsAny<ArraySegment<byte>>(),
                It.IsAny<WebSocketMessageType>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>())
            )
            .Returns<ArraySegment<byte>, WebSocketMessageType, bool, CancellationToken>
            (
                (segment, type, eom, _) =>
                {
                    sendCalls.Add((segment.ToArray(), type, eom));
                    return Task.CompletedTask;
                }
            );

            mockWs.Setup(ws => ws.CloseOutputAsync
            (
                It.IsAny<WebSocketCloseStatus>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>())
            )
            .Returns(Task.CompletedTask);

            socket = new(mockWs.Object);
        }

        [TearDown]
        public override void TearDown()
        {
            socket?.CloseAsync().GetAwaiter().GetResult();
            base.TearDown();
        }

        [Test]
        public async Task SendAsync_SmallMessage_IsSentInSingleFrame()
        {
            const string message = "123";

            await socket.SendAsync(1, "requestId", message);

            Assert.That(sendCalls, Has.Count.EqualTo(1));
            var (data, messageType, endOfMessage) = sendCalls[0];
            Assert.That(Encoding.UTF8.GetString(data), Is.EqualTo(message));
            Assert.That(messageType, Is.EqualTo(WebSocketMessageType.Text));
            Assert.That(endOfMessage, Is.True);
        }

        [Test]
        public async Task SendAsync_SmallMessage_DataMatchesOriginalBytes()
        {
            const string message = "123 🎮";
            var expectedBytes = Encoding.UTF8.GetBytes(message);

            await socket.SendAsync(1, "requestId", message);

            Assert.That(sendCalls, Has.Count.EqualTo(1));
            Assert.That(sendCalls[0].data, Is.EqualTo(expectedBytes));
        }

        [Test]
        public async Task SendAsync_MessageExactlyOneFrameLength_IsSentAsSingleFrame()
        {
            var message = new string('A', MaxFrameLength); // Exactly MaxFrameLength UTF-8 bytes.

            await socket.SendAsync(1, "requestId", message);

            Assert.That(sendCalls, Has.Count.EqualTo(1));
            Assert.That(sendCalls[0].data.Length, Is.EqualTo(MaxFrameLength));
            Assert.That(sendCalls[0].endOfMessage, Is.True);
        }

        [Test]
        public async Task SendAsync_MessageLargerThanOneFrame_IsSentInMultipleChunks()
        {
            var message = new string('A', MaxFrameLength * 2 + 1000); // Two full frames + a partial frame.
            var totalBytes = Encoding.UTF8.GetByteCount(message);

            await socket.SendAsync(1, "requestId", message);

            Assert.That(sendCalls, Has.Count.EqualTo(3));

            Assert.That(sendCalls[0].data.Length, Is.EqualTo(MaxFrameLength));
            Assert.That(sendCalls[0].endOfMessage, Is.False);

            Assert.That(sendCalls[1].data.Length, Is.EqualTo(MaxFrameLength));
            Assert.That(sendCalls[1].endOfMessage, Is.False);

            var expectedLastChunk = totalBytes - MaxFrameLength * 2;
            Assert.That(sendCalls[2].data.Length, Is.EqualTo(expectedLastChunk));
            Assert.That(sendCalls[2].endOfMessage, Is.True);
        }

        [Test]
        public async Task SendAsync_LargeMessage_AllChunksReconstructToOriginalBytes()
        {
            var srcBytes = new byte[MaxFrameLength + 5000];
            for (var i = 0; i < srcBytes.Length; i++)
            {
                srcBytes[i] = (byte)(i % 128); // Use a mix of byte values to catch off-by-one offset errors.
            }
            var message = Encoding.UTF8.GetString(srcBytes);
            var expectedBytes = Encoding.UTF8.GetBytes(message);

            await socket.SendAsync(1, "requestId", message);

            // Reassemble all chunks and compare byte-for-byte.
            var reconstructed = new List<byte>();
            foreach (var (data, _, _) in sendCalls)
            {
                reconstructed.AddRange(data);
            }

            Assert.That(reconstructed.ToArray(), Is.EqualTo(expectedBytes));
        }

        [Test]
        public async Task SendAsync_OnlyLastFrame_HasEndOfMessageTrue()
        {
            var message = new string('B', MaxFrameLength * 3 + 500); // Three full frames + a partial frame => 4 chunks total.

            await socket.SendAsync(1, "requestId", message);

            Assert.That(sendCalls, Has.Count.EqualTo(4));

            for (var i = 0; i < sendCalls.Count - 1; i++)
            {
                Assert.That(sendCalls[i].endOfMessage, Is.False, $"Frame {i} (of {sendCalls.Count}) must not carry endOfMessage=true.");
            }

            Assert.That(sendCalls[^1].endOfMessage, Is.True, "The last frame must carry endOfMessage=true.");
        }

        [Test]
        public async Task SendAsync_AllFrames_UseWebSocketTextMessageType()
        {
            // Send more than one frame to cover every chunk, not just the last.
            var message = new string('C', MaxFrameLength + 100);

            await socket.SendAsync(1, "requestId", message);

            Assert.That(sendCalls, Has.Count.GreaterThan(1));

            foreach (var (_, messageType, _) in sendCalls)
            {
                Assert.That(messageType, Is.EqualTo(WebSocketMessageType.Text));
            }
        }

        [Test]
        public async Task SendAsync_EmptyMessage_SendsSingleEmptyFrame()
        {
            await socket.SendAsync(1, "requestId", "");

            // The implementation sends exactly one frame even for empty payloads so that the
            // remote peer receives a well-formed WebSocket message.
            Assert.That(sendCalls, Has.Count.EqualTo(1));
            Assert.That(sendCalls[0].data, Is.Empty);
            Assert.That(sendCalls[0].messageType, Is.EqualTo(WebSocketMessageType.Text));
            Assert.That(sendCalls[0].endOfMessage, Is.True);
        }

        [Test]
        public async Task SendAsync_MultipleSends_AreDeliveredInOrder()
        {
            var task1 = socket.SendAsync(1, "requestId1", "First");
            var task2 = socket.SendAsync(2, "requestId2", "Second");
            var task3 = socket.SendAsync(3, "requestId3", "Third");

            await task3;

            Assert.That(sendCalls, Has.Count.EqualTo(3));
            Assert.That(Encoding.UTF8.GetString(sendCalls[0].data), Is.EqualTo("First"));
            Assert.That(Encoding.UTF8.GetString(sendCalls[1].data), Is.EqualTo("Second"));
            Assert.That(Encoding.UTF8.GetString(sendCalls[2].data), Is.EqualTo("Third"));

            await task1;
            await task2;
        }

        [Test]
        public async Task SendAsync_MultipleLargeMessages_AreDeliveredInOrder()
        {
            // Each message is larger than one frame so the interleaving constraint is visible.
            var message1 = new string('1', MaxFrameLength + 100);
            var message2 = new string('2', MaxFrameLength + 200);

            var task1 = socket.SendAsync(1, "requestId1", message1);
            var task2 = socket.SendAsync(2, "requestId2", message2);

            await task2;

            // Verify all frames from first message arrive before any frame from second message.
            var message1Bytes = Encoding.UTF8.GetBytes(message1);
            var message2Bytes = Encoding.UTF8.GetBytes(message2);

            var reconstructed1 = new List<byte>();
            var reconstructed2 = new List<byte>();
            var seenMessage2 = false;

            foreach (var (data, _, _) in sendCalls)
            {
                if (!seenMessage2 && reconstructed1.Count < message1Bytes.Length)
                {
                    reconstructed1.AddRange(data);
                    if (reconstructed1.Count == message1Bytes.Length)
                    {
                        seenMessage2 = true;
                    }
                }
                else
                {
                    reconstructed2.AddRange(data);
                }
            }

            Assert.That(reconstructed1.ToArray(), Is.EqualTo(message1Bytes));
            Assert.That(reconstructed2.ToArray(), Is.EqualTo(message2Bytes));

            await task1;
        }
    }
}

