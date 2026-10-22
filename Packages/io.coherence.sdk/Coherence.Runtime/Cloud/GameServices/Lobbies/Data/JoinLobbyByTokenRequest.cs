// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Cloud
{
    using Newtonsoft.Json;
    using System.Collections.Generic;
    using Utils;

    /// <summary>Body of <c>POST /lobbies/join-by-token</c>.</summary>
    internal struct JoinLobbyByTokenRequest
    {
        [JsonProperty("invite_token")]
        public string InviteToken;

        [JsonProperty("attributes")]
        public List<CloudAttribute> PlayerAttributes;

        public static string GetRequestBody(string inviteToken, List<CloudAttribute> playerAttr)
            => CoherenceJson.SerializeObject(new JoinLobbyByTokenRequest
            {
                InviteToken = inviteToken,
                PlayerAttributes = playerAttr
            });
    }
}
