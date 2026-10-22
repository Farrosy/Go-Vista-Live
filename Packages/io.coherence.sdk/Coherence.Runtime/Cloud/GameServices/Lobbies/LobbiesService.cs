// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

#if UNITY_5_3_OR_NEWER
// IMPORTANT: Used by the pure-dotnet client, DON'T REMOVE.
#define UNITY
#endif

namespace Coherence.Cloud
{
    using Common;
    using Log;
    using Runtime;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Logger = Log.Logger;

    public class LobbiesService : IAsyncDisposable, IDisposable
    {
        private readonly IRequestFactory requestFactory;
        private readonly IAuthClientInternal authClient;
        private readonly RequestThrottle throttle;
        private readonly IRuntimeSettings runtimeSettings;
        private readonly Logger logger;
        private readonly bool shouldDisposeLogger;

        // Handed to sessions only when a logger was injected; otherwise each session creates its own.
        [MaybeNull]
        private readonly Logger sessionLogger;

        private const string lobbiesResolveEndpoint = "/lobbies";
        internal const string refreshLobbiesAsyncPathParams = "?ids={0}";
        private const string playCallback = lobbiesResolveEndpoint + "/play";

        // Friends endpoint but consumed here for consistency and simplicity.
        private const string inviteCallback = FriendsService.FriendsEndpoint + "/lobby-invite";


        private readonly HashSet<string> lobbySessionIds = new();
        private readonly Dictionary<string, LobbySession> lobbySessions = new();
        private readonly List<Action<RequestResponse<IReadOnlyList<LobbyData>>>> fetchLobbiesCallbackList = new();
        private bool shouldDisposeRequestFactoryAndAuthClient;

        internal IEnumerable<LobbySession> CachedLobbies => lobbySessions.Values;

        /// <summary>
        /// Invoked when the owner starts the game.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Contains the Lobby ID and the <see cref="RoomData"/> that you can use
        /// to join the game session through <c>CoherenceBridge.JoinRoom</c>.
        /// </para>
        /// <para>
        /// With no handler registered here, coherence joins the room for you, but only through
        /// a <c>CoherenceBridge</c> that is using this same <see cref="CloudService"/>.
        /// </para>
        /// </remarks>
        public event Action<string, RoomData> OnPlaySessionStarted;

        internal event Action<RoomData> OnPlaySessionStartedInternal;

        /// <summary>Invoked when a player invites you to a Lobby they are in.</summary>
        /// <remarks>
        /// Upon accept, hand the invite to <see cref="AcceptLobbyInviteAsync"/>.
        /// Invites are only received while connected.
        /// Invites sent to disconnected players are discarded.
        /// </remarks>
        /// <seealso cref="LobbySession.InviteAsync"/>
        /// <seealso cref="LobbyInvite"/>
        public event Action<LobbyInvite> OnLobbyInviteReceived;

        public LobbiesService(CloudCredentialsPair credentialsPair = null, IRuntimeSettings runtimeSettings = null) : this(credentialsPair, runtimeSettings, playerAccountProvider: null, throttle: null) { }

        internal LobbiesService([MaybeNull] CloudCredentialsPair credentialsPair,
            [MaybeNull] IRuntimeSettings runtimeSettings, [MaybeNull] IPlayerAccountProvider playerAccountProvider,
            [MaybeNull] RequestThrottle throttle, [MaybeNull] Logger logger = null)
        {
            shouldDisposeLogger = logger is null;
            sessionLogger = logger;
            this.logger = logger ?? Log.GetLogger<LobbiesService>();
#if UNITY
            runtimeSettings ??= RuntimeSettings.Instance;
#endif
            this.runtimeSettings = runtimeSettings;

            if (credentialsPair is null)
            {
                shouldDisposeRequestFactoryAndAuthClient = true;
                credentialsPair =
                    #if UNITY
                    SimulatorUtility.IsSimulator ? CloudCredentialsFactory.ForSimulator(runtimeSettings, playerAccountProvider) :
                    #endif
                    CloudCredentialsFactory.ForClient(runtimeSettings, playerAccountProvider);
                credentialsPair.authClient.AutoLogin().Then(task =>
                {
                    if (task.IsFaulted)
                    {
                        this.logger.Warning(Warning.RuntimeCloudLoginFailedMsg, task.Exception.ToString());
                    }
                    else if (task.IsCompletedSuccessfully && task.Result.Type is not Result.Success)
                    {
                        this.logger.Error(task.Result.Error, task.Result.ToString());
                    }
                });
            }

            this.requestFactory = credentialsPair.requestFactory;
            this.authClient = credentialsPair.authClient;
            this.throttle = throttle ?? credentialsPair.requestFactory.Throttle;

            this.authClient.OnLogin += OnLogin;
            this.authClient.OnLogout += OnLogout;

            requestFactory.AddPushCallback(playCallback, OnPlayStarted);
            requestFactory.AddPushCallback(inviteCallback, OnLobbyInvited);
        }

        /// <returns>Returns the internal cooldown for the Find Or Create Lobby endpoint.</returns>
        public TimeSpan GetFindOrCreateLobbyCooldown()
        {
            return requestFactory.GetRequestCooldown(lobbiesResolveEndpoint + "/match", "POST");
        }

        /// <summary>Endpoint to do matchmaking and find a suitable Lobby. If no suitable Lobby is found, one will be created using the CreateLobbyOptions.</summary>
        /// <param name="onRequestFinished">Callback that will be invoked when the request finished.</param>
        /// <param name="findOptions">Options that will be used to try to find a suitable Lobby.</param>
        /// <param name="createOptions">
        /// <inheritdoc cref="FindOrCreateLobbyAsync(FindLobbyOptions, CreateLobbyOptions, CancellationToken)"
        /// path="/param[@name='createOptions']/node()"/>
        /// </param>
        /// <remarks>
        /// <inheritdoc cref="FindOrCreateLobbyAsync(FindLobbyOptions, CreateLobbyOptions, CancellationToken)"
        /// path="/remarks/node()"/>
        /// </remarks>
        public void FindOrCreateLobby(FindLobbyOptions findOptions, CreateLobbyOptions createOptions, Action<RequestResponse<LobbySession>> onRequestFinished)
        {
            var pathParams = "/match";

            var requestBodyForRegion = FindLobbyRequest.GetRequestBody(findOptions, createOptions);

            requestFactory.SendRequest(lobbiesResolveEndpoint, pathParams, "POST", requestBodyForRegion,
                null, $"{nameof(LobbiesService)}.{nameof(FindOrCreateLobby)}", GetAuthToken(), response =>
            {
                var requestResponse = RequestResponse<LobbySession>.GetRequestResponse(response);

                if (requestResponse.Status == RequestStatus.Fail)
                {
                    onRequestFinished?.Invoke(requestResponse);
                    return;
                }

                LobbyData lobby;

                try
                {
                    lobby = DeserializeLobbyData(response.Result);
                }
                catch (Exception exception)
                {
                    requestResponse.Status = RequestStatus.Fail;
                    requestResponse.Exception =
                        DeserializationFailed(nameof(FindOrCreateLobby), response.Result, exception);
                    onRequestFinished?.Invoke(requestResponse);
                    return;
                }

                logger.Trace("FindLobby - end", ("lobbyId", lobby.Id));
                requestResponse.Result = AddOrUpdateSession(lobby);
                onRequestFinished?.Invoke(requestResponse);
            });
        }

        /// <summary>Endpoint to do matchmaking and find a suitable Lobby. If no suitable Lobby is found, one will be created using the CreateLobbyOptions.</summary>
        /// <param name="findOptions">Options that will be used to try to find a suitable Lobby.</param>
        /// <param name="createOptions">
        /// Options that will be used to create a Lobby if no suitable Lobby is found.
        /// <see cref="CreateLobbyOptions.Reservations"/> also applies to a found Lobby.
        /// </param>
        /// <remarks>
        /// <para>
        /// With <see cref="CreateLobbyOptions.Reservations"/>, you join a found Lobby and slots are reserved for the
        /// rest of your party in a single step. Lobbies that can't fit the whole party, or whose
        /// <see cref="LobbyData.MaxReservationsPerRequest"/> is too low, are skipped. When none
        /// qualifies, a new Lobby is created with the slots reserved.
        /// </para>
        /// <para>
        /// Creation flags such as <see cref="CreateLobbyOptions.MaxReservationsPerRequest"/> only apply to a created
        /// Lobby. A found Lobby keeps its own; read them off <see cref="LobbySession.LobbyData"/>.
        /// </para>
        /// </remarks>
        /// <param name="cancellationToken">Optional parameter to cancel the request.</param>
        public async Task<LobbySession> FindOrCreateLobbyAsync(FindLobbyOptions findOptions, CreateLobbyOptions createOptions, CancellationToken cancellationToken = default)
        {
            const string method = "POST";
            var pathParams = "/match";
            var requestBodyForRegion = FindLobbyRequest.GetRequestBody(findOptions, createOptions);

            await throttle.WaitForCooldown(lobbiesResolveEndpoint + pathParams, method, cancellationToken);

            var textResponse = await requestFactory.SendRequestAsync(lobbiesResolveEndpoint, pathParams,
                method, requestBodyForRegion, null, $"{nameof(LobbiesService)}.{nameof(FindOrCreateLobbyAsync)}", GetAuthToken());

            var lobby = ReadLobbyData(textResponse, nameof(FindOrCreateLobbyAsync));
            return AddOrUpdateSession(lobby);
        }

        /// <returns>Returns the internal cooldown for the Create Lobby endpoint.</returns>
        public TimeSpan GetCreateLobbyCooldown()
        {
            return requestFactory.GetRequestCooldown(lobbiesResolveEndpoint, "POST");
        }

        /// <summary>Endpoint to create a Lobby directly without doing matchmaking.</summary>
        /// <param name="onRequestFinished">Callback that will be invoked when the request finished.</param>
        /// <param name="createOptions">Options that will be used to create a Lobby.</param>
        public void CreateLobby(CreateLobbyOptions createOptions, Action<RequestResponse<LobbySession>> onRequestFinished)
        {
            var requestBodyForRegion = LobbyCreationRequest.GetRequestBody(createOptions);

            requestFactory.SendRequest(lobbiesResolveEndpoint, "POST", requestBodyForRegion,
                null, $"{nameof(LobbiesService)}.{nameof(CreateLobby)}", GetAuthToken(), response =>
                {
                    var requestResponse = RequestResponse<LobbySession>.GetRequestResponse(response);

                    if (requestResponse.Status == RequestStatus.Fail)
                    {
                        onRequestFinished?.Invoke(requestResponse);
                        return;
                    }

                    LobbyData lobby;

                    try
                    {
                        lobby = DeserializeLobbyData(response.Result);
                    }
                    catch (Exception exception)
                    {
                        requestResponse.Status = RequestStatus.Fail;
                        requestResponse.Exception =
                            DeserializationFailed(nameof(CreateLobby), response.Result, exception);
                        onRequestFinished?.Invoke(requestResponse);
                        return;
                    }

                    logger.Trace("CreateLobby - end", ("lobbyId", lobby.Id));
                    requestResponse.Result = AddOrUpdateSession(lobby);
                    onRequestFinished?.Invoke(requestResponse);
                });
        }

        /// <summary>Endpoint to create a Lobby directly without doing matchmaking.</summary>
        /// <param name="createOptions">Options that will be used to create a Lobby.</param>
        /// <param name="cancellationToken">Optional parameter to cancel the request.</param>
        public async Task<LobbySession> CreateLobbyAsync(CreateLobbyOptions createOptions, CancellationToken cancellationToken = default)
        {
            const string method = "POST";
            var requestBodyForRegion = LobbyCreationRequest.GetRequestBody(createOptions);

            await throttle.WaitForCooldown(lobbiesResolveEndpoint, method, cancellationToken);

            var textResponse = await requestFactory.SendRequestAsync(lobbiesResolveEndpoint,
                method, requestBodyForRegion, null, $"{nameof(LobbiesService)}.{nameof(CreateLobbyAsync)}", GetAuthToken());

            var lobby = ReadLobbyData(textResponse, nameof(CreateLobbyAsync));
            return AddOrUpdateSession(lobby);
        }

        /// <returns>Returns the internal cooldown for the Find Lobbies endpoint.</returns>
        public TimeSpan GetFindLobbiesCooldown()
        {
            return requestFactory.GetRequestCooldown(lobbiesResolveEndpoint + "/search", "POST");
        }

        /// <summary>Find current active Lobbies that you will be able to join.</summary>
        /// <param name="onRequestFinished">Callback that will be invoked when the request finished.</param>
        /// <param name="findOptions">Optional parameter to filter the returned Lobbies.</param>
        public void FindLobbies(Action<RequestResponse<IReadOnlyList<LobbyData>>> onRequestFinished, FindLobbyOptions findOptions = null)
        {
            if (WaitForOngoingRequest(onRequestFinished))
            {
                return;
            }

            var pathParams = "/search";

            var requestBodyForRegion = FetchLobbiesRequest.GetRequestBody(findOptions);

            requestFactory.SendRequest(lobbiesResolveEndpoint, pathParams, "POST", requestBodyForRegion,
                null, $"{nameof(LobbiesService)}.{nameof(FindLobbies)}", GetAuthToken(), response =>
                {
                    var requestResponse = RequestResponse<IReadOnlyList<LobbyData>>.GetRequestResponse(response);

                    if (requestResponse.Status == RequestStatus.Fail)
                    {
                        requestResponse.Result = new List<LobbyData>();

                        foreach (var callback in fetchLobbiesCallbackList)
                        {
                            callback?.Invoke(requestResponse);
                        }

                        fetchLobbiesCallbackList.Clear();
                        return;
                    }

                    try
                    {
                        var lobbies = OnFetch(response.Result);

                        requestResponse.Result = lobbies;
                    }
                    catch (Exception exception)
                    {
                        requestResponse.Status = RequestStatus.Fail;
                        requestResponse.Exception = new ResponseDeserializationException(Result.InvalidResponse, exception.Message);

                        logger.Error(Error.RuntimeCloudDeserializationException,
                            ("Request", nameof(FindLobbies)),
                            ("Response", response.Result),
                            ("exception", exception));
                    }
                    finally
                    {
                        foreach (var callback in fetchLobbiesCallbackList)
                        {
                            callback?.Invoke(requestResponse);
                        }

                        fetchLobbiesCallbackList.Clear();
                    }
                });
        }

        /// <summary>Find current active Lobbies that you will be able to join.</summary>
        /// <param name="findOptions">Optional parameter to filter the returned Lobbies.</param>
        /// <param name="cancellationToken">Optional parameter to cancel the request.</param>
        public async Task<IReadOnlyList<LobbyData>> FindLobbiesAsync(FindLobbyOptions findOptions = null, CancellationToken cancellationToken = default)
        {
            const string method = "POST";
            const string pathParams = "/search";

            var requestBodyForRegion = FetchLobbiesRequest.GetRequestBody(findOptions);

            await throttle.WaitForCooldown(lobbiesResolveEndpoint + pathParams, method, cancellationToken);

            var textResponse = await requestFactory.SendRequestAsync(lobbiesResolveEndpoint, pathParams,
                method, requestBodyForRegion, null, $"{nameof(LobbiesService)}.{nameof(FindLobbiesAsync)}", GetAuthToken());

            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var lobbies = OnFetch(textResponse);

                logger.Trace("FindLobbiesAsync - end", ("lobbies count", lobbies.Count));

                return lobbies;
            }
            catch (Exception exception)
            {
                logger.Error(Error.RuntimeCloudDeserializationException,
                    ("Request", nameof(FindLobbiesAsync)),
                    ("Response", textResponse),
                    ("exception", exception));

                throw new ResponseDeserializationException(Result.InvalidResponse, exception.Message);
            }
        }

        /// <summary>Join the supplied Lobby.</summary>
        /// <param name="lobby">The lobby to join.</param>
        /// <param name="onRequestFinished">Callback that will be invoked when the request finished.</param>
        /// <param name="playerAttr">Optional parameter to add player attributes to the joined Lobby.</param>
        /// <param name="password">Optional parameter to specify the Password to join a private Lobby.</param>
        /// <param name="reservations">
        /// <inheritdoc cref="JoinLobbyAsync(string, List{CloudAttribute}, string, LobbyReservations,
        /// CancellationToken)" path="/param[@name='reservations']/node()"/>
        /// </param>
        /// <seealso cref="JoinLobbyByCodeAsync"/>
        /// <seealso cref="JoinLobbyWithTokenAsync"/>
        public void JoinLobby(LobbyData lobby, Action<RequestResponse<LobbySession>> onRequestFinished,
            List<CloudAttribute> playerAttr = null, string password = null,
            [AllowNull] LobbyReservations reservations = null)
        {
            var pathParams = $"/{lobby.Id}/players";

            var requestBody = JoinLobbyRequest.GetRequestBody(playerAttr, password, reservations: reservations);

            requestFactory.SendRequest(lobbiesResolveEndpoint, pathParams, "POST", requestBody,
                null, $"{nameof(LobbiesService)}.{nameof(JoinLobby)}", GetAuthToken(), response =>
                {
                    var requestResponse = RequestResponse<LobbySession>.GetRequestResponse(response);

                    if (requestResponse.Status == RequestStatus.Fail)
                    {
                        onRequestFinished?.Invoke(requestResponse);
                        return;
                    }

                    LobbyData joinedLobby;

                    try
                    {
                        joinedLobby = DeserializeLobbyData(response.Result);
                    }
                    catch (Exception exception)
                    {
                        requestResponse.Status = RequestStatus.Fail;
                        requestResponse.Exception =
                            DeserializationFailed(nameof(JoinLobby), response.Result, exception);
                        onRequestFinished?.Invoke(requestResponse);
                        return;
                    }

                    logger.Trace("JoinLobby - end", ("lobbyId", joinedLobby.Id));
                    requestResponse.Result = AddOrUpdateSession(joinedLobby);
                    onRequestFinished?.Invoke(requestResponse);
                });
        }

        /// <summary>Join the supplied Lobby.</summary>
        /// <param name="lobby">The lobby to join.</param>
        /// <param name="playerAttr">Optional parameter to add player attributes to the joined Lobby.</param>
        /// <param name="password">Optional parameter to specify the Password to join a private Lobby.</param>
        /// <param name="reservations">
        /// <inheritdoc cref="JoinLobbyAsync(string, List{CloudAttribute}, string, LobbyReservations,
        /// CancellationToken)" path="/param[@name='reservations']/node()"/>
        /// </param>
        /// <param name="cancellationToken">Optional parameter to cancel the request.</param>
        /// <seealso cref="JoinLobbyByCodeAsync"/>
        /// <seealso cref="JoinLobbyWithTokenAsync"/>
        public Task<LobbySession> JoinLobbyAsync(LobbyData lobby, List<CloudAttribute> playerAttr = null,
            string password = null, [AllowNull] LobbyReservations reservations = null,
            CancellationToken cancellationToken = default)
            => JoinLobbyAsync(lobby.Id, playerAttr, password, reservations, cancellationToken);

        /// <inheritdoc cref="JoinLobbyAsync(LobbyData, List{CloudAttribute}, string, LobbyReservations,
        /// CancellationToken)"/>
        public Task<LobbySession> JoinLobbyAsync(LobbyData lobby, List<CloudAttribute> playerAttr,
            string password, CancellationToken cancellationToken)
            => JoinLobbyAsync(lobby.Id, playerAttr, password, null, cancellationToken);

        /// <summary>Join a coherence Cloud Lobby.</summary>
        /// <param name="lobbyId"><see cref="LobbyData.Id">Id</see> of the lobby to join.</param>
        /// <param name="playerAttr">Optional parameter to add player attributes to the joined Lobby.</param>
        /// <param name="password">Optional parameter to specify the password to join a private Lobby.</param>
        /// <param name="reservations">
        /// Optional slots to reserve for the rest of your party, joining you and reserving them in a single step.
        /// Fails with <see cref="ErrorCode.LobbyFull"/>, without joining, if the lobby can't fit everyone.
        /// Fails with <see cref="ErrorCode.LobbyReservationsNotAllowed"/> if it would reserve more new slots than the
        /// lobby's <see cref="LobbyData.MaxReservationsPerRequest"/>. Players already in the lobby, or who already
        /// have a reservation, are skipped and don't count, so every member of the party can send the same
        /// reservations.
        /// </param>
        /// <param name="cancellationToken">Optional parameter to cancel the request.</param>
        /// <seealso cref="LobbySession.ReserveSlotsAsync"/>
        /// <seealso cref="JoinLobbyByCodeAsync"/>
        /// <seealso cref="JoinLobbyWithTokenAsync"/>
        public Task<LobbySession> JoinLobbyAsync(string lobbyId, List<CloudAttribute> playerAttr = null,
            string password = null, [AllowNull] LobbyReservations reservations = null,
            CancellationToken cancellationToken = default)
            => JoinLobbyCoreAsync($"/{lobbyId}/players",
                JoinLobbyRequest.GetRequestBody(playerAttr, password, reservations: reservations),
                nameof(JoinLobbyAsync), cancellationToken);

        /// <summary>Join the Lobby via join code.</summary>
        /// <remarks>
        /// <para>
        /// A join code is chosen by whoever creates the Lobby,
        /// and is short enough for a player to read out and another to type in.
        /// It lasts as long as the Lobby does.
        /// </para>
        /// <para>
        /// Joining by code too often (currently over 10 a minute) fails with
        /// <see cref="ErrorCode.LobbyRateLimited"/>, to keep players from guessing codes.
        /// </para>
        /// </remarks>
        /// <param name="joinCode">
        /// The <see cref="CreateLobbyOptions.JoinCode">join code</see>, as the player typed it.
        /// </param>
        /// <param name="playerAttr">Optional parameter to add player attributes to the joined Lobby.</param>
        /// <param name="cancellationToken">Optional parameter to cancel the request.</param>
        /// <returns>The joined Lobby.</returns>
        /// <seealso cref="CreateLobbyOptions.JoinCode"/>
        /// <seealso cref="LobbyData.JoinCode"/>
        public async Task<LobbySession> JoinLobbyByCodeAsync(string joinCode,
            List<CloudAttribute> playerAttr = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(joinCode))
            {
                throw new ArgumentException("Pass the join code of the lobby to join.", nameof(joinCode));
            }

            return await JoinLobbyCoreAsync("/join-by-code",
                JoinLobbyByCodeRequest.GetRequestBody(joinCode, playerAttr), nameof(JoinLobbyByCodeAsync),
                cancellationToken);
        }

        private async Task<LobbySession> JoinLobbyCoreAsync(string pathParams, string requestBody, string requestName,
            CancellationToken cancellationToken)
        {
            var textResponse = await SendLobbyRequestAsync(pathParams, requestBody, requestName, cancellationToken);
            var lobby = ReadLobbyData(textResponse, requestName);
            var lobbySession = AddOrUpdateSession(lobby);

            if (lobbySession.LobbyData.RoomData is { } room)
            {
                RaisePlaySessionStarted(lobbySession.LobbyData.Id, room);
            }

            return lobbySession;
        }

        // Unlike a join, reserving slots as a member doesn't start the play session again.
        internal async Task ReserveSlotsAsync(LobbySession lobbySession, LobbyReservations reservations,
            CancellationToken cancellationToken)
        {
            if (reservations?.PlayerAccountIds is not { Count: > 0 })
            {
                throw new ArgumentException("Pass the players to reserve slots for.", nameof(reservations));
            }

            // The request goes to the join endpoint, which seats a player who isn't in the lobby. So it must never be
            // sent once the session is gone, such as after leaving while it waited for the request cooldown.
            using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken,
                lobbySession.DisposalToken);
            string textResponse;

            try
            {
                textResponse = await SendLobbyRequestAsync($"/{lobbySession.LobbyData.Id}/players",
                    JoinLobbyRequest.GetRequestBody(null, null, reservations: reservations),
                    nameof(ReserveSlotsAsync), cancellation.Token);
            }
            catch (OperationCanceledException) when (lobbySession.IsDisposed
                && !cancellationToken.IsCancellationRequested)
            {
                throw DisposedWhileReservingSlots(sent: false);
            }

            // Left or disposed while the request was in flight: don't bring the lobby back behind the caller's back.
            if (lobbySession.IsDisposed)
            {
                throw DisposedWhileReservingSlots(sent: true);
            }

            lobbySession.UpdateLobbyData(ReadLobbyData(textResponse, nameof(ReserveSlotsAsync)));
        }

        private static ObjectDisposedException DisposedWhileReservingSlots(bool sent)
            => new(nameof(LobbySession), sent
                ? "The lobby session was disposed while slots were being reserved in it. " +
                    "The slots may still have been reserved."
                : "The lobby session was disposed before the slots could be reserved. Nothing was sent.");

        private LobbyData ReadLobbyData(string textResponse, string requestName)
        {
            LobbyData lobby;

            try
            {
                lobby = DeserializeLobbyData(textResponse);
            }
            catch (Exception exception)
            {
                throw DeserializationFailed(requestName, textResponse, exception);
            }

            logger.Trace($"{requestName} - end", ("lobbyId", lobby.Id));
            return lobby;
        }

        private ResponseDeserializationException DeserializationFailed(string requestName, string textResponse,
            Exception exception)
        {
            logger.Error(Error.RuntimeCloudDeserializationException,
                ("Request", requestName),
                ("Response", textResponse),
                ("exception", exception));

            return new(Result.InvalidResponse, exception.Message);
        }

        internal async Task<string> CreateInviteTokenAsync(string lobbyId, TimeSpan? timeToLive, CancellationToken cancellationToken)
        {
            var textResponse = await SendLobbyRequestAsync($"/{lobbyId}/invite-token",
                CreateInviteTokenRequest.GetRequestBody(timeToLive), nameof(CreateInviteTokenAsync),
                cancellationToken);

            return ReadResponse<InviteTokenResponse>(textResponse, nameof(CreateInviteTokenAsync),
                "Could not read the invite token sent by coherence Cloud. The response has been logged.")
                .InviteToken;
        }

        internal Task InviteToLobbyAsync(string lobbyId, PlayerAccountId player, CancellationToken cancellationToken)
        {
            if (player == PlayerAccountId.None)
            {
                throw new ArgumentException("Pass the player you want to invite.", nameof(player));
            }

            return SendLobbyRequestAsync($"/{lobbyId}/invites",
                InviteToLobbyRequest.GetRequestBody(player), nameof(InviteToLobbyAsync), cancellationToken);
        }

        /// <summary>Join a Lobby via invitation.</summary>
        /// <param name="invite">The invite handed by <see cref="OnLobbyInviteReceived"/>.</param>
        /// <param name="playerAttr">Optional parameter to add player attributes to the joined Lobby.</param>
        /// <param name="reservations">
        /// <inheritdoc cref="JoinLobbyAsync(string, List{CloudAttribute}, string, LobbyReservations,
        /// CancellationToken)" path="/param[@name='reservations']/node()"/>
        /// </param>
        /// <param name="cancellationToken">Optional parameter to cancel the request.</param>
        /// <returns>The Lobby that the player joined.</returns>
        /// <remarks>
        /// The invite only admits you. Players you reserve slots for still need an invite of their own,
        /// or the lobby's password.
        /// </remarks>
        /// <seealso cref="OnLobbyInviteReceived"/>
        /// <seealso cref="JoinLobbyWithTokenAsync"/>
        public async Task<LobbySession> AcceptLobbyInviteAsync([DisallowNull] LobbyInvite invite,
            List<CloudAttribute> playerAttr = null, [AllowNull] LobbyReservations reservations = null,
            CancellationToken cancellationToken = default)
        {
            if (invite is null)
            {
                throw new ArgumentNullException(nameof(invite),
                    $"Pass the invite that arrived through {nameof(OnLobbyInviteReceived)}.");
            }

            return await JoinInvitedLobbyAsync(invite.Lobby.Id, playerAttr, invite.InviteToken, reservations,
                nameof(AcceptLobbyInviteAsync), cancellationToken);
        }

        /// <summary>Join the Lobby an invite token was minted for.</summary>
        /// <remarks>
        /// <para>
        /// An invite token is minted by coherence Cloud through
        /// <see cref="LobbySession.CreateInviteTokenAsync"/>.
        /// Hand it to other players. It can be used by multiple users, multiple times,
        /// until it expires.
        /// </para>
        /// <para>
        /// Joining with a token too often (currently over 30 a minute) fails with
        /// <see cref="ErrorCode.LobbyRateLimited"/>.
        /// </para>
        /// </remarks>
        /// <param name="inviteToken">The invite token, as it was handed to you.</param>
        /// <param name="playerAttr">Optional parameter to add player attributes to the joined Lobby.</param>
        /// <param name="cancellationToken">Optional parameter to cancel the request.</param>
        /// <returns>The joined Lobby.</returns>
        /// <seealso cref="LobbySession.CreateInviteTokenAsync"/>
        /// <seealso cref="AcceptLobbyInviteAsync"/>
        /// <seealso cref="ResolveLobbyFromInviteTokenAsync"/>
        public async Task<LobbySession> JoinLobbyWithTokenAsync([DisallowNull] string inviteToken,
            List<CloudAttribute> playerAttr = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(inviteToken))
            {
                throw new ArgumentException("Pass the invite token you were handed.", nameof(inviteToken));
            }

            return await JoinLobbyCoreAsync("/join-by-token",
                JoinLobbyByTokenRequest.GetRequestBody(inviteToken, playerAttr), nameof(JoinLobbyWithTokenAsync),
                cancellationToken);
        }

        /// <summary>Look up a Lobby via invite token.</summary>
        /// <remarks>
        /// Looking up tokens too often (currently over 30 a minute) fails with
        /// <see cref="ErrorCode.LobbyRateLimited"/>.
        /// </remarks>
        /// <param name="inviteToken">The invite token, as it was handed to you.</param>
        /// <param name="cancellationToken">Optional parameter to cancel the request.</param>
        /// <returns>The Lobby, in its current state.</returns>
        /// <seealso cref="JoinLobbyWithTokenAsync"/>
        /// <seealso cref="LobbySession.CreateInviteTokenAsync"/>
        public async Task<LobbyData> ResolveLobbyFromInviteTokenAsync([DisallowNull] string inviteToken,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(inviteToken))
            {
                throw new ArgumentException("Pass the invite token you were handed.", nameof(inviteToken));
            }

            var textResponse = await SendLobbyRequestAsync("/invites/resolve",
                ResolveInviteTokenRequest.GetRequestBody(inviteToken), nameof(ResolveLobbyFromInviteTokenAsync),
                cancellationToken);

            return ReadResponse<ResolveInviteTokenResponse>(textResponse, nameof(ResolveLobbyFromInviteTokenAsync),
                "Could not read the lobby the invite token names. The response has been logged.").Lobby;
        }

        // What an invite response says, or why it could not be read.
        private T ReadResponse<T>(string textResponse, string requestName, string deserializationError)
        {
            try
            {
                return Utils.CoherenceJson.DeserializeObject<T>(textResponse);
            }
            catch (Exception exception)
            {
                logger.Error(Error.RuntimeCloudDeserializationException,
                    ("Request", requestName),
                    ("Response", textResponse),
                    ("exception", exception));

                throw new ResponseDeserializationException(Result.InvalidResponse, deserializationError);
            }
        }

        private Task<LobbySession> JoinInvitedLobbyAsync(string lobbyId,
            List<CloudAttribute> playerAttr, string inviteToken, LobbyReservations reservations, string requestName,
            CancellationToken cancellationToken)
            => JoinLobbyCoreAsync($"/{lobbyId}/players",
                JoinLobbyRequest.GetRequestBody(playerAttr, password: null, inviteToken, reservations), requestName,
                cancellationToken);

        private async Task<string> SendLobbyRequestAsync(string pathParams, string body, string requestName,
            CancellationToken cancellationToken)
        {
            const string method = "POST";

            await throttle.WaitForCooldown(lobbiesResolveEndpoint + pathParams, method, cancellationToken);
            // A cooldown that ignores cancellation must not let a canceled request through.
            cancellationToken.ThrowIfCancellationRequested();

            return await requestFactory.SendRequestAsync(lobbiesResolveEndpoint, pathParams, method, body, null,
                $"{nameof(LobbiesService)}.{requestName}", GetAuthToken());
        }

        /// <summary>Refresh the current data for the supplied Lobby.</summary>
        /// <param name="lobby">Lobby you want to refresh the data of.</param>
        /// <param name="onRequestFinished">Callback that will be invoked when the request finished.</param>
        public void RefreshLobby(LobbyData lobby, Action<RequestResponse<LobbyData>> onRequestFinished)
        {
            RefreshLobby(lobby.Id, onRequestFinished);
        }

        /// <summary>Refresh the current data for the supplied Lobby.</summary>
        /// <param name="lobby">Lobby you want to refresh the data of.</param>
        public async Task<LobbyData> RefreshLobbyAsync(LobbyData lobby)
        {
            return await RefreshLobbyAsync(lobby.Id);
        }

        /// <summary>Refresh the current data for the Lobby with the supplied id.</summary>
        /// <param name="lobbyId">Lobby ID you want to refresh the data of.</param>
        /// <param name="onRequestFinished">Callback that will be invoked when the request finished.</param>
        public void RefreshLobby(string lobbyId, Action<RequestResponse<LobbyData>> onRequestFinished)
        {
            var pathParams = $"/{lobbyId}";

            requestFactory.SendRequest(lobbiesResolveEndpoint, pathParams, "GET", null,
                null, $"{nameof(LobbiesService)}.{nameof(RefreshLobby)}", GetAuthToken(), response =>
                {
                    var requestResponse = RequestResponse<LobbyData>.GetRequestResponse(response);

                    if (requestResponse.Status == RequestStatus.Fail)
                    {
                        onRequestFinished?.Invoke(requestResponse);
                        return;
                    }

                    LobbyData updatedLobby;

                    try
                    {
                        updatedLobby = DeserializeLobbyData(response.Result);
                    }
                    catch (Exception exception)
                    {
                        requestResponse.Status = RequestStatus.Fail;
                        requestResponse.Exception =
                            DeserializationFailed(nameof(RefreshLobby), response.Result, exception);
                        onRequestFinished?.Invoke(requestResponse);
                        return;
                    }

                    logger.Trace("RefreshLobby - end", ("lobbyId", updatedLobby.Id));
                    requestResponse.Result = updatedLobby;
                    CreateActiveLobbySessionIfPlayerIsInLobby(updatedLobby);
                    onRequestFinished?.Invoke(requestResponse);
                });
        }

        /// <summary>Refresh the current data for the Lobby with the supplied id.</summary>
        /// <param name="lobbyId">Lobby ID you want to refresh the data of.</param>
        /// <param name="cancellationToken">Optional parameter to cancel the request.</param>
        /// <exception cref="TaskCanceledException">
        /// Thrown when the operation is canceled using the <paramref name="cancellationToken"/>.
        /// </exception>
        /// <exception cref="ResponseDeserializationException">
        /// Thrown when deserializing the response from the server fails.
        /// </exception>
        public async Task<LobbyData> RefreshLobbyAsync(string lobbyId, CancellationToken cancellationToken = default)
        {
            const string method = "GET";
            var pathParams = $"/{lobbyId}";

            await throttle.WaitForCooldown(lobbiesResolveEndpoint + pathParams, method, cancellationToken);

            var textResponse = await requestFactory.SendRequestAsync(lobbiesResolveEndpoint, pathParams,
                method, null, null, $"{nameof(LobbiesService)}.{nameof(RefreshLobbyAsync)}", GetAuthToken());

            var updatedLobby = ReadLobbyData(textResponse, nameof(RefreshLobbyAsync));
            CreateActiveLobbySessionIfPlayerIsInLobby(updatedLobby);
            return updatedLobby;
        }

        /// <summary>Refresh the current data for the lobbies with the supplied ids.</summary>
        /// <param name="lobbyIds">IDs of the lobbies you want to refresh the data of.</param>
        /// <param name="cancellationToken">Optional parameter to cancel the request.</param>
        /// <exception cref="TaskCanceledException">
        /// Thrown when the operation is canceled using the <paramref name="cancellationToken"/>.
        /// </exception>
        /// <exception cref="ResponseDeserializationException">
        /// Thrown when deserializing the response from the server fails.
        /// </exception>
        public async Task<IReadOnlyList<LobbyData>> RefreshLobbiesAsync(string[] lobbyIds, CancellationToken cancellationToken = default)
        {
            const string method = "GET";
            var idsString = string.Join(",", lobbyIds);
            var pathParams = string.Format(refreshLobbiesAsyncPathParams, idsString);

            await throttle.WaitForCooldown(lobbiesResolveEndpoint + pathParams, method, cancellationToken);

            var textResponse = await requestFactory.SendRequestAsync(lobbiesResolveEndpoint, pathParams,
                method, null, null, $"{nameof(LobbiesService)}.{nameof(RefreshLobbyAsync)}", GetAuthToken());

            LobbyData[] updatedLobbies;

            try
            {
                updatedLobbies = DeserializeLobbiesData(textResponse);
            }
            catch (Exception exception)
            {
                throw DeserializationFailed(nameof(RefreshLobbiesAsync), textResponse, exception);
            }

            logger.Trace("RefreshLobbiesAsync - end", ("lobbyIds", idsString));

            foreach (var updatedLobby in updatedLobbies)
            {
                CreateActiveLobbySessionIfPlayerIsInLobby(updatedLobby);
            }

            return updatedLobbies;
        }

        /// <summary>Get stats for the usage of Lobbies for your current coherence Project.</summary>
        /// <param name="onRequestFinished">Callback that will be invoked when the request finished.</param>
        /// <param name="tags">Optional list of tags to filter the fetched stats.</param>
        /// <param name="regions">Optional list of regions to filter the fetched stats.</param>
        public void FetchLobbyStats(Action<RequestResponse<LobbyStats>> onRequestFinished, List<string> tags = null, List<string> regions = null)
        {
            var pathParams = "/stats";

            var requestBody = StatsRequest.GetRequestBody(tags, regions);

            requestFactory.SendRequest(lobbiesResolveEndpoint, pathParams, "POST", requestBody,
                null, $"{nameof(LobbiesService)}.{nameof(FetchLobbyStats)}", GetAuthToken(), response =>
                {
                    var requestResponse = RequestResponse<LobbyStats>.GetRequestResponse(response);

                    if (requestResponse.Status == RequestStatus.Fail)
                    {
                        onRequestFinished?.Invoke(requestResponse);
                        return;
                    }

                    try
                    {
                        var stats = Utils.CoherenceJson.DeserializeObject<LobbyStats>(response.Result);

                        requestResponse.Result = stats;
                    }
                    catch (Exception exception)
                    {
                        requestResponse.Status = RequestStatus.Fail;
                        requestResponse.Exception = new ResponseDeserializationException(Result.InvalidResponse, exception.Message);

                        logger.Error(Error.RuntimeCloudDeserializationException,
                            ("Request", nameof(FetchLobbyStats)),
                            ("Response", response.Result),
                            ("exception", exception));
                    }
                    finally
                    {
                        onRequestFinished?.Invoke(requestResponse);
                    }
                });
        }

        /// <summary>Get stats for the usage of Lobbies for your current coherence Project.</summary>
        /// <param name="tags">Optional list of tags to filter the fetched stats.</param>
        /// <param name="regions">Optional list of regions to filter the fetched stats.</param>
        /// <param name="cancellationToken">Optional parameter to cancel the request.</param>
        public async Task<LobbyStats> FetchLobbyStatsAsync(List<string> tags = null, List<string> regions = null, CancellationToken cancellationToken = default)
        {
            const string method = "POST";
            var pathParams = "/stats";

            var requestBody = StatsRequest.GetRequestBody(tags, regions);

            await throttle.WaitForCooldown(lobbiesResolveEndpoint + pathParams, method, cancellationToken);

            var textResponse = await requestFactory.SendRequestAsync(lobbiesResolveEndpoint, pathParams,
                method, requestBody, null, $"{nameof(LobbiesService)}.{nameof(FetchLobbyStats)}", GetAuthToken());

            try
            {
                var stats = Utils.CoherenceJson.DeserializeObject<LobbyStats>(textResponse);

                return stats;
            }
            catch (Exception exception)
            {
                logger.Error(Error.RuntimeCloudDeserializationException,
                    ("Request", nameof(FetchLobbyStats)),
                    ("Response", textResponse),
                    ("exception", exception));

                throw new ResponseDeserializationException(Result.InvalidResponse, exception.Message);
            }
        }

        /// <summary>Get a LobbySession instance for a Lobby that you have joined and you're a part of.</summary>
        /// <param name="lobbyId">Id of the lobby you want to get the <see cref="LobbySession"/> for.</param>
        /// <param name="cancellationToken">Optional parameter to cancel the request.</param>
        /// <exception cref="TaskCanceledException">
        /// Thrown when the operation is canceled using the <paramref name="cancellationToken"/>.
        /// </exception>
        /// <exception cref="ResponseDeserializationException">
        /// Thrown when the response from the server cannot be deserialized into a <see cref="LobbySession"/>.
        /// </exception>
        /// <returns>
        /// A task that represents the asynchronous operation. When the task completes successfully, it will contain
        /// a <see cref="LobbySession"/> if this player account is still in the lobby with the specified <paramref name="lobbyId"/>;
        /// otherwise, it will contain <see langword="null"/>.
        /// </returns>
        public async Task<LobbySession> GetActiveLobbySessionForLobbyId(string lobbyId, CancellationToken cancellationToken = default)
        {
            if (lobbySessions.TryGetValue(lobbyId, out var lobbySession) && !lobbySession.IsDisposed)
            {
                return lobbySession;
            }

            var lobbyData = await RefreshLobbyAsync(lobbyId, cancellationToken);
            return CreateActiveLobbySessionIfPlayerIsInLobby(lobbyData);
        }

        /// <summary>Iterate all active LobbySession instances.</summary>
        /// <remarks>A LobbySession instance is used to interface with a Lobby that you are a part of.</remarks>
        [Obsolete("This method is deprecated and will be removed in a future version. Use GetLobbySessionIds or GetLobbySessionsAsync() instead.")]
        [Deprecated("08/2025", 2, 1, 0, Reason = "Replaced by GetLobbySessionIds and GetLobbySessionsAsync because GetLobbySessions could not provide reliable results after logging out.")]
        public IEnumerable<LobbySession> GetLobbySessions()
        {
            var disposedSessionIds = new List<string>(0);

            foreach (var lobbySession in lobbySessions.Values)
            {
                if (!lobbySession.IsDisposed)
                {
                    yield return lobbySession;
                }
                else
                {
                    disposedSessionIds.Add(lobbySession.LobbyData.Id);
                }
            }

            RemoveSessions(disposedSessionIds);
        }

        /// <summary>
        /// Retrieves identifiers of all <see cref="LobbySession">lobby sessions</see> that this player account is currently a part of.
        /// </summary>
        public IEnumerable<string> GetLobbySessionIds()
        {
            var disposedSessionIds = new List<string>(0);

            foreach (var lobbySessionId in lobbySessionIds)
            {
                if (!lobbySessions.TryGetValue(lobbySessionId, out var lobbySession) || !lobbySession.IsDisposed)
                {
                    yield return lobbySessionId;
                }
                else
                {
                    disposedSessionIds.Add(lobbySessionId);
                }
            }

            RemoveSessions(disposedSessionIds);
        }

        /// <summary>
        /// Retrieves all <see cref="LobbySession">lobby sessions</see> that this player account is currently part of asynchronously.
        /// </summary>
        /// <param name="cancellationToken">Optional parameter to cancel the request.</param>
        /// <returns>
        /// A task that represents the asynchronous operation. When the task completes successfully, it will contain
        /// a list of <see cref="LobbySession">lobby sessions</see>.
        /// </returns>
        /// <exception cref="TaskCanceledException">
        /// Thrown when the operation is canceled using the <paramref name="cancellationToken"/>.
        /// </exception>
        /// <exception cref="ResponseDeserializationException">
        /// Thrown when the response from the server cannot be deserialized into a <see cref="LobbySession"/>.
        /// </exception>
        /// <example>
        /// <code source="Cloud/LobbiesService/GetLobbySessionsAsync.cs" language="csharp" region="Sample"/>
        /// </example>
        public async Task<IReadOnlyList<LobbySession>> GetLobbySessionsAsync(CancellationToken cancellationToken = default)
        {
            if (lobbySessionIds.Count is 0)
            {
                return Array.Empty<LobbySession>();
            }

            var results = new List<LobbySession>(lobbySessionIds.Count);
            var disposedSessionIds = new List<string>(0);
            foreach (var lobbySessionId in lobbySessionIds)
            {
                if (lobbySessions.TryGetValue(lobbySessionId, out var lobbySession))
                {
                    if (lobbySession.IsDisposed)
                    {
                        disposedSessionIds.Add(lobbySession.LobbyData.Id);
                    }
                    else
                    {
                        results.Add(lobbySession);
                    }
                }
            }
            RemoveSessions(disposedSessionIds);

            if (results.Count == lobbySessionIds.Count)
            {
                return results;
            }

            var fetchDataFor = lobbySessionIds.Except(results.Select(session => session.LobbyData.Id)).ToArray();
            if(fetchDataFor.Length == 0)
            {
                return results;
            }

            var lobbies = await RefreshLobbiesAsync(fetchDataFor, cancellationToken);
            foreach(var lobby in lobbies)
            {
                if (lobbySessions.TryGetValue(lobby.Id, out var lobbySession) && !lobbySession.IsDisposed)
                {
                    results.Add(lobbySession);
                }
            }

            disposedSessionIds.Clear();
            foreach (var result in results)
            {
                if (lobbySessions.TryGetValue(result.LobbyData.Id, out var lobbySession))
                {
                    if (lobbySession.IsDisposed)
                    {
                        disposedSessionIds.Add(lobbySession.LobbyData.Id);
                    }
                }
            }
            RemoveSessions(disposedSessionIds);

            return results;
        }

        public void Dispose()
        {
            ClearSessions();

            UnsubscribeFromInvitePush();

            authClient.OnLogin -= OnLogin;
            authClient.OnLogout -= OnLogout;

            if (shouldDisposeRequestFactoryAndAuthClient)
            {
                shouldDisposeRequestFactoryAndAuthClient = false;
                CloudCredentialsPair.Dispose(authClient, requestFactory);
            }

            if (shouldDisposeLogger)
            {
                logger.Dispose();
            }
        }

        public async ValueTask DisposeAsync()
        {
            ClearSessions();

            UnsubscribeFromInvitePush();

            authClient.OnLogin -= OnLogin;
            authClient.OnLogout -= OnLogout;

            if (shouldDisposeRequestFactoryAndAuthClient)
            {
                shouldDisposeRequestFactoryAndAuthClient = false;
                await CloudCredentialsPair.DisposeAsync(authClient, requestFactory);
            }

            if (shouldDisposeLogger)
            {
                logger.Dispose();
            }
        }

        // A request factory handed in from outside outlives this service, so the callback this service added
        // comes off with it. playCallback's own registration predates this and is left as it was.
        private void UnsubscribeFromInvitePush() => requestFactory.RemovePushCallback(inviteCallback, OnLobbyInvited);

        private LobbyData DeserializeLobbyData(string response)
        {
            var lobby = Utils.CoherenceJson.DeserializeObject<LobbyData>(response);

            // Every lobby response lists its players: without them, it can't be applied to a session.
            if (lobby.players is null)
            {
                throw new FormatException("The lobby in the response has no players.");
            }

            return StampRoomToken(lobby);
        }

        private LobbyData StampRoomToken(LobbyData lobby)
        {
            if (lobby.RoomData is not { } room)
            {
                return lobby;
            }

            AddTokenToRoom(ref room);
            lobby.RoomData = room;

            return lobby;
        }

        private LobbyData[] DeserializeLobbiesData(string response)
        {
            var lobbies = Utils.CoherenceJson.DeserializeObject<LobbiesData>(response).Lobbies
                ?? Array.Empty<LobbyData>();

            for (var i = 0; i < lobbies.Length; i++)
            {
                lobbies[i] = StampRoomToken(lobbies[i]);
            }

            return lobbies;
        }

        private List<LobbyData> OnFetch(string text) => Utils.CoherenceJson.DeserializeObject<List<LobbyData>>(text);

        [return: MaybeNull]
        private LobbySession CreateActiveLobbySessionIfPlayerIsInLobby(LobbyData updatedLobby)
        {
            foreach (var player in updatedLobby.Players)
            {
                if (player.Id.Equals(authClient.PlayerAccountId))
                {
                    return AddOrUpdateSession(updatedLobby);
                }
            }

            // Hand the session the lobby it left first, so its LobbyData is current when it disposes itself.
            if (lobbySessions.TryGetValue(updatedLobby.Id, out var lobbySession))
            {
                lobbySession.UpdateLobbyData(updatedLobby);
            }

            RemoveSession(updatedLobby.Id);
            return null;
        }

        private bool WaitForOngoingRequest(Action<RequestResponse<IReadOnlyList<LobbyData>>> onRequestFinished)
        {
            fetchLobbiesCallbackList.Add(onRequestFinished);
            return fetchLobbiesCallbackList.Count > 1;
        }

        private void OnPlayStarted(string responseBody)
        {
            PlayCallbackResponse response = default;

            try
            {
                response = Utils.CoherenceJson.DeserializeObject<PlayCallbackResponse>(responseBody);

                AddTokenToRoom(ref response.Room);
            }
            catch (Exception exception)
            {
                logger.Error(Error.RuntimeCloudDeserializationException,
                    ("Request", nameof(ConnectionClosedResponse)),
                    ("Response", responseBody),
                    ("exception", exception));

                return;
            }

            RaisePlaySessionStarted(response.LobbyId, response.Room);
        }

        private void OnLobbyInvited(string responseBody)
        {
            var inviteReceived = OnLobbyInviteReceived;

            if (inviteReceived is null)
            {
                logger.Trace("Lobby invite dropped: nothing is listening on " + nameof(OnLobbyInviteReceived));
                return;
            }

            LobbyInvitePayload payload;

            try
            {
                payload = Utils.CoherenceJson.DeserializeObject<LobbyInvitePayload>(responseBody);
            }
            catch (Exception exception)
            {
                logger.Error(Error.RuntimeCloudDeserializationException,
                    ("Request", inviteCallback),
                    ("Response", responseBody),
                    ("exception", exception));

                return;
            }

            inviteReceived.Invoke(new(payload.ResolveInviter(), StampRoomToken(payload.Lobby),
                string.IsNullOrEmpty(payload.InviteToken) ? null : payload.InviteToken));
        }

        private void RaisePlaySessionStarted(string lobbyId, RoomData room)
        {
            if (OnPlaySessionStarted != null)
            {
                OnPlaySessionStarted.Invoke(lobbyId, room);
            }
            else
            {
                OnPlaySessionStartedInternal?.Invoke(room);
            }
        }

        private void AddTokenToRoom(ref RoomData room)
        {
            if (runtimeSettings.IsWebGL)
            {
                room.Host.Ip = runtimeSettings.ApiEndpoint;
                room.Host.Port = runtimeSettings.RemoteWebPort;
            }

            room.AuthToken = GetAuthToken();
        }

        // The session the game already holds is updated in place: a second one would stay subscribed to every
        // lobby push, and run its own reservation timers, until logout.
        private LobbySession AddOrUpdateSession(LobbyData lobby)
        {
            if (lobbySessions.TryGetValue(lobby.Id, out var lobbySession) && !lobbySession.IsDisposed)
            {
                lobbySession.UpdateLobbyData(lobby);
            }
            else
            {
                lobbySession = new LobbySession(this, lobby, authClient, requestFactory, sessionLogger);
                lobbySessions[lobby.Id] = lobbySession;
            }

            lobbySessionIds.Add(lobby.Id);
            return lobbySession;
        }

        private void RemoveSessions(List<string> lobbyIds)
        {
            foreach (var lobbyId in lobbyIds)
            {
                RemoveSession(lobbyId);
            }
        }

        private void RemoveSession(string lobbyId)
        {
            // Bookkeeping first, so OnLobbyDisposed handlers see the lobby as left.
            lobbySessionIds.Remove(lobbyId);

            if (lobbySessions.Remove(lobbyId, out var lobbySession))
            {
                lobbySession.Dispose();
            }
        }

        private void ClearSessions()
        {
            // Disposing raises OnLobbyDisposed, whose handlers may call back into this service.
            var sessionsToDispose = lobbySessions.Values.ToArray();
            lobbySessions.Clear();
            lobbySessionIds.Clear();

            foreach (var lobbySession in sessionsToDispose)
            {
                lobbySession.Dispose();
            }
        }

        private void OnLogin(LoginResponse response)
        {
            // Game clients log out before logging in again, which already disposed every session. Simulators log in
            // again on each reconnect and token refresh without logging out; their sessions are kept, unless the
            // login response says the player is no longer in that lobby.
            var lobbyIds = response.LobbyIds is { } ids ? new HashSet<string>(ids) : new HashSet<string>();
            var droppedLobbyIds = lobbySessions
                .Where(entry => entry.Value.IsDisposed || !lobbyIds.Contains(entry.Key))
                .Select(entry => entry.Key)
                .ToArray();

            lobbySessionIds.Clear();
            lobbySessionIds.UnionWith(lobbyIds);

            foreach (var lobbyId in droppedLobbyIds)
            {
                if (lobbySessions.Remove(lobbyId, out var lobbySession))
                {
                    lobbySession.Dispose();
                }
            }
        }

        private void OnLogout() => ClearSessions();

        private SessionToken GetAuthToken()
        {
            var authToken = authClient.SessionToken;
#if UNITY
            authToken = !string.IsNullOrEmpty(SimulatorUtility.AuthToken)
                ? new(SimulatorUtility.AuthToken)
                : authToken;
#endif
            return authToken;
        }
    }
}
