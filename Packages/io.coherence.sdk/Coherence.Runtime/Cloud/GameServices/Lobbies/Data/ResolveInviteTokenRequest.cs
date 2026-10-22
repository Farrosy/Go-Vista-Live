// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Cloud
{
    using Newtonsoft.Json;
    using Utils;

    /// <summary>Body of <c>POST /lobbies/invites/resolve</c>.</summary>
    internal struct ResolveInviteTokenRequest
    {
        [JsonProperty("invite_token")]
        public string InviteToken;

        public static string GetRequestBody(string inviteToken)
            => CoherenceJson.SerializeObject(new ResolveInviteTokenRequest { InviteToken = inviteToken });
    }
}
