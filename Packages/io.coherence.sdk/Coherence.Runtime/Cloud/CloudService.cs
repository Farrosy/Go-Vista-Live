// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

#if UNITY_5_3_OR_NEWER
// IMPORTANT: Used by the pure-dotnet client, DON'T REMOVE.
// Any changes to the Unity version of the request should be reflected
// in the HttpClient version.
// TODO: Separate Http client impl. with common options/policy layer (coherence/unity#1764)
#define UNITY
#endif

namespace Coherence.Cloud
{
    using Common;
    using Runtime;
    using System;
    using System.Collections;
    using System.ComponentModel;
    using System.Diagnostics.CodeAnalysis;
    using System.Threading.Tasks;
    using Runtime.Utils;
    using Log;

    /// <summary>
    ///     Runtime API to be able to interface with an Organization and Project from the coherence Cloud.
    ///     Check the coherence Cloud tab in the coherence Hub window for more details.
    /// </summary>
    public class CloudService : IDisposable
    {
        internal const string RequestIDHeader = "X-Coherence-Request-ID";
        internal const string ClientVersionHeader = "X-Coherence-Client";
        internal const string SchemaIdHeader = "X-Coherence-Schema-ID";
        internal const string RSVersionHeader = "X-Coherence-Engine";

        internal bool shouldDisposeRequestFactoryAndAuthClient;

        /// <summary>
        ///     Returns true when the Web Socket is connected.
        /// </summary>
        public bool IsConnectedToCloud => requestFactory.IsReady;

        /// <summary>
        ///     Returns true when the Web Socket is connected and when we are logged in to coherence Cloud.
        /// </summary>
        public bool IsLoggedIn => requestFactory.IsReady && authClient.LoggedIn;

        /// <summary>
        ///     RuntimeSettings that you pass through the constructor, if none is specified, RuntimeSettings.Instance will be used.
        /// </summary>
        public IRuntimeSettings RuntimeSettings => runtimeSettings;

        /// <summary>
        /// Fetch the available active worlds.
        /// </summary>
        public WorldsService Worlds { get; }

        /// <summary>
        /// Fetch, create and delete rooms.
        /// </summary>
        public CloudRooms Rooms { get; }

        /// <summary>
        /// Create, find, join and manage lobbies.
        /// </summary>
        /// <remarks>
        /// Lobbies can be used to group players together to enable matchmaking,
        /// exchanging messages in real-time, and more.
        /// </remarks>
        public LobbiesService Lobbies => Rooms.LobbyService;

        /// <summary>
        /// Fetch information about available regions.
        /// </summary>
        public RegionsService Regions => Rooms.RegionsService;

        [EditorBrowsable(EditorBrowsableState.Never)]
        [Deprecated("08/2025", 2, 1, 0, Reason = "Accessors for still relevant services were migrated to " + nameof(CloudService) + " .")]
        public GameServices GameServices { get; }

        /// <summary>
        /// Controls game servers.
        /// </summary>
        public IGameServersService GameServers { get; }

        /// <summary>
        /// Report players for policy violations.
        /// </summary>
        [NotNull]
        public ReportsService Reports { get; }

        /// <summary>
        /// Syncs the friend lists from other platforms into coherence Cloud.
        /// </summary>
        [NotNull]
        public FriendsService Friends { get; }

        /// <summary>
        /// Store and load game data.
        /// </summary>
#pragma warning disable CS0618 // Type or member is obsolete
        public CloudStorage CloudStorage => GameServices.CloudStorage;
#pragma warning restore CS0618 // Type or member is obsolete

        /// <summary>
        /// Callback fired when the WebSocket connection is interrupted or lost. The WebSocket will attempt to reconnect automatically.
        /// </summary>
        public event Action OnConnectionLost;

        internal AnalyticsClient AnalyticsClient { get; private set; }
        internal bool IsDisposed { get; private set; }

        private readonly IRequestFactoryInternal requestFactory;
        private readonly IAuthClientInternal authClient;
        private readonly IRuntimeSettings runtimeSettings;
        private IPlayerAccountProvider playerAccountProvider;
        private bool shouldDisposePlayerAccountProvider;

        internal IRequestFactoryInternal RequestFactory => requestFactory;
        internal IAuthClientInternal AuthClient => authClient;
        internal IPlayerAccountProvider PlayerAccountProvider => playerAccountProvider;

        /// <summary>
        ///     Creates a standalone <see cref="CloudService"/> for a client.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The returned instance is owned by the caller, who is responsible for
        /// <see cref="Dispose">disposing</see> it.
        /// When used within Unity's MonoBehaviour lifecycle,
        /// a <see langword="using"/> statements might not be enough to ensure disposal.
        /// In such cases, dispose manually on deactivation, destruction, application quit event,
        /// or alike.
        /// </para>
        /// </remarks>
        /// <param name="runtimeSettings">
        /// Settings to use. If <see langword="null"/>, <see cref="Coherence.RuntimeSettings.Instance"/> is used.
        /// </param>
        /// <returns>A new <see cref="CloudService"/> owned by the caller.</returns>
        public static CloudService ForClient(IRuntimeSettings runtimeSettings = null)
            => ForClient(null, runtimeSettings, null, false);

        internal static CloudService ForClient([MaybeNull] IPlayerAccountProvider playerAccountProvider, IRuntimeSettings runtimeSettings = null, CloudUniqueId cloudUniqueId = default, bool autoLoginAsGuest = false)
        {
#if UNITY
            runtimeSettings ??= Coherence.RuntimeSettings.Instance;
#endif
            CloudService result = null;
            var shouldDisposePlayerAccountProvider = false;
            if (playerAccountProvider is null)
            {
                playerAccountProvider = new NewPlayerAccountProvider(() => result, cloudUniqueId, runtimeSettings);
                shouldDisposePlayerAccountProvider = true;
            }

            var credentialsPair = CloudCredentialsFactory.ForClient(runtimeSettings, playerAccountProvider);
            result = new(credentialsPair, runtimeSettings, playerAccountProvider) { shouldDisposeRequestFactoryAndAuthClient = true };
            result.playerAccountProvider = playerAccountProvider;
            result.shouldDisposePlayerAccountProvider = shouldDisposePlayerAccountProvider;

            if (autoLoginAsGuest)
            {
                result.authClient.LoginAsGuest().Then(task =>
                {
                    if (task.IsFaulted)
                    {
                        Log.GetLogger<CloudService>().Warning(Warning.RuntimeCloudLoginFailedMsg, task.Exception.ToString());
                    }
                    else if (task.IsCompletedSuccessfully && task.Result.Type is not Result.Success)
                    {
                        Log.GetLogger<CloudService>().Error(task.Result.Error, task.Result.ToString());
                    }
                });
            }

            return result;
        }

#if UNITY
        internal static CloudService ForSimulator(IRuntimeSettings runtimeSettings = null)
        {
            runtimeSettings ??= Coherence.RuntimeSettings.Instance;
            CloudService result = null;
            var playerAccountProvider = new SimulatorPlayerAccountProvider(() => result);
            var credentialsPair = CloudCredentialsFactory.ForSimulator(runtimeSettings, playerAccountProvider);
            result = new(credentialsPair, runtimeSettings, playerAccountProvider) { shouldDisposeRequestFactoryAndAuthClient = !SimulatorUtility.UseSharedCloudCredentials };
            result.playerAccountProvider = playerAccountProvider;
            result.shouldDisposePlayerAccountProvider = true;
            return result;
        }
#endif

        private CloudService(CloudCredentialsPair credentials, IRuntimeSettings runtimeSettings, IPlayerAccountProvider playerAccountProvider)
        {
            this.playerAccountProvider = playerAccountProvider;
            this.runtimeSettings = runtimeSettings;
            authClient = credentials.authClient;
            requestFactory = credentials.requestFactory;
            GameServices = new(credentials);
            Rooms = new(credentials, runtimeSettings, playerAccountProvider);
            Worlds = new(credentials, runtimeSettings, playerAccountProvider);
            AnalyticsClient = new(playerAccountProvider, runtimeSettings, requestFactory);
            GameServers = new GameServersService(credentials, runtimeSettings);
            Reports = new(credentials, runtimeSettings, playerAccountProvider, throttle: null);
            Friends = new(credentials);
            requestFactory.OnWebSocketConnectionError += OnWebSocketError;
        }

        /// <remarks>
        /// <para>
        /// This constructor is only meant to be used for testing purposes, enabling the injecting of test doubles;
        /// usually <see cref="ForClient"/> should be used instead.
        /// </para>
        /// <para>
        /// Note: Calling <see cref="Dispose"/> on this object will also result in Dispose being called on all the injected services.
        /// </para>
        /// </remarks>
        internal CloudService(CloudCredentialsPair credentials, IRuntimeSettings runtimeSettings, IPlayerAccountProvider playerAccountProvider, GameServices gameServices = null, CloudRooms rooms = null, WorldsService worlds = null, AnalyticsClient analyticsClient = null, GameServersService gameServers = null, ReportsService reports = null, FriendsService friends = null)
        {
            this.playerAccountProvider = playerAccountProvider;
            this.runtimeSettings = runtimeSettings;
            authClient = credentials.authClient;
            requestFactory = credentials.requestFactory;
            GameServices = gameServices;
            Rooms = rooms;
            Worlds = worlds;
            AnalyticsClient = analyticsClient;
            GameServers = gameServers;
            Reports = reports;
            Friends = friends;
            requestFactory.OnWebSocketConnectionError += OnWebSocketError;
        }

        /// <summary>
        /// Wait for the CloudService to be ready within a Coroutine.
        /// </summary>
        public IEnumerator WaitForCloudServiceLoginRoutine()
        {
            while (!IsLoggedIn)
            {
                yield return null;
            }
        }

        /// <summary>
        /// Async method you can use to wait for the CloudService to be ready.
        /// </summary>
        /// <returns>Returns true when the CloudService is ready.</returns>
        public async Task<bool> WaitForCloudServiceLoginAsync(int millisecondsPollDelay)
        {
            while (!IsLoggedIn)
            {
                await TimeSpan.FromMilliseconds(millisecondsPollDelay);
            }

            return true;
        }

        internal async Task<bool> WaitForCloudServiceLoginAsync()
        {
            while (!IsLoggedIn)
            {
                await Task.Yield();
            }

            return true;
        }

        /// <summary>
        /// Releases all resources owned by this <see cref="CloudService"/>, including the cloud
        /// services it exposes (<see cref="Worlds"/>, <see cref="Rooms"/>, <see cref="Lobbies"/>,
        /// <see cref="GameServers"/>, <see cref="CloudStorage"/>).
        /// </summary>
        /// <remarks>
        /// Only dispose an instance you own. You own it if you created it yourself via
        /// <see cref="ForClient(IRuntimeSettings)"/> and did not hand ownership to anything else.
        /// <para>
        /// Do <b>not</b> call this on an instance you obtained from another object:
        /// <see cref="PlayerAccount.Services"/> and the toolkit's <c>CoherenceBridge.CloudService</c>
        /// are owned and disposed by the player account / bridge respectively. Disposing such a
        /// borrowed instance tears down services that its owner is still using.
        /// </para>
        /// </remarks>
        public void Dispose()
        {
            if (IsDisposed)
            {
                return;
            }

            IsDisposed = true;

            requestFactory.OnWebSocketConnectionError -= OnWebSocketError;
            GameServices.Dispose();
            Rooms.Dispose();
            Worlds.Dispose();
            Reports.Dispose();
            Friends.Dispose();

            if (shouldDisposeRequestFactoryAndAuthClient)
            {
                shouldDisposeRequestFactoryAndAuthClient = false;
                CloudCredentialsPair.Dispose(authClient, requestFactory);
            }

            if (shouldDisposePlayerAccountProvider)
            {
                shouldDisposePlayerAccountProvider = false;
                playerAccountProvider.Dispose();
            }
        }

        /// <param name="waitForOngoingOperationsToFinish">
        /// If true, then ongoing and queued cloud operations are allowed to finish before the services
        /// performing them are shut down; otherwise, the operations should be canceled immediately.
        /// </param>
        internal async ValueTask DisposeAsync(bool waitForOngoingOperationsToFinish)
        {
            if (IsDisposed)
            {
                return;
            }

            IsDisposed = true;
            requestFactory.OnWebSocketConnectionError -= OnWebSocketError;
            var disposeGameServices = GameServices.DisposeAsync(waitForOngoingOperationsToFinish);
            var disposeRooms = Rooms.DisposeAsync();
            var disposeWorlds = Worlds.DisposeAsync();

            // Don't start disposing request factory until we've finished disposing all the services that depend on it.
            await disposeGameServices;
            await disposeRooms;
            await disposeWorlds;
            Reports.Dispose();
            await Friends.DisposeAsync(waitForOngoingOperationsToFinish);

            if (shouldDisposeRequestFactoryAndAuthClient)
            {
                shouldDisposeRequestFactoryAndAuthClient = false;
                await CloudCredentialsPair.DisposeAsync(authClient, requestFactory);
            }

            if (shouldDisposePlayerAccountProvider)
            {
                shouldDisposePlayerAccountProvider = false;
                playerAccountProvider.Dispose();
            }
        }

        private void OnWebSocketError()
        {
            OnConnectionLost?.Invoke();
        }
    }
}
