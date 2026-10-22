// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Runtime.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Coherence.Cloud;
    using Coherence.Tests;
    using Common;
    using Moq;
    using Moq.Language.Flow;
    using NUnit.Framework;

    [TestFixture]
    public class GameServersServiceTests : CoherenceTest
    {
        private readonly Mock<IAuthClientInternal> authClient = new();
        private readonly Mock<IRuntimeSettings> runtimeSettings = new();

        private const string TestGameServerJson = @"
        {
            ""id"": 33568963,
            ""region"": ""eu"",
            ""slug"": ""ref-gamesrv4"",
            ""tag"": ""test"",
            ""kv"": {""foo"": ""bar""},
            ""size"": ""unity.8x"",
            ""max_players"": 10,
            ""connected_players"": 1,
            ""suspended"": true,
            ""ip"": ""127.0.0.1"",
            ""port"": 42050,
            ""created_at"": 1703165531,
            ""last_started_at"": 1703165531
        }
        ";

        [Test]
        public async Task DeployAsync_Should_ReturnIdObject_When_GivenCorrectOptions()
        {
            var (requestMock, service) = NewTestService();

            SetSendRequestAsyncResponse(requestMock, Task.FromResult(@"{""id"": 123, ""secret"": ""secret""}"));

            var gameServer = await service.DeployAsync(new GameServerDeployOptions());
            Assert.AreEqual(gameServer.Id, 123);
            Assert.AreEqual(gameServer.Secret, "secret");
        }

        [Test]
        public async Task DeployAsync_Should_ThrowException_When_ServerRespondsWithError()
        {
            var (requestMock, service) = NewTestService();

            var ex = new RequestException(ErrorCode.Unknown, 400, "Bad Request");
            SetSendRequestAsyncThrows(requestMock, ex);

            await AssertThrowsAsync<RequestException>(() => service.DeployAsync(new GameServerDeployOptions()));
        }

        [Test]
        [Description("A 429 response from the server should still propagate as a RequestException so callers can detect and retry throttled deploy requests.")]
        public async Task DeployAsync_Should_ThrowRequestException_When_ServerThrottles()
        {
            var (requestMock, service) = NewTestService();

            var ex = new RequestException(ErrorCode.TooManyRequests, 429, "Too Many Requests");
            SetSendRequestAsyncThrows(requestMock, ex);

            await AssertThrowsAsync<RequestException>(() => service.DeployAsync(new GameServerDeployOptions()));
        }

        [Test]
        public async Task ListAsync_Should_ReturnGameServers_When_GivenCorrectOptions()
        {
            var (requestMock, service) = NewTestService();

            SetSendRequestAsyncResponse(requestMock, Task.FromResult($@"
            [
                {TestGameServerJson},
                {TestGameServerJson}
            ]
            "));

            var gameServers = await service.ListAsync(new GameServerListOptions());
            Assert.AreEqual(gameServers.Count, 2);
            ValidateTestGameServer(gameServers[0]);
        }

        [Test]
        public async Task ListAsync_Should_ThrowException_When_ServerRespondsWithError()
        {
            var (requestMock, service) = NewTestService();

            var ex = new RequestException(ErrorCode.Unknown, 500, "Internal Server Error");
            SetSendRequestAsyncThrows(requestMock, ex);

            await AssertThrowsAsync<RequestException>(() => service.ListAsync(new GameServerListOptions()));
        }

        [Test]
        [Description("A 429 response from the server should still propagate as a RequestException so callers can detect and retry throttled list requests.")]
        public async Task ListAsync_Should_ThrowRequestException_When_ServerThrottles()
        {
            var (requestMock, service) = NewTestService();

            var ex = new RequestException(ErrorCode.TooManyRequests, 429, "Too Many Requests");
            SetSendRequestAsyncThrows(requestMock, ex);

            await AssertThrowsAsync<RequestException>(() => service.ListAsync(new GameServerListOptions()));
        }

        [Test]
        public async Task MatchAsync_Should_ReturnGameServer_When_ThereIsAMatch()
        {
            var (requestMock, service) = NewTestService();

            SetSendRequestAsyncResponse(requestMock, Task.FromResult($"{{\"gameserver\": {TestGameServerJson}}}"));

            var optionalGameServer = await service.MatchAsync(new GameServerMatchOptions());
            Assert.NotNull(optionalGameServer.GameServerData);
            ValidateTestGameServer(optionalGameServer.GameServerData.Value);
        }

        [Test]
        public async Task MatchAsync_Should_ReturnEmptyGameServerData_When_ThereIsNoMatch()
        {
            var (requestMock, service) = NewTestService();

            SetSendRequestAsyncResponse(requestMock, Task.FromResult(@"{""gameserver"": null}"));

            var optionalGameServer = await service.MatchAsync(new GameServerMatchOptions());
            Assert.Null(optionalGameServer.GameServerData);
        }

        [Test]
        public async Task MatchAsync_Should_ThrowException_When_ServerRespondsWithError()
        {
            var (requestMock, service) = NewTestService();

            var ex = new RequestException(ErrorCode.Unknown, 500, "Internal Server Error");
            SetSendRequestAsyncThrows(requestMock, ex);

            await AssertThrowsAsync<RequestException>(() => service.MatchAsync(new GameServerMatchOptions()));
        }

        [Test]
        [Description("A 429 response from the server should still propagate as a RequestException so callers can detect and retry throttled match requests.")]
        public async Task MatchAsync_Should_ThrowRequestException_When_ServerThrottles()
        {
            var (requestMock, service) = NewTestService();

            var ex = new RequestException(ErrorCode.TooManyRequests, 429, "Too Many Requests");
            SetSendRequestAsyncThrows(requestMock, ex);

            await AssertThrowsAsync<RequestException>(() => service.MatchAsync(new GameServerMatchOptions()));
        }

        [Test]
        public async Task GetAsync_Should_ReturnGameServer_When_ItExists()
        {
            var (requestMock, service) = NewTestService();

            SetSendRequestAsyncResponse(requestMock, Task.FromResult(TestGameServerJson));

            var gameServer = await service.GetAsync(33568963);
            ValidateTestGameServer(gameServer);
        }

        [Test]
        public async Task GetAsync_Should_RaiseException_When_ItDoesntExist()
        {
            var (requestMock, service) = NewTestService();

            var ex = new RequestException(ErrorCode.Unknown, 404, "Not Found");
            SetSendRequestAsyncThrows(requestMock, ex);

            await AssertThrowsAsync<RequestException>(() => service.GetAsync(0));
        }

        [Test]
        [Description("A 429 response from the server should still propagate as a RequestException so callers can detect and retry throttled get requests.")]
        public async Task GetAsync_Should_ThrowRequestException_When_ServerThrottles()
        {
            var (requestMock, service) = NewTestService();

            var ex = new RequestException(ErrorCode.TooManyRequests, 429, "Too Many Requests");
            SetSendRequestAsyncThrows(requestMock, ex);

            await AssertThrowsAsync<RequestException>(() => service.GetAsync(1));
        }

        [Test]
        public async Task SuspendAsync_Should_Succeed_When_ItExists()
        {
            var (requestMock, service) = NewTestService();

            SetSendRequestAsyncResponse(requestMock, Task.FromResult(""));

            await service.SuspendAsync(1);
        }

        [Test]
        public async Task SuspendAsync_Should_ThrowException_When_ItDoesntExist()
        {
            var (requestMock, service) = NewTestService();

            var ex = new RequestException(ErrorCode.Unknown, 404, "Not Found");
            SetSendRequestAsyncThrows(requestMock, ex);

            await AssertThrowsAsync<RequestException>(() => service.SuspendAsync(0));
        }

        [Test]
        [Description("A 429 response from the server should be swallowed so a throttled suspend does not crash the caller.")]
        public async Task SuspendAsync_Should_NotThrow_When_ServerThrottles()
        {
            var (requestMock, service) = NewTestService();

            var ex = new RequestException(ErrorCode.TooManyRequests, 429, "Too Many Requests");
            SetSendRequestAsyncThrows(requestMock, ex);

            await service.SuspendAsync(1);
        }

        [Test]
        public async Task ResumeAsync_Should_Succeed_When_ItExists()
        {
            var (requestMock, service) = NewTestService();

            SetSendRequestAsyncResponse(requestMock, Task.FromResult(""));

            await service.ResumeAsync(1);
        }

        [Test]
        public async Task ResumeAsync_Should_ThrowException_When_ItDoesntExist()
        {
            var (requestMock, service) = NewTestService();

            var ex = new RequestException(ErrorCode.Unknown, 404, "Not Found");
            SetSendRequestAsyncThrows(requestMock, ex);

            await AssertThrowsAsync<RequestException>(() => service.ResumeAsync(0));
        }

        [Test]
        [Description("A 429 response from the server should be swallowed so a throttled resume does not crash the caller.")]
        public async Task ResumeAsync_Should_NotThrow_When_ServerThrottles()
        {
            var (requestMock, service) = NewTestService();

            var ex = new RequestException(ErrorCode.TooManyRequests, 429, "Too Many Requests");
            SetSendRequestAsyncThrows(requestMock, ex);

            await service.ResumeAsync(1);
        }

        [Test]
        public async Task DeleteAsync_Should_Succeed_When_ItExists()
        {
            var (requestMock, service) = NewTestService();

            SetSendRequestAsyncResponse(requestMock, Task.FromResult(""));

            await service.DeleteAsync(1);
        }

        [Test]
        public async Task DeleteAsync_Should_ThrowException_When_ItDoesntExist()
        {
            var (requestMock, service) = NewTestService();

            var ex = new RequestException(ErrorCode.Unknown, 404, "Not Found");
            SetSendRequestAsyncThrows(requestMock, ex);

            await AssertThrowsAsync<RequestException>(() => service.DeleteAsync(0));
        }

        [Test]
        [Description("A 429 response from the server should be swallowed so a throttled delete does not crash the caller.")]
        public async Task DeleteAsync_Should_NotThrow_When_ServerThrottles()
        {
            var (requestMock, service) = NewTestService();

            var ex = new RequestException(ErrorCode.TooManyRequests, 429, "Too Many Requests");
            SetSendRequestAsyncThrows(requestMock, ex);

            await service.DeleteAsync(1);
        }

        private void ValidateTestGameServer(GameServerData server)
        {
            Assert.AreEqual(server.Id, 33568963);
            Assert.AreEqual(server.Region, "eu");
            Assert.AreEqual(server.Slug, "ref-gamesrv4");
            Assert.AreEqual(server.Tag, "test");
            Assert.AreEqual(server.KV, new Dictionary<string, string>
            {
                { "foo", "bar" },
            });
            Assert.AreEqual(server.Size, "unity.8x");
            Assert.AreEqual(server.MaxPlayers, 10);
            Assert.AreEqual(server.ConnectedPlayers, 1);
            Assert.AreEqual(server.Suspended, true);
            Assert.AreEqual(server.Ip, "127.0.0.1");
            Assert.AreEqual(server.Port, 42050);
            Assert.AreEqual(server.CreatedAt, 1703165531);
            Assert.AreEqual(server.LastStartedAt, 1703165531);
        }

        //
        // Test helpers
        //

        private (Mock<IRequestFactoryInternal>, IGameServersService) NewTestService()
        {
            var requestMock = new Mock<IRequestFactoryInternal>(MockBehavior.Strict);
            var credentials = new CloudCredentialsPair(authClient.Object, requestMock.Object);
            var service = new GameServersService(credentials, runtimeSettings.Object);
            return (requestMock, service);
        }

        private static async Task<TException> AssertThrowsAsync<TException>(Func<Task> action)
            where TException : Exception
        {
            try
            {
                await action();
            }
            catch (TException ex) when (ex.GetType() == typeof(TException))
            {
                return ex;
            }
            catch (Exception ex)
            {
                Assert.Fail($"Expected exception of type {typeof(TException)} but got {ex.GetType()}: {ex}");
                throw;
            }

            Assert.Fail($"Expected exception of type {typeof(TException)} but no exception was thrown.");
            return null;
        }

        private static void SetSendRequestAsyncResponse(Mock<IRequestFactoryInternal> mock, Task<string> response)
            => SetupSendRequestAsync(mock).Returns(response);

        private static void SetSendRequestAsyncThrows(Mock<IRequestFactoryInternal> mock, Exception ex)
            => SetupSendRequestAsync(mock).Throws(ex);

        private static ISetup<IRequestFactoryInternal, Task<string>> SetupSendRequestAsync(Mock<IRequestFactoryInternal> mock) => mock.Setup(factory => factory.SendRequestAsync
        (
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<Dictionary<string, string>>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<bool>())
        );
    }
}
