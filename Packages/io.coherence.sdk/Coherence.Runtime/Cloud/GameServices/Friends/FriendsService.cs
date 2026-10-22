// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Cloud
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Diagnostics.CodeAnalysis;
    using System.Threading;
    using System.Threading.Tasks;
    using Common;
    using Log;
    using Runtime;
    using Utils;
    using Logger = Log.Logger;

    /// <summary>Manages friend lists from different platforms, stored in coherence Cloud.</summary>
    /// <remarks>
    /// <para>Reach it through <see cref="CloudService.Friends">PlayerAccount.Main.Services.Friends</see>.</para>
    /// <para>
    /// Read the friend list from the platform(s) yourself (Game Center, Steam, etc).
    /// Then, hand the ids to <see cref="SyncFriendsAsync"/>.
    /// coherence keeps the ones that belong to players of your game and drops the rest.
    /// </para>
    /// <para>
    /// Friendships go both ways: a player only appears in <see cref="Friends"/>
    /// once both sides have synced each other.
    /// coherence only mirrors external friend lists;
    /// players add/delete friends in the platform's own UI,
    /// and you have to sync the changes to Cloud afterwards.
    /// </para>
    /// <para>
    /// Logging in does not fetch friends. Call <see cref="SyncFriendsAsync"/>,
    /// or <see cref="RefreshFriendsAsync"/> if you only want to read the list.
    /// From then on coherence keeps the list current while the player is connected,
    /// raising <see cref="OnFriendAdded"/>, <see cref="OnFriendRemoved"/> and
    /// <see cref="OnFriendPresenceChanged"/> as changes come in.
    /// </para>
    /// </remarks>
    /// <example><code source="Runtime/Friends/FriendsList.cs" language="csharp" region="Sample"/></example>
    /// <seealso cref="LobbySession.InviteAsync"/>
    /// <seealso cref="LobbiesService.OnLobbyInviteReceived"/>
    public sealed class FriendsService : IDisposable
    {
        internal const string FriendsEndpoint = "/friends";
        private const string FriendsUpdatePush = FriendsEndpoint + "/update";
        private const string FriendsPresencePush = FriendsEndpoint + "/presence";
        private const string PresenceEndpoint = "/presence";

        /// <summary>The list of friends for the logged in player account.</summary>
        /// <remarks>
        /// Empty until the first <see cref="SyncFriendsAsync"/> or <see cref="RefreshFriendsAsync"/> finishes.
        /// </remarks>
        public ReadOnlyCollection<Friend> Friends { get; }

        /// <summary>Whether the <see cref="CloudService"/> that owns this service has been disposed.</summary>
        /// <remarks>
        /// Once disposed, the friend list is empty and calling
        /// <see cref="SyncFriendsAsync"/> or <see cref="RefreshFriendsAsync"/> throws.
        /// Check this if your game can outlive the player account it logged in with.
        /// </remarks>
        public bool IsDisposed => disposalCts.IsCancellationRequested;

        /// <summary>Raised when a new friend is acknowledged.</summary>
        /// <remarks>For a friendship to be acknowledged, both player accounts need to claim each other.</remarks>
        public event Action<Friend> OnFriendAdded;

        /// <summary>Raised when a friend is no longer.</summary>
        /// <remarks>
        /// A friend is removed when either side dropped them from their synced list.
        /// The friend is handed over as it was last known,
        /// so their name is still there to show in a message.
        /// It is already gone from <see cref="Friends"/> by then.
        /// Not raised when the player logs out, when the whole list is dropped at once.
        /// </remarks>
        public event Action<Friend> OnFriendRemoved;

        /// <summary>Raised when a friend comes or goes, or changes what they are up to.</summary>
        /// <remarks>
        /// <para>
        /// Hands over the friend as they are now, already updated in <see cref="Friends"/>.
        /// Read <see cref="Friend.IsOnline"/>, <see cref="Friend.Presence"/> and
        /// <see cref="Friend.LobbyIds"/> off them rather than keeping track of what changed.
        /// </para>
        /// <para>
        /// Not raised for somebody who is not a friend yet.
        /// They arrive through <see cref="OnFriendAdded"/>, already carrying all of the above.
        /// </para>
        /// </remarks>
        /// <example><code source="Runtime/Friends/FriendsPresence.cs" language="csharp" region="Sample"/></example>
        public event Action<Friend> OnFriendPresenceChanged;

        private readonly IRequestFactory requestFactory;
        private readonly IAuthClientInternal authClient;
        private readonly RequestThrottle throttle;
        private readonly Logger logger;
        private readonly bool shouldDisposeLogger;
        private static readonly List<Friend> NoFriends = new(0);

        private readonly List<Friend> friends = new();

        /// <summary>Cancelled once the service is disposed.</summary>
        /// <remarks>
        /// Never disposed itself: its token is still read after that point, and a source with no timer
        /// or wait handle has nothing to release.
        /// </remarks>
        private readonly CancellationTokenSource disposalCts = new();

        /// <summary>Requests in flight, so a teardown that asked for them can wait them out.</summary>
        private int running;

        private bool disposing;

        /// <summary>
        /// The presence last set through <see cref="SetPresenceAsync"/>, to put back up after a reconnect.
        /// </summary>
        /// <remarks>It belongs to the connection that set it, and a fresh connection starts without one.</remarks>
        [MaybeNull] private string lastPresence;

        private int presenceWrites;

        /// <summary>The newest of <see cref="presenceWrites"/> that coherence Cloud accepted.</summary>
        /// <remarks>
        /// A newer write that fails leaves the older one in place, so that one is what a reconnect puts back up.
        /// </remarks>
        private int landedPresenceWrite;

        /// <summary>Whether a friend list was ever uploaded and has been loaded once.</summary>
        /// <remarks>
        /// Different from <see cref="hasFetched"/>, which determines
        /// when the list is new enough to raise events for.
        /// </remarks>
        private bool hasSynced;

        /// <summary>Whether the friend list was ever read.</summary>
        /// <remarks>
        /// A game that never asked for one is not handed one behind its back when the connection comes back.
        /// </remarks>
        private bool hasFetched;

        /// <summary>Cancelled when a connection ends.</summary>
        /// <remarks>
        /// Whatever the socket before it was still waiting on gives up rather than landing on top of the
        /// connection that replaced it. Replaced rather than disposed, for the same reason as
        /// <see cref="disposalCts"/>.
        /// </remarks>
        private CancellationTokenSource connectionCts = new();

        /// <summary>Cancelled when the player logs out.</summary>
        /// <remarks>
        /// A catch-up nobody asked for cannot land afterwards and refill the list for a player who is
        /// already gone. Replaced rather than disposed, for the same reason as
        /// <see cref="disposalCts"/>.
        /// </remarks>
        private CancellationTokenSource loginCts = new();

        internal FriendsService(CloudCredentialsPair credentialsPair, [MaybeNull] RequestThrottle throttle = null, [MaybeNull] Logger logger = null)
        {
            Friends = new ReadOnlyCollection<Friend>(friends);
            shouldDisposeLogger = logger is null;
            this.logger = logger ?? Log.GetLogger<FriendsService>();
            requestFactory = credentialsPair.requestFactory;
            authClient = credentialsPair.authClient;
            this.throttle = throttle ?? credentialsPair.requestFactory.Throttle;

            requestFactory.AddPushCallback(FriendsUpdatePush, OnFriendsUpdated);
            requestFactory.AddPushCallback(FriendsPresencePush, OnFriendPresenceUpdated);
            requestFactory.OnWebSocketConnect += OnWebSocketConnect;
            authClient.OnLogout += OnLogout;
        }

        /// <summary>Uploads the player's current friends from a given platform.</summary>
        /// <param name="source">
        /// The platform the ids come from, such as <c>"gamecenter"</c>, <c>"psn"</c>,
        /// <c>"nintendo"</c>, <c>"xbox"</c>, <c>"steam"</c>, <c>"epic"</c>,
        /// or <c>"custom"</c>.
        /// </param>
        /// <param name="friendIds">
        /// The friends' ids on that platform, as the platform gives them to you.
        /// coherence Cloud currently accepts up to 2000 ids, each up to 64 characters long.
        /// </param>
        /// <param name="cancellationToken">Cancels the request.</param>
        /// <returns>How many of the uploaded friends play your game, and how many were dropped.</returns>
        /// <remarks>
        /// <para>Syncing the friends list through this method replaces whatever was synced before.</para>
        /// <para>
        /// Call this method only after logging in,
        /// and again whenever the platform tells you the friend list changed.
        /// Each platform is synced on its own, so for example,
        /// syncing Steam never disturbs friends synced from Game Center.
        /// </para>
        /// <para>
        /// Passing an empty list is how you remove every friend synced from that platform,
        /// which you can do when the player revokes access to their friend list.
        /// If you could not read the list at all, do not sync through this method;
        /// leave the friends already synced from it alone.
        /// </para>
        /// <para>
        /// The friend list is refreshed before the returned request finishes,
        /// so <see cref="Friends"/> is up to date by then.
        /// </para>
        /// <para>
        /// The request fails without a <paramref name="source"/>, with a <see langword="null"/>
        /// <paramref name="friendIds"/>, or when coherence Cloud turns it down:
        /// <see cref="ErrorCode.FriendsRateLimited"/> for too many syncs in a row (currently over 10 a minute),
        /// <see cref="ErrorCode.FriendsUnsupportedSource"/> for a platform it does not accept,
        /// or <see cref="ErrorCode.PlayerSessionRequired"/> when no player is logged in.
        /// It also fails when the answer coherence Cloud sends cannot be read.
        /// Check <c>HasFailed</c> or use <c>OnFail</c> to see which.
        /// </para>
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        /// This service has been disposed.
        /// Thrown at the call site rather than reported through the returned request.
        /// </exception>
        /// <example><code source="Runtime/Friends/FriendsSync.cs" language="csharp" region="Sample"/></example>
        public FriendsOperation<FriendsSyncResult> SyncFriendsAsync([DisallowNull] string source, [DisallowNull] IReadOnlyList<string> friendIds, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            return new(Running(SyncInternalAsync(source, friendIds, cancellationToken)), cancellationToken);
        }

        /// <summary>Fetches the player's friends from coherence Cloud, updating <see cref="Friends"/>.</summary>
        /// <param name="cancellationToken">Cancels the request.</param>
        /// <remarks>
        /// <see cref="SyncFriendsAsync"/> already leaves the list up to date.
        /// Use this to read the list without syncing,
        /// or after the player was away long enough that you would rather be sure.
        /// <para>
        /// Changes acknowledged when running this method will trigger <see cref="OnFriendRemoved"/>,
        /// <see cref="OnFriendAdded"/> and <see cref="OnFriendPresenceChanged"/> events.
        /// </para>
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        /// This service has been disposed.
        /// Thrown at the call site rather than reported through the returned request.
        /// </exception>
        public FriendsOperation<ReadOnlyCollection<Friend>> RefreshFriendsAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            return new(Running(FetchAsync(cancellationToken)), cancellationToken);
        }

        /// <summary>Broadcasts online presence on this player account to its friends.</summary>
        /// <param name="presence">
        /// Pass <see langword="null"/> or empty string to clear online presence.
        /// coherence Cloud currently accepts up to 512 bytes of UTF-8 text without null characters, and fails a
        /// longer one with <see cref="ErrorCode.PresencePayloadInvalid"/> rather than truncating it.
        /// </param>
        /// <param name="cancellationToken">Cancels the request.</param>
        /// <remarks>
        /// <para>
        /// Received through <see cref="OnFriendPresenceChanged"/> and <see cref="Friend.Presence"/>.
        /// </para>
        /// <para>
        /// coherence Cloud currently accepts up to 120 presence updates a minute, counting
        /// <see cref="SetAppearOfflineAsync"/>, and fails the rest with <see cref="ErrorCode.PresenceRateLimited"/>.
        /// </para>
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        /// This service has been disposed.
        /// Thrown at the call site rather than reported through the returned request.
        /// </exception>
        /// <example><code source="Runtime/Friends/FriendsPresence.cs" language="csharp" region="Sample"/></example>
        public FriendsOperation SetPresenceAsync([AllowNull] string presence,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            return new(Running(WritePresenceAsync(presence, nameof(SetPresenceAsync), cancellationToken)),
                cancellationToken);
        }

        /// <summary>Hides the player from their friends, without logging them out.</summary>
        /// <param name="appearOffline">
        /// <see langword="true"/> to disappear, <see langword="false"/> to turn up again.
        /// </param>
        /// <param name="cancellationToken">Cancels the request.</param>
        /// <remarks>
        /// <para>
        /// Unlike presence, this state is kept on the player rather than on the connection: it survives a
        /// disconnect, a reconnect and a logout, and stays applied until it's set back to
        /// <see langword="false"/>.
        /// </para>
        /// <para>
        /// While hidden, friends will see the player offline, with no presence and no lobbies. Hiding does
        /// not clear the presence though: it stays on the connection out of sight, and friends see it again as
        /// soon as <paramref name="appearOffline"/> is set back to <see langword="false"/>, without the
        /// game sending it a second time.
        /// </para>
        /// <para>Shares the rate limit of <see cref="SetPresenceAsync"/>.</para>
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        /// This service has been disposed.
        /// Thrown at the call site rather than reported through the returned request.
        /// </exception>
        public FriendsOperation SetAppearOfflineAsync(bool appearOffline,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            return new(Running(PostPresenceAsync(SetAppearOfflineRequest.GetRequestBody(appearOffline),
                nameof(SetAppearOfflineAsync), cancellationToken)), cancellationToken);
        }

        /// <summary>Drops the friend list and cancels whatever was still on its way.</summary>
        /// <remarks>
        /// Called by the <see cref="CloudService"/> that owns this service. Do not call it yourself.
        /// Requests still on their way report <c>IsCanceled</c> rather than failing.
        /// </remarks>
        public void Dispose() => _ = DisposeAsync(waitForOngoingOperationsToFinish: false);

        /// <param name="waitForOngoingOperationsToFinish">
        /// If <see langword="true"/>, requests already on their way are allowed to finish;
        /// otherwise they are cancelled at once.
        /// </param>
        /// <inheritdoc cref="Dispose"/>
        internal async ValueTask DisposeAsync(bool waitForOngoingOperationsToFinish)
        {
            if (disposing || IsDisposed)
            {
                return;
            }

            disposing = true;

            while (waitForOngoingOperationsToFinish && running > 0)
            {
                await Task.Yield();
            }

            // A request still on its way has nobody left to report back to.
            disposalCts.Cancel();

            requestFactory.RemovePushCallback(FriendsUpdatePush, OnFriendsUpdated);
            requestFactory.RemovePushCallback(FriendsPresencePush, OnFriendPresenceUpdated);
            requestFactory.OnWebSocketConnect -= OnWebSocketConnect;
            authClient.OnLogout -= OnLogout;
            OnFriendAdded = null;
            OnFriendRemoved = null;
            OnFriendPresenceChanged = null;
            friends.Clear();

            if (shouldDisposeLogger)
            {
                logger.Dispose();
            }
        }

        private async Task<FriendsSyncResult> SyncInternalAsync(string source, IReadOnlyList<string> friendIds, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(source))
            {
                throw new ArgumentException("A friend list has to be synced from somewhere. Pass the source of the friend list the ids came from, such as \"steam\".", nameof(source));
            }

            if (friendIds is null)
            {
                throw new ArgumentNullException(nameof(friendIds), "To remove every friend synced from this platform, pass an empty list rather than null.");
            }

            const string method = "POST";
            const string pathParams = "/native-sync";
            var body = SyncFriendsRequest.GetRequestBody(source, friendIds);

            using var request = RequestScope(cancellationToken);

            await throttle.WaitForCooldown(FriendsEndpoint + pathParams, method, request.Token);
            var textResponse = await requestFactory.SendRequestAsync(FriendsEndpoint, pathParams, method, body, null,
                $"{nameof(FriendsService)}.{nameof(SyncFriendsAsync)}", authClient.SessionToken);

            if (!TryDeserialize<FriendsSyncResult>(textResponse, nameof(SyncFriendsAsync), out var result))
            {
                throw new ResponseDeserializationException(Result.InvalidResponse, "Could not read what coherence Cloud made of the friend list. The response has been logged.");
            }

            // The list exists server-side from here on, so a catch-up has something to read
            // even if the fetch below is cut short by a reconnect.
            hasSynced = true;

            await FetchAsync(cancellationToken);
            return result;
        }

        private async Task<ReadOnlyCollection<Friend>> FetchAsync(CancellationToken cancellationToken)
        {
            const string method = "GET";

            using var request = RequestScope(cancellationToken);

            await throttle.WaitForCooldown(FriendsEndpoint, method, request.Token);
            var textResponse = await requestFactory.SendRequestAsync(FriendsEndpoint, method, null, null,
                $"{nameof(FriendsService)}.{nameof(RefreshFriendsAsync)}", authClient.SessionToken);

            if (!TryDeserialize<FriendsResponse>(textResponse, nameof(RefreshFriendsAsync), out var response))
            {
                throw new ResponseDeserializationException(Result.InvalidResponse, "Could not read the friend list sent by coherence Cloud. The response has been logged.");
            }

            request.Token.ThrowIfCancellationRequested();

            ReplaceFriends(response.Friends);
            return Friends;
        }

        private async Task WritePresenceAsync(string presence, string requestName,
            CancellationToken cancellationToken)
        {
            var write = ++presenceWrites;
            presence = presence is { Length: > 0 } ? presence : null;

            await PostPresenceAsync(SetPresenceRequest.GetRequestBody(presence), requestName, cancellationToken);

            if (write > landedPresenceWrite)
            {
                landedPresenceWrite = write;
                lastPresence = presence;
            }
        }

        private async Task PostPresenceAsync(string body, string requestName, CancellationToken cancellationToken)
        {
            const string method = "POST";

            using var request = RequestScope(cancellationToken);

            await throttle.WaitForCooldown(PresenceEndpoint, method, request.Token);
            _ = await requestFactory.SendRequestAsync(PresenceEndpoint, method, body, null,
                $"{nameof(FriendsService)}.{requestName}", authClient.SessionToken);

            request.Token.ThrowIfCancellationRequested();
        }

        private void OnFriendPresenceUpdated(string responseBody)
        {
            if (!TryDeserialize<FriendPresencePayload>(responseBody, FriendsPresencePush, out var payload))
            {
                return;
            }

            var index = IndexOf(payload.PlayerId);
            if (index < 0)
            {
                return;
            }

            var previous = friends[index];
            var friend = previous;
            friend.isOnline = payload.IsOnline;
            friend.lobbyIds = payload.LobbyIds;
            friend.presence = payload.Presence;
            friend.lastOnlineAt = payload.LastOnlineAt;
            friends[index] = friend;

            if (PresenceDiffers(previous, friend))
            {
                OnFriendPresenceChanged?.Invoke(friend);
            }
        }

        private async void OnWebSocketConnect()
        {
            connectionCts.Cancel();
            connectionCts = new();

            if (IsDisposed || !(hasFetched || hasSynced || lastPresence is not null))
            {
                return;
            }

            try
            {
                await CatchUpAsync(loginCts.Token);
            }
            catch (OperationCanceledException)
            {
                // The player logged out, or the service went away, while we were catching up.
            }
            catch (Exception exception)
            {
                logger.Warning(Warning.RuntimeCloudFriendsCatchUpFailed, ("exception", exception));
            }
        }

        private async Task CatchUpAsync(CancellationToken cancellationToken)
        {
            var writes = presenceWrites;

            if (hasFetched || hasSynced)
            {
                try
                {
                    await FetchAsync(cancellationToken);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    // The presence belongs to the connection, not to the list, so it goes back up regardless.
                    logger.Warning(Warning.RuntimeCloudFriendsCatchUpFailed, ("exception", exception));
                }
            }

            if (writes == presenceWrites && lastPresence is { } presence)
            {
                await WritePresenceAsync(presence, nameof(SetPresenceAsync), cancellationToken);
            }
        }

        private void ReplaceFriends([AllowNull] List<Friend> incoming)
        {
            var before = hasFetched ? friends.ToArray() : null;

            friends.Clear();
            if (incoming is not null)
            {
                friends.AddRange(incoming);
            }

            hasFetched = true;

            if (before is null)
            {
                return;
            }

            // Every arrival is checked off the friends held before, so whoever is left in held at the
            // end is whoever did not arrive.
            var held = new Dictionary<PlayerAccountId, Friend>(before.Length);

            foreach (var friend in before)
            {
                held[friend.Id] = friend;
            }

            foreach (var friend in incoming ?? NoFriends)
            {
                if (!held.Remove(friend.Id, out var previous))
                {
                    OnFriendAdded?.Invoke(friend);
                }
                else if (PresenceDiffers(previous, friend))
                {
                    OnFriendPresenceChanged?.Invoke(friend);
                }
            }

            foreach (var friend in held.Values)
            {
                OnFriendRemoved?.Invoke(friend);
            }
        }

        private static bool PresenceDiffers(Friend before, Friend after)
            => before.IsOnline != after.IsOnline
               || before.Presence != after.Presence
               || before.lastOnlineAt != after.lastOnlineAt
               || LobbiesDiffer(before.lobbyIds, after.lobbyIds);

        private static bool LobbiesDiffer([AllowNull] List<string> before, [AllowNull] List<string> after)
        {
            var beforeCount = before?.Count ?? 0;
            var afterCount = after?.Count ?? 0;

            if (beforeCount != afterCount)
            {
                return true;
            }

            for (var i = 0; i < beforeCount; i++)
            {
                if (before[i] != after[i])
                {
                    return true;
                }
            }

            return false;
        }

        private void OnFriendsUpdated(string responseBody)
        {
            if (!TryDeserialize<FriendsUpdatePayload>(responseBody, FriendsUpdatePush, out var payload))
            {
                return;
            }

            if (payload.Removed is not null)
            {
                foreach (var playerId in payload.Removed)
                {
                    if (TryRemove(playerId, out var friend))
                    {
                        OnFriendRemoved?.Invoke(friend);
                    }
                }
            }

            if (payload.Added is null)
            {
                return;
            }

            foreach (var friend in payload.Added)
            {
                if (Add(friend, out var previous))
                {
                    OnFriendAdded?.Invoke(friend);
                }
                else if (PresenceDiffers(previous, friend))
                {
                    OnFriendPresenceChanged?.Invoke(friend);
                }
            }
        }

        private CancellationTokenSource RequestScope(CancellationToken cancellationToken)
            => CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, connectionCts.Token, loginCts.Token, disposalCts.Token);

        private void ThrowIfDisposed()
        {
            if (IsDisposed)
            {
                throw new ObjectDisposedException(nameof(FriendsService));
            }
        }

        private void OnLogout()
        {
            loginCts.Cancel();
            loginCts = new();

            friends.Clear();
            lastPresence = null;
            hasFetched = false;
            hasSynced = false;
        }

        private async Task Running(Task task)
        {
            running++;

            try
            {
                await task;
            }
            finally
            {
                running--;
            }
        }

        private async Task<T> Running<T>(Task<T> task)
        {
            running++;

            try
            {
                return await task;
            }
            finally
            {
                running--;
            }
        }

        /// <returns>
        /// True when this friend was not in the list already, handing back what was there if they were.
        /// </returns>
        private bool Add(Friend friend, out Friend previous)
        {
            var index = IndexOf(friend.Id);
            if (index >= 0)
            {
                previous = friends[index];
                friends[index] = friend;
                return false;
            }

            previous = default;
            friends.Add(friend);
            return true;
        }

        /// <returns>
        /// True when this friend was in the list, handing over the last state we had for them.
        /// </returns>
        private bool TryRemove(PlayerAccountId playerId, out Friend removed)
        {
            var index = IndexOf(playerId);
            if (index < 0)
            {
                removed = default;
                return false;
            }

            removed = friends[index];
            friends.RemoveAt(index);
            return true;
        }

        private int IndexOf(PlayerAccountId playerId)
        {
            for (var i = 0; i < friends.Count; i++)
            {
                if (friends[i].Id == playerId)
                {
                    return i;
                }
            }

            return -1;
        }

        private bool TryDeserialize<T>(string body, string what, out T result)
        {
            try
            {
                result = CoherenceJson.DeserializeObject<T>(body);
                return true;
            }
            catch (Exception exception)
            {
                logger.Error(Error.RuntimeCloudDeserializationException,
                    ("Request", what),
                    ("Response", body),
                    ("exception", exception));

                result = default;
                return false;
            }
        }
    }
}
