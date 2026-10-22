// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Runtime.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Threading.Tasks;
    using Coherence.Cloud;
    using Coherence.Tests;
    using Coherence.Utils;
    using NUnit.Framework;

    /// <summary>
    /// Edit Mode unit tests for the lobby invite surface of <see cref="LobbiesService"/>.
    /// </summary>
    public class LobbiesServiceInviteTests : CoherenceTest
    {
        private const string LobbyId = "33657213";
        private const string InviteToken = "eyJhbGciOiJIUzI1NiJ9.payload.signature";

        private MockAuthClientBuilder authClientBuilder;
        private MockRequestFactoryBuilder requestFactoryBuilder;
        private LobbiesService lobbies;

        private CloudCredentialsPair CloudCredentialsPair
            => new(authClientBuilder.AuthClient, requestFactoryBuilder.RequestFactory);

        public override void SetUp()
        {
            base.SetUp();
            authClientBuilder = new();
            requestFactoryBuilder = new();
        }

        public override void TearDown()
        {
            lobbies?.Dispose();
            lobbies = null;
            base.TearDown();
        }

        // Built on first use, never in SetUp: MockRequestFactoryBuilder captures its response delegate when the
        // mock is built, so anything that touches the request factory has to come after the test's RecordCalls.
        private LobbiesService Lobbies => lobbies ??= new(CloudCredentialsPair,
            new MockRuntimeSettingsBuilder().RuntimeSettings, playerAccountProvider: null, throttle: null);

        [Test]
        [Description("Creating a token posts to the invite-token endpoint of the lobby it was asked for.")]
        public async Task CreateInviteTokenAsync_PostsToTheLobbysInviteTokenEndpoint()
        {
            var calls = RecordCalls();
            using var lobby = CreateLobbySession();

            await lobby.CreateInviteTokenAsync();

            Assert.That(calls, Has.Count.EqualTo(1));
            Assert.That(calls[0].path, Is.EqualTo($"/lobbies/{LobbyId}/invite-token"));
            Assert.That(calls[0].method, Is.EqualTo("POST"));
        }

        [Test]
        public async Task CreateInviteTokenAsync_ReturnsTheToken()
        {
            RecordCalls();
            using var lobby = CreateLobbySession();

            var token = await lobby.CreateInviteTokenAsync();

            Assert.That(token, Is.EqualTo(InviteToken));
        }

        [Test]
        public async Task CreateInviteTokenAsync_SendsRequestedTimeToLiveAsWholeSeconds()
        {
            var calls = RecordCalls();
            using var lobby = CreateLobbySession();

            await lobby.CreateInviteTokenAsync(TimeSpan.FromMinutes(5));

            var sent = CoherenceJson.DeserializeObject<CreateInviteTokenRequest>(calls[0].body);
            Assert.That(sent.TimeToLiveSeconds, Is.EqualTo(300));
        }

        [Test]
        [Description("No requested lifetime sends no ttl_seconds, so coherence Cloud applies its own default.")]
        public async Task CreateInviteTokenAsync_OmitsTimeToLive_WhenNoneIsRequested()
        {
            var calls = RecordCalls();
            using var lobby = CreateLobbySession();

            await lobby.CreateInviteTokenAsync();

            Assert.That(calls[0].body, Does.Not.Contain("ttl_seconds"));
        }

        [Test]
        [Description("Any requested lifetime is sent as is, leaving coherence Cloud to decide whether it's valid.")]
        public async Task CreateInviteTokenAsync_SendsTimeToLive_WithoutValidatingIt()
        {
            var calls = RecordCalls();
            using var lobby = CreateLobbySession();

            await lobby.CreateInviteTokenAsync(TimeSpan.Zero);

            var sent = CoherenceJson.DeserializeObject<CreateInviteTokenRequest>(calls[0].body);
            Assert.That(sent.TimeToLiveSeconds, Is.EqualTo(0));
        }

        [Test]
        [Description("A lifetime coherence Cloud rejects surfaces the Cloud's own error to the caller.")]
        public async Task CreateInviteTokenAsync_Throws_WhenCloudRejectsTheTimeToLive()
        {
            const string hint = "body.ttl_seconds must be greater or equal than 1 but got value 0";
            var rejection = new RequestException(ErrorCode.InvalidInput, HttpStatusCode.BadRequest, hint);
            requestFactoryBuilder.SendRequestAsyncReturns((_, _, _, _, _, _, _) =>
                Task.FromException<string>(rejection));
            using var lobby = CreateLobbySession();

            RequestException exception = null;
            try
            {
                await lobby.CreateInviteTokenAsync(TimeSpan.Zero);
            }
            catch (RequestException caught)
            {
                exception = caught;
            }

            Assert.That(exception, Is.Not.Null, "Expected RequestException was not thrown.");
            Assert.That(exception.ErrorCode, Is.EqualTo(ErrorCode.InvalidInput));
            Assert.That(exception.Message, Is.EqualTo(hint));
        }

        [Test]
        [Description("The token names the lobby and admits the player, so joining takes a single request.")]
        public async Task JoinLobbyWithTokenAsync_JoinsInOneRequest()
        {
            var calls = RecordCalls();

            var lobbySession = await Lobbies.JoinLobbyWithTokenAsync(InviteToken);

            Assert.That(lobbySession.LobbyData.Id, Is.EqualTo(LobbyId));

            Assert.That(calls, Has.Count.EqualTo(1));
            Assert.That(calls[0].path, Is.EqualTo("/lobbies/join-by-token"));
            Assert.That(CoherenceJson.DeserializeObject<JoinLobbyByTokenRequest>(calls[0].body).InviteToken,
                Is.EqualTo(InviteToken));
        }

        [Test]
        [Description("Player attributes given to a token join reach the lobby, as they would on any other join.")]
        public async Task JoinLobbyWithTokenAsync_SendsThePlayerAttributes()
        {
            var calls = RecordCalls();

            await Lobbies.JoinLobbyWithTokenAsync(InviteToken, new() { new CloudAttribute("rank", "gold") });

            var join = CoherenceJson.DeserializeObject<JoinLobbyByTokenRequest>(calls[0].body);

            Assert.That(join.PlayerAttributes, Has.Count.EqualTo(1));
            Assert.That(join.PlayerAttributes[0].Key, Is.EqualTo("rank"));
        }

        [Test]
        [Description("An invitee admitted mid-session is taken into the room, as the /lobbies/play push would.")]
        public async Task JoinLobbyWithTokenAsync_JoinsTheRoom_WhenTheSessionIsAlreadyRunning()
        {
            RecordCalls(joinedWithRoom: true);
            string startedForLobby = null;
            Lobbies.OnPlaySessionStarted += (lobbyId, _) => startedForLobby = lobbyId;

            await Lobbies.JoinLobbyWithTokenAsync(InviteToken);

            Assert.That(startedForLobby, Is.EqualTo(LobbyId));
        }

        [Test]
        [Description("A lobby whose session hasn't started carries no room, and nothing is joined.")]
        public async Task JoinLobbyWithTokenAsync_DoesNotJoinARoom_WhenTheSessionHasNotStarted()
        {
            RecordCalls();
            var started = false;
            Lobbies.OnPlaySessionStarted += (_, _) => started = true;

            await Lobbies.JoinLobbyWithTokenAsync(InviteToken);

            Assert.That(started, Is.False);
        }

        [Test]
        public async Task JoinLobbyWithTokenAsync_Fails_WhenNoTokenIsGiven()
        {
            var calls = RecordCalls();

            await AssertThrows<ArgumentException>(() => Lobbies.JoinLobbyWithTokenAsync(""));

            Assert.That(calls, Is.Empty);
        }

        [Test]
        [Description("Resolving hands back the lobby the token names, without joining it.")]
        public async Task ResolveLobbyFromInviteTokenAsync_ReturnsTheLobbyWithoutJoining()
        {
            var calls = RecordCalls();

            var lobby = await Lobbies.ResolveLobbyFromInviteTokenAsync(InviteToken);

            Assert.That(lobby.Id, Is.EqualTo(LobbyId));

            Assert.That(calls, Has.Count.EqualTo(1));
            Assert.That(calls[0].path, Is.EqualTo("/lobbies/invites/resolve"));
            Assert.That(calls[0].method, Is.EqualTo("POST"));
            Assert.That(CoherenceJson.DeserializeObject<ResolveInviteTokenRequest>(calls[0].body).InviteToken,
                Is.EqualTo(InviteToken));
        }

        [Test]
        [Description("Resolving is a look, not a join, so it leaves no lobby session behind.")]
        public async Task ResolveLobbyFromInviteTokenAsync_DoesNotOpenALobbySession()
        {
            RecordCalls();

            _ = await Lobbies.ResolveLobbyFromInviteTokenAsync(InviteToken);

            Assert.That(Lobbies.CachedLobbies, Is.Empty);
        }

        [Test]
        public async Task ResolveLobbyFromInviteTokenAsync_Fails_WhenNoTokenIsGiven()
        {
            var calls = RecordCalls();

            await AssertThrows<ArgumentException>(() => Lobbies.ResolveLobbyFromInviteTokenAsync(""));

            Assert.That(calls, Is.Empty);
        }

        [Test]
        [Description("Inviting a player posts their id to the invites endpoint of the lobby the sender is in.")]
        public async Task InviteAsync_PostsThePlayerToTheLobbysInvitesEndpoint()
        {
            var calls = RecordCalls();
            using var lobby = CreateLobbySession();

            await lobby.InviteAsync("p2");

            Assert.That(calls, Has.Count.EqualTo(1));
            Assert.That(calls[0].path, Is.EqualTo($"/lobbies/{LobbyId}/invites"));
            Assert.That(calls[0].method, Is.EqualTo("POST"));
            Assert.That(CoherenceJson.DeserializeObject<InviteToLobbyRequest>(calls[0].body).PlayerId,
                Is.EqualTo("p2"));
        }

        [Test]
        public async Task InviteAsync_Fails_WhenNoPlayerIsGiven()
        {
            var calls = RecordCalls();
            using var lobby = CreateLobbySession();

            await AssertThrows<ArgumentException>(() => lobby.InviteAsync(PlayerAccountId.None));

            Assert.That(calls, Is.Empty);
        }

        [Test]
        [Description("The invite push is handed over with the inviter's display data and the lobby it names.")]
        public void OnLobbyInviteReceived_Fires_WhenTheInvitePushArrives()
        {
            var received = ReceiveInvite(InvitePush(withToken: true));
            Assert.That(received.Inviter.Id, Is.EqualTo((PlayerAccountId)"p2"));
            Assert.That(received.Inviter.DisplayName, Is.EqualTo("Bob"));
            Assert.That(received.Lobby.Id, Is.EqualTo(LobbyId));
            Assert.That(received.InviteToken, Is.EqualTo(InviteToken));

            // Taken from the lobby's own roster, so it is the whole player and not just what the push named.
            Assert.That(received.Inviter.Attributes, Has.Count.EqualTo(1));
            Assert.That(received.Inviter.Attributes[0].Key, Is.EqualTo("hat"));
        }

        [Test]
        [Description("A roster that arrived without the inviter still names them, from what the push carried.")]
        public void OnLobbyInviteReceived_FallsBackToThePushedInviter_WhenTheRosterOmitsThem()
        {
            var received = ReceiveInvite(InvitePush(withToken: true, withInviterInRoster: false));
            Assert.That(received.Inviter.Id, Is.EqualTo((PlayerAccountId)"p2"));
            Assert.That(received.Inviter.Username, Is.EqualTo("bob"));
            Assert.That(received.Inviter.DisplayName, Is.EqualTo("Bob"));
            Assert.That(received.Inviter.Attributes, Is.Empty);
        }

        [Test]
        [Description("A push carrying no roster at all is not a crash: the inviter is what the push named.")]
        public void OnLobbyInviteReceived_FallsBackToThePushedInviter_WhenThereIsNoRoster()
        {
            var received = ReceiveInvite(
                "{\"inviter\":{\"player_id\":\"p2\",\"username\":\"bob\",\"display_name\":\"Bob\"}," +
                "\"lobby\":{\"id\":\"" + LobbyId + "\"},\"invite_token\":\"" + InviteToken + "\"}");
            Assert.That(received.Inviter.Id, Is.EqualTo((PlayerAccountId)"p2"));
            Assert.That(received.Inviter.DisplayName, Is.EqualTo("Bob"));
        }

        [Test]
        [Description("An inviter who may not create tokens sends none, and the lobby alone is enough to join.")]
        public void OnLobbyInviteReceived_HandsBackNoToken_WhenThePushCarriedNone()
        {
            var received = ReceiveInvite(InvitePush(withToken: false));
            Assert.That(received.InviteToken, Is.Null);
        }

        [Test]
        [Description("Accepting joins the lobby the invite named, presenting the token it carried.")]
        public async Task AcceptLobbyInviteAsync_JoinsTheInvitedLobbyWithItsToken()
        {
            var calls = RecordCalls();
            var invite = new LobbyInvite(Inviter(), CreateLobbyData(), InviteToken);

            var lobbySession = await Lobbies.AcceptLobbyInviteAsync(invite);

            Assert.That(lobbySession.LobbyData.Id, Is.EqualTo(LobbyId));
            Assert.That(calls, Has.Count.EqualTo(1));
            Assert.That(calls[0].path, Is.EqualTo($"/lobbies/{LobbyId}/players"));
            Assert.That(CoherenceJson.DeserializeObject<JoinLobbyRequest>(calls[0].body).InviteToken,
                Is.EqualTo(InviteToken));
        }

        [Test]
        [Description("Player attributes given when accepting an invite reach the lobby, as on any other join.")]
        public async Task AcceptLobbyInviteAsync_SendsThePlayerAttributes()
        {
            var calls = RecordCalls();
            var invite = new LobbyInvite(Inviter(), CreateLobbyData(), InviteToken);

            await Lobbies.AcceptLobbyInviteAsync(invite, new() { new CloudAttribute("rank", "gold") });

            var join = CoherenceJson.DeserializeObject<JoinLobbyRequest>(calls[0].body);

            Assert.That(join.PlayerAttributes, Has.Count.EqualTo(1));
            Assert.That(join.PlayerAttributes[0].Key, Is.EqualTo("rank"));
        }

        [Test]
        [Description("Reservations passed when accepting an invite are sent with the join.")]
        public async Task AcceptLobbyInviteAsync_SendsReservations_WhenReservationsArePassed()
        {
            var calls = RecordCalls();
            var invite = new LobbyInvite(Inviter(), CreateLobbyData(), InviteToken);

            await Lobbies.AcceptLobbyInviteAsync(invite,
                reservations: new(new PlayerAccountId[] { "p2" }, TimeSpan.FromSeconds(30)));

            var join = CoherenceJson.DeserializeObject<JoinLobbyRequest>(calls[0].body);

            Assert.That(join.InviteToken, Is.EqualTo(InviteToken));
            Assert.That(join.Reservations, Is.Not.Null);
            Assert.That(join.Reservations.Value.PlayerIds, Is.EqualTo(new List<string> { "p2" }));
        }

        [Test]
        public async Task AcceptLobbyInviteAsync_Fails_WhenNoInviteIsGiven()
        {
            var calls = RecordCalls();

            await AssertThrows<ArgumentNullException>(() => Lobbies.AcceptLobbyInviteAsync(null));

            Assert.That(calls, Is.Empty);
        }

        [Test]
        [Description("An invite to a lobby with no password carries no token, and accepting it is a plain join.")]
        public async Task AcceptLobbyInviteAsync_SendsNoToken_WhenTheInviteCarriedNone()
        {
            var calls = RecordCalls();
            var invite = new LobbyInvite(Inviter(), CreateLobbyData(), inviteToken: null);

            await Lobbies.AcceptLobbyInviteAsync(invite);

            Assert.That(calls, Has.Count.EqualTo(1));
            Assert.That(calls[0].path, Is.EqualTo($"/lobbies/{LobbyId}/players"));
            Assert.That(CoherenceJson.DeserializeObject<JoinLobbyRequest>(calls[0].body).InviteToken, Is.Null);
        }

        [Test]
        [Description("An invite into a session already running hands over a room that can be joined as it is.")]
        public void OnLobbyInviteReceived_CarriesARoomReadyToJoin_WhenTheSessionIsAlreadyRunning()
        {
            var received = ReceiveInvite(
                "{\"inviter\":{\"player_id\":\"p2\"},\"lobby\":" + LobbyBody(withRoom: true) + "}");

            Assert.That(received.Lobby.RoomData, Is.Not.Null);
            Assert.That(received.Lobby.RoomData.Value.AuthToken,
                Is.EqualTo((string)authClientBuilder.AuthClient.SessionToken));
        }

        [Test]
        [Description("A disposed service stops listening for invites, so a request factory that outlives it does not call back into it.")]
        public void Dispose_StopsListeningForInvites()
        {
            _ = Lobbies;

            lobbies.Dispose();
            lobbies = null;

            Assert.That(requestFactoryBuilder.GetPushCallbackCount("/friends/lobby-invite"), Is.Zero);
        }

        [Test]
        public async Task InviteAsync_Fails_WhenTheLobbySessionIsDisposed()
        {
            var calls = RecordCalls();
            var lobby = CreateLobbySession();
            lobby.Dispose();

            await AssertThrows<RequestException>(() => lobby.InviteAsync("p2"));

            Assert.That(calls, Is.Empty);
        }

        [Test]
        public async Task CreateInviteTokenAsync_Fails_WhenTheLobbySessionIsDisposed()
        {
            var calls = RecordCalls();
            var lobby = CreateLobbySession();
            lobby.Dispose();

            await AssertThrows<RequestException>(() => lobby.CreateInviteTokenAsync());

            Assert.That(calls, Is.Empty);
        }

        private static async Task AssertThrows<TException>(Func<Task> call) where TException : Exception
        {
            try
            {
                await call();
            }
            catch (TException)
            {
                return;
            }

            Assert.Fail($"Expected {typeof(TException).Name} was not thrown.");
        }

        // Raises the invite push and hands back what the event delivered.
        private LobbyInvite ReceiveInvite(string push)
        {
            LobbyInvite received = null;
            Lobbies.OnLobbyInviteReceived += invite => received = invite;

            requestFactoryBuilder.RaisePushCallback("/friends/lobby-invite", push);

            Assert.That(received, Is.Not.Null, "The invite push was not delivered.");

            return received;
        }

        // Records every request, answering a resolve with the lobby the token names and a join with the
        // lobby as it looks once the player is in it.
        private List<(string path, string method, string body)> RecordCalls(bool joinedWithRoom = false)
        {
            var calls = new List<(string path, string method, string body)>();
            var joined = LobbyBody(joinedWithRoom);

            requestFactoryBuilder.SendRequestAsyncReturns((basePath, pathParams, method, body, _, _, _) =>
            {
                calls.Add((basePath + pathParams, method, body));

                return Task.FromResult(pathParams switch
                {
                    "/invites/resolve" => "{\"lobby\":" + LobbyBody() + "}",
                    _ when pathParams.EndsWith("/invite-token") =>
                        "{\"invite_token\":\"" + InviteToken + "\",\"expires_at\":1786451681}",
                    _ => joined,
                });
            });

            return calls;
        }

        private LobbySession CreateLobbySession()
            => new(Lobbies, CreateLobbyData(), authClientBuilder.AuthClient, requestFactoryBuilder.RequestFactory);

        // Deserialized rather than constructed, because that is how an inviter reaches LobbyInvite.
        private static LobbyPlayer Inviter()
            => CoherenceJson.DeserializeObject<LobbyPlayer>(
                "{\"player_id\":\"p2\",\"username\":\"bob\",\"display_name\":\"Bob\"}");

        private LobbyData CreateLobbyData()
            => new() { Id = LobbyId, players = new() { new(authClientBuilder.PlayerAccountId) } };

        private string InvitePush(bool withToken, bool withInviterInRoster = true)
            => "{\"inviter\":{\"player_id\":\"p2\",\"username\":\"bob\",\"display_name\":\"Bob\"}," +
               "\"lobby\":" + LobbyBody(inviterInRoster: withInviterInRoster) + "," +
               (withToken ? "\"invite_token\":\"" + InviteToken + "\"" : "\"invite_token\":\"\"") + "}";

        private string LobbyBody(bool withRoom = false, bool inviterInRoster = false)
            => "{\"id\":\"" + LobbyId + "\",\"name\":\"probe\",\"region\":\"eu\",\"max_players\":4," +
               "\"owner_id\":\"" + authClientBuilder.PlayerAccountId + "\",\"room_id\":0," +
               (withRoom ? "\"room\":{\"id\":1},": "") +
               "\"closed\":false,\"unlisted\":false,\"private\":false,\"attributes\":[]," +
               "\"players\":[{\"player_id\":\"" + authClientBuilder.PlayerAccountId + "\"," +
               "\"id\":\"" + authClientBuilder.PlayerAccountId + "\"}" +
               (inviterInRoster
                   ? ",{\"player_id\":\"p2\",\"username\":\"bob\",\"display_name\":\"Bob\"," +
                     "\"attributes\":[{\"key\":\"hat\",\"val\":\"party\"}]}"
                   : "") +
               "],\"created_at\":1786451681}";
    }
}
