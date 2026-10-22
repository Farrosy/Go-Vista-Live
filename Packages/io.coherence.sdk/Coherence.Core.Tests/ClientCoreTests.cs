// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Core.Tests
{
    using Brisk;
    using Brisk.Models;
    using Brisk.Tests;
    using Brook;
    using Common;
    using Connection;
    using Entities;
    using Log;
    using Moq;
    using NUnit.Framework;
    using ProtocolDef;
    using Stats;
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Threading;
    using Transport;
    using Coherence.Tests;
    using Coherence.Common.Tests;

    public class ClientCoreTests : CoherenceTest
    {
        [Test]
        public void Timeout_DoesntTriggerIfKeepaliveWasReceivedJustBeforeUpdate()
        {
            // Arrange
            ClientCore clientCore = GetTestClientCore(out BriskServicesMocks briskServices, out Mock<ITransport> transportMock);
            EndpointData endpointData = GetTestEndpoint();

            clientCore.Connect(endpointData, ConnectionSettings.Default);
            BriskTestUtils.SetUpTransportConnectResponse(transportMock);
            RunUnityUpdateForClientCore(clientCore);
            Assert.That(clientCore.ConnectionState, Is.EqualTo(ConnectionState.Connected));

            briskServices.ConnectionTimer.Setup(m => m.Elapsed)
                .Returns(ConnectionSettings.Default.DisconnectTimeout);
            BriskTestUtils.SetUpTransportKeepAliveResponse(transportMock);

            // Act
            RunUnityUpdateForClientCore(clientCore);

            // Assert
            Assert.That(clientCore.ConnectionState, Is.EqualTo(ConnectionState.Connected));
        }

        [Test]
        [Description("Verify that a client resolving its host reports that it is opening, not disconnected.")]
        public void ConnectionState_IsOpening_WhileResolvingHost()
        {
            var resolver = new DeferredDomainNameResolver();
            using var clientCore = GetTestClientCore(out _, out _, domainNameResolver: resolver);

            clientCore.Connect(GetTestEndpoint(), ConnectionSettings.Default);

            Assert.That(clientCore.ConnectionState, Is.EqualTo(ConnectionState.Opening));
        }

        [Test]
        [Description("Verify that a connect made while the host is resolving is refused, not started a second time.")]
        public void Connect_IsRefused_WhileResolvingHost()
        {
            var resolver = new DeferredDomainNameResolver();
            var transportFactory = new Mock<ITransportFactory>();
            using var clientCore = GetTestClientCore(out _, out var transportMock, domainNameResolver: resolver,
                transportFactory: transportFactory);
            var endpoint = GetTestEndpoint();

            clientCore.Connect(endpoint, ConnectionSettings.Default);
            clientCore.Connect(endpoint, ConnectionSettings.Default);
            resolver.Complete(IPAddress.Parse(endpoint.host));

            Assert.That(logger.GetCountForWarningID(Warning.CoreClientConnectFailed), Is.EqualTo(1));
            Assert.That(resolver.ResolveCount, Is.EqualTo(1));
            transportFactory.Verify(m => m.Create(It.IsAny<ushort>(), It.IsAny<Stats>(), It.IsAny<Logger>()),
                Times.Once);
            Assert.That(clientCore.ConnectionState, Is.EqualTo(ConnectionState.Connecting));
        }

        [Test]
        [Description("Verify that a client whose host fails to resolve reports that it is disconnected.")]
        public void ConnectionState_IsDisconnected_WhenResolvingHostFails()
        {
            var resolver = new DeferredDomainNameResolver();
            using var clientCore = GetTestClientCore(out _, out _, domainNameResolver: resolver);

            clientCore.Connect(GetTestEndpoint(), ConnectionSettings.Default);
            resolver.Fail();

            Assert.That(clientCore.ConnectionState, Is.EqualTo(ConnectionState.Disconnected));
        }

        [Test]
        [Description("Verify that a client disconnected while resolving its host reports that it is disconnected.")]
        public void ConnectionState_IsDisconnected_WhenDisconnectedWhileResolvingHost()
        {
            var resolver = new DeferredDomainNameResolver();
            using var clientCore = GetTestClientCore(out _, out _, domainNameResolver: resolver);

            clientCore.Connect(GetTestEndpoint(), ConnectionSettings.Default);
            clientCore.Disconnect();

            Assert.That(resolver.WasCanceled, Is.True);
            Assert.That(clientCore.ConnectionState, Is.EqualTo(ConnectionState.Disconnected));
        }

        private static void RunUnityUpdateForClientCore(ClientCore clientCore)
        {
            clientCore.UpdateReceiving();
            clientCore.UpdateSending();
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

        private ClientCore GetTestClientCore(out BriskServicesMocks briskServices, out Mock<ITransport> transportMock,
            IDomainNameResolver domainNameResolver = null, Mock<ITransportFactory> transportFactory = null)
        {
            transportMock = new Mock<ITransport>();
            transportMock.SetupGet(m => m.CanSend).Returns(true);
            BriskTestUtils.SetUpTransportOpenAndClose(transportMock);

            briskServices = new BriskServicesMocks();

            var transport = transportMock.Object;
            transportFactory ??= new Mock<ITransportFactory>();
            transportFactory.Setup(m => m.Create(It.IsAny<ushort>(), It.IsAny<Stats>(), It.IsAny<Logger>()))
                .Returns(transport);

            var domainNameResolverMock = new Mock<IDomainNameResolver>();
            domainNameResolverMock.Setup(r => r.Resolve(It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<Logger>(), It.IsAny<Action<IPAddress>>(), It.IsAny<Action>())).
                Callback((string hostname, CancellationToken cancellationToken, Logger logger, Action<IPAddress> onSuccess, Action onFailure) => onSuccess(IPAddress.Parse(hostname)));

            return new ClientCore(TestSchema.Definition, logger,
                null, briskServices.GetServices(), transportFactory.Object,
                domainNameResolver ?? domainNameResolverMock.Object);
        }

        /// <summary>Resolves a host name only when the test says so, the way a real DNS lookup does.</summary>
        private sealed class DeferredDomainNameResolver : IDomainNameResolver
        {
            public int ResolveCount { get; private set; }
            public bool WasCanceled => cancellationToken.IsCancellationRequested;

            private CancellationToken cancellationToken;
            private Action<IPAddress> onSuccess;
            private Action onFailure;

            public void Resolve(string hostname, CancellationToken cancellationToken, Logger logger,
                Action<IPAddress> onSuccess, Action onFailure)
            {
                ResolveCount++;
                this.cancellationToken = cancellationToken;
                this.onSuccess = onSuccess;
                this.onFailure = onFailure;
            }

            public void Complete(IPAddress address) => onSuccess(address);
            public void Fail() => onFailure();
        }
    }
}
