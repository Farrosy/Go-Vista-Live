// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

#if UNITY_5_3_OR_NEWER
// IMPORTANT: Used by the pure-dotnet client, DON'T REMOVE.
#define UNITY
#endif

namespace Coherence.Cloud
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Common;
    using Log;
    using Runtime;
    using Utils;

    public class GameServersService : IGameServersService, IAsyncDisposable, IDisposable
    {
        private readonly Logger logger = Log.GetLogger<GameServersService>();

        private readonly IAuthClient authClient;
        private readonly IRequestFactory requestFactory;
        private readonly string authToken;

        private const string DeployPath = "/gameservers";
        private const string ListPath = "/gameservers";
        private const string MatchPath = "/gameservers/match";

        private SecretsCache secretsCache = new();
        private bool shouldDisposeRequestFactoryAndAuthClient;
        private readonly IPlayerAccountProvider playerAccountProviderToDispose;

        internal GameServersService() { } // for test doubles

        public GameServersService(CloudCredentialsPair credentialsPair = null,
            IRuntimeSettings runtimeSettings = null)
        {
#if UNITY
            runtimeSettings ??= RuntimeSettings.Instance;
#endif
            if (credentialsPair is null)
            {
                shouldDisposeRequestFactoryAndAuthClient = true;
                playerAccountProviderToDispose =
                        #if UNITY
                        SimulatorUtility.IsSimulator ? throw new ArgumentNullException(nameof(credentialsPair)) :
                        #endif
                        new NewPlayerAccountProvider(runtimeSettings: runtimeSettings);

                credentialsPair = CloudCredentialsFactory.ForClient(runtimeSettings, playerAccountProviderToDispose);
                credentialsPair.authClient.LoginAsGuest().Then(task =>
                {
                    if (task.IsFaulted)
                    {
                        logger.Warning(Warning.RuntimeCloudLoginFailedMsg, task.Exception.ToString());
                    }
                    else if (task.IsCompletedSuccessfully && task.Result.Type is not Result.Success)
                    {
                        logger.Error(task.Result.Error, task.Result.ToString());
                    }
                });
            }
            requestFactory = credentialsPair.RequestFactory;
            authClient = credentialsPair.AuthClient;
            authToken = credentialsPair.authClient.SessionToken;
        }

        public async Task<GameServerDeployResult> DeployAsync(GameServerDeployOptions deployOptions)
        {
            var reqName = $"{nameof(GameServersService)}.{nameof(DeployAsync)}";
            logger.Trace($"{reqName} - start",
                ("region", deployOptions.Region),
                ("slug", deployOptions.Slug),
                ("tag", deployOptions.Tag),
                ("kv", "{" + string.Join(",", deployOptions.KV?.Select(kv => kv.Key + "=" + kv.Value) ?? Enumerable.Empty<string>()) + "}"),
                ("size", deployOptions.Size),
                ("max_players", deployOptions.MaxPlayers));

            var reqBody = CoherenceJson.SerializeObject(deployOptions);
            var task = requestFactory.SendRequestAsync(DeployPath, "", "POST", reqBody, null, reqName, authToken);
            string respBody;
            try
            {
                respBody = await task;
            }
            catch (RequestException ex) when (ex.ErrorCode == ErrorCode.TooManyRequests)
            {
                logger.Warning(Warning.RuntimeCloudGameServersTooManyRequests, ("Request", reqName));
                throw;
            }

            try
            {
                var gameServer = CoherenceJson.DeserializeObject<GameServerDeployResult>(respBody);
                logger.Trace($"{reqName} - end", ("id", gameServer.Id), ("secret", gameServer.Secret));
                secretsCache.Add(gameServer.Id, gameServer.Secret);
                return gameServer;
            }
            catch (Exception ex)
            {
                logger.Error(Error.RuntimeCloudDeserializationException,
                    ("Request", reqName),
                    ("Response", respBody),
                    ("exception", ex));
                throw new ResponseDeserializationException(Result.InvalidResponse, ex.Message);
            }
        }

        public async Task<List<GameServerData>> ListAsync(GameServerListOptions listOptions)
        {
            var reqName = $"{nameof(GameServersService)}.{nameof(ListAsync)}";
            logger.Trace($"{reqName} - start",
                ("region", listOptions.Region),
                ("slug", listOptions.Slug),
                ("tag", listOptions.Tag),
                ("suspended", listOptions.Suspended),
                ("size", listOptions.Size),
                ("max_players", listOptions.MaxPlayers));

            var queryParams = listOptions.QueryParams();
            var task = requestFactory.SendRequestAsync(ListPath, queryParams, "GET", null, null, reqName, authToken);
            string respBody;
            try
            {
                respBody = await task;
            }
            catch (RequestException ex) when (ex.ErrorCode == ErrorCode.TooManyRequests)
            {
                logger.Warning(Warning.RuntimeCloudGameServersTooManyRequests, ("Request", reqName));
                throw;
            }

            try
            {
                var gameServers = CoherenceJson.DeserializeObject<List<GameServerData>>(respBody);
                logger.Trace($"{reqName} - end", ("num", gameServers.Count));
                return gameServers;
            }
            catch (Exception ex)
            {
                logger.Error(Error.RuntimeCloudDeserializationException,
                    ("Request", nameof(ListAsync)),
                    ("Response", respBody),
                    ("exception", ex));
                throw new ResponseDeserializationException(Result.InvalidResponse, ex.Message);
            }
        }

        public async Task<OptionalGameServerData> MatchAsync(GameServerMatchOptions matchOptions)
        {
            var reqName = $"{nameof(GameServersService)}.{nameof(MatchAsync)}";
            logger.Trace($"{reqName} - start",
                ("region", matchOptions.Region),
                ("slug", matchOptions.Slug),
                ("tag", matchOptions.Tag),
                ("size", matchOptions.Size),
                ("max_players", matchOptions.MaxPlayers));

            var reqBody = CoherenceJson.SerializeObject(matchOptions);
            var task = requestFactory.SendRequestAsync(MatchPath, "", "POST", reqBody, null, reqName, authToken);
            string respBody;
            try
            {
                respBody = await task;
            }
            catch (RequestException ex) when (ex.ErrorCode == ErrorCode.TooManyRequests)
            {
                logger.Warning(Warning.RuntimeCloudGameServersTooManyRequests, ("Request", reqName));
                throw;
            }

            try
            {
                var gameServerData = CoherenceJson.DeserializeObject<OptionalGameServerData>(respBody);
                logger.Trace($"{reqName} - end", ("id", gameServerData.GameServerData?.Id));
                return gameServerData;
            }
            catch (Exception ex)
            {
                logger.Error(Error.RuntimeCloudDeserializationException,
                    ("Request", reqName),
                    ("Response", respBody),
                    ("exception", ex));
                throw new ResponseDeserializationException(Result.InvalidResponse, ex.Message);
            }
        }

        public async Task<GameServerData> GetAsync(ulong serverId)
        {
            var reqName = $"{nameof(GameServersService)}.{nameof(GetAsync)}";
            logger.Trace($"{reqName} - start", ("id", serverId));

            var task = requestFactory.SendRequestAsync(GetGetPath(serverId), "", "GET", null, null, reqName, authToken);
            string respBody;
            try
            {
                respBody = await task;
            }
            catch (RequestException ex) when (ex.ErrorCode == ErrorCode.TooManyRequests)
            {
                logger.Warning(Warning.RuntimeCloudGameServersTooManyRequests, ("Request", reqName));
                throw;
            }

            try
            {
                var gameServerData = CoherenceJson.DeserializeObject<GameServerData>(respBody);
                logger.Trace($"{reqName} - end", ("id", gameServerData.Id));
                return gameServerData;
            }
            catch (Exception ex)
            {
                logger.Error(Error.RuntimeCloudDeserializationException,
                    ("Request", reqName),
                    ("Response", respBody),
                    ("exception", ex));
                throw new ResponseDeserializationException(Result.InvalidResponse, ex.Message);
            }
        }

        public async Task SuspendAsync(ulong serverId)
        {
            await SetStateAsync(serverId, true);
        }

        public async Task ResumeAsync(ulong serverId)
        {
            await SetStateAsync(serverId, false);
        }

        private async Task SetStateAsync(ulong serverId, bool suspended)
        {
            var reqName = $"{nameof(GameServersService)}.{nameof(SetStateAsync)}";
            logger.Trace($"{reqName} - start",
                ("id", serverId),
                ("suspended", suspended),
                ("secret", secretsCache.Get(serverId)));

            var stateOptions = new GameServerStateOptions
            {
                Suspended = suspended,
                Secret = secretsCache.Get(serverId),
            };
            var reqBody = CoherenceJson.SerializeObject(stateOptions);
            try
            {
                await requestFactory.SendRequestAsync(GetStatePath(serverId), "", "POST", reqBody, null, reqName,
                    authToken);
            }
            catch (RequestException ex) when (ex.ErrorCode == ErrorCode.TooManyRequests)
            {
                logger.Warning(Warning.RuntimeCloudGameServersTooManyRequests, ("Request", reqName));
                return;
            }

            logger.Trace($"{reqName} - end", ("id", serverId), ("suspended", suspended));
        }

        public async Task DeleteAsync(ulong serverId)
        {
            var reqName = $"{nameof(GameServersService)}.{nameof(DeleteAsync)}";
            logger.Trace($"{reqName} - start", ("id", serverId), ("secret", secretsCache.Get(serverId)));

            var deleteOptions = new GameServerDeleteOptions
            {
                Secret = secretsCache.Get(serverId),
            };
            var reqBody = CoherenceJson.SerializeObject(deleteOptions);
            try
            {
                await requestFactory.SendRequestAsync(GetDeletePath(serverId), "", "DELETE", reqBody, null, reqName,
                    authToken);
            }
            catch (RequestException ex) when (ex.ErrorCode == ErrorCode.TooManyRequests)
            {
                logger.Warning(Warning.RuntimeCloudGameServersTooManyRequests, ("Request", reqName));
                return;
            }

            secretsCache.Remove(serverId);
            logger.Trace($"{reqName} - end", ("id", serverId));
        }

        //
        // Path helpers
        //

        private static string GetGetPath(ulong id)
        {
            return $"/gameservers/{id}";
        }

        private static string GetStatePath(ulong id)
        {
            return $"/gameservers/{id}/status";
        }

        private static string GetDeletePath(ulong id)
        {
            return $"/gameservers/{id}";
        }

        public void Dispose()
        {
            if (shouldDisposeRequestFactoryAndAuthClient)
            {
                shouldDisposeRequestFactoryAndAuthClient = false;
                CloudCredentialsPair.Dispose(authClient, requestFactory);
            }

            playerAccountProviderToDispose?.Dispose();
        }

        public async ValueTask DisposeAsync()
        {
            if (shouldDisposeRequestFactoryAndAuthClient)
            {
                shouldDisposeRequestFactoryAndAuthClient = false;
                await CloudCredentialsPair.DisposeAsync(authClient, requestFactory);
            }

            playerAccountProviderToDispose?.Dispose();
        }
    }
}
