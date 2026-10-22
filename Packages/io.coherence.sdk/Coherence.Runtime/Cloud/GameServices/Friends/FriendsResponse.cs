// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Cloud
{
    using System.Collections.Generic;
    using Newtonsoft.Json;

    /// <summary>Body of <c>GET /friends</c>.</summary>
    internal struct FriendsResponse
    {
        [JsonProperty("friends")]
        public List<Friend> Friends;
    }
}
