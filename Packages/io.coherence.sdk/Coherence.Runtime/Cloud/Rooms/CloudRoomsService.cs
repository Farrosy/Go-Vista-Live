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
    using Runtime.Utils;
    using Logger = Log.Logger;

    /// <summary>
    /// Public API to interface with Rooms from your Project in the coherence Cloud.
    /// If you wish to create, delete or fetch rooms in a self-hosted Replication Server, you can instantiate
    /// a <see cref="ReplicationServerRoomsService" /> instead.
    /// </summary>
    public class CloudRoomsService : IRoomsService, IAsyncDisposable, IDisposable
    {
        private IRequestFactory requestFactory;
        private RoomsCache roomsCache;

        private IAuthClientInternal authClient;
        private readonly IRuntimeSettings runtimeSettings;
        private readonly Logger logger = Log.GetLogger<CloudRoomsService>();

        private readonly string roomsResolveEndpoint = "/rooms";

        private List<Action<RequestResponse<IReadOnlyList<RoomData>>>> fetchRoomsCallbackList = new List<Action<RequestResponse<IReadOnlyList<RoomData>>>>();

        private bool isFetchingRooms;

        private string region;
        private bool shouldDisposeRequestFactoryAndAuthClient;

        /// <summary>
        /// List of cached Rooms that were fetched by the FetchRooms method.
        /// </summary>
        public IReadOnlyList<RoomData> CachedRooms => roomsCache.CachedRooms;

        public CloudRoomsService(string region, CloudCredentialsPair credentialsPair, IRuntimeSettings runtimeSettings) : this(region, credentialsPair, runtimeSettings, null)  { }

        internal CloudRoomsService(string region, [MaybeNull] CloudCredentialsPair credentialsPair, [MaybeNull] IRuntimeSettings runtimeSettings, [MaybeNull] IPlayerAccountProvider playerAccountProvider)
        {
#if UNITY
            runtimeSettings ??= RuntimeSettings.Instance;
#endif
            this.region = region;
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
                        logger.Warning(Warning.RuntimeCloudLoginFailedMsg, task.Exception.ToString());
                    }
                    else if (task.IsCompletedSuccessfully && task.Result.Type is not Result.Success)
                    {
                        logger.Error(task.Result.Error, task.Result.ToString());
                    }
                });
            }

            this.requestFactory = credentialsPair.RequestFactory;
            this.authClient = credentialsPair.authClient;
            this.roomsCache = new RoomsCache(region);
        }

        /// <returns>Returns the internal cooldown for the Remove Room endpoint.</returns>
        public TimeSpan GetRemoveRoomCooldown()
        {
            return requestFactory.GetRequestCooldown(roomsResolveEndpoint, "DELETE");
        }

        /// <summary>Remove an existing room that you have created in the coherence Cloud.</summary>
        /// <param name="roomData">The room to remove.</param>
        /// <param name="onRequestFinished">Callback that will be invoked when the request finished.</param>
        public void RemoveRoom(RoomData roomData, Action<RequestResponse<string>> onRequestFinished)
        {
            var pathParams = $"/{roomData.UniqueId}/{roomData.Secret}";

            logger.Trace("RemoveRoom", ("endpoint", roomsResolveEndpoint), ("path", pathParams));

            requestFactory.SendRequest(roomsResolveEndpoint, pathParams, "DELETE", null, null, $"{nameof(CloudRoomsService)}.{nameof(RemoveRoom)}", GetAuthToken(), response =>
            {
                var requestResponse = RequestResponse<string>.GetRequestResponse(response);

                if (requestResponse.Status == RequestStatus.Fail)
                {
                    onRequestFinished?.Invoke(requestResponse);
                    return;
                }

                roomsCache.RemoveRoom(roomData.UniqueId);
                onRequestFinished?.Invoke(requestResponse);
            });
        }

        /// <summary>Remove an existing room that you have created in the coherence Cloud.</summary>
        /// <param name="onRequestFinished">Callback that will be invoked when the request finished.</param>
        /// <param name="uniqueID">Unique ID of the room you wish to remove. Can be found in RoomData.UniqueId</param>
        /// <param name="secret">Token that will allow you to remove the room. Can be found in RoomData.Secret</param>
        [Obsolete("Use the " + nameof(RoomData) + " overload instead."), Deprecated("05/2026", 2, 2, 0, Reason = "Use the " + nameof(RoomData) + " overload instead.")]
        public void RemoveRoom(ulong uniqueID, string secret, Action<RequestResponse<string>> onRequestFinished)
            => RemoveRoom(new RoomData { UniqueId = uniqueID, Secret = secret }, onRequestFinished);

        /// <summary>Unlist an existing room that you have created in the coherence Cloud.</summary>
        /// <param name="roomData">The room to unlist.</param>
        /// <param name="onRequestFinished">Callback that will be invoked when the request finished.</param>
        public void UnlistRoom(RoomData roomData, Action<RequestResponse<string>> onRequestFinished)
        {
            var pathParams = $"/{roomData.UniqueId}/unlist";

            var requestBody = Utils.CoherenceJson.SerializeObject(new RoomUnlistRequest()
            {
                Secret = roomData.Secret
            });

            logger.Trace("UnlistRoom", ("endpoint", roomsResolveEndpoint), ("path", pathParams), ("body", requestBody));

            requestFactory.SendRequest(roomsResolveEndpoint, pathParams, "PATCH", requestBody, null, $"{nameof(CloudRoomsService)}.{nameof(UnlistRoom)}", GetAuthToken(), response =>
            {
                var requestResponse = RequestResponse<string>.GetRequestResponse(response);

                if (requestResponse.Status == RequestStatus.Fail)
                {
                    onRequestFinished?.Invoke(requestResponse);
                    return;
                }

                onRequestFinished?.Invoke(requestResponse);
            });
        }

        /// <summary>Unlist an existing room that you have created in the coherence Cloud.</summary>
        /// <param name="onRequestFinished">Callback that will be invoked when the request finished.</param>
        /// <param name="uniqueID">Unique ID of the room you wish to unlist. Can be found in RoomData.UniqueId</param>
        /// <param name="secret">Token that will allow you to unlist the room. Can be found in RoomData.Secret</param>
        [Obsolete("Use the " + nameof(RoomData) + " overload instead."), Deprecated("05/2026", 2, 2, 0, Reason = "Use the " + nameof(RoomData) + " overload instead.")]
        public void UnlistRoom(ulong uniqueID, string secret, Action<RequestResponse<string>> onRequestFinished)
            => UnlistRoom(new RoomData { UniqueId = uniqueID, Secret = secret }, onRequestFinished);

        /// <summary>Remove an existing room that you have created in the coherence Cloud.</summary>
        /// <param name="roomData">The room to remove.</param>
        public Task RemoveRoomAsync(RoomData roomData) => RemoveRoomAsync(roomData, CancellationToken.None);

        /// <summary>Remove an existing room that you have created in the coherence Cloud.</summary>
        /// <param name="roomData">The room to remove.</param>
        /// <param name="cancellationToken">Cancellation token to cancel the request.</param>
        public async Task RemoveRoomAsync(RoomData roomData, CancellationToken cancellationToken)
        {
            var pathParams = $"/{roomData.UniqueId}/{roomData.Secret}";

            logger.Trace("RemoveRoom", ("endpoint", roomsResolveEndpoint), ("path", pathParams));

            await Wait.For(GetRemoveRoomCooldown(), cancellationToken);

            // TODO: Add cancellation token support to SendRequestAsync
            await requestFactory.SendRequestAsync(roomsResolveEndpoint, pathParams, "DELETE", null, null, $"{nameof(CloudRoomsService)}.{nameof(RemoveRoomAsync)}", GetAuthToken());

            roomsCache.RemoveRoom(roomData.UniqueId);
        }

        /// <summary>Remove an existing room that you have created in the coherence Cloud.</summary>
        /// <param name="uniqueID">Unique ID of the room you wish to remove. Can be found in RoomData.UniqueId</param>
        /// <param name="secret">Token that will allow you to remove the room. Can be found in RoomData.Secret</param>
        [Obsolete("Use the " + nameof(RoomData) + " overload instead."), Deprecated("05/2026", 2, 2, 0, Reason = "Use the " + nameof(RoomData) + " overload instead.")]
        public Task RemoveRoomAsync(ulong uniqueID, string secret)
            => RemoveRoomAsync(new RoomData { UniqueId = uniqueID, Secret = secret });

        /// <summary>Unlist an existing room that you have created in the coherence Cloud.</summary>
        /// <param name="roomData">The room to unlist.</param>
        /// <param name="cancellationToken">Cancellation token to cancel the request.</param>
        public async Task UnlistRoomAsync(RoomData roomData, CancellationToken cancellationToken = default)
        {
            var pathParams = $"/{roomData.UniqueId}/unlist";

            var requestBody = Utils.CoherenceJson.SerializeObject(new RoomUnlistRequest
            {
                Secret = roomData.Secret
            });

            logger.Trace("UnlistRoom", ("endpoint", roomsResolveEndpoint), ("path", pathParams));

            await Wait.For(requestFactory.GetRequestCooldown(roomsResolveEndpoint, "PATCH"), cancellationToken);

            // TODO: Add cancellation token support to SendRequestAsync
            await requestFactory.SendRequestAsync(roomsResolveEndpoint, pathParams, "PATCH", requestBody, null, $"{nameof(CloudRoomsService)}.{nameof(UnlistRoomAsync)}", GetAuthToken());
        }

        /// <summary>Unlist an existing room that you have created in the coherence Cloud.</summary>
        /// <param name="uniqueID">Unique ID of the room you wish to unlist. Can be found in RoomData.UniqueId</param>
        /// <param name="secret">Token that will allow you to unlist the room. Can be found in RoomData.Secret</param>
        [Obsolete("Use the " + nameof(RoomData) + " overload instead."), Deprecated("05/2026", 2, 2, 0, Reason = "Use the " + nameof(RoomData) + " overload instead.")]
        public Task UnlistRoomAsync(ulong uniqueID, string secret)
            => UnlistRoomAsync(new RoomData { UniqueId = uniqueID, Secret = secret });

        /// <returns>Returns the internal cooldown for the Create Room endpoint.</returns>
        public TimeSpan GetCreateRoomCooldown()
        {
            return requestFactory.GetRequestCooldown(roomsResolveEndpoint, "POST");
        }

        /// <summary>Create a room with the given options in the coherence Cloud.</summary>
        /// <param name="onRequestFinished">Callback that will be invoked when the request finished, it includes the created room.</param>
        /// <param name="roomCreationOptions">Instance of all the room options that you wish to create the room with.</param>
        public void CreateRoom(Action<RequestResponse<RoomData>> onRequestFinished, RoomCreationOptions roomCreationOptions)
            => CreateRoom(onRequestFinished, roomCreationOptions, CancellationToken.None);

        /// <summary>Create a room with the given options in the coherence Cloud.</summary>
        /// <param name="onRequestFinished">Callback that will be invoked when the request finished, it includes the created room.</param>
        /// <param name="roomCreationOptions">Instance of all the room options that you wish to create the room with.</param>
        /// <param name="cancellationToken">Cancellation token to cancel the request.</param>
        public void CreateRoom(Action<RequestResponse<RoomData>> onRequestFinished, RoomCreationOptions roomCreationOptions, CancellationToken cancellationToken)
        {
            string requestBody = GetRoomCreationRequestBody(roomCreationOptions);

            logger.Trace("CreateRoom - start", ("region", region), ("endpoint", roomsResolveEndpoint), ("maxClients", roomCreationOptions.MaxClients), ("tags", string.Join(",", roomCreationOptions.Tags ?? new string[] { })), ("findOrCreate", false));

            // TODO: Add cancellation token support to SendRequest
            requestFactory.SendRequest(roomsResolveEndpoint, "POST", requestBody, null, $"{nameof(CloudRoomsService)}.{nameof(CreateRoom)}", GetAuthToken(),
            response =>
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                var requestResponse = RequestResponse<RoomData>.GetRequestResponse(response);

                if (requestResponse.Status == RequestStatus.Fail)
                {
                    onRequestFinished?.Invoke(requestResponse);
                    return;
                }

                try
                {
                    RoomData room = DeserializeCreatedRoom(response.Result);

                    requestResponse.Result = room;
                }
                catch (Exception exception)
                {
                    requestResponse.Status = RequestStatus.Fail;
                    requestResponse.Exception = new ResponseDeserializationException(Result.InvalidResponse, exception.Message);

                    logger.Error(Error.RuntimeCloudDeserializationException,
                        ("Request", nameof(CreateRoom)),
                        ("Response", response.Result),
                        ("exception", exception));
                }
                finally
                {
                    onRequestFinished?.Invoke(requestResponse);
                }
            });
        }

        /// <summary>Create a room with the given options in the coherence Cloud asynchronously.</summary>
        /// <param name="roomCreationOptions">Instance of all the room options that you wish to create the room with.</param>
        public Task<RoomData> CreateRoomAsync(RoomCreationOptions roomCreationOptions) => CreateRoomAsync(roomCreationOptions, CancellationToken.None);

        /// <summary>Create a room with the given options in the coherence Cloud asynchronously.</summary>
        /// <param name="roomCreationOptions">Instance of all the room options that you wish to create the room with.</param>
        /// <param name="cancellationToken">Cancellation token to cancel the request.</param>
        public async Task<RoomData> CreateRoomAsync(RoomCreationOptions roomCreationOptions, CancellationToken cancellationToken)
        {
            var requestBodyForRegion = GetRoomCreationRequestBody(roomCreationOptions);

            logger.Trace("CreateRoom - start", ("region", region), ("endpoint", roomsResolveEndpoint), ("maxClients", roomCreationOptions.MaxClients), ("tags", string.Join(",", roomCreationOptions.Tags ?? new string[] { })), ("findOrCreate", roomCreationOptions.FindOrCreate));

            await Wait.For(GetCreateRoomCooldown(), cancellationToken);

            // TODO: Add cancellation token support to SendRequest
            var textResponse = await requestFactory.SendRequestAsync(roomsResolveEndpoint, "POST", requestBodyForRegion, null, $"{nameof(CloudRoomsService)}.{nameof(CreateRoomAsync)}", GetAuthToken());

            try
            {
                RoomData room = DeserializeCreatedRoom(textResponse);

                return room;
            }
            catch (Exception exception)
            {
                logger.Error(Error.RuntimeCloudDeserializationException,
                    ("Request", nameof(CreateRoomAsync)),
                    ("Response", textResponse),
                    ("exception", exception));

                throw new ResponseDeserializationException(Result.InvalidResponse, exception.Message);
            }
        }

        /// <summary>Match against a joinable room with the given options in the coherence Cloud.</summary>
        /// <param name="onRequestFinished">Callback that will be invoked when the request finished, it includes the matched room or null.</param>
        /// <param name="tags">Filter the results by a list of tags.</param>
        public void MatchRoom(Action<RequestResponse<RoomMatchResponse>> onRequestFinished, string[] tags = null)
        {
            var requestBody = Utils.CoherenceJson.SerializeObject(new RoomMatchRequest()
            {
                Tags = tags ?? new string[] { },
                Region = region,
                SimSlug = runtimeSettings.SimulatorSlug,
            });

            string pathParams = $"/match";

            logger.Trace("MatchRoom - start", ("endpoint", roomsResolveEndpoint));

            requestFactory.SendRequest(roomsResolveEndpoint, pathParams, "POST", requestBody, null, $"{nameof(CloudRoomsService)}.{nameof(MatchRoom)}", GetAuthToken(),
            response =>
            {
                var requestResponse = RequestResponse<RoomMatchResponse>.GetRequestResponse(response);

                if (requestResponse.Status == RequestStatus.Fail || response.Result == "")
                {
                    onRequestFinished?.Invoke(requestResponse);
                    return;
                }

                try
                {
                    var resp = Utils.CoherenceJson.DeserializeObject<RoomMatchResponse>(response.Result);
                    if (resp.Room != null)
                    {
                        resp.Room = DeserializeCreatedRoom(Utils.CoherenceJson.SerializeObject(resp.Room.Value));
                    }

                    requestResponse.Result = resp;
                }
                catch (Exception exception)
                {
                    requestResponse.Status = RequestStatus.Fail;
                    requestResponse.Exception = new ResponseDeserializationException(Result.InvalidResponse, exception.Message);

                    logger.Error(Error.RuntimeCloudDeserializationException,
                        ("Request", nameof(MatchRoom)),
                        ("Response", response.Result),
                        ("exception", exception));
                }
                finally
                {
                    onRequestFinished?.Invoke(requestResponse);
                }
            });
        }

        /// <summary>Match a room with the given options in the coherence Cloud asynchronously.</summary>
        /// <param name="tags">Filter the results by a list of tags.</param>
        /// <param name="cancellationToken">Cancellation token to cancel the request.</param>
        public async Task<RoomMatchResponse> MatchRoomAsync(string[] tags = null, CancellationToken cancellationToken = default)
        {
            var requestBody = Utils.CoherenceJson.SerializeObject(new RoomMatchRequest
            {
                Tags = tags ?? new string[] { },
                Region = region,
                SimSlug = runtimeSettings.SimulatorSlug,
            });

            const string pathParams = "/match";

            logger.Trace("MatchRoomAsync - start", ("endpoint", roomsResolveEndpoint));

            await Wait.For(requestFactory.GetRequestCooldown(roomsResolveEndpoint, "POST"), cancellationToken);

            // TODO: Add cancellation token support to SendRequestAsync
            var textResponse = await requestFactory.SendRequestAsync(roomsResolveEndpoint, pathParams, "POST", requestBody, null, $"{nameof(CloudRoomsService)}.{nameof(MatchRoomAsync)}", GetAuthToken());

            try
            {
                var resp = Utils.CoherenceJson.DeserializeObject<RoomMatchResponse>(textResponse);
                if (resp.Room != null)
                {
                    resp.Room = DeserializeCreatedRoom(Utils.CoherenceJson.SerializeObject(resp.Room.Value));
                }
                return resp;
            }
            catch (Exception exception)
            {
                logger.Error(Error.RuntimeCloudDeserializationException,
                    ("Request", nameof(MatchRoomAsync)),
                    ("Response", textResponse),
                    ("exception", exception));

                throw new ResponseDeserializationException(Result.InvalidResponse, exception.Message);
            }
        }

        /// <returns>Returns the internal cooldown for the Fetch Rooms endpoint.</returns>
        public TimeSpan GetFetchRoomsCooldown() => requestFactory.GetRequestCooldown(roomsResolveEndpoint, "GET");

        /// <summary>Fetch the available rooms in the coherence Cloud.</summary>
        /// <param name="onRequestFinished">Callback that will be invoked when the request finished, it includes the fetched rooms.</param>
        /// <param name="tags">Filter the results by a list of tags.</param>
        public void FetchRooms(Action<RequestResponse<IReadOnlyList<RoomData>>> onRequestFinished, string[] tags = null)
            => FetchRooms(onRequestFinished, tags, CancellationToken.None);

        /// <summary>Fetch the available rooms in the coherence Cloud.</summary>
        /// <param name="onRequestFinished">Callback that will be invoked when the request finished, it includes the fetched rooms.</param>
        /// <param name="tags">Filter the results by a list of tags.</param>
        /// <param name="cancellationToken">Cancellation token to cancel the request.</param>
        public void FetchRooms(Action<RequestResponse<IReadOnlyList<RoomData>>> onRequestFinished, [MaybeNull] string[] tags, CancellationToken cancellationToken)
        {
            if (cancellationToken.CanBeCanceled)
            {
                var invokeUnlessCancellationRequested = onRequestFinished;
                onRequestFinished = response =>
                {
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        invokeUnlessCancellationRequested(response);
                    }
                };
            }

            if (WaitForOngoingRequest(onRequestFinished))
            {
                return;
            }

            roomsCache.ClearRooms();

            var qs = new List<string>()
            {
                $"region={Uri.EscapeDataString(region)}"
            };

            if (tags != null && tags.Length > 0)
            {
                qs.Add($"tags={Uri.EscapeDataString(String.Join(",", tags ?? new string[] { }))}");
            }

            if (!string.IsNullOrEmpty(runtimeSettings.SimulatorSlug))
            {
                qs.Add($"sim_slug={Uri.EscapeDataString(runtimeSettings.SimulatorSlug)}");
            }

            string pathParams = $"?{String.Join("&", qs)}";

            logger.Trace("FetchRooms - start", ("endpoint", roomsResolveEndpoint), ("params", pathParams));

            // TODO: Add cancellation token support to SendRequest
            requestFactory.SendRequest(roomsResolveEndpoint, pathParams, "GET", null, null, $"{nameof(CloudRoomsService)}.{nameof(FetchRooms)}", GetAuthToken(),
                response =>
                {
                    var requestResponse = RequestResponse<IReadOnlyList<RoomData>>.GetRequestResponse(response);

                    if (requestResponse.Status == RequestStatus.Fail)
                    {
                        requestResponse.Result = new List<RoomData>();
                        foreach (var callback in fetchRoomsCallbackList)
                        {
                            callback?.Invoke(requestResponse);
                        }
                        fetchRoomsCallbackList.Clear();
                        return;
                    }

                    try
                    {
                        string authToken = GetAuthToken();

                        var rooms = OnFetch(response.Result, authToken); // Backend filters rooms by tags

                        logger.Trace("FetchRooms - end", ("rooms count", rooms.Count));

                        roomsCache.PopulateRooms(rooms);

                        requestResponse.Result = rooms;
                    }
                    catch (Exception ex)
                    {
                        requestResponse.Status = RequestStatus.Fail;
                        requestResponse.Exception = new ResponseDeserializationException(Result.InvalidResponse, ex.Message);

                        logger.Error(Error.RuntimeCloudDeserializationException,
                            ("Request", nameof(FetchRooms)),
                            ("Response", requestResponse.Result),
                            ("exception", ex));
                    }
                    finally
                    {
                        foreach (var callback in fetchRoomsCallbackList)
                        {
                            callback?.Invoke(requestResponse);
                        }

                        fetchRoomsCallbackList.Clear();
                    }
                });
        }

        /// <summary>Fetch the available rooms in the coherence Cloud asynchronously.</summary>
        /// <param name="tags">Filter the results by a list of tags.</param>
        public Task<IReadOnlyList<RoomData>> FetchRoomsAsync(string[] tags = null) => FetchRoomsAsync(tags, CancellationToken.None);

        /// <summary>Fetch the available rooms in the coherence Cloud asynchronously.</summary>
        /// <param name="tags">Filter the results by a list of tags.</param>
        /// <param name="cancellationToken">Cancellation token to cancel the request.</param>
        public async Task<IReadOnlyList<RoomData>> FetchRoomsAsync([MaybeNull] string[] tags, CancellationToken cancellationToken)
        {
            await AwaitForPreviousFetchRoomsAsync(cancellationToken);

            isFetchingRooms = true;

            roomsCache.ClearRooms();

            var qs = new List<string>{
                $"tags={Uri.EscapeDataString(String.Join(",", tags ?? new string[] { }))}",
                $"region={Uri.EscapeDataString(region)}"
            };

            if (!string.IsNullOrEmpty(runtimeSettings.SimulatorSlug))
            {
                qs.Add($"sim_slug={Uri.EscapeDataString(runtimeSettings.SimulatorSlug)}");
            }

            var pathParams = $"?{String.Join("&", qs)}";

            logger.Trace("FetchRooms - start", ("endpoint", roomsResolveEndpoint));

            await GetFetchRoomsCooldown();
            if (cancellationToken.IsCancellationRequested)
            {
                isFetchingRooms = false;
                throw new TaskCanceledException("FetchRoomsAsync operation was canceled.", null, cancellationToken);
            }

            // TODO: Add cancellation token support to SendRequestAsync
            var responseText = await requestFactory.SendRequestAsync(roomsResolveEndpoint, pathParams, "GET", null, null, $"{nameof(CloudRoomsService)}.{nameof(FetchRoomsAsync)}", GetAuthToken());

            if (cancellationToken.IsCancellationRequested)
            {
                isFetchingRooms = false;
                throw new TaskCanceledException("FetchRoomsAsync operation was canceled.", null, cancellationToken);
            }

            try
            {
                var authToken = GetAuthToken();

                var rooms = OnFetch(responseText, authToken); // Backend filters rooms by tags

                logger.Trace("FetchRooms - end", ("rooms count", rooms.Count));

                roomsCache.PopulateRooms(rooms);

                return rooms;
            }
            catch (Exception ex)
            {
                isFetchingRooms = false;

                logger.Error(Error.RuntimeCloudDeserializationException,
                    ("Request", nameof(FetchRoomsAsync)),
                    ("Response", responseText),
                    ("exception", ex));

                throw new ResponseDeserializationException(Result.InvalidResponse, ex.Message);
            }
            finally
            {
                isFetchingRooms = false;
            }
        }

        private async Task AwaitForPreviousFetchRoomsAsync(CancellationToken cancellationToken)
        {
            while (isFetchingRooms && !cancellationToken.IsCancellationRequested)
            {
                await Task.Yield();
            }

            if (cancellationToken.IsCancellationRequested)
            {
                throw new TaskCanceledException("FetchRoomsAsync operation was canceled.", null, cancellationToken);
            }
        }

        private RoomData DeserializeCreatedRoom(string textResponse)
        {
            var room = Utils.CoherenceJson.DeserializeObject<RoomData>(textResponse);
            if (runtimeSettings.IsWebGL)
            {
                room.Host.Ip = runtimeSettings.ApiEndpoint;
                room.Host.Port = runtimeSettings.RemoteWebPort;
            }

            room.AuthToken = GetAuthToken();

            logger.Trace("CreateRoom - end", ("roomID", room.Id));

            roomsCache.AddRoom(room);
            return room;
        }

        private bool WaitForOngoingRequest(Action<RequestResponse<IReadOnlyList<RoomData>>> onRequestFinished)
        {
            fetchRoomsCallbackList.Add(onRequestFinished);

            return fetchRoomsCallbackList.Count > 1;
        }

        private List<RoomData> OnFetch(string text, string sessionToken)
        {
            RoomFetchResponse response = Utils.CoherenceJson.DeserializeObject<RoomFetchResponse>(text);

            var result = response.Rooms.Select(room =>
            {
                if (runtimeSettings.IsWebGL)
                {
                    room.Host.Ip = runtimeSettings.ApiEndpoint;
                    room.Host.Port = runtimeSettings.RemoteWebPort;
                }

                room.AuthToken = sessionToken;

                return room;
            });

            return result.ToList();
        }

        private string GetRoomCreationRequestBody(RoomCreationOptions roomCreationOptions)
        {
            var requestBody = Utils.CoherenceJson.SerializeObject(new RoomCreationRequest()
            {
                Tags = roomCreationOptions.Tags ?? new string[] { },
                KV = roomCreationOptions.KeyValues ?? new Dictionary<string, string>(),
                Region = region,
                MaxClients = roomCreationOptions.MaxClients,
                SimSlug = runtimeSettings.SimulatorSlug,
                SimPayload = roomCreationOptions.SimPayload,
                FindOrCreate = roomCreationOptions.FindOrCreate,
            });
            return requestBody;
        }

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

        public void Dispose()
        {
            if (shouldDisposeRequestFactoryAndAuthClient)
            {
                shouldDisposeRequestFactoryAndAuthClient = false;
                CloudCredentialsPair.Dispose(authClient, requestFactory);
            }

            logger?.Dispose();
        }

        public async ValueTask DisposeAsync()
        {
            if (shouldDisposeRequestFactoryAndAuthClient)
            {
                shouldDisposeRequestFactoryAndAuthClient = false;
                await CloudCredentialsPair.DisposeAsync(authClient, requestFactory);
            }

            logger?.Dispose();
        }
    }
}
