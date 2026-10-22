// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

#if UNITY_5_3_OR_NEWER
// IMPORTANT: Used by the pure-dotnet client, DON'T REMOVE.
#define UNITY
#endif

namespace Coherence.Cloud
{
    using Newtonsoft.Json;
    using System.Collections.Generic;
    using Utils;

    internal struct FindLobbyRequest
    {
        [JsonProperty("name")]
        public string Name;

        [JsonProperty("tag")]
        public string Tag;

        [JsonProperty("max_players")]
        public int MaxPlayers;

        [JsonProperty("region")]
        public string Region;

        [JsonProperty("sim_slug")]
        public string SimSlug;

        [JsonProperty("invite_permissions")]
        public string InvitePermissions;

        [JsonProperty("join_code")]
        public string JoinCode;

        [JsonProperty("hide_from_presence")]
        public bool HideFromPresence;

        /// <seealso cref="CreateLobbyOptions.MaxReservationsPerRequest"/>
        [JsonProperty("join_reservations", NullValueHandling = NullValueHandling.Ignore)]
        public JoinReservationsPolicy? JoinReservations;

        [JsonProperty("filters")]
        public List<LobbyFilter> Filters;

        [JsonProperty("sort")]
        public List<LobbySortOption> Sort;

        [JsonProperty("lobby_attr")]
        public List<CloudAttribute> LobbyAttributes;

        [JsonProperty("player_attr")]
        public List<CloudAttribute> PlayerAttributes;

        [JsonProperty("reservations", NullValueHandling = NullValueHandling.Ignore)]
        public ReservationRequest? Reservations;

        public static string GetRequestBody(FindLobbyOptions findOptions, CreateLobbyOptions createOptions)
        {
            if (findOptions.LobbyFilters?.Count > 3)
            {
                Warn("Maximum amount of Lobby Filters are three. Additional Filters will be ignored.");
            }

            List<LobbySortOption> sortOptions = null;

            if (findOptions.Sort != null)
            {
                sortOptions = new List<LobbySortOption>();
                foreach (var kv in findOptions.Sort)
                {
                    sortOptions.Add(new LobbySortOption()
                    {
                        Key = kv.Key.ToString(),
                        Descending = kv.Value
                    });
                }
            }

            var requestBody = CoherenceJson.SerializeObject(new FindLobbyRequest
            {
                Name = string.IsNullOrEmpty(createOptions.Name) ? null : createOptions.Name,
                Tag = createOptions.Tag,
                Region = createOptions.Region,
                MaxPlayers = createOptions.MaxPlayers,
                Filters = findOptions.LobbyFilters,
                Sort = sortOptions,
                SimSlug = createOptions.SimulatorSlug,
                InvitePermissions = createOptions.InvitePermissions is LobbyInvitePermissions.AllMembers
                    ? LobbyData.InvitePermissionsAllMembers
                    : LobbyData.InvitePermissionsOwnerOnly,
                JoinCode = string.IsNullOrEmpty(createOptions.JoinCode) ? null : createOptions.JoinCode,
                HideFromPresence = createOptions.HideFromPresence,
                JoinReservations = JoinReservationsPolicy.FromOptions(createOptions),
                LobbyAttributes = createOptions.LobbyAttributes,
                PlayerAttributes = createOptions.PlayerAttributes,
                Reservations = ReservationRequest.FromOptions(createOptions.Reservations)
            });

            return requestBody;
        }

        private static void Warn(string message)
        {
#if UNITY
            UnityEngine.Debug.LogWarning(message);
#else
            System.Console.WriteLine(message);
#endif
        }
    }
}
