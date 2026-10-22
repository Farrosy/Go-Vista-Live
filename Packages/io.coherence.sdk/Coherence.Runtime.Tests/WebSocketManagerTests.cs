// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Runtime.Tests
{
    using System;
    using System.Threading.Tasks;
    using Coherence.Tests;
    using Coherence.Utils;
    using Common;
    using Connection;
    using Log;
    using Moq;
    using NUnit.Framework;

    /// <summary>
    /// Edit mode unit tests for <see cref="WebSocketManager"/>.
    /// </summary>
    public class WebSocketManagerTests : CoherenceTest
    {
        private Mock<IRuntimeSettings> runtimeSettingsMock;
        private RequestIdSource idSource;
        private WebSocketManager webSocketManager;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();

            runtimeSettingsMock = new();
            runtimeSettingsMock.Setup(x => x.WebSocketEndpoint).Returns("WebSocketEndpoint");
            runtimeSettingsMock.Setup(x => x.RuntimeKey).Returns("RuntimeKey");
            runtimeSettingsMock.Setup(x => x.SdkVersion).Returns("SdkVersion");
            idSource = new();
            webSocketManager = new(runtimeSettingsMock.Object, idSource, logger);
        }

        [TearDown]
        public override void TearDown()
        {
            webSocketManager?.Dispose();
            base.TearDown();
        }

        [Test]
        public void AddPushCallback_Returns_False_If_Callback_For_RequestId_Already_Registered()
        {
            webSocketManager.AddPushCallback("/test/path", null);

            var wasAdded = webSocketManager.AddPushCallback("/test/path", null);

            Assert.That(wasAdded, Is.False, "Should not allow duplicate callback registration.");
        }

        [Test]
        public void WebSocketManager_Constructor_Registers_Callback_For_ConnectionReconnectPath()
        {
            var wasCallbackMissing = webSocketManager.AddPushCallback(WebSocketManager.ConnectionReconnectPath, null);

            Assert.That(wasCallbackMissing, Is.False, $"WebSocketManager constructor should already have registered callback for \"{WebSocketManager.ConnectionReconnectPath}\"");
        }

        [TestCase("/lobbies/some/topic/added/later", TestName = "topic added after this SDK shipped")]
        public void HandleResponse_Does_Not_Report_Missing_Callback_For_An_Unsubscribed_Push_Topic(string topic)
        {
            var meta = CoherenceJson.SerializeObject(new ResponseMeta { RequestId = topic });

            webSocketManager.HandleResponse(meta + "\nBody");

            Assert.That(logger.GetLogLevelCount(LogLevel.Error), Is.Zero,
                "A push for a topic this SDK does not know about should not be reported as an error.");
        }

        [Test]
        public void HandleResponse_Reports_Missing_Callback_For_A_Response_To_An_Unknown_Request()
        {
            var meta = CoherenceJson.SerializeObject(new ResponseMeta { RequestId = "deadbeef:1" });

            webSocketManager.HandleResponse(meta + "\nBody");

            Assert.That(logger.GetCountForErrorID(Error.RuntimeWebsocketMissingResponseCallback), Is.EqualTo(1));
        }

        [Test]
        public void HandleResponse_Advances_ResumeId_When_Frame_Carries_One()
        {
            webSocketManager.HandleResponse("{\"ts\":1,\"resume_id\":\"42\"}\nBody");

            Assert.That(webSocketManager.ResumeId, Is.EqualTo("42"));
        }

        // The backend omits `resume_id` on frames that must not move the cursor. Clearing it there
        // would make the next reconnect skip the replay of the messages missed while disconnected.
        [TestCase("{\"ts\":2}", TestName = "resume_id absent")]
        [TestCase("{\"ts\":2,\"resume_id\":null}", TestName = "resume_id null")]
        [TestCase("{\"ts\":2,\"resume_id\":\"\"}", TestName = "resume_id empty")]
        public void HandleResponse_Keeps_ResumeId_When_Frame_Carries_None(string meta)
        {
            webSocketManager.HandleResponse("{\"ts\":1,\"resume_id\":\"42\"}\nBody");

            webSocketManager.HandleResponse(meta + "\nBody");

            Assert.That(webSocketManager.ResumeId, Is.EqualTo("42"), "Cursor should only be advanced by frames that carry a resume_id.");
        }

        [TestCase("\"resume_id\":\"42\",", "42", TestName = "push with resume_id")]
        [TestCase("", null, TestName = "push without resume_id")]
        [Description("A push's resume_id is exposed while its callback runs, and cleared afterwards.")]
        public void HandleResponse_ExposesPushResumeId_OnlyWhilePushCallbackRuns(string resumeIdField,
            string expected)
        {
            IWebSocketManager manager = webSocketManager;
            string seen = "unset";
            webSocketManager.AddPushCallback("/test/push", (_, _, _) => seen = manager.PushResumeId);
            webSocketManager.HandleResponse("{\"ts\":1,\"resume_id\":\"41\"}\nBody");

            webSocketManager.HandleResponse("{\"ts\":2," + resumeIdField + "\"requestId\":\"/test/push\"}\nBody");

            Assert.That(seen, Is.EqualTo(expected));
            Assert.That(manager.PushResumeId, Is.Null);
        }

        [Test]
        public async Task HandleResponse_Keeps_ResumeId_When_It_Receives_Reconnect_Push_Message()
        {
            webSocketManager.Connect();
            webSocketManager.HandleResponse("{\"ts\":1,\"resume_id\":\"42\"}\nBody");
            var reconnectMeta = CoherenceJson.SerializeObject(new ResponseMeta { RequestId = WebSocketManager.ConnectionReconnectPath });

            webSocketManager.HandleResponse(reconnectMeta + "\nBody");

            Assert.That(webSocketManager.ResumeId, Is.EqualTo("42"), "Reconnect frames carry no resume_id, and must not wipe the replay cursor of the reconnect that follows.");

            // Let the reconnect that the push message triggered complete before tearing down.
            await WaitForReconnect();
        }

        [Test]
        public async Task WebSocketManager_Disconnects_And_Reconnects_When_It_Receives_Reconnect_Push_Message()
        {
            var responseMeta = CoherenceJson.SerializeObject(new ResponseMeta { RequestId = WebSocketManager.ConnectionReconnectPath });
            var text = responseMeta + "\nBody";
            webSocketManager.Connect();
            Assert.That(webSocketManager.Enabled, Is.True);

            webSocketManager.HandleResponse(text);

            // WebSocket should be disabled immediately after reconnect is requested.
            Assert.That(webSocketManager.Enabled, Is.False);

            await WaitForReconnect();

            // WebSocket should be re-enabled after MaxWaitBeforeReconnectMilliSeconds milliseconds (at the latest).
            Assert.That(webSocketManager.Enabled, Is.True);
        }

        [Test]
        public async Task OnReconnectRequested_Reenables_Even_When_Websocket_Stays_Non_Null_Across_The_Delay()
        {
            webSocketManager.Connect();
            var mockSocket = new Mock<IWebSocket>();
            mockSocket.Setup(s => s.CloseAsync()).Returns(Task.CompletedTask);
            webSocketManager.SetWebSocketForTests(mockSocket.Object);
            var responseMeta = CoherenceJson.SerializeObject(new ResponseMeta { RequestId = WebSocketManager.ConnectionReconnectPath });

            webSocketManager.HandleResponse(responseMeta + "\nBody");
            await WaitForReconnect();

            Assert.That(webSocketManager.Enabled, Is.True, "A reconnect push should always re-enable the manager, regardless of how quickly the old socket's close completes.");
        }

        [Test]
        public async Task OnReconnectRequested_Clears_Websocket_So_A_New_Socket_Can_Open()
        {
            webSocketManager.Connect();
            var mockSocket = new Mock<IWebSocket>();
            mockSocket.Setup(s => s.CloseAsync()).Returns(Task.CompletedTask);
            webSocketManager.SetWebSocketForTests(mockSocket.Object);
            var responseMeta = CoherenceJson.SerializeObject(new ResponseMeta { RequestId = WebSocketManager.ConnectionReconnectPath });

            webSocketManager.HandleResponse(responseMeta + "\nBody");
            await WaitForReconnect();

            Assert.That(webSocketManager.HasWebSocketForTests, Is.False, "The old socket reference should be cleared so Update() is free to open a new one.");
        }

        private static async Task WaitForReconnect()
        {
            var waitUntil = DateTime.Now.AddMilliseconds(WebSocketManager.MaxWaitBeforeReconnectMilliSeconds);
            while (DateTime.Now < waitUntil)
            {
                await Task.Yield();
            }
            await Task.Yield();
        }
    }
}
