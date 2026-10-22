// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Cloud
{
    using Newtonsoft.Json;
    using Utils;

    /// <summary>Body of <c>POST /lobbies/{id}/invites</c>.</summary>
    internal struct InviteToLobbyRequest
    {
        [JsonProperty("player_id")]
        public string PlayerId;

        public static string GetRequestBody(PlayerAccountId friend)
            => CoherenceJson.SerializeObject(new InviteToLobbyRequest { PlayerId = friend });
    }
}
