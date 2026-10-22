// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Cloud
{
    using System.Collections.Generic;
    using Newtonsoft.Json;

    /// <summary>Body of the <c>/friends/presence</c> push.</summary>
    /// <remarks>Every push holds full state rather than just what changed.</remarks>
    internal struct FriendPresencePayload
    {
        [JsonProperty("player_id")]
        public string PlayerId;

        [JsonProperty("online")]
        public bool IsOnline;

        [JsonProperty("lobby_ids")]
        public List<string> LobbyIds;

        [JsonProperty("presence")]
        public string Presence;

        [JsonProperty("last_online_at")]
        public long? LastOnlineAt;
    }
}
