// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Brisk.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using Brook;
    using Brook.Octet;
    using Coherence.Tests;
    using Common;
    using Connection;
    using Models;
    using Moq;
    using NUnit.Framework;
    using Serializers;
    using Tend;
    using Transport;

    public class ShutdownNoticeTests : CoherenceTest
    {
        private static readonly DateTimeOffset ShutdownTime =
            new DateTimeOffset(2026, 7, 29, 18, 0, 0, TimeSpan.Zero);

        [Test]
        [Description("Verifies that a ShutdownNotice survives a serialize and deserialize round trip.")]
        public void ShutdownNotice_RoundTripsThroughSerializer()
        {
            // Arrange
            var payload = new CustomPayload(new byte[] { 0x01, 0x02, 0x03 });
            var outStream = new OutOctetStream(1200);

            // Act
            BriskSerializer.SerializeOobMessage(outStream, new ShutdownNotice(ShutdownTime, "Instance is retiring", payload), ProtocolDef.Version.CurrentVersion);

            var inStream = new InOctetStream(outStream.Close().ToArray());
            var message = BriskSerializer.DeserializeOobMessage(inStream, ProtocolDef.Version.CurrentVersion);

            // Assert
            Assert.That(message, Is.TypeOf<ShutdownNotice>());
            Assert.That(message.Type, Is.EqualTo(OobMessageType.ShutdownNotice));
            Assert.That(message.IsReliable, Is.True);

            var notice = (ShutdownNotice)message;
            Assert.That(notice.ShutdownTimeUtc, Is.EqualTo(ShutdownTime));
            Assert.That(notice.Reason, Is.EqualTo("Instance is retiring"));
            Assert.That(notice.Payload.Bytes, Is.EqualTo(payload.Bytes));
        }

        [Test]
        [Description("Verifies that a ShutdownNotice with an empty reason and payload round trips with both empty.")]
        public void ShutdownNotice_RoundTripsEmptyReasonAndPayload()
        {
            // Arrange
            var outStream = new OutOctetStream(64);

            // Act
            BriskSerializer.SerializeOobMessage(outStream, new ShutdownNotice(ShutdownTime, "", CustomPayload.Empty), ProtocolDef.Version.CurrentVersion);

            var inStream = new InOctetStream(outStream.Close().ToArray());
            var message = BriskSerializer.DeserializeOobMessage(inStream, ProtocolDef.Version.CurrentVersion);

            // Assert
            var notice = (ShutdownNotice)message;
            Assert.That(notice.ShutdownTimeUtc, Is.EqualTo(ShutdownTime));
            Assert.That(notice.Reason, Is.Empty);
            Assert.That(notice.Payload.IsEmpty, Is.True);
        }

        [Test]
        [Description("Verifies that a ShutdownNotice built from a non-UTC time is stored and sent as UTC.")]
        public void ShutdownNotice_ConvertsTimeToUtc()
        {
            // Arrange
            var localTime = new DateTimeOffset(2026, 7, 29, 20, 0, 0, TimeSpan.FromHours(2));

            // Act
            var notice = new ShutdownNotice(localTime, "", CustomPayload.Empty);

            // Assert
            Assert.That(notice.ShutdownTimeUtc.Offset, Is.EqualTo(TimeSpan.Zero));
            Assert.That(notice.ShutdownTimeUtc, Is.EqualTo(ShutdownTime));
        }

        [Test]
        [Description("Verifies that receiving a ShutdownNotice raises OnShutdownNotice, even before the handshake completes.")]
        public void ShutdownNotice_RaisesOnShutdownNoticeWhileConnecting()
        {
            // Arrange
            var brisk = BriskTestUtils.GetTestBrisk(out _, out var transportMock);

            ShutdownNoticeInfo? received = null;
            brisk.OnShutdownNotice += notice => received = notice;

            transportMock.Setup(m => m.Receive(It.IsAny<List<(IInOctetStream, IPEndPoint)>>()))
                .Callback((List<(IInOctetStream, IPEndPoint)> buffer) =>
                {
                    buffer.Add((BriskTestUtils.CreateIncomingOobPacket(new ShutdownNotice(ShutdownTime, "Instance is retiring", new CustomPayload("maintenance"))), null));
                });

            brisk.Connect(new EndpointData(), ConnectionType.Client);

            // Act
            brisk.Receive(new Queue<InPacket>());

            // Assert
            Assert.That(brisk.State, Is.EqualTo(ConnectionState.Connecting));
            Assert.That(received?.ShutdownTimeUtc, Is.EqualTo(ShutdownTime));
            Assert.That(received?.Reason, Is.EqualTo("Instance is retiring"));
            Assert.That(received?.Payload.AsString, Is.EqualTo("maintenance"));
        }

        [Test]
        [Description("Verifies that receiving a ShutdownNotice does not disconnect or error the connection.")]
        public void ShutdownNotice_DoesNotAffectConnection()
        {
            // Arrange
            var brisk = BriskTestUtils.GetTestBrisk(out _, out var transportMock);

            ConnectionException connectionException = null;
            brisk.OnError += exception => connectionException = exception;

            ConnectionCloseReason? closeReason = null;
            brisk.OnDisconnect += reason => closeReason = reason;

            transportMock.Setup(m => m.Receive(It.IsAny<List<(IInOctetStream, IPEndPoint)>>()))
                .Callback((List<(IInOctetStream, IPEndPoint)> buffer) =>
                {
                    buffer.Add((BriskTestUtils.CreateIncomingOobPacket(new ShutdownNotice(ShutdownTime, "", CustomPayload.Empty)), null));
                });

            brisk.Connect(new EndpointData(), ConnectionType.Client);

            // Act
            brisk.Receive(new Queue<InPacket>());

            // Assert
            Assert.That(connectionException, Is.Null);
            Assert.That(closeReason, Is.Null);
        }
    }
}
