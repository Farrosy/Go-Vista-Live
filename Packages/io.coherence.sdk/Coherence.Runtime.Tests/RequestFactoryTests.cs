// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Runtime.Tests
{
    using System.Collections.Generic;
    using Coherence.Cloud;
    using Coherence.Tests;
    using Common;
    using Moq;
    using NUnit.Framework;

    public class RequestFactoryTests : CoherenceTest
    {
        private Mock<IWebSocketManager> webSocketManagerMock;
        private Mock<IRuntimeSettings> runtimeSettingsMock;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();
            webSocketManagerMock = new Mock<IWebSocketManager>();
            runtimeSettingsMock = new Mock<IRuntimeSettings>();
        }

        // IsReady

        [Test]
        [Description("IsReady should return true when WebSocket is disabled, regardless of connection state.")]
        public void IsReady_Should_Be_True_When_WebSocketIsDisabled()
        {
            using var factory = new RequestFactory(runtimeSettingsMock.Object, useWebSocket: false);
            Assert.IsTrue(factory.IsReady);
        }

        [Test]
        [Description("IsReady should return true when the WebSocket is enabled and the connection is up.")]
        public void IsReady_Should_Be_True_When_WebSocketIsEnabledAndConnected()
        {
            webSocketManagerMock.SetupGet(ws => ws.Enabled).Returns(true);
            webSocketManagerMock.Setup(ws => ws.IsConnected()).Returns(true);

            using var factory = new RequestFactory(runtimeSettingsMock.Object, webSocketManagerMock.Object);

            Assert.IsTrue(factory.IsReady);
        }

        [Test]
        [Description(
            "IsReady should return false when the WebSocket is enabled but the connection has not been established yet.")]
        public void IsReady_Should_Be_False_When_WebSocketIsEnabledButNotConnected()
        {
            webSocketManagerMock.SetupGet(ws => ws.Enabled).Returns(true);
            webSocketManagerMock.Setup(ws => ws.IsConnected()).Returns(false);

            using var factory = new RequestFactory(runtimeSettingsMock.Object, webSocketManagerMock.Object);

            Assert.IsFalse(factory.IsReady);
        }

        [Test]
        [Description("IsReady should return false when the WebSocket manager reports itself as not enabled.")]
        public void IsReady_Should_Be_False_When_WebSocketIsNotEnabled()
        {
            webSocketManagerMock.SetupGet(ws => ws.Enabled).Returns(false);

            using var factory = new RequestFactory(runtimeSettingsMock.Object, webSocketManagerMock.Object);

            Assert.IsFalse(factory.IsReady);
        }

        // CanConnectWebSocket

        [Test]
        [Description("CanConnectWebSocket should return true when a non-empty runtime key is configured.")]
        public void CanConnectWebSocket_Should_Be_True_When_RuntimeKeyIsSet()
        {
            runtimeSettingsMock.SetupGet(rs => rs.RuntimeKey).Returns("runtime-key");

            using var factory = new RequestFactory(runtimeSettingsMock.Object, webSocketManagerMock.Object);

            Assert.IsTrue(factory.CanConnectWebSocket);
        }

        [Test]
        [Description(
            "CanConnectWebSocket should return false when the runtime key is empty, as there is nothing to connect with.")]
        public void CanConnectWebSocket_Should_Be_False_When_RuntimeKeyIsEmpty()
        {
            runtimeSettingsMock.SetupGet(rs => rs.RuntimeKey).Returns(string.Empty);

            using var factory = new RequestFactory(runtimeSettingsMock.Object, webSocketManagerMock.Object);

            Assert.IsFalse(factory.CanConnectWebSocket);
        }

        // Event propagation

        [Test]
        [Description(
            "OnWebSocketConnect should fire when the underlying WebSocket manager raises its OnConnect event.")]
        public void OnWebSocketConnect_Should_Be_Fired_When_WebSocketConnects()
        {
            using var factory = new RequestFactory(runtimeSettingsMock.Object, webSocketManagerMock.Object);
            var fired = false;
            factory.OnWebSocketConnect += () => fired = true;

            webSocketManagerMock.Raise(ws => ws.OnConnect += null);

            Assert.IsTrue(fired);
        }

        [Test]
        [Description(
            "OnWebSocketDisconnect should fire when the underlying WebSocket manager raises its OnDisconnect event.")]
        public void OnWebSocketDisconnect_Should_Be_Fired_When_WebSocketDisconnects()
        {
            using var factory = new RequestFactory(runtimeSettingsMock.Object, webSocketManagerMock.Object);
            var fired = false;
            factory.OnWebSocketDisconnect += () => fired = true;

            webSocketManagerMock.Raise(ws => ws.OnDisconnect += null);

            Assert.IsTrue(fired);
        }

        [Test]
        [Description(
            "OnWebSocketConnectionError should fire when the underlying WebSocket manager raises its OnWebSocketFail event.")]
        public void OnWebSocketConnectionError_Should_Be_Fired_When_WebSocketFails()
        {
            using var factory = new RequestFactory(runtimeSettingsMock.Object, webSocketManagerMock.Object);
            var fired = false;
            factory.OnWebSocketConnectionError += () => fired = true;

            webSocketManagerMock.Raise(ws => ws.OnWebSocketFail += null);

            Assert.IsTrue(fired);
        }

        [Test]
        [Description(
            "OnWebSocketParametersNotValid should fire with the original message when the WebSocket manager rejects its connection parameters.")]
        public void OnWebSocketParametersNotValid_Should_Be_Fired_When_WebSocketParametersAreInvalid()
        {
            using var factory = new RequestFactory(runtimeSettingsMock.Object, webSocketManagerMock.Object);
            string receivedMessage = null;
            factory.OnWebSocketParametersNotValid += msg => receivedMessage = msg;

            webSocketManagerMock.Raise(ws => ws.OnWebSocketParametersNotValid += null, "invalid params");

            Assert.AreEqual("invalid params", receivedMessage);
        }

        // Request routing

        [Test]
        [Description("SendRequest should delegate to the WebSocket manager when the WebSocket is enabled.")]
        public void SendRequest_Should_Delegate_To_WebSocket_Manager_When_WebSocket_Enabled()
        {
            webSocketManagerMock.SetupGet(ws => ws.Enabled).Returns(true);

            using var factory = new RequestFactory(runtimeSettingsMock.Object, webSocketManagerMock.Object);
            factory.SendRequest("/rooms", "GET", null, null, "Test", null, _ => { });

            webSocketManagerMock.Verify(ws => ws.SendRequest(
                    "/rooms",
                    "GET",
                    null,
                    It.IsAny<Dictionary<string, string>>(),
                    null,
                    It.IsAny<OnRequest>()),
                Times.Once);
        }

        [Test]
        [Description(
            "SendRequestAsync should delegate to the WebSocket manager and return the response body when the WebSocket is enabled.")]
        public async System.Threading.Tasks.Task
            SendRequestAsync_Should_Delegate_To_WebSocket_Manager_And_Return_Body_When_WebSocket_Enabled()
        {
            webSocketManagerMock.SetupGet(ws => ws.Enabled).Returns(true);
            webSocketManagerMock.Setup(ws => ws.SendRequest(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<Dictionary<string, string>>(), It.IsAny<string>(), It.IsAny<OnRequest>()))
                .Callback<string, string, string, Dictionary<string, string>, string, OnRequest>(
                    (_, _, _, _, _, cb) => cb.Invoke(200, "response-body", "req-1"));

            using var factory = new RequestFactory(runtimeSettingsMock.Object, webSocketManagerMock.Object);
            var result = await factory.SendRequestAsync("/rooms", "GET", null, null, "Test", null);

            Assert.AreEqual("response-body", result);
            webSocketManagerMock.Verify(ws => ws.SendRequest(
                    "/rooms",
                    "GET",
                    null,
                    It.IsAny<Dictionary<string, string>>(),
                    null,
                    It.IsAny<OnRequest>()),
                Times.Once);
        }
    }
}
