// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Cloud
{
    using Newtonsoft.Json;
    using Utils;

    /// <summary>Body of <c>POST /presence</c> when setting or clearing the presence.</summary>
    internal struct SetPresenceRequest
    {
        [JsonProperty("presence")]
        public string Presence;

        public static string GetRequestBody(string presence)
            => CoherenceJson.SerializeObject(new SetPresenceRequest { Presence = presence });
    }
}
