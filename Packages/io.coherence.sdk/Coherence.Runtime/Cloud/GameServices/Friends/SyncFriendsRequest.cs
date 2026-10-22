// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Cloud
{
    using System.Collections.Generic;
    using Newtonsoft.Json;
    using Utils;

    /// <summary>Body of <c>POST /friends/native-sync</c>.</summary>
    internal struct SyncFriendsRequest
    {
        [JsonProperty("source")]
        public string Source;

        [JsonProperty("friends")]
        public IReadOnlyList<string> FriendIds;

        public static string GetRequestBody(string source, IReadOnlyList<string> friendIds)
            => CoherenceJson.SerializeObject(new SyncFriendsRequest { Source = source, FriendIds = friendIds });
    }
}
