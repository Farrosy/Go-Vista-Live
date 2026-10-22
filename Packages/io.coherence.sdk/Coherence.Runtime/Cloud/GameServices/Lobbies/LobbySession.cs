// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Cloud
{
    using Runtime;
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Log;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using Common;
    using Runtime.Utils;
    using Logger = Log.Logger;

    public class LobbySession : IDisposableInternal
    {
        private LobbiesService lobbiesService;
        private LobbyData lobbyData;
        private IAuthClientInternal authClient;
        private IRequestFactory requestFactory;

        [MaybeNull]
        private IRequestFactoryInternal internalRequestFactory;

        [MaybeNull]
        private LobbyOwnerSession lobbyOwnerSession;
        private LobbyPlayer? myPlayer;
        private LobbyPlayer ownerPlayer;
        /// <summary>Expiry timers, by reserved player, with the reservation each one times.</summary>
        private readonly Dictionary<string, (LobbyReservation Reservation, CancellationTokenSource Cancellation)>
            reservationExpiryTimers = new();

        /// <summary>Reservations already reported through <see cref="OnReservationExpired"/>.</summary>
        /// <remarks>
        /// Expiry is timed on this device's clock, so a refresh can still list a reservation that already expired
        /// here. It is not reported a second time.
        /// </remarks>
        private readonly HashSet<LobbyReservation> expiredReservations = new();

        /// <summary>Canceled when this session is disposed, to stop requests that haven't been sent yet.</summary>
        private readonly CancellationTokenSource disposalCancellation = new();

        internal CancellationToken DisposalToken => disposalCancellation.Token;

        private static readonly string lobbiesResolveEndpoint = "/lobbies";
        private static readonly string messageReceivedCallback = $"{lobbiesResolveEndpoint}/messages";
        private static readonly string playerJoinedCallback = $"{lobbiesResolveEndpoint}/player/join";
        private static readonly string playerLeftCallback = $"{lobbiesResolveEndpoint}/player/leave";
        private static readonly string playerAttributesChanged = $"{lobbiesResolveEndpoint}/players/attrs";
        private static readonly string lobbyOwnerChanged = $"{lobbiesResolveEndpoint}/owner";
        private static readonly string lobbyAttributesChanged = $"{lobbiesResolveEndpoint}/attrs";
        private static readonly string reservationsChanged = $"{lobbiesResolveEndpoint}/reservations";

        /// <summary>The <c>resume_id</c> of the last <c>/lobbies/reservations</c> push applied.</summary>
        /// <remarks>
        /// <para>
        /// Each such push carries the full reservation list, so one that arrives after a newer one is stale and
        /// dropped. Other lobby pushes are changes, not snapshots, and are always applied; a player-joined push
        /// older than it just doesn't consume a reservation.
        /// </para>
        /// <para>
        /// HTTP responses (join, refresh, reserving slots) carry no push id, so they can't be ordered against
        /// pushes: a push that was already in flight can still overwrite a newer response, until the next push
        /// corrects it.
        /// </para>
        /// </remarks>
        private long lastReservationsResumeId;

        private readonly Logger logger;
        private readonly bool shouldDisposeLogger;

        public LobbyData LobbyData => lobbyData;

        /// <summary>
        /// Callback that will be invoked when your Player receives one or messages through the Lobby.
        /// </summary>
        public event Action<LobbySession, MessagesReceived> OnMessageReceived;

        /// <summary>
        /// Callback that will be invoked when a new Player joins the Lobby.
        /// </summary>
        /// <seealso cref="InviteAsync"/>
        public event Action<LobbySession, LobbyPlayer> OnPlayerJoined;

        /// <summary>
        /// Callback that will be invoked when a Player leaves the Lobby.
        /// </summary>
        public event Action<LobbySession, LobbyPlayer, string> OnPlayerLeft;

        /// <summary>
        /// Callback that will be invoked when a Player within the Lobby adds or updates an Attribute.
        /// </summary>
        public event Action<LobbySession, LobbyPlayer, IReadOnlyList<CloudAttribute>> OnPlayerAttributesChanged;

        /// <summary>
        /// Callback that will be invoked when the Lobby changes active owner.
        /// </summary>
        public event Action<LobbySession, LobbyPlayer> OnLobbyOwnerChanged;

        /// <summary>
        /// Callback that will be invoked when the owner of the Lobby adds or updates a Lobby attribute,
        /// or when an indexed Attribute gets updated through a Player updating his/her own Attribute.
        /// </summary>
        public event Action<LobbySession, IReadOnlyList<CloudAttribute>> OnLobbyAttributesChanged;

        /// <summary>
        /// Event that is raised when a <see cref="LobbyReservations">reserved slot</see>
        /// expires because the reserved player didn't join in time.
        /// </summary>
        /// <remarks>
        /// Once a reserved slot expires it becomes available to any player. Expiry is timed on this device, for
        /// reservations made by any member, including ones made after you joined.
        /// </remarks>
        public event Action<LobbySession, LobbyReservation> OnReservationExpired;

        /// <summary>
        /// Callback that will be invoked when a Lobby its updated by any of the previous cases.
        /// For example, if a player leaves or joins or updates the Attributes, this Callback will always be invoked
        /// alongside the specific one. Also invoked when another member reserves slots, changing
        /// <see cref="LobbyData.Reservations"/>.
        /// </summary>
        public event Action<LobbySession> OnLobbyUpdated;

        /// <summary>
        /// Event that is raised when a Lobby is disposed and will no longer be usable.
        /// </summary>
        /// <remarks>
        /// This happens when <see cref="LeaveLobby">leaving the Lobby</see>, when
        /// <see cref="PlayerAccount.Logout">logging out</see> from coherence Cloud, and when coherence Cloud reports
        /// the player is no longer in the Lobby: through a push, a refresh, or on logging in.
        /// </remarks>
        public event Action<LobbySession> OnLobbyDisposed;

        /// <summary>
        /// Player struct that represents your Player within the Lobby.
        /// </summary>
        public LobbyPlayer? MyPlayer => myPlayer;

        /// <summary>
        /// Player struct that represents the Player who is the Owner of the Lobby.
        /// </summary>
        public LobbyPlayer OwnerPlayer => ownerPlayer;

        /// <summary>
        /// Instance of LobbyOwnerSession that will allow you to call endpoints that are only available for the Owner of the Lobby.
        /// If you're not the Owner of the Lobby, this property will return null.
        /// </summary>
        [MaybeNull]
        public LobbyOwnerSession LobbyOwnerActions => lobbyOwnerSession;

        /// <summary>
        /// If the current LobbySession is disposed, it means that you're no longer part of this Lobby and this instance cannot be reused.
        /// Attempting to use any Endpoint while disposed will throw an exception.
        /// </summary>
        public bool IsDisposed { get; private set; }

        string IDisposableInternal.InitializationContext { get; set; }
        string IDisposableInternal.InitializationStackTrace { get; set; }
        bool IDisposableInternal.IsDisposed
        {
            get => IsDisposed;
            set => IsDisposed = value;
        }

        public LobbySession(LobbiesService lobbiesService, LobbyData lobbyData, AuthClient authClient, RequestFactory requestFactory)
            : this(lobbiesService, lobbyData, (IAuthClientInternal)authClient, requestFactory) { }

        internal LobbySession(LobbiesService lobbiesService, LobbyData lobbyData, IAuthClientInternal authClient,
            IRequestFactory requestFactory, [MaybeNull] Logger logger = null)
        {
            shouldDisposeLogger = logger is null;
            this.logger = logger ?? Log.GetLogger<LobbySession>();
            this.OnInitialized();
            this.lobbiesService = lobbiesService;
            this.lobbyData = lobbyData;
            this.authClient = authClient;
            this.requestFactory = requestFactory;
            internalRequestFactory = requestFactory as IRequestFactoryInternal;

            try
            {
                // Subscribed first, so that ParseLobbyData disposing a session for a lobby we're not in
                // unsubscribes too.
                requestFactory.AddPushCallback(messageReceivedCallback, OnMessageReceivedHandler);
                requestFactory.AddPushCallback(playerJoinedCallback, OnPlayerJoinedHandler);
                requestFactory.AddPushCallback(playerLeftCallback, OnPlayerLeftHandler);
                requestFactory.AddPushCallback(playerAttributesChanged, OnPlayerAttributesChangedHandler);
                requestFactory.AddPushCallback(lobbyOwnerChanged, OnLobbyOwnerChangedHandler);
                requestFactory.AddPushCallback(lobbyAttributesChanged, OnLobbyAttributesChangedHandler);
                requestFactory.AddPushCallback(reservationsChanged, OnReservationsChangedHandler);
                this.authClient.OnLogout += OnLogout;

                ParseLobbyData();
            }
            catch
            {
                // Nobody gets this session to dispose: undo what was subscribed, and don't report it as leaked.
                Dispose();
                throw;
            }
        }

        internal void UpdateLobbyData(LobbyData lobbyData)
        {
            // A lobby that was left stays left, even if a response for it arrives afterwards.
            if (IsDisposed)
            {
                return;
            }

            this.lobbyData = lobbyData;
            ParseLobbyData();
        }

        /// <summary>Refresh the current data for the active Lobby.</summary>
        public void RefreshLobby(Action onFinished)
        {
            lobbiesService.RefreshLobby(lobbyData, response =>
            {
                if (response.Status == RequestStatus.Success)
                {
                    UpdateLobbyData(response.Result);
                }

                onFinished?.Invoke();
            });
        }

        /// <summary>Refresh the current data for the active Lobby.</summary>
        public async Task RefreshLobbyAsync()
        {
            UpdateLobbyData(await lobbiesService.RefreshLobbyAsync(lobbyData));
        }

        /// <summary>Creates an invite token for this Lobby.</summary>
        /// <param name="timeToLive">
        /// How long the token keeps working.
        /// Leave it out for coherence Cloud's default, currently an hour.
        /// coherence Cloud currently accepts from a second up to a day, and fails the request otherwise.
        /// </param>
        /// <param name="cancellationToken">Optional parameter to cancel the request.</param>
        /// <returns>The token, to hand out as you see fit.</returns>
        /// <remarks>
        /// <para>
        /// A token is not tied to whoever uses it, is multi-use until it expires,
        /// and cannot be taken back on its own.
        /// Closing the Lobby invalidates every token minted for that Lobby.
        /// </para>
        /// <para>
        /// Password verification is skipped when joining with an invite token.
        /// </para>
        /// <para>
        /// Only owners can create invites, unless the Lobby was created with
        /// <see cref="CreateLobbyOptions.InvitePermissions"/> set to
        /// <see cref="LobbyInvitePermissions.AllMembers"/>.
        /// Anyone else gets
        /// <see cref="ErrorCode.LobbyInvitePolicy"/>, a player no longer in the Lobby
        /// <see cref="ErrorCode.LobbyNotFound"/>, and minting too quickly (currently over 30 a minute)
        /// <see cref="ErrorCode.LobbyRateLimited"/>.
        /// </para>
        /// </remarks>
        /// <seealso cref="LobbiesService.JoinLobbyWithTokenAsync"/>
        /// <seealso cref="CreateLobbyOptions.InvitePermissions"/>
        /// <seealso cref="InviteAsync"/>
        public Task<string> CreateInviteTokenAsync(TimeSpan? timeToLive = null, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            return lobbiesService.CreateInviteTokenAsync(lobbyData.Id, timeToLive, cancellationToken);
        }

        /// <summary>Invites a player to this Lobby.</summary>
        /// <param name="player">The player to invite.</param>
        /// <param name="cancellationToken">Optional parameter to cancel the request.</param>
        /// <remarks>
        /// Inviting too often (currently over 60 an hour) fails with <see cref="ErrorCode.LobbyRateLimited"/>.
        /// </remarks>
        /// <seealso cref="LobbiesService.OnLobbyInviteReceived"/>
        /// <seealso cref="LobbiesService.AcceptLobbyInviteAsync"/>
        /// <seealso cref="CreateInviteTokenAsync"/>
        public Task InviteAsync(PlayerAccountId player, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            return lobbiesService.InviteToLobbyAsync(lobbyData.Id, player, cancellationToken);
        }

        /// <summary>Reserves slots in this Lobby for specific players, such as the rest of your party.</summary>
        /// <param name="reservations">The players to reserve slots for, and for how long.</param>
        /// <param name="cancellationToken">Optional parameter to cancel the request.</param>
        /// <remarks>
        /// <para>
        /// Either every slot is reserved, or the request fails and none is: with <see cref="ErrorCode.LobbyFull"/>
        /// if the Lobby can't fit everyone. Players already in the Lobby, or who already have a reservation, are
        /// skipped, and an existing reservation keeps its original expiry.
        /// </para>
        /// <para>
        /// <see cref="LobbyData"/> is updated with the new reservations, and other members learn about them through
        /// <see cref="OnLobbyUpdated"/>. Reserved players still join on their own, and still need the Lobby's
        /// password or an invite if it has one.
        /// </para>
        /// <para>
        /// Limited by <see cref="LobbyData.MaxReservationsPerRequest"/>, even for the owner: reserving more new slots
        /// than it allows fails with <see cref="ErrorCode.LobbyReservationsNotAllowed"/>.
        /// </para>
        /// <para>
        /// Throws <see cref="ObjectDisposedException"/> if this session is disposed. If that happens before the request
        /// is sent, such as while it waits for the request cooldown, nothing is sent. If it happens while the request
        /// is in flight, the slots may still have been reserved.
        /// </para>
        /// </remarks>
        /// <seealso cref="LobbyReservations"/>
        /// <seealso cref="LobbiesService.JoinLobbyAsync(string, List{CloudAttribute}, string, LobbyReservations,
        /// CancellationToken)"/>
        public Task ReserveSlotsAsync([DisallowNull] LobbyReservations reservations,
            CancellationToken cancellationToken = default)
        {
            if (IsDisposed)
            {
                throw new ObjectDisposedException(nameof(LobbySession),
                    "This lobby session was disposed, as you're no longer in the lobby.");
            }

            return lobbiesService.ReserveSlotsAsync(this, reservations, cancellationToken);
        }

        /// <summary>Leave the active Lobby. The current LobbySession instance will be disposed.</summary>
        public void LeaveLobby(Action<RequestResponse<bool>> onRequestFinished)
        {
            ThrowIfDisposed();

            var pathParams = $"/{lobbyData.Id}/players/{authClient.PlayerAccountId}";

            requestFactory.SendRequest(lobbiesResolveEndpoint, pathParams, "DELETE", null,
                null, $"{nameof(LobbySession)}.{nameof(LeaveLobby)}", authClient.SessionToken, response =>
                {
                    var requestResponse = RequestResponse<bool>.GetRequestResponse(response);

                    if (response.Status == RequestStatus.Success
                        || (response.Exception is RequestException requestException && requestException.ErrorCode == ErrorCode.LobbyActionNotAllowed))
                    {
                        requestResponse.Status = RequestStatus.Success;
                        requestResponse.Exception = null;
                        requestResponse.Result = true;

                        Dispose();

                        onRequestFinished?.Invoke(requestResponse);
                    }
                    else
                    {
                        requestResponse.Result = false;
                        onRequestFinished?.Invoke(requestResponse);
                    }
                });
        }

        /// <summary>Leave the active Lobby. The current LobbySession instance will be disposed.</summary>
        public async Task<bool> LeaveLobbyAsync()
        {
            ThrowIfDisposed();

            var pathParams = $"/{lobbyData.Id}/players/{authClient.PlayerAccountId}";

            var task = requestFactory.SendRequestAsync(lobbiesResolveEndpoint, pathParams, "DELETE", null, null, $"{nameof(LobbySession)}.{nameof(LeaveLobbyAsync)}", authClient.SessionToken);

            await task;

            if (task.Exception != null &&
                (!(task.Exception.InnerException is RequestException requestException)
                 || requestException.ErrorCode != ErrorCode.LobbyActionNotAllowed))
            {
                throw task.Exception;
            }

            Dispose();

            return true;
        }

        /// <returns>Returns the internal cooldown for the Send Message endpoint.</returns>
        public TimeSpan GetSendMessageCooldown()
        {
            return requestFactory.GetRequestCooldown(lobbiesResolveEndpoint + $"/{lobbyData.Id}/messages", "POST");
        }

        /// <summary>Send a message to other Players of the current Lobby.</summary>
        /// <param name="messages">List of messages to be sent to other Players.</param>
        /// <param name="onRequestFinished">Callback that will be invoked when the request finished.</param>
        /// <param name="targets">Optional list of Players to send the messages to. If left empty, it will be send to everyone.</param>
        public void SendMessage(List<string> messages, Action<RequestResponse<bool>> onRequestFinished, List<LobbyPlayer> targets = null)
        {
            ThrowIfDisposed();

            var pathParams = $"/{lobbyData.Id}/messages";

            var requestBody = SendMessageRequest.GetRequestBody(messages, targets);

            requestFactory.SendRequest(lobbiesResolveEndpoint, pathParams, "POST", requestBody,
                null, $"{nameof(LobbySession)}.{nameof(SendMessage)}", authClient.SessionToken, response =>
                {
                    var requestResponse = RequestResponse<bool>.GetRequestResponse(response);

                    if (response.Status == RequestStatus.Success)
                    {
                        requestResponse.Result = true;
                        onRequestFinished?.Invoke(requestResponse);
                    }
                    else
                    {
                        requestResponse.Result = false;
                        onRequestFinished?.Invoke(requestResponse);
                    }
                });
        }

        /// <summary>Send a message to other Players of the current Lobby.</summary>
        /// <param name="messages">List of messages to be sent to other Players.</param>
        /// <param name="targets">Optional list of Players to send the messages to. If left empty, it will be send to everyone.</param>
        public async Task<bool> SendMessageAsync(List<string> messages, List<LobbyPlayer> targets = null)
        {
            ThrowIfDisposed();

            var pathParams = $"/{lobbyData.Id}/messages";

            var requestBody = SendMessageRequest.GetRequestBody(messages, targets);

            var task = requestFactory.SendRequestAsync(lobbiesResolveEndpoint, pathParams,
                "POST", requestBody, null, $"{nameof(LobbySession)}.{nameof(SendMessageAsync)}", authClient.SessionToken);

            await task;

            if (task.Exception != null)
            {
                throw task.Exception;
            }

            return true;
        }

        /// <returns>Returns the internal cooldown for the Add Or Update My Attributes endpoint.</returns>
        public TimeSpan GetAddOrUpdateMyAttributesCooldown()
        {
            return requestFactory.GetRequestCooldown(lobbiesResolveEndpoint + $"/{lobbyData.Id}/players/{authClient.PlayerAccountId}/attrs", "PATCH");
        }

        /// <summary>
        /// Through this method you will be able to add new Attributes or update existing Attributes.
        /// Deleting existing Attributes is not supported, the list supplied as parameter will be merged with your
        /// current Attributes if the request succeeds with the backend.
        /// </summary>
        /// <param name="attributes">List of Attributes to be added or updated for the current Lobby.</param>
        /// <param name="onRequestFinished">Callback that will be invoked when the request finished.</param>
        public void AddOrUpdateMyAttributes(List<CloudAttribute> attributes, Action<RequestResponse<bool>> onRequestFinished)
        {
            ThrowIfDisposed();

            var pathParams = $"/{lobbyData.Id}/players/{authClient.PlayerAccountId}/attrs";

            var requestBody = SetAttributesRequest.GetRequestBody(attributes);

            requestFactory.SendRequest(lobbiesResolveEndpoint, pathParams, "PATCH", requestBody,
                null, $"{nameof(LobbySession)}.{nameof(AddOrUpdateMyAttributes)}", authClient.SessionToken, response =>
                {
                    var requestResponse = RequestResponse<bool>.GetRequestResponse(response);

                    if (response.Status == RequestStatus.Success)
                    {
                        requestResponse.Result = true;
                        myPlayer.Value.MergeAttributesWith(attributes);
                        onRequestFinished?.Invoke(requestResponse);
                    }
                    else
                    {
                        requestResponse.Result = false;
                        onRequestFinished?.Invoke(requestResponse);
                    }
                });
        }

        /// <summary>
        /// Through this method you will be able to add new Attributes or update existing Attributes.
        /// Deleting existing Attributes is not supported, the list supplied as parameter will be merged with your
        /// current Attributes if the request succeeds with the backend.
        /// </summary>
        /// <param name="attributes">List of Attributes to be added or updated for the current Lobby.</param>
        public async Task<bool> AddOrUpdateMyAttributesAsync(List<CloudAttribute> attributes)
        {
            ThrowIfDisposed();

            var pathParams = $"/{lobbyData.Id}/players/{authClient.PlayerAccountId}/attrs";

            var requestBody = SetAttributesRequest.GetRequestBody(attributes);

            var task = requestFactory.SendRequestAsync(lobbiesResolveEndpoint, pathParams, "PATCH",
                requestBody, null, $"{nameof(LobbySession)}.{nameof(AddOrUpdateMyAttributesAsync)}", authClient.SessionToken);

            await task;

            if (task.Exception != null)
            {
                throw task.Exception;
            }

            myPlayer.Value.MergeAttributesWith(attributes);

            return true;
        }

        ~LobbySession()
        {
            if (!this.OnFinalized())
            {
                logger.Warning(Warning.RuntimeCloudGameServicesResourceLeak, this.GetResourceLeakWarningMessage());
            }
        }

        public void Dispose()
        {
            if (this.OnDisposed())
            {
                return;
            }

            disposalCancellation.Cancel();
            disposalCancellation.Dispose();

            requestFactory.RemovePushCallback(messageReceivedCallback, OnMessageReceivedHandler);
            requestFactory.RemovePushCallback(playerJoinedCallback, OnPlayerJoinedHandler);
            requestFactory.RemovePushCallback(playerLeftCallback, OnPlayerLeftHandler);
            requestFactory.RemovePushCallback(playerAttributesChanged, OnPlayerAttributesChangedHandler);
            requestFactory.RemovePushCallback(lobbyOwnerChanged, OnLobbyOwnerChangedHandler);
            requestFactory.RemovePushCallback(lobbyAttributesChanged, OnLobbyAttributesChangedHandler);
            requestFactory.RemovePushCallback(reservationsChanged, OnReservationsChangedHandler);
            this.authClient.OnLogout -= OnLogout;

            foreach (var (_, cancellationTokenSource) in reservationExpiryTimers.Values)
            {
                cancellationTokenSource.Cancel();
                cancellationTokenSource.Dispose();
            }
            reservationExpiryTimers.Clear();

            // Game code runs here, and a session can be disposed from within a refresh, a login or a push. A handler
            // that throws must not break those, nor stop the next session being disposed.
            try
            {
                OnLobbyDisposed?.Invoke(this);
            }
            catch (Exception exception)
            {
                logger.Error(Error.RuntimeCloudLobbyDisposedHandlerException, ("lobbyId", lobbyData.Id),
                    ("exception", exception));
            }

            if (shouldDisposeLogger)
            {
                logger.Dispose();
            }
        }

        private void OnLogout() => Dispose();

        private void ThrowIfDisposed()
        {
            if (IsDisposed)
            {
                throw new RequestException(0,
                    $"{nameof(LobbySession)} has been disposed. This session is no longer usable.");
            }
        }

        private void ParseLobbyData()
        {
            myPlayer = null;
            lobbyOwnerSession = null;

            foreach (var player in lobbyData.Players)
            {
                if (player.Id.Equals(authClient.PlayerAccountId))
                {
                    myPlayer = player;
                }

                if (lobbyData.OwnerId == player.Id)
                {
                    ownerPlayer = player;
                }
            }

            UpdateLobbyOwnerSession();

            if (!myPlayer.HasValue)
            {
                Dispose();
                return;
            }

            ScheduleReservationExpirations();
        }

        private void ScheduleReservationExpirations()
        {
            var reservations = lobbyData.reservations;

            if (expiredReservations.Count > 0)
            {
                // Forget the ones coherence Cloud has dropped too, and hide the rest as already expired.
                expiredReservations.RemoveWhere(expired => reservations is null || !reservations.Contains(expired));
                reservations?.RemoveAll(expiredReservations.Contains);
            }

            var liveIds = new HashSet<string>(reservations?.Count ?? 0);

            if (reservations is not null)
            {
                foreach (var reservation in reservations)
                {
                    string id = reservation.PlayerAccountId;
                    liveIds.Add(id);

                    if (reservationExpiryTimers.TryGetValue(id, out var timer))
                    {
                        if (timer.Reservation.Equals(reservation))
                        {
                            continue;
                        }

                        // Reserved again with a new expiry, after the previous reservation expired.
                        timer.Cancellation.Cancel();
                        timer.Cancellation.Dispose();
                    }

                    var cancellationTokenSource = new CancellationTokenSource();
                    reservationExpiryTimers[id] = (reservation, cancellationTokenSource);
                    _ = WaitForReservationExpiry(reservation, cancellationTokenSource);
                }
            }

            if (reservationExpiryTimers.Count is 0)
            {
                return;
            }

            List<string> staleIds = null;

            foreach (var id in reservationExpiryTimers.Keys)
            {
                if (!liveIds.Contains(id))
                {
                    (staleIds ??= new()).Add(id);
                }
            }

            if (staleIds is null)
            {
                return;
            }

            foreach (var id in staleIds)
            {
                reservationExpiryTimers.Remove(id, out var timer);
                timer.Cancellation.Cancel();
                timer.Cancellation.Dispose();
            }
        }

        private async Task WaitForReservationExpiry(LobbyReservation reservation, CancellationTokenSource cancellationTokenSource)
        {
            var delay = reservation.ExpiresAt - DateTimeOffset.UtcNow;

            if (delay > TimeSpan.Zero)
            {
                try
                {
                    await Wait.For(delay, cancellationTokenSource.Token);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
            else
            {
                // Yield at least once so this never fires synchronously from within the constructor/ParseLobbyData
                // call that created the reservation - callers can always subscribe right after getting the LobbySession.
                await Task.Yield();
            }

            if (IsDisposed || cancellationTokenSource.IsCancellationRequested)
            {
                return;
            }

            string id = reservation.PlayerAccountId;

            if (!reservationExpiryTimers.TryGetValue(id, out var currentTimer)
                || !ReferenceEquals(currentTimer.Cancellation, cancellationTokenSource))
            {
                // A newer reservation (or a refresh that dropped this one) has already superseded this timer.
                return;
            }

            reservationExpiryTimers.Remove(id);
            cancellationTokenSource.Dispose();

            // Only this reservation: a newer one for the same player has a timer of its own.
            var index = lobbyData.reservations?.IndexOf(reservation) ?? -1;

            if (index < 0)
            {
                return;
            }

            lobbyData.reservations?.RemoveAt(index);
            expiredReservations.Add(reservation);

            // A snapshot older than a player-joined push can still list a reservation for someone who joined. Their
            // slot is filled, not freed, so it isn't reported. Snapshots aren't pruned by the player list instead,
            // since that list can itself be behind: a leave push can arrive after a snapshot reserving that player.
            if (lobbyData.players?.Exists(player => player.Id.Equals(reservation.PlayerAccountId)) ?? false)
            {
                OnLobbyUpdated?.Invoke(this);
                return;
            }

            OnReservationExpired?.Invoke(this, reservation);
            OnLobbyUpdated?.Invoke(this);
        }

        private void UpdateLobbyOwnerSession()
            => lobbyOwnerSession = myPlayer.HasValue && myPlayer.Value == ownerPlayer
            ? new LobbyOwnerSession(this.LobbyData, this.authClient, this.requestFactory)
            : null;

        private void OnMessageReceivedHandler(string responseBody)
        {
            MessagesReceived response;
            try
            {
                response = Utils.CoherenceJson.DeserializeObject<MessagesReceived>(responseBody);
            }
            catch (Exception exception)
            {
                logger.Error(Error.RuntimeCloudDeserializationException,
                    ("Request", nameof(MessagesReceived)),
                    ("Response", responseBody),
                    ("exception", exception));

                return;
            }

            if (response.LobbyId.Equals(lobbyData.Id))
            {
                OnMessageReceived?.Invoke(this, response);
            }
        }

        private void OnPlayerJoinedHandler(string responseBody)
        {
            PlayerJoinedPayload response;

            try
            {
                response = Utils.CoherenceJson.DeserializeObject<PlayerJoinedPayload>(responseBody);
            }
            catch (Exception exception)
            {
                logger.Error(Error.RuntimeCloudDeserializationException,
                    ("Request", nameof(PlayerJoinedPayload)),
                    ("Response", responseBody),
                    ("exception", exception));

                return;
            }

            if (!response.LobbyId.Equals(lobbyData.Id))
            {
                return;
            }

            if (!lobbyData.players.Contains(response.Player))
            {
                lobbyData.players.Add(response.Player);
            }

            // The server consumed the joiner's reservation. Strip it locally too, so ParseLobbyData cancels its expiry
            // timer instead of it firing OnReservationExpired for a slot that was actually filled. Unless this join is
            // older than the last reservations snapshot applied: that snapshot already accounts for it, and may
            // reserve a slot for the same player again after they left.
            if (!IsOlderThanLastReservationsSnapshot(internalRequestFactory?.PushResumeId))
            {
                lobbyData.reservations?.RemoveAll(reservation =>
                    reservation.PlayerAccountId.Equals(response.Player.Id));
            }

            ParseLobbyData();

            OnPlayerJoined?.Invoke(this, response.Player);
            OnLobbyUpdated?.Invoke(this);
        }

        private void OnPlayerLeftHandler(string responseBody)
        {
            PlayerLeftPayload response;

            try
            {
                response = Utils.CoherenceJson.DeserializeObject<PlayerLeftPayload>(responseBody);
            }
            catch (Exception exception)
            {
                logger.Error(Error.RuntimeCloudDeserializationException,
                    ("Request", nameof(PlayerLeftPayload)),
                    ("Response", responseBody),
                    ("exception", exception));

                return;
            }

            if (!response.LobbyId.Equals(lobbyData.Id))
            {
                return;
            }

            LobbyPlayer? playerToBeRemoved = null;

            foreach (var player in lobbyData.Players)
            {
                if (player.Id.Equals(response.PlayerId))
                {
                    playerToBeRemoved = player;
                    break;
                }
            }

            if (!playerToBeRemoved.HasValue)
            {
                return;
            }

            lobbyData.players.Remove(playerToBeRemoved.Value);
            ParseLobbyData();

            OnPlayerLeft?.Invoke(this, playerToBeRemoved.Value, response.Reason);
            OnLobbyUpdated?.Invoke(this);
        }

        private void OnPlayerAttributesChangedHandler(string responseBody)
        {
            PlayerAttributesChangedPayload response;

            try
            {
                response = Utils.CoherenceJson.DeserializeObject<PlayerAttributesChangedPayload>(responseBody);
            }
            catch (Exception exception)
            {
                logger.Error(Error.RuntimeCloudDeserializationException,
                    ("Request", nameof(PlayerAttributesChangedPayload)),
                    ("Response", responseBody),
                    ("exception", exception));

                return;
            }

            if (!response.LobbyId.Equals(lobbyData.Id))
            {
                return;
            }

            LobbyPlayer? playerWithChangedAttributes = null;

            foreach (var player in lobbyData.Players)
            {
                if (player.Id.Equals(response.PlayerId))
                {
                    playerWithChangedAttributes = player;
                    player.MergeAttributesWith(response.AttributesChanged);
                    break;
                }
            }

            if (!playerWithChangedAttributes.HasValue)
            {
                return;
            }

            OnPlayerAttributesChanged?.Invoke(this, playerWithChangedAttributes.Value, playerWithChangedAttributes.Value.Attributes);
            OnLobbyUpdated?.Invoke(this);
        }

        private void OnLobbyOwnerChangedHandler(string responseBody)
        {
            OwnerChangedPayload response;
            try
            {
                response = Utils.CoherenceJson.DeserializeObject<OwnerChangedPayload>(responseBody);
            }
            catch (Exception exception)
            {
                logger.Error(Error.RuntimeCloudDeserializationException,
                    ("Request", nameof(OwnerChangedPayload)),
                    ("Response", responseBody),
                    ("exception", exception));

                return;
            }

            if (!response.LobbyId.Equals(lobbyData.Id))
            {
                return;
            }

            LobbyPlayer? newOwner = null;

            foreach (var player in lobbyData.Players)
            {
                if (player.Id.Equals(response.PlayerId))
                {
                    newOwner = player;
                    break;
                }
            }

            if (!newOwner.HasValue)
            {
                return;
            }

            lobbyData.ownerId = newOwner.Value.Id;
            ownerPlayer = newOwner.Value;

            UpdateLobbyOwnerSession();

            OnLobbyOwnerChanged?.Invoke(this, newOwner.Value);
            OnLobbyUpdated?.Invoke(this);
        }

        private void OnLobbyAttributesChangedHandler(string responseBody)
        {
            LobbyAttributesChangedPayload response;

            try
            {
                response = Utils.CoherenceJson.DeserializeObject<LobbyAttributesChangedPayload>(responseBody);
            }
            catch (Exception exception)
            {
                logger.Error(Error.RuntimeCloudDeserializationException,
                    ("Request", nameof(LobbyAttributesChangedPayload)),
                    ("Response", responseBody),
                    ("exception", exception));

                return;
            }

            if (!response.LobbyId.Equals(lobbyData.Id))
            {
                return;
            }

            lobbyData.MergeAttributesWith(response.AttributesChanged);

            OnLobbyAttributesChanged?.Invoke(this, lobbyData.lobbyAttributes);
            OnLobbyUpdated?.Invoke(this);
        }

        // Pushes without a resume_id can't be ordered, so they never count as older.
        private bool IsOlderThanLastReservationsSnapshot([MaybeNull] string resumeId)
            => long.TryParse(resumeId, out var id) && IsOlderThanLastReservationsSnapshot(id);

        private bool IsOlderThanLastReservationsSnapshot(long resumeId) => resumeId <= lastReservationsResumeId;

        private void OnReservationsChangedHandler(string responseBody)
        {
            // Unsubscribing only takes effect on the request factory's next update, so a push can still arrive.
            if (IsDisposed)
            {
                return;
            }

            ReservationsChangedPayload response;

            try
            {
                response = Utils.CoherenceJson.DeserializeObject<ReservationsChangedPayload>(responseBody);
            }
            catch (Exception exception)
            {
                logger.Error(Error.RuntimeCloudDeserializationException,
                    ("Request", nameof(ReservationsChangedPayload)),
                    ("Response", responseBody),
                    ("exception", exception));

                return;
            }

            if (!string.Equals(response.LobbyId, lobbyData.Id))
            {
                return;
            }

            // Compared as numbers: as strings, "10" sorts before "9".
            if (long.TryParse(internalRequestFactory?.PushResumeId, out var resumeId))
            {
                if (IsOlderThanLastReservationsSnapshot(resumeId))
                {
                    logger.Debug("Dropping out-of-order reservations push", ("lobbyId", lobbyData.Id),
                        ("resumeId", resumeId), ("lastResumeId", lastReservationsResumeId));
                    return;
                }

                lastReservationsResumeId = resumeId;
            }

            // A full snapshot: reservations missing from it were consumed or released, and rescheduling cancels their
            // expiry timers, while new ones get timers at their expires_at.
            lobbyData.reservations = response.Reservations ?? new();
            ScheduleReservationExpirations();

            OnLobbyUpdated?.Invoke(this);
        }

        public override string ToString() => $"LobbySession(\"{LobbyData.Name}\", {LobbyData.Id})";
    }
}
