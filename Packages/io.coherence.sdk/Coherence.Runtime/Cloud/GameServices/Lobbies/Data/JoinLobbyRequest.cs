// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Cloud
{
    using Newtonsoft.Json;
    using System.Collections.Generic;
    using Utils;

    internal struct JoinLobbyRequest
    {
        [JsonProperty("password")]
        public string Password;

        [JsonProperty("invite_token")]
        public string InviteToken;

        [JsonProperty("attributes")]
        public List<CloudAttribute> PlayerAttributes;

        [JsonProperty("reservations", NullValueHandling = NullValueHandling.Ignore)]
        public ReservationRequest? Reservations;

        public static string GetRequestBody(List<CloudAttribute> playerAttr, string password,
            string inviteToken = null, LobbyReservations reservations = null)
        {
            var requestBody = CoherenceJson.SerializeObject(new JoinLobbyRequest()
            {
                Password = password,
                InviteToken = inviteToken,
                PlayerAttributes = playerAttr,
                Reservations = ReservationRequest.FromOptions(reservations)
            });

            return requestBody;
        }
    }
}
