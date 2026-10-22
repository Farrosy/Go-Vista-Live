// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Core.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Threading;
    using Brisk.Models;
    using Brisk.Tests;
    using Brook;
    using Coherence.Common.Tests;
    using Coherence.Tests;
    using Common;
    using Connection;
    using Log;
    using Moq;
    using NUnit.Framework;
    using Stats;
    using Transport;

    public class ClientCoreShutdownNoticeTests : CoherenceTest
    {
        private static readonly Logger Logger = Log.GetLogger<ClientCoreShutdownNoticeTests>();

        private static readonly DateTimeOffset ShutdownTime =
            new DateTimeOffset(2026, 7, 29, 18, 0, 0, TimeSpan.Zero);

        private const string ShutdownReason = "Instance is retiring";

        private static readonly CustomPayload ShutdownPayload = new CustomPayload("maintenance");

        [Test]
        [Description("Verifies that a received shutdown notice raises OnShutdownNotice and is cached in ScheduledShutdown.")]
        public void ShutdownNotice_RaisesEventAndCachesTime()
        {
            // Arrange
            var clientCore = GetTestClientCore(out _, out var transportMock);

            ShutdownNoticeInfo? received = null;
            clientCore.OnShutdownNotice += notice => received = notice;

            clientCore.Connect(GetTestEndpoint(), ConnectionSettings.Default);
            SetUpTransportShutdownNotice(transportMock);

            // Act
            clientCore.UpdateReceiving();

            // Assert
            Assert.That(received?.ShutdownTimeUtc, Is.EqualTo(ShutdownTime));
            Assert.That(received?.Reason, Is.EqualTo(ShutdownReason));
            Assert.That(received?.Payload.AsString, Is.EqualTo(ShutdownPayload.AsString));
            Assert.That(clientCore.ScheduledShutdown?.ShutdownTimeUtc, Is.EqualTo(ShutdownTime));
            Assert.That(clientCore.ScheduledShutdown?.Reason, Is.EqualTo(ShutdownReason));
            Assert.That(clientCore.ScheduledShutdown?.Payload.AsString, Is.EqualTo(ShutdownPayload.AsString));
        }

        [Test]
        [Description("Verifies that ScheduledShutdown is null until a shutdown notice arrives.")]
        public void ScheduledShutdown_IsNullWithoutNotice()
        {
            // Arrange
            var clientCore = GetTestClientCore(out _, out _);

            // Assert
            Assert.That(clientCore.ScheduledShutdown, Is.Null);
        }

        [Test]
        [Description("Verifies that a listener subscribing after the notice arrived can still read the cached notice.")]
        public void ShutdownNotice_IsReadableAfterTheEventWasRaised()
        {
            // Arrange
            var clientCore = GetTestClientCore(out _, out var transportMock);

            clientCore.Connect(GetTestEndpoint(), ConnectionSettings.Default);
            SetUpTransportShutdownNotice(transportMock);
            clientCore.UpdateReceiving();

            ShutdownNoticeInfo? received = null;

            // Act
            clientCore.OnShutdownNotice += notice => received = notice;

            // Assert
            Assert.That(received, Is.Null, "A late listener is not invoked for an earlier notice.");
            Assert.That(clientCore.ScheduledShutdown?.ShutdownTimeUtc, Is.EqualTo(ShutdownTime));
            Assert.That(clientCore.ScheduledShutdown?.Payload.AsString, Is.EqualTo(ShutdownPayload.AsString));
        }

        [Test]
        [Description("Verifies that disconnecting clears the cached shutdown notice.")]
        public void ShutdownNotice_IsClearedOnDisconnect()
        {
            // Arrange
            var clientCore = GetTestClientCore(out _, out var transportMock);

            clientCore.Connect(GetTestEndpoint(), ConnectionSettings.Default);
            SetUpTransportShutdownNotice(transportMock);
            clientCore.UpdateReceiving();
            Assert.That(clientCore.ScheduledShutdown?.ShutdownTimeUtc, Is.EqualTo(ShutdownTime));

            // Act
            clientCore.Disconnect();

            // Assert
            Assert.That(clientCore.ScheduledShutdown, Is.Null);
        }

        private static void SetUpTransportShutdownNotice(Mock<ITransport> transportMock)
        {
            transportMock
                .Setup(m => m.Receive(It.IsAny<List<(IInOctetStream, IPEndPoint)>>()))
                .Callback((List<(IInOctetStream, IPEndPoint)> buffer) =>
                {
                    buffer.Add((BriskTestUtils.CreateIncomingOobPacket(new ShutdownNotice(ShutdownTime, ShutdownReason, ShutdownPayload)), null));
                });
        }

        private static EndpointData GetTestEndpoint()
        {
            return new EndpointData()
            {
                host = "127.0.0.1",
                port = 1000,
                schemaId = "testSchemaId",
                authToken = "testAuthToken"
            };
        }

        private static ClientCore GetTestClientCore(out BriskServicesMocks briskServices, out Mock<ITransport> transportMock)
        {
            transportMock = new Mock<ITransport>();
            transportMock.SetupGet(m => m.CanSend).Returns(true);
            BriskTestUtils.SetUpTransportOpenAndClose(transportMock);

            briskServices = new BriskServicesMocks();

            var transportFactoryMock = new Mock<ITransportFactory>();
            transportFactoryMock.Setup(m => m.Create(It.IsAny<ushort>(), It.IsAny<Stats>(), It.IsAny<Logger>()))
                .Returns(transportMock.Object);

            var domainNameResolverMock = new Mock<IDomainNameResolver>();
            domainNameResolverMock
                .Setup(r => r.Resolve(It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<Logger>(),
                    It.IsAny<Action<IPAddress>>(), It.IsAny<Action>()))
                .Callback((string hostname, CancellationToken cancellationToken, Logger logger,
                    Action<IPAddress> onSuccess, Action onFailure) => onSuccess(IPAddress.Parse(hostname)));

            return new ClientCore(TestSchema.Definition, Logger,
                null, briskServices.GetServices(), transportFactoryMock.Object, domainNameResolverMock.Object);
        }
    }
}
