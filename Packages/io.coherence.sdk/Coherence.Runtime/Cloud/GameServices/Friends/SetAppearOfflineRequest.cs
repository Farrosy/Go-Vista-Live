// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Cloud
{
    using Newtonsoft.Json;
    using Utils;

    /// <summary>Body of <c>POST /presence</c> when hiding the player, or bringing them back.</summary>
    /// <inheritdoc cref="SetPresenceRequest"/>
    internal struct SetAppearOfflineRequest
    {
        [JsonProperty("appear_offline")]
        public bool AppearOffline;

        public static string GetRequestBody(bool appearOffline)
            => CoherenceJson.SerializeObject(new SetAppearOfflineRequest { AppearOffline = appearOffline });
    }
}
