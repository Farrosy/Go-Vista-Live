// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Cloud
{
    using Newtonsoft.Json;

    /// <summary>How a <see cref="FriendsService.SyncFriendsAsync">friend list sync</see> went.</summary>
    /// <remarks>coherence only keeps the friends who play on this project.</remarks>
    public struct FriendsSyncResult
    {
        /// <summary>How many are players of the active project.</summary>
        [JsonIgnore]
        public int Resolved => resolved;

        /// <summary>
        /// How many were dropped, because they have never logged in to this project, they are the
        /// local player, or were listed more than once.
        /// </summary>
        [JsonIgnore]
        public int Discarded => discarded;

        [JsonProperty("resolved")]
        internal int resolved;

        [JsonProperty("discarded")]
        internal int discarded;

        public override string ToString() => $"FriendsSyncResult(Resolved:{Resolved}, Discarded:{Discarded})";
    }
}
