// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Cloud
{
    using Newtonsoft.Json;
    using System.Collections.Generic;
    using Utils;

    /// <summary>Body of <c>POST /lobbies/join-by-code</c>.</summary>
    internal struct JoinLobbyByCodeRequest
    {
        [JsonProperty("join_code")]
        public string JoinCode;

        [JsonProperty("attributes")]
        public List<CloudAttribute> PlayerAttributes;

        public static string GetRequestBody(string joinCode, List<CloudAttribute> playerAttr)
            => CoherenceJson.SerializeObject(new JoinLobbyByCodeRequest
            {
                JoinCode = joinCode,
                PlayerAttributes = playerAttr
            });
    }
}
