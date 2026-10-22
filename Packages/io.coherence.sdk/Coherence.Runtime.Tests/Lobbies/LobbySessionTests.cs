// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Runtime.Tests.Lobbies
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Coherence.Cloud;
    using Coherence.Tests;
    using Coherence.Utils;
    using Log;
    using NUnit.Framework;
    using Utils;

    /// <summary>
    /// Edit Mode unit tests for <see cref="LobbySession"/>.
    /// </summary>
    public sealed class LobbySessionTests : CoherenceTest
    {
        private const string LobbyId = "lobbyXYZ";
        private const string JoinPush = "/lobbies/player/join";
        private const string ReservationsPush = "/lobbies/reservations";
        private const string LeavePush = "/lobbies/player/leave";

        private MockAuthClientBuilder authClientBuilder;
        private MockRequestFactoryBuilder requestFactoryBuilder;
        private LobbiesService lobbies;
        private string lobbyResponse;
        private TaskCompletionSource<string> pendingResponse;
        private TaskCompletionSource<bool> cooldown;

        private IAuthClientInternal AuthClient => authClientBuilder.AuthClient;
        private IRequestFactoryInternal RequestFactory => requestFactoryBuilder.RequestFactory;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();

            authClientBuilder = new();
            requestFactoryBuilder = new();
            lobbyResponse = null;
            pendingResponse = null;
            cooldown = new();
            cooldown.SetResult(true);
        }

        [TearDown]
        public override void TearDown()
        {
            lobbies?.Dispose();
            lobbies = null;
            base.TearDown();
        }

        // Built on first use: the request factory mock captures its response delegate when it is built.
        private LobbiesService Lobbies => lobbies ??= CreateLobbiesService();

        [Test]
        [Description("A reservation whose expires_at is already in the past fires OnReservationExpired shortly after construction, and is removed from LobbyData.Reservations, without requiring the subscriber to have raced the constructor.")]
        public async Task OnReservationExpired_Fires_For_Already_Expired_Reservation()
        {
            var reservation = CoherenceJson.DeserializeObject<LobbyReservation>("{\"player_id\":\"p1\",\"expires_at\":1}");
            var lobbyData = CreateLobbyData(new() { reservation });
            using var session = new LobbySession(null, lobbyData, AuthClient, RequestFactory);
            var onExpiredCompletionSource = new TaskCompletionSource<LobbyReservation>(TaskCreationOptions.RunContinuationsAsynchronously);
            var updatedCount = 0;
            session.OnReservationExpired += (_, r) => onExpiredCompletionSource.TrySetResult(r);
            session.OnLobbyUpdated += _ => updatedCount++;

            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(1));
            await using var registration = timeout.Token.Register(() => onExpiredCompletionSource.TrySetCanceled(timeout.Token));

            LobbyReservation expired;

            try
            {
                expired = await onExpiredCompletionSource.Task;
            }
            catch (OperationCanceledException)
            {
                Assert.Fail("OnReservationExpired did not fire within the timeout.");
                return;
            }

            Assert.That(expired.PlayerAccountId, Is.EqualTo((PlayerAccountId)"p1"));
            Assert.That(session.LobbyData.Reservations, Is.Empty);
            Assert.That(updatedCount, Is.EqualTo(1));
        }

        [Test]
        [Description("A reservation that hasn't expired yet doesn't fire OnReservationExpired and stays in LobbyData.Reservations.")]
        public async Task OnReservationExpired_Does_Not_Fire_For_Live_Reservation()
        {
            var farFuture = DateTimeOffset.UtcNow.AddDays(1).ToUnixTimeSeconds();
            var reservation = CoherenceJson.DeserializeObject<LobbyReservation>(
                "{\"player_id\":\"p1\",\"expires_at\":" + farFuture + "}");
            var lobbyData = CreateLobbyData(new() { reservation });
            using var session = new LobbySession(null, lobbyData, AuthClient, RequestFactory);

            var fired = false;
            session.OnReservationExpired += (_, _) => fired = true;

            // Give any (incorrectly) scheduled immediate firing a chance to happen.
            await Wait.For(TimeSpan.FromMilliseconds(50));

            Assert.That(fired, Is.False);
            Assert.That(session.LobbyData.Reservations, Has.Count.EqualTo(1));
        }

        [Test]
        [Description("When the reserved player actually joins, the matching reservation is removed locally too, so a " +
            "pending expiry timer for it doesn't later fire OnReservationExpired for a slot that was " +
            "actually filled.")]
        public async Task OnPlayerJoined_Removes_Matching_Reservation_Without_Firing_OnReservationExpired()
        {
            var farFuture = DateTimeOffset.UtcNow.AddDays(1).ToUnixTimeSeconds();
            var reservation = CoherenceJson.DeserializeObject<LobbyReservation>(
                "{\"player_id\":\"p1\",\"expires_at\":" + farFuture + "}");
            var lobbyData = CreateLobbyData(new() { reservation });
            using var session = new LobbySession(null, lobbyData, AuthClient, RequestFactory);
            var expiredFired = false;
            session.OnReservationExpired += (_, _) => expiredFired = true;

            // Simulate the server's /lobbies/player/join push, as if "p1" (the reserved player) just joined.
            requestFactoryBuilder.RaisePushCallback("/lobbies/player/join", "{\"lobby_id\":\"lobbyXYZ\",\"player\":{\"id\":\"p1\"}}");

            Assert.That(session.LobbyData.Reservations, Is.Empty);
            Assert.That(session.LobbyData.Players, Has.Count.EqualTo(2));

            // Give the (now-canceled) timer a moment to prove it doesn't fire for the consumed reservation.
            await Task.Delay(50);

            Assert.That(expiredFired, Is.False);
        }

        [Test]
        [Description("Disposing a LobbySession cancels pending reservation-expiry timers so OnReservationExpired never fires afterwards, even once the reservation's expiry time passes.")]
        public async Task Dispose_Cancels_Pending_Reservation_Timers()
        {
            // expires_at has whole-second granularity, so this lands 1-2 seconds out; disposing immediately and
            // waiting past that window is what proves the timer was actually canceled, not just "not yet due".
            var soon = DateTimeOffset.UtcNow.AddSeconds(1).ToUnixTimeSeconds();
            var reservation = CoherenceJson.DeserializeObject<LobbyReservation>(
                "{\"player_id\":\"p1\",\"expires_at\":" + soon + "}");
            var lobbyData = CreateLobbyData(new() { reservation });
            var session = new LobbySession(null, lobbyData, AuthClient, RequestFactory);
            var fired = false;
            session.OnReservationExpired += (_, _) => fired = true;

            session.Dispose();

            await Task.Delay(3000);

            Assert.That(fired, Is.False);
        }

        [Test]
        [Description("A refresh can hand back a reservation this client already saw expire, as coherence Cloud's " +
            "clock may lag this one. The slot was already reported expired, so it must not be reported " +
            "again.")]
        public async Task OnReservationExpired_Fires_Once_When_A_Refresh_Returns_An_Expired_Reservation()
        {
            lobbyResponse = LobbyBody(Reservation("p1", expiresAt: 1));
            var session = await Lobbies.JoinLobbyAsync(LobbyId);
            var expiredCount = 0;
            var firstExpiry = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            session.OnReservationExpired += (_, _) =>
            {
                expiredCount++;
                firstExpiry.TrySetResult(true);
            };

            await WaitOrFail(firstExpiry.Task, "OnReservationExpired did not fire for the expired reservation.");
            await session.RefreshLobbyAsync();
            await Wait.For(TimeSpan.FromMilliseconds(100));

            Assert.That(expiredCount, Is.EqualTo(1));
            Assert.That(session.LobbyData.Reservations, Is.Empty);
        }

        [Test]
        [Description("A refresh that no longer lists a reservation (the slot was filled or released server-side) " +
            "cancels its expiry timer, so OnReservationExpired never fires for it.")]
        public async Task OnReservationExpired_Does_Not_Fire_When_A_Refresh_Drops_The_Reservation()
        {
            var soon = DateTimeOffset.UtcNow.AddSeconds(1).ToUnixTimeSeconds();
            lobbyResponse = LobbyBody(Reservation("p1", soon));
            var session = await Lobbies.JoinLobbyAsync(LobbyId);
            var fired = false;
            session.OnReservationExpired += (_, _) => fired = true;

            lobbyResponse = LobbyBody();
            await session.RefreshLobbyAsync();
            await Task.Delay(3000);

            Assert.That(fired, Is.False);
            Assert.That(session.LobbyData.Reservations, Is.Empty);
        }

        [Test]
        [Description("Each reservation expires on its own clock: an expired one is reported and removed while a live one stays.")]
        public async Task OnReservationExpired_Fires_Only_For_The_Reservation_That_Expired()
        {
            var farFuture = DateTimeOffset.UtcNow.AddDays(1).ToUnixTimeSeconds();
            var lobbyData = CreateLobbyData(new()
            {
                CoherenceJson.DeserializeObject<LobbyReservation>(Reservation("p1", expiresAt: 1)),
                CoherenceJson.DeserializeObject<LobbyReservation>(Reservation("p2", farFuture)),
            });
            using var session = new LobbySession(null, lobbyData, AuthClient, RequestFactory);
            var expired = new List<PlayerAccountId>();
            session.OnReservationExpired += (_, reservation) => expired.Add(reservation.PlayerAccountId);

            await Wait.For(TimeSpan.FromMilliseconds(100));

            Assert.That(expired, Is.EqualTo(new[] { (PlayerAccountId)"p1" }));
            Assert.That(session.LobbyData.Reservations, Has.Count.EqualTo(1));
            Assert.That(session.LobbyData.Reservations[0].PlayerAccountId, Is.EqualTo((PlayerAccountId)"p2"));
        }

        [Test]
        [Description("Refreshing a lobby updates the session the game already holds instead of opening another one behind its back, which would stay subscribed to every lobby push (and run its own reservation timers) until logout.")]
        public async Task RefreshLobbyAsync_Keeps_A_Single_LobbySession_Per_Lobby()
        {
            lobbyResponse = LobbyBody();
            var session = await Lobbies.JoinLobbyAsync(LobbyId);

            await session.RefreshLobbyAsync();
            await session.RefreshLobbyAsync();
            await Lobbies.RefreshLobbyAsync(LobbyId);

            Assert.That(requestFactoryBuilder.GetPushCallbackCount(JoinPush), Is.EqualTo(1));
            Assert.That(await Lobbies.GetActiveLobbySessionForLobbyId(LobbyId), Is.SameAs(session));
        }

        [Test]
        [Description("Joining a lobby the player is already in hands back the session the game already holds, so its event subscriptions keep working.")]
        public async Task JoinLobbyAsync_Returns_The_Existing_LobbySession_When_Already_In_The_Lobby()
        {
            lobbyResponse = LobbyBody();
            var first = await Lobbies.JoinLobbyAsync(LobbyId);

            var second = await Lobbies.JoinLobbyAsync(LobbyId);

            Assert.That(second, Is.SameAs(first));
            Assert.That(first.IsDisposed, Is.False);
            Assert.That(requestFactoryBuilder.GetPushCallbackCount(JoinPush), Is.EqualTo(1));
        }

        [Test]
        [Description("Reserving slots sends only the reservations, and the session shows them afterwards.")]
        public async Task ReserveSlotsAsync_SendsReservationsAndUpdatesLobbyData_WhenCalledByMember()
        {
            lobbyResponse = LobbyBody();
            var session = await Lobbies.JoinLobbyAsync(LobbyId);
            var expiresAt = DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeSeconds();
            lobbyResponse = LobbyBody(Reservation("p2", expiresAt), Reservation("p3", expiresAt));

            await session.ReserveSlotsAsync(new(new PlayerAccountId[] { "p2", "p3" }, TimeSpan.FromSeconds(60)));

            var sentRequest = requestFactoryBuilder.SendRequestAsyncWasCalledWith;
            var request = CoherenceJson.DeserializeObject<JoinLobbyRequest>(sentRequest.body);

            Assert.That(sentRequest.pathParams, Is.EqualTo($"/{LobbyId}/players"));
            Assert.That(request.Reservations, Is.Not.Null);
            Assert.That(request.Reservations.Value.PlayerIds, Is.EqualTo(new List<string> { "p2", "p3" }));
            Assert.That(request.Reservations.Value.TotalSeconds, Is.EqualTo(60));
            Assert.That(request.PlayerAttributes, Is.Null);
            Assert.That(session.LobbyData.Reservations, Has.Count.EqualTo(2));
        }

        [Test]
        [Description("Reserving slots in a lobby whose game already started doesn't start the play session again.")]
        public async Task ReserveSlotsAsync_DoesNotStartPlaySession_WhenRoomAlreadyStarted()
        {
            lobbyResponse = LobbyBody().Replace("\"created_at\"", "\"room_id\":1,\"room\":{\"id\":1},\"created_at\"");
            var session = await Lobbies.JoinLobbyAsync(LobbyId);
            var started = 0;
            Lobbies.OnPlaySessionStarted += (_, _) => started++;

            await session.ReserveSlotsAsync(new(new PlayerAccountId[] { "p2" }, TimeSpan.FromSeconds(60)));

            Assert.That(started, Is.Zero);
        }

        [Test]
        public async Task ReserveSlotsAsync_ThrowsArgumentException_WhenNoPlayerIsGiven()
        {
            lobbyResponse = LobbyBody();
            var session = await Lobbies.JoinLobbyAsync(LobbyId);

            try
            {
                await session.ReserveSlotsAsync(new(Array.Empty<PlayerAccountId>(), TimeSpan.FromSeconds(60)));
                Assert.Fail("Expected an ArgumentException.");
            }
            catch (ArgumentException) { }

            Assert.That(requestFactoryBuilder.SendRequestAsyncWasCalledWith.body, Does.Not.Contain("reservations"),
                "Nothing should be sent after the join.");
        }

        [Test]
        [Description("A reservations push replaces the session's reservations and raises OnLobbyUpdated.")]
        public async Task ReservationsPush_ReplacesReservationsAndRaisesOnLobbyUpdated_WhenForThisLobby()
        {
            lobbyResponse = LobbyBody();
            var session = await Lobbies.JoinLobbyAsync(LobbyId);
            var updated = 0;
            session.OnLobbyUpdated += _ => updated++;
            var expiresAt = DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeSeconds();

            requestFactoryBuilder.RaisePushCallback(ReservationsPush,
                ReservationsBody(Reservation("p2", expiresAt), Reservation("p3", expiresAt)), resumeId: "7");

            Assert.That(session.LobbyData.Reservations, Has.Count.EqualTo(2));
            Assert.That(updated, Is.EqualTo(1));
        }

        [Test]
        [Description("A reservation missing from a push doesn't fire OnReservationExpired later.")]
        public async Task ReservationsPush_CancelsReservationExpiry_WhenHoldIsDropped()
        {
            var soon = DateTimeOffset.UtcNow.AddSeconds(1).ToUnixTimeSeconds();
            lobbyResponse = LobbyBody(Reservation("p1", soon));
            var session = await Lobbies.JoinLobbyAsync(LobbyId);
            var fired = false;
            session.OnReservationExpired += (_, _) => fired = true;

            requestFactoryBuilder.RaisePushCallback(ReservationsPush, ReservationsBody(), resumeId: "7");
            await Task.Delay(1500);

            Assert.That(session.LobbyData.Reservations, Is.Empty);
            Assert.That(fired, Is.False);
        }

        [Test]
        [Description("A reservation that arrives in a push fires OnReservationExpired when it expires.")]
        public async Task ReservationsPush_RaisesOnReservationExpired_WhenNewHoldExpires()
        {
            lobbyResponse = LobbyBody();
            var session = await Lobbies.JoinLobbyAsync(LobbyId);
            var expired =
                new TaskCompletionSource<LobbyReservation>(TaskCreationOptions.RunContinuationsAsynchronously);
            session.OnReservationExpired += (_, reservation) => expired.TrySetResult(reservation);

            requestFactoryBuilder.RaisePushCallback(ReservationsPush, ReservationsBody(Reservation("p2", 1)),
                resumeId: "7");

            await WaitOrFail(expired.Task, "OnReservationExpired did not fire for the pushed hold.");
            Assert.That(expired.Task.Result.PlayerAccountId, Is.EqualTo((PlayerAccountId)"p2"));
        }

        [Test]
        [Description("A push with a lower resume_id than the last one applied is ignored, comparing as numbers.")]
        public async Task ReservationsPush_IsIgnored_WhenOlderThanLastApplied()
        {
            lobbyResponse = LobbyBody();
            var session = await Lobbies.JoinLobbyAsync(LobbyId);
            var expiresAt = DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeSeconds();

            requestFactoryBuilder.RaisePushCallback(ReservationsPush, ReservationsBody(), resumeId: "10");
            requestFactoryBuilder.RaisePushCallback(ReservationsPush,
                ReservationsBody(Reservation("p2", expiresAt)), resumeId: "9");

            Assert.That(session.LobbyData.Reservations, Is.Empty);
        }

        [Test]
        [Description("A push without a resume_id is applied.")]
        public async Task ReservationsPush_IsApplied_WhenItHasNoResumeId()
        {
            lobbyResponse = LobbyBody();
            var session = await Lobbies.JoinLobbyAsync(LobbyId);
            var expiresAt = DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeSeconds();

            requestFactoryBuilder.RaisePushCallback(ReservationsPush, ReservationsBody(), resumeId: "10");
            requestFactoryBuilder.RaisePushCallback(ReservationsPush, ReservationsBody(Reservation("p2", expiresAt)));

            Assert.That(session.LobbyData.Reservations, Has.Count.EqualTo(1));
        }

        [Test]
        [Description("A push for another lobby is ignored.")]
        public async Task ReservationsPush_IsIgnored_WhenForAnotherLobby()
        {
            lobbyResponse = LobbyBody();
            var session = await Lobbies.JoinLobbyAsync(LobbyId);
            var expiresAt = DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeSeconds();

            requestFactoryBuilder.RaisePushCallback(ReservationsPush,
                ReservationsBodyFor("other", Reservation("p2", expiresAt)), resumeId: "7");

            Assert.That(session.LobbyData.Reservations, Is.Empty);
        }

        [Test]
        [Description("A push older than a player-joined push can still list that player's reservation. Their slot " +
            "is filled, so it must not be reported as expired.")]
        public async Task ReservationsPush_DoesNotReportExpiry_ForAPlayerAlreadyInTheLobby()
        {
            lobbyResponse = LobbyBody();
            var session = await Lobbies.JoinLobbyAsync(LobbyId);
            var expired = false;
            session.OnReservationExpired += (_, _) => expired = true;
            RaisePlayerJoined("p2");

            requestFactoryBuilder.RaisePushCallback(ReservationsPush, ReservationsBody(Reservation("p2", 1)),
                resumeId: "5");
            await Task.Delay(100);

            Assert.That(expired, Is.False);
            Assert.That(session.LobbyData.Reservations, Is.Empty);
        }

        [Test]
        [Description("A push can reserve a slot for a player who left, before the older player-left push arrives. " +
            "The reservation is valid and must be kept.")]
        public async Task ReservationsPush_KeepsTheReservation_WhenItArrivesBeforeAnOlderPlayerLeft()
        {
            lobbyResponse = LobbyBody();
            var session = await Lobbies.JoinLobbyAsync(LobbyId);
            RaisePlayerJoined("p2");
            var expiresAt = DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeSeconds();

            requestFactoryBuilder.RaisePushCallback(ReservationsPush, ReservationsBody(Reservation("p2", expiresAt)),
                resumeId: "7");
            requestFactoryBuilder.RaisePushCallback(LeavePush,
                "{\"lobby_id\":\"" + LobbyId + "\",\"player_id\":\"p2\",\"reason\":\"left\"}");

            Assert.That(session.LobbyData.Reservations, Has.Count.EqualTo(1));
            Assert.That(session.LobbyData.Reservations[0].PlayerAccountId, Is.EqualTo((PlayerAccountId)"p2"));
        }

        [Test]
        [Description("A player-joined push older than the last snapshot is already accounted for by it, so it must " +
            "not drop a reservation that snapshot made after the player left.")]
        public async Task PlayerJoinedPush_KeepsTheReservation_WhenOlderThanTheLastSnapshot()
        {
            lobbyResponse = LobbyBody();
            var session = await Lobbies.JoinLobbyAsync(LobbyId);
            var expiresAt = DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeSeconds();

            // Saved as: p2 joins (6), p2 leaves (7), p2 is reserved again (8). Delivered as 8, 6, 7.
            requestFactoryBuilder.RaisePushCallback(ReservationsPush, ReservationsBody(Reservation("p2", expiresAt)),
                resumeId: "8");
            RaisePlayerJoined("p2", resumeId: "6");
            requestFactoryBuilder.RaisePushCallback(LeavePush,
                "{\"lobby_id\":\"" + LobbyId + "\",\"player_id\":\"p2\",\"reason\":\"left\"}", resumeId: "7");

            Assert.That(session.LobbyData.Reservations, Has.Count.EqualTo(1));
            Assert.That(session.LobbyData.Reservations[0].PlayerAccountId, Is.EqualTo((PlayerAccountId)"p2"));
        }

        [Test]
        [Description("A player-joined push newer than the last snapshot consumed the player's reservation.")]
        public async Task PlayerJoinedPush_DropsTheReservation_WhenNewerThanTheLastSnapshot()
        {
            lobbyResponse = LobbyBody();
            var session = await Lobbies.JoinLobbyAsync(LobbyId);
            var expiresAt = DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeSeconds();
            requestFactoryBuilder.RaisePushCallback(ReservationsPush, ReservationsBody(Reservation("p2", expiresAt)),
                resumeId: "8");

            RaisePlayerJoined("p2", resumeId: "9");

            Assert.That(session.LobbyData.Reservations, Is.Empty);
        }

        private void RaisePlayerJoined(string playerId, string resumeId = null)
            => requestFactoryBuilder.RaisePushCallback(JoinPush,
                "{\"lobby_id\":\"" + LobbyId + "\",\"player\":{\"player_id\":\"" + playerId + "\"}}",
                resumeId);

        [Test]
        [Description("Disposing the session while slots are being reserved throws, and doesn't reopen the session.")]
        public async Task ReserveSlotsAsync_ThrowsObjectDisposedException_WhenDisposedWhileInFlight()
        {
            lobbyResponse = LobbyBody();
            var session = await Lobbies.JoinLobbyAsync(LobbyId);
            pendingResponse = new(TaskCreationOptions.RunContinuationsAsynchronously);
            var reserveTask = session.ReserveSlotsAsync(new(new PlayerAccountId[] { "p2" }, TimeSpan.FromSeconds(60)));

            session.Dispose();
            pendingResponse.SetResult(LobbyBody());

            try
            {
                await reserveTask;
                Assert.Fail("Expected an ObjectDisposedException.");
            }
            catch (ObjectDisposedException) { }

            Assert.That(requestFactoryBuilder.GetPushCallbackCount(JoinPush), Is.Zero);
        }

        [Test]
        [Description("Reserving slots in a session that was already disposed throws ObjectDisposedException.")]
        public async Task ReserveSlotsAsync_ThrowsObjectDisposedException_WhenAlreadyDisposed()
        {
            lobbyResponse = LobbyBody();
            var session = await Lobbies.JoinLobbyAsync(LobbyId);
            session.Dispose();

            try
            {
                await session.ReserveSlotsAsync(new(new PlayerAccountId[] { "p2" }, TimeSpan.FromSeconds(60)));
                Assert.Fail("Expected an ObjectDisposedException.");
            }
            catch (ObjectDisposedException) { }
        }

        [Test]
        [Description("A joined lobby that can't be applied to a session is reported as an invalid response.")]
        public async Task JoinLobbyAsync_ThrowsResponseDeserializationException_WhenTheLobbyCannotBeApplied()
        {
            lobbyResponse = "{\"id\":\"" + LobbyId + "\"}";

            try
            {
                await Lobbies.JoinLobbyAsync(LobbyId);
                Assert.Fail("Expected a ResponseDeserializationException.");
            }
            catch (ResponseDeserializationException exception)
            {
                Assert.That(exception.ErrorCode, Is.EqualTo(Result.InvalidResponse));
            }

            Assert.That(logger.GetCountForErrorID(Error.RuntimeCloudDeserializationException), Is.EqualTo(1));
        }

        [Test]
        [Description("A player reserved again with a later expiry keeps their reservation past the earlier one.")]
        public async Task ReservationsPush_KeepsTheReservation_WhenThePlayerIsReservedAgainWithALaterExpiry()
        {
            var soon = DateTimeOffset.UtcNow.AddSeconds(1).ToUnixTimeSeconds();
            lobbyResponse = LobbyBody(Reservation("p2", soon));
            var session = await Lobbies.JoinLobbyAsync(LobbyId);
            var fired = false;
            session.OnReservationExpired += (_, _) => fired = true;
            var later = DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeSeconds();

            requestFactoryBuilder.RaisePushCallback(ReservationsPush, ReservationsBody(Reservation("p2", later)),
                resumeId: "7");
            await Task.Delay(1500);

            Assert.That(fired, Is.False);
            Assert.That(session.LobbyData.Reservations, Has.Count.EqualTo(1));
            Assert.That(session.LobbyData.Reservations[0].ExpiresAt.ToUnixTimeSeconds(), Is.EqualTo(later));
        }

        [Test]
        [Description("Reserving slots goes through the join endpoint, so leaving while it waits for the request " +
            "cooldown must not send it: that would seat the player again.")]
        public async Task ReserveSlotsAsync_SendsNothing_WhenDisposedWhileWaitingForTheCooldown()
        {
            cooldown = new();
            using var session = new LobbySession(Lobbies, CreateLobbyData(new()), AuthClient, RequestFactory);
            var reserveTask = session.ReserveSlotsAsync(new(new PlayerAccountId[] { "p2" }, TimeSpan.FromSeconds(60)));

            session.Dispose();
            cooldown.TrySetResult(true);

            try
            {
                await reserveTask;
                Assert.Fail("Expected an ObjectDisposedException.");
            }
            catch (ObjectDisposedException) { }

            Assert.That(requestFactoryBuilder.SendRequestAsyncWasCalled, Is.False);
        }

        [Test]
        [Description("A session for a lobby the player isn't in disposes itself, and must not stay subscribed.")]
        public void Constructor_LeavesNoPushSubscriptions_WhenThePlayerIsNotInTheLobby()
        {
            var lobbyData = new LobbyData { Id = LobbyId, MaxPlayers = 4, players = new() };

            using var session = new LobbySession(null, lobbyData, AuthClient, RequestFactory);

            Assert.That(session.IsDisposed, Is.True);
            Assert.That(requestFactoryBuilder.GetPushCallbackCount(JoinPush), Is.Zero);
            Assert.That(requestFactoryBuilder.GetPushCallbackCount(ReservationsPush), Is.Zero);
        }

        [Test]
        [Description("A refresh showing the player left the lobby disposes their session, instead of dropping it " +
            "while it stays subscribed until it's collected and reported as a leak.")]
        public async Task RefreshLobbyAsync_DisposesTheSession_WhenThePlayerIsNoLongerInTheLobby()
        {
            lobbyResponse = LobbyBody();
            var session = await Lobbies.JoinLobbyAsync(LobbyId);
            lobbyResponse = LobbyBodyWithout();

            await Lobbies.RefreshLobbyAsync(LobbyId);

            Assert.That(session.IsDisposed, Is.True);
            Assert.That(session.MyPlayer, Is.Null, "The session should hold the lobby the player left.");
            Assert.That(requestFactoryBuilder.GetPushCallbackCount(JoinPush), Is.Zero);
            Assert.That(Lobbies.GetLobbySessionIds(), Is.Empty);
        }

        [Test]
        [Description("Refreshing a session the player was removed from disposes it, holding the lobby it left.")]
        public async Task SessionRefreshLobbyAsync_DisposesTheSessionWithCurrentData_WhenThePlayerIsNoLongerInTheLobby()
        {
            lobbyResponse = LobbyBody();
            var session = await Lobbies.JoinLobbyAsync(LobbyId);
            lobbyResponse = LobbyBodyWithout();

            await session.RefreshLobbyAsync();

            Assert.That(session.IsDisposed, Is.True);
            Assert.That(session.MyPlayer, Is.Null);
            Assert.That(session.LobbyData.Players, Has.Count.EqualTo(1));
        }

        [Test]
        [Description("A throwing OnLobbyDisposed handler is logged, and doesn't turn a valid refresh into a failure.")]
        public async Task RefreshLobbyAsync_LogsTheHandlerException_WhenOnLobbyDisposedThrows()
        {
            lobbyResponse = LobbyBody();
            var session = await Lobbies.JoinLobbyAsync(LobbyId);
            session.OnLobbyDisposed += _ => throw new InvalidOperationException("handler");
            lobbyResponse = LobbyBodyWithout();

            await Lobbies.RefreshLobbyAsync(LobbyId);

            Assert.That(session.IsDisposed, Is.True);
            Assert.That(logger.GetCountForErrorID(Error.RuntimeCloudLobbyDisposedHandlerException), Is.EqualTo(1));
            Assert.That(Lobbies.GetLobbySessionIds(), Is.Empty);
        }

        [Test]
        [Description("Logging in again disposes the sessions for lobbies the player is no longer in.")]
        public async Task OnLogin_DisposesSessions_ForLobbiesThePlayerIsNoLongerIn()
        {
            lobbyResponse = LobbyBody();
            var session = await Lobbies.JoinLobbyAsync(LobbyId);

            authClientBuilder.RaiseOnLogin(new LoginResponse { LobbyIds = new List<string> { "another-lobby" } });

            Assert.That(session.IsDisposed, Is.True);
            Assert.That(requestFactoryBuilder.GetPushCallbackCount(JoinPush), Is.Zero);
            Assert.That(Lobbies.GetLobbySessionIds(), Is.EquivalentTo(new[] { "another-lobby" }));
        }

        [Test]
        [Description("Logging in again, such as after a reconnect, keeps the sessions for lobbies the player is " +
            "still in, so the game's references to them keep working.")]
        public async Task OnLogin_KeepsSessions_ForLobbiesThePlayerIsStillIn()
        {
            lobbyResponse = LobbyBody();
            var session = await Lobbies.JoinLobbyAsync(LobbyId);

            authClientBuilder.RaiseOnLogin(new LoginResponse { LobbyIds = new List<string> { LobbyId } });

            Assert.That(session.IsDisposed, Is.False);
            Assert.That(Lobbies.GetLobbySessionIds(), Is.EquivalentTo(new[] { LobbyId }));
            Assert.That(await Lobbies.GetActiveLobbySessionForLobbyId(LobbyId), Is.SameAs(session));
        }

        [Test]
        [Description("A login response without a lobby list reads as the player being in none, so every session " +
            "is disposed.")]
        public async Task OnLogin_DisposesSessions_WhenTheLoginResponseListsNoLobbies()
        {
            lobbyResponse = LobbyBody();
            var session = await Lobbies.JoinLobbyAsync(LobbyId);

            authClientBuilder.RaiseOnLogin(new LoginResponse { LobbyIds = null });

            Assert.That(session.IsDisposed, Is.True);
            Assert.That(Lobbies.GetLobbySessionIds(), Is.Empty);
        }

        [Test]
        [Description("A session whose constructor throws is never handed out, so nobody could dispose it: it undoes " +
            "its own subscriptions.")]
        public void Constructor_RemovesItsPushCallbacks_WhenSubscribingThrows()
        {
            requestFactoryBuilder.AddPushCallbackThrows(ReservationsPush);

            try
            {
                _ = new LobbySession(null, CreateLobbyData(new()), AuthClient, RequestFactory);
                Assert.Fail("Expected an InvalidOperationException.");
            }
            catch (InvalidOperationException) { }

            Assert.That(requestFactoryBuilder.GetPushCallbackCount(JoinPush), Is.Zero);
        }

        [Test]
        [Description("A valid response is read fine even if creating its session fails. That failure surfaces as " +
            "itself, not as a deserialization error.")]
        public async Task RefreshLobbyAsync_ThrowsTheSessionError_WhenCreatingTheSessionFails()
        {
            requestFactoryBuilder.AddPushCallbackThrows(ReservationsPush);
            lobbyResponse = LobbyBody();

            try
            {
                await Lobbies.RefreshLobbyAsync(LobbyId);
                Assert.Fail("Expected an InvalidOperationException.");
            }
            catch (InvalidOperationException) { }

            Assert.That(logger.GetCountForErrorID(Error.RuntimeCloudDeserializationException), Is.Zero);
            Assert.That(requestFactoryBuilder.GetPushCallbackCount(JoinPush), Is.Zero);
        }

        private string LobbyBodyWithout()
            => "{\"id\":\"" + LobbyId + "\",\"name\":\"probe\",\"max_players\":4," +
               "\"players\":[{\"player_id\":\"someone-else\"}],\"created_at\":1786451681}";

        private static string ReservationsBody(params string[] reservations)
            => ReservationsBodyFor(LobbyId, reservations);

        private static string ReservationsBodyFor(string lobbyId, params string[] reservations)
            => "{\"lobby_id\":\"" + lobbyId + "\",\"reservations\":[" + string.Join(",", reservations) + "]}";

        private static async Task WaitOrFail(Task task, string message)
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(1));
            var timedOut = new TaskCompletionSource<bool>();
            await using var registration = timeout.Token.Register(() => timedOut.TrySetResult(true));

            if (await Task.WhenAny(task, timedOut.Task) != task)
            {
                Assert.Fail(message);
            }
        }

        private LobbiesService CreateLobbiesService()
        {
            requestFactoryBuilder.SendRequestAsyncReturns((_, _, _, _, _, _, _) =>
                pendingResponse?.Task ?? Task.FromResult(lobbyResponse));
            return new(new(AuthClient, RequestFactory), new MockRuntimeSettingsBuilder().RuntimeSettings,
                playerAccountProvider: null, new FakeRequestThrottle(cooldown), logger);
        }

        private static string Reservation(string playerId, long expiresAt)
            => "{\"player_id\":\"" + playerId + "\",\"expires_at\":" + expiresAt + "}";

        private string LobbyBody(params string[] reservations)
            => "{\"id\":\"" + LobbyId + "\",\"name\":\"probe\",\"max_players\":4," +
               "\"owner_id\":\"" + authClientBuilder.PlayerAccountId + "\"," +
               "\"players\":[{\"player_id\":\"" + authClientBuilder.PlayerAccountId + "\"}]," +
               "\"reservations\":[" + string.Join(",", reservations) + "],\"created_at\":1786451681}";

        private LobbyData CreateLobbyData(List<LobbyReservation> reservations) => new()
        {
            Id = LobbyId,
            MaxPlayers = 4,
            players = new() { new(authClientBuilder.PlayerAccountId) },
            reservations = reservations,
        };
    }
}
