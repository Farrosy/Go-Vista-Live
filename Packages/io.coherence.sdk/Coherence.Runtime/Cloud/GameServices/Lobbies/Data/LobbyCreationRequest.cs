// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Cloud
{
    using Newtonsoft.Json;
    using System.Collections.Generic;
    using Utils;

    internal struct LobbyCreationRequest
    {
        [JsonProperty("tag")]
        public string Tag;

        [JsonProperty("name")]
        public string Name;

        [JsonProperty("max_players")]
        public int MaxPlayers;

        [JsonProperty("unlisted")]
        public bool Unlisted;

        [JsonProperty("password")]
        public string Password;

        /// <summary>Who may create invite tokens for the lobby.</summary>
        /// <seealso cref="LobbyData.invitePermissions"/>
        [JsonProperty("invite_permissions")]
        public string InvitePermissions;

        [JsonProperty("join_code")]
        public string JoinCode;

        /// <summary>Whether the lobby should be listed as part of the players' presence.</summary>
        /// <seealso cref="CreateLobbyOptions.HideFromPresence"/>
        [JsonProperty("hide_from_presence")]
        public bool HideFromPresence;

        /// <seealso cref="CreateLobbyOptions.MaxReservationsPerRequest"/>
        [JsonProperty("join_reservations", NullValueHandling = NullValueHandling.Ignore)]
        public JoinReservationsPolicy? JoinReservations;

        [JsonProperty("region")]
        public string Region;

        [JsonProperty("sim_slug")]
        public string SimSlug;

        [JsonProperty("lobby_attr")]
        public List<CloudAttribute> LobbyAttributes;

        [JsonProperty("player_attr")]
        public List<CloudAttribute>  PlayerAttributes;

        [JsonProperty("reservations", NullValueHandling = NullValueHandling.Ignore)]
        public ReservationRequest? Reservations;

        public static string GetRequestBody(CreateLobbyOptions options)
        {
            var requestBody = CoherenceJson.SerializeObject(new LobbyCreationRequest
            {
                Tag = options.Tag,
                Region = options.Region,
                Name = options.Name,
                MaxPlayers = options.MaxPlayers,
                Unlisted = options.Unlisted,
                Password = options.Password,
                InvitePermissions = options.InvitePermissions is LobbyInvitePermissions.AllMembers
                    ? LobbyData.InvitePermissionsAllMembers
                    : LobbyData.InvitePermissionsOwnerOnly,
                JoinCode = string.IsNullOrEmpty(options.JoinCode) ? null : options.JoinCode,
                HideFromPresence = options.HideFromPresence,
                JoinReservations = JoinReservationsPolicy.FromOptions(options),
                SimSlug = options.SimulatorSlug,
                LobbyAttributes = options.LobbyAttributes,
                PlayerAttributes = options.PlayerAttributes,
                Reservations = ReservationRequest.FromOptions(options.Reservations)
            });

            return requestBody;
        }
    }
}
