// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Runtime.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Coherence.Cloud;
    using Coherence.Tests;
    using Coherence.Utils;
    using Common;
    using NUnit.Framework;

    /// <summary>
    /// Edit Mode unit tests for <see cref="LobbiesService"/>.
    /// </summary>
    public class LobbiesServiceTests : CoherenceTest
    {
        private MockAuthClientBuilder authClientBuilder;
        private MockRequestFactoryBuilder requestFactoryBuilder;
        private MockRuntimeSettingsBuilder runtimeSettingsBuilder;

        private CloudCredentialsPair CloudCredentialsPair => new(AuthClient, RequestFactory);
        private IAuthClientInternal AuthClient => authClientBuilder.AuthClient;
        private IRequestFactoryInternal RequestFactory => requestFactoryBuilder.RequestFactory;
        private IRuntimeSettings RuntimeSettings => runtimeSettingsBuilder.RuntimeSettings;

        public override void SetUp()
        {
            base.SetUp();
            authClientBuilder = new();
            requestFactoryBuilder = new();
            runtimeSettingsBuilder = new();
        }

        [Test]
        public async Task FindOrCreateLobbyAsync_CanBeCanceled()
        {
            using var lobbiesService = CreateLobbiesService(new TaskCompletionSource<bool>());
            using var cancellationTokenSource = new CancellationTokenSource();
            var task = lobbiesService.FindOrCreateLobbyAsync(new(), new(), cancellationTokenSource.Token);

            await Task.Yield();
            Assert.That(task.IsCompleted, Is.False);
            cancellationTokenSource.Cancel();

            try { await task; }
            catch (OperationCanceledException) { return; }

            Assert.Fail("Expected OperationCanceledException was not thrown.");
        }

        [Test]
        public async Task CreateLobbyAsync_CanBeCanceled()
        {
            using var lobbiesService = CreateLobbiesService(new TaskCompletionSource<bool>());
            using var cancellationTokenSource = new CancellationTokenSource();
            var task = lobbiesService.CreateLobbyAsync(new(), cancellationTokenSource.Token);

            await Task.Yield();
            Assert.That(task.IsCompleted, Is.False);
            cancellationTokenSource.Cancel();

            try { await task; }
            catch (OperationCanceledException) { return; }

            Assert.Fail("Expected OperationCanceledException was not thrown.");
        }

        [Test]
        public async Task FindLobbiesAsync_CanBeCanceled()
        {
            using var lobbiesService = CreateLobbiesService(new TaskCompletionSource<bool>());
            using var cancellationTokenSource = new CancellationTokenSource();
            var task = lobbiesService.FindLobbiesAsync(new(), cancellationTokenSource.Token);

            await Task.Yield();
            Assert.That(task.IsCompleted, Is.False);
            cancellationTokenSource.Cancel();

            try { await task; }
            catch (OperationCanceledException) { return; }

            Assert.Fail("Expected OperationCanceledException was not thrown.");
        }

        [Test]
        public async Task JoinLobbyAsync_CanBeCanceled()
        {
            using var lobbiesService = CreateLobbiesService(new TaskCompletionSource<bool>());
            using var cancellationTokenSource = new CancellationTokenSource();
            // Positional, as 2.2 callers wrote it.
            var task = lobbiesService.JoinLobbyAsync(new LobbyData { Id = "33657213" }, null, null,
                cancellationTokenSource.Token);

            await Task.Yield();
            Assert.That(task.IsCompleted, Is.False);
            cancellationTokenSource.Cancel();

            try { await task; }
            catch (OperationCanceledException) { return; }

            Assert.Fail("Expected OperationCanceledException was not thrown.");
        }

        [Test]
        public async Task RefreshLobbyAsync_CanBeCanceled()
        {
            using var lobbiesService = CreateLobbiesService(new TaskCompletionSource<bool>());
            using var cancellationTokenSource = new CancellationTokenSource();
            var task = lobbiesService.RefreshLobbyAsync("lobbyId", cancellationTokenSource.Token);

            await Task.Yield();
            Assert.That(task.IsCompleted, Is.False);
            cancellationTokenSource.Cancel();

            try { await task; }
            catch (OperationCanceledException) { return; }

            Assert.Fail("Expected OperationCanceledException was not thrown.");
        }

        [Test]
        public async Task FetchLobbyStatsAsync_CanBeCanceled()
        {
            using var lobbiesService = CreateLobbiesService(new TaskCompletionSource<bool>());
            using var cancellationTokenSource = new CancellationTokenSource();
            var task = lobbiesService.FetchLobbyStatsAsync(null, null, cancellationTokenSource.Token);

            await Task.Yield();
            Assert.That(task.IsCompleted, Is.False);
            cancellationTokenSource.Cancel();

            try { await task; }
            catch (OperationCanceledException) { return; }

            Assert.Fail("Expected OperationCanceledException was not thrown.");
        }

        [Test]
        public async Task FindOrCreateLobbyAsync_Waits_For_Throttling()
        {
            var cooldownCompletionSource = new TaskCompletionSource<bool>();
            using var lobbiesService = CreateLobbiesService(cooldownCompletionSource);
            var task = lobbiesService.FindOrCreateLobbyAsync(new(), new());

            await Task.Yield();
            Assert.That(task.IsCompleted, Is.False);
            cooldownCompletionSource.SetCanceled();

            try { await task; }
            catch (OperationCanceledException) { return; }

            Assert.Fail("Expected OperationCanceledException was not thrown.");
        }

        [Test]
        public async Task CreateLobbyAsync_Waits_For_Throttling()
        {
            var cooldownCompletionSource = new TaskCompletionSource<bool>();
            using var lobbiesService = CreateLobbiesService(cooldownCompletionSource);
            var task = lobbiesService.CreateLobbyAsync(new());

            await Task.Yield();
            Assert.That(task.IsCompleted, Is.False);
            cooldownCompletionSource.SetCanceled();

            try { await task; }
            catch (OperationCanceledException) { return; }

            Assert.Fail("Expected OperationCanceledException was not thrown.");
        }

        [Test]
        public async Task FindLobbiesAsync_Waits_For_Throttling()
        {
            var cooldownCompletionSource = new TaskCompletionSource<bool>();
            using var lobbiesService = CreateLobbiesService(cooldownCompletionSource);
            var task = lobbiesService.FindLobbiesAsync();

            await Task.Yield();
            Assert.That(task.IsCompleted, Is.False);
            cooldownCompletionSource.SetCanceled();

            try { await task; }
            catch (OperationCanceledException) { return; }

            Assert.Fail("Expected OperationCanceledException was not thrown.");
        }

        [Test]
        public async Task JoinLobbyAsync_Waits_For_Throttling()
        {
            var cooldownCompletionSource = new TaskCompletionSource<bool>();
            using var lobbiesService = CreateLobbiesService(cooldownCompletionSource);
            var task = lobbiesService.JoinLobbyAsync(new LobbyData { Id = "33657213" }, null, null);

            await Task.Yield();
            Assert.That(task.IsCompleted, Is.False);
            cooldownCompletionSource.SetCanceled();

            try { await task; }
            catch (OperationCanceledException) { return; }

            Assert.Fail("Expected OperationCanceledException was not thrown.");
        }

        [Test]
        public async Task RefreshLobbyAsync_Waits_For_Throttling()
        {
            var cooldownCompletionSource = new TaskCompletionSource<bool>();
            using var lobbiesService = CreateLobbiesService(cooldownCompletionSource);
            var task = lobbiesService.RefreshLobbyAsync("lobbyId");

            await Task.Yield();
            Assert.That(task.IsCompleted, Is.False);
            cooldownCompletionSource.SetCanceled();

            try { await task; }
            catch (OperationCanceledException) { return; }

            Assert.Fail("Expected OperationCanceledException was not thrown.");
        }

        [Test]
        public async Task FetchLobbyStatsAsync_Waits_For_Throttling()
        {
            var cooldownCompletionSource = new TaskCompletionSource<bool>();
            using var lobbiesService = CreateLobbiesService(cooldownCompletionSource);
            var task = lobbiesService.FetchLobbyStatsAsync(null, null);

            await Task.Yield();
            Assert.That(task.IsCompleted, Is.False);
            cooldownCompletionSource.SetCanceled();

            try { await task; }
            catch (OperationCanceledException) { return; }

            Assert.Fail("Expected OperationCanceledException was not thrown.");
        }

        [Test]
        public void GetLobbySessionIds_Returns_LobbyIds_From_LoginResponse()
        {
            using var lobbiesService = CreateLobbiesService();
            var expectedLobbyIds = new List<string> { "lobby-id" };
            var loginResponse = new LoginResponse { LobbyIds = expectedLobbyIds };

            authClientBuilder.RaiseOnLogin(loginResponse);

            Assert.That(lobbiesService.GetLobbySessionIds(), Is.EquivalentTo(expectedLobbyIds));
        }

        [Test]
        public void GetLobbySessionIds_Returns_Empty_Collection_After_Logout()
        {
            using var lobbiesService = CreateLobbiesService();
            var loginResponseLobbyIds = new List<string> { "lobby-id" };
            var loginResponse = new LoginResponse { LobbyIds = loginResponseLobbyIds };
            authClientBuilder.RaiseOnLogin(loginResponse);

            authClientBuilder.RaiseOnLogout();

            Assert.That(lobbiesService.GetLobbySessionIds(), Is.Empty);
        }

        [Test]
        public async Task GetLobbySessionsAsync_Returns_Lobbies_Listed_In_LoginResponse()
        {
            requestFactoryBuilder.SendRequestAsyncReturns((_, pathParams, _, _, _, _, _) =>
            {
                var calledWithId = ExtractId(pathParams);
                return Task.FromResult(CoherenceJson.SerializeObject(new LobbiesData { Lobbies = CreateLobbyData(calledWithId) } ));
                static string ExtractId(string pathParams)
                {
                    var i = LobbiesService.refreshLobbiesAsyncPathParams.IndexOf("{0}", StringComparison.Ordinal);
                    return pathParams.Substring(i, pathParams.Length - (LobbiesService.refreshLobbiesAsyncPathParams.Length - "{0}".Length));
                }
            });
            using var lobbiesService = CreateLobbiesService();

            const string lobbyId = "lobby-id";
            authClientBuilder.RaiseOnLogin(new() { LobbyIds = new() { lobbyId } });

            var lobbySessions = await lobbiesService.GetLobbySessionsAsync();

            var expectedLobbyData = CreateLobbyData(lobbyId);
            Assert.That(lobbySessions.Select(x => x.LobbyData), Is.EquivalentTo(expectedLobbyData));

            LobbyData[] CreateLobbyData(string id) => new LobbyData[] { new() { Id = id, players = new() { new(authClientBuilder.PlayerAccountId, "", new()) }, lobbyAttributes = new() } };
        }

        [Test]
        public async Task GetLobbySessionsAsync_Returns_Empty_Collection_After_Logout()
        {
            using var lobbiesService = CreateLobbiesService();
            var expectedLobbyIds = new List<string> { "lobby-id" };
            var loginResponse = new LoginResponse { LobbyIds = expectedLobbyIds };
            authClientBuilder.RaiseOnLogin(loginResponse);

            authClientBuilder.RaiseOnLogout();

            var sessionIds = await lobbiesService.GetLobbySessionsAsync();
            Assert.That(sessionIds, Is.Empty);
        }

        [Test]
        [Description("A created lobby reports the creation time returned by the Play API.")]
        public async Task CreateLobbyAsync_Surfaces_CreatedAt()
        {
            requestFactoryBuilder.SendRequestAsyncReturns(LobbyResponse);
            using var lobbiesService = CreateLobbiesService();

            var lobbySession = await lobbiesService.CreateLobbyAsync(new());

            Assert.That(lobbySession.LobbyData.CreatedAt, Is.EqualTo(DateTimeOffset.FromUnixTimeSeconds(CreatedAtUnixSeconds)));
        }

        [Test]
        [Description("Found lobbies report the creation time returned by the Play API, so games can sort by lobby age.")]
        public async Task FindLobbiesAsync_Surfaces_CreatedAt()
        {
            requestFactoryBuilder.SendRequestAsyncReturns($"[{LobbyResponse}]");
            using var lobbiesService = CreateLobbiesService();

            var lobbies = await lobbiesService.FindLobbiesAsync();

            Assert.That(lobbies.Select(x => x.CreatedAt),
                Is.EquivalentTo(new[] { DateTimeOffset.FromUnixTimeSeconds(CreatedAtUnixSeconds) }));
        }

        [Test]
        [TestCase(FilterOperator.LessThan)]
        [TestCase(FilterOperator.LessOrEqualThan)]
        [TestCase(FilterOperator.GreaterThan)]
        [TestCase(FilterOperator.GreaterOrEqualThan)]
        [Description("FindLobbiesAsync sends a createdAt filter, serialized as unix seconds, for every single-value operator the backend accepts.")]
        public async Task FindLobbiesAsync_Sends_CreatedAt_Filter_For_Every_Supported_Operator(FilterOperator filterOperator)
        {
            requestFactoryBuilder.SendRequestAsyncReturns("[]");
            using var lobbiesService = CreateLobbiesService();
            var createdAt = DateTimeOffset.UtcNow.AddMinutes(-5);
            var findOptions = new FindLobbyOptions
            {
                LobbyFilters = new() { new LobbyFilter().WithCreatedAt(filterOperator, createdAt) }
            };

            await lobbiesService.FindLobbiesAsync(findOptions);

            var sentRequest = CoherenceJson.DeserializeObject<FetchLobbiesRequest>(requestFactoryBuilder.SendRequestAsyncWasCalledWith.body);
            var sentFilter = sentRequest.LobbyFilters[0];

            Assert.That(sentFilter.Key, Is.EqualTo(nameof(FilterKey.createdAt)));
            Assert.That(sentFilter.LogicOperator, Is.EqualTo(new LobbyFilter().FilterOperatorToString(filterOperator)));
            Assert.That(sentFilter.Values, Has.Count.EqualTo(1));
            Assert.That(sentFilter.Values[0], Is.EqualTo(createdAt.ToUnixTimeSeconds()));
        }

        [Test]
        [Description("FindLobbiesAsync sends both bounds of a createdAt Between filter, serialized as unix seconds.")]
        public async Task FindLobbiesAsync_Sends_CreatedAt_Between_Filter_With_Both_Bounds()
        {
            requestFactoryBuilder.SendRequestAsyncReturns("[]");
            using var lobbiesService = CreateLobbiesService();
            var createdAtFrom = DateTimeOffset.UtcNow.AddMinutes(-10);
            var createdAtTo = DateTimeOffset.UtcNow.AddMinutes(-5);
            var findOptions = new FindLobbyOptions
            {
                LobbyFilters = new() { new LobbyFilter().WithCreatedAt(FilterOperator.Between, createdAtFrom, createdAtTo) }
            };

            await lobbiesService.FindLobbiesAsync(findOptions);

            var sentRequest = CoherenceJson.DeserializeObject<FetchLobbiesRequest>(requestFactoryBuilder.SendRequestAsyncWasCalledWith.body);
            var sentFilter = sentRequest.LobbyFilters[0];

            Assert.That(sentFilter.Key, Is.EqualTo(nameof(FilterKey.createdAt)));
            Assert.That(sentFilter.LogicOperator, Is.EqualTo(new LobbyFilter().FilterOperatorToString(FilterOperator.Between)));
            Assert.That(sentFilter.Values, Has.Count.EqualTo(2));
            Assert.That(sentFilter.Values[0], Is.EqualTo(createdAtFrom.ToUnixTimeSeconds()));
            Assert.That(sentFilter.Values[1], Is.EqualTo(createdAtTo.ToUnixTimeSeconds()));
        }

        [Test]
        [Description("A lobby id is all a join needs, so it can be joined without holding its LobbyData.")]
        public async Task JoinLobbyAsync_JoinsByLobbyId()
        {
            requestFactoryBuilder.SendRequestAsyncReturns(LobbyResponse);
            using var lobbiesService = CreateLobbiesService();

            var lobbySession = await lobbiesService.JoinLobbyAsync("33657213");

            Assert.That(requestFactoryBuilder.SendRequestAsyncWasCalledWith.pathParams,
                Is.EqualTo("/33657213/players"));
            Assert.That(lobbySession.LobbyData.Id, Is.EqualTo("33657213"));
        }

        [Test]
        [Description("Reservations passed to a join are sent with the join.")]
        public async Task JoinLobbyAsync_SendsReservations_WhenReservationsArePassed()
        {
            requestFactoryBuilder.SendRequestAsyncReturns(LobbyResponse);
            using var lobbiesService = CreateLobbiesService();
            var reservations = new LobbyReservations(new PlayerAccountId[] { "p2", "p3" }, TimeSpan.FromSeconds(20));

            await lobbiesService.JoinLobbyAsync("33657213", reservations: reservations);

            var sentRequest = requestFactoryBuilder.SendRequestAsyncWasCalledWith;
            var sentReservations = CoherenceJson.DeserializeObject<JoinLobbyRequest>(sentRequest.body).Reservations;

            Assert.That(sentRequest.pathParams, Is.EqualTo("/33657213/players"));
            Assert.That(sentReservations, Is.Not.Null);
            Assert.That(sentReservations.Value.PlayerIds, Is.EqualTo(new List<string> { "p2", "p3" }));
            Assert.That(sentReservations.Value.TotalSeconds, Is.EqualTo(20));
        }

        [Test]
        [Description("Reservations passed to the callback-based join are sent with the join.")]
        public void JoinLobby_SendsReservations_WhenReservationsArePassed()
        {
            requestFactoryBuilder.SendRequestReturns(LobbyResponse);
            using var lobbiesService = CreateLobbiesService();
            var reservations = new LobbyReservations(new PlayerAccountId[] { "p2" }, TimeSpan.FromSeconds(60));
            RequestResponse<LobbySession>? response = null;

            lobbiesService.JoinLobby(new LobbyData { Id = "33657213" }, result => response = result,
                reservations: reservations);

            Assert.That(requestFactoryBuilder.SendRequestWasCalled, Is.True, "JoinLobby sent no request.");
            Assert.That(response, Is.Not.Null, "JoinLobby's callback was not invoked.");
            Assert.That(response.Value.Status, Is.EqualTo(RequestStatus.Success));

            var sentRequest = requestFactoryBuilder.SendRequestWasCalledWith;
            var sentReservations = CoherenceJson.DeserializeObject<JoinLobbyRequest>(sentRequest.body).Reservations;

            Assert.That(sentRequest.pathParams, Is.EqualTo("/33657213/players"));
            Assert.That(sentReservations, Is.Not.Null);
            Assert.That(sentReservations.Value.PlayerIds, Is.EqualTo(new List<string> { "p2" }));
            Assert.That(sentReservations.Value.TotalSeconds, Is.EqualTo(60));
        }

        [Test]
        [Description("A join code resolves and joins in one request, without any lobby id in the path.")]
        public async Task JoinLobbyByCodeAsync_PostsTheCodeToTheJoinByCodeEndpoint()
        {
            requestFactoryBuilder.SendRequestAsyncReturns(LobbyResponse);
            using var lobbiesService = CreateLobbiesService();

            var lobbySession = await lobbiesService.JoinLobbyByCodeAsync("JOIN17");

            var sentRequest = requestFactoryBuilder.SendRequestAsyncWasCalledWith;

            Assert.That(sentRequest.pathParams, Is.EqualTo("/join-by-code"));
            Assert.That(sentRequest.method, Is.EqualTo("POST"));
            Assert.That(CoherenceJson.DeserializeObject<JoinLobbyByCodeRequest>(sentRequest.body).JoinCode,
                Is.EqualTo("JOIN17"));
            Assert.That(lobbySession.LobbyData.Id, Is.EqualTo("33657213"));
        }

        [Test]
        [Description("Can join a room of an already started game.")]
        public async Task JoinLobbyAsync_JoinsTheRoom_WhenTheSessionIsAlreadyRunning()
        {
            requestFactoryBuilder.SendRequestAsyncReturns(LobbyResponseWithRoom);
            using var lobbiesService = CreateLobbiesService();
            string startedForLobby = null;
            lobbiesService.OnPlaySessionStarted += (lobbyId, _) => startedForLobby = lobbyId;

            await lobbiesService.JoinLobbyAsync("33657213");

            Assert.That(startedForLobby, Is.EqualTo("33657213"));
        }

        [Test]
        [Description("Shouldn't join a room when the game hasn't started yet.")]
        public async Task JoinLobbyAsync_DoesNotJoinARoom_WhenTheSessionHasNotStarted()
        {
            requestFactoryBuilder.SendRequestAsyncReturns(LobbyResponse);
            using var lobbiesService = CreateLobbiesService();
            var started = false;
            lobbiesService.OnPlaySessionStarted += (_, _) => started = true;

            await lobbiesService.JoinLobbyAsync("33657213");

            Assert.That(started, Is.False);
        }

        [Test]
        [Description("A code admits you the way an invite does, so it carries you into a session already running.")]
        public async Task JoinLobbyByCodeAsync_JoinsTheRoom_WhenTheSessionIsAlreadyRunning()
        {
            requestFactoryBuilder.SendRequestAsyncReturns(LobbyResponseWithRoom);
            using var lobbiesService = CreateLobbiesService();
            string startedForLobby = null;
            lobbiesService.OnPlaySessionStarted += (lobbyId, _) => startedForLobby = lobbyId;

            await lobbiesService.JoinLobbyByCodeAsync("JOIN17");

            Assert.That(startedForLobby, Is.EqualTo("33657213"));
        }

        [Test]
        public async Task JoinLobbyByCodeAsync_Fails_WhenNoCodeIsGiven()
        {
            using var lobbiesService = CreateLobbiesService();

            try { await lobbiesService.JoinLobbyByCodeAsync(""); }
            catch (ArgumentException) { return; }

            Assert.Fail("Expected ArgumentException was not thrown.");
        }

        [Test]
        public async Task JoinLobbyByCodeAsync_CanBeCanceled()
        {
            using var lobbiesService = CreateLobbiesService(new TaskCompletionSource<bool>());
            using var cancellationTokenSource = new CancellationTokenSource();
            var task = lobbiesService.JoinLobbyByCodeAsync("JOIN17", null, cancellationTokenSource.Token);

            await Task.Yield();
            Assert.That(task.IsCompleted, Is.False);
            cancellationTokenSource.Cancel();

            try { await task; }
            catch (OperationCanceledException) { return; }

            Assert.Fail("Expected OperationCanceledException was not thrown.");
        }

        [Test]
        public async Task JoinLobbyByCodeAsync_Waits_For_Throttling()
        {
            var cooldownCompletionSource = new TaskCompletionSource<bool>();
            using var lobbiesService = CreateLobbiesService(cooldownCompletionSource);
            var task = lobbiesService.JoinLobbyByCodeAsync("JOIN17");

            await Task.Yield();
            Assert.That(task.IsCompleted, Is.False);
            cooldownCompletionSource.SetCanceled();

            try { await task; }
            catch (OperationCanceledException) { return; }

            Assert.Fail("Expected OperationCanceledException was not thrown.");
        }

        private const long CreatedAtUnixSeconds = 1786451681;

        /// <summary>
        /// A lobby as returned by the Play API, including the <c>created_at</c> unix timestamp.
        /// </summary>
        private const string LobbyResponse =
            "{\"id\":\"33657213\",\"name\":\"probe\",\"region\":\"eu\",\"max_players\":4," +
            "\"owner_id\":\"33657197\",\"room_id\":0,\"closed\":false,\"unlisted\":false," +
            "\"private\":false,\"attributes\":[],\"players\":[],\"created_at\":1786451681}";

        /// <summary>The same lobby, as it comes back once its game session has started.</summary>
        private const string LobbyResponseWithRoom =
            "{\"id\":\"33657213\",\"name\":\"probe\",\"region\":\"eu\",\"max_players\":4," +
            "\"owner_id\":\"33657197\",\"room_id\":1,\"room\":{\"id\":1},\"closed\":false," +
            "\"unlisted\":false,\"private\":false,\"attributes\":[],\"players\":[],\"created_at\":1786451681}";

        private LobbiesService CreateLobbiesService(TaskCompletionSource<bool> cooldownCompletionSource = null)
        {
            if (cooldownCompletionSource is null)
            {
                cooldownCompletionSource = new();
                cooldownCompletionSource.SetResult(true);
            }

            return CreateLobbiesService(new FakeRequestThrottle(cooldownCompletionSource));
        }

        private LobbiesService CreateLobbiesService(RequestThrottle throttle)
            => new(CloudCredentialsPair, RuntimeSettings, playerAccountProvider: null, throttle);
    }
}
