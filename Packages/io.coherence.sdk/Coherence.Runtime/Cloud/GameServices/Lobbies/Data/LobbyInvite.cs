// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Cloud
{
    using System.Diagnostics.CodeAnalysis;

    /// <summary>
    /// An invitation to <see cref="LobbiesService.AcceptLobbyInviteAsync">join a lobby</see>, from a player
    /// who is in it.
    /// </summary>
    /// <remarks>
    /// See <see cref="LobbiesService.OnLobbyInviteReceived"/> for how invites arrive and what to do with them.
    /// </remarks>
    /// <seealso cref="LobbiesService.AcceptLobbyInviteAsync"/>
    public sealed record LobbyInvite
    {
        public LobbyPlayer Inviter { get; }
        public LobbyData Lobby { get; }

        /// <remarks>
        /// Password verification is skipped when joining with this token.
        /// <see langword="null"/> when no token is needed,
        /// which only happens on a lobby with no password.
        /// </remarks>
        [MaybeNull]
        public string InviteToken { get; }

        internal LobbyInvite(LobbyPlayer inviter, LobbyData lobby, [MaybeNull] string inviteToken)
        {
            Inviter = inviter;
            Lobby = lobby;
            InviteToken = inviteToken;
        }
    }
}
