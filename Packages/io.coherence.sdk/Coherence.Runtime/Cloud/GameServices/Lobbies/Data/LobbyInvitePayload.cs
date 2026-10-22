// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Cloud
{
    using Newtonsoft.Json;

    /// <summary>Body of the <c>/friends/invite</c> push.</summary>
    internal struct LobbyInvitePayload
    {
        [JsonProperty("inviter")]
        public LobbyPlayer Inviter;

        [JsonProperty("lobby")]
        public LobbyData Lobby;

        [JsonProperty("invite_token")]
        public string InviteToken;

        public LobbyPlayer ResolveInviter()
        {
            if (Lobby.Players is { } players)
            {
                foreach (var player in players)
                {
                    if (player.Id == Inviter.Id)
                    {
                        return player;
                    }
                }
            }

            return Inviter;
        }
    }
}
