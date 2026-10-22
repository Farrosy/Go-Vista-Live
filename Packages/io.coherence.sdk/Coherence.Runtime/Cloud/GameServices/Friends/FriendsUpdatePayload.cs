// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Cloud
{
    using System.Collections.Generic;
    using Newtonsoft.Json;

    /// <summary>Body of the <c>/friends/update</c> push: who became a friend, and who stopped being one.</summary>
    internal struct FriendsUpdatePayload
    {
        [JsonProperty("added")]
        public List<Friend> Added;

        [JsonProperty("removed")]
        public List<string> Removed;
    }
}
