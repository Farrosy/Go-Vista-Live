// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Cloud
{
    using Newtonsoft.Json;

    /// <summary>Response of <c>POST /lobbies/{id}/invite-token</c>.</summary>
    internal struct InviteTokenResponse
    {
        [JsonProperty("invite_token")]
        public string InviteToken;
    }
}
