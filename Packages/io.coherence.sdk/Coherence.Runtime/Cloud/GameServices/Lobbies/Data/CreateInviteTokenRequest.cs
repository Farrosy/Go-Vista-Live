// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Cloud
{
    using System;
    using Newtonsoft.Json;
    using Runtime.Utils;
    using Utils;

    /// <summary>Body of <c>POST /lobbies/{id}/invite-token</c>.</summary>
    internal struct CreateInviteTokenRequest
    {
        /// <remarks>
        /// Omitted when the caller didn't ask for a time to live,
        /// so coherence Cloud applies its own default.
        /// </remarks>
        [JsonProperty("ttl_seconds", NullValueHandling = NullValueHandling.Ignore)]
        public int? TimeToLiveSeconds;

        public static string GetRequestBody(TimeSpan? timeToLive)
            => CoherenceJson.SerializeObject(new CreateInviteTokenRequest
            {
                TimeToLiveSeconds = timeToLive is { } ttl
                    ? ttl.ToWholeSeconds()
                    : null
            });
    }
}
