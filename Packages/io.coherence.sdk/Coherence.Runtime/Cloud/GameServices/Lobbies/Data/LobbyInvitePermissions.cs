// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Cloud
{
    /// <summary>Who may invite players to a Lobby, and create invite tokens for it.</summary>
    /// <remarks>Chosen when the Lobby is created, and fixed for its lifetime.</remarks>
    /// <seealso cref="CreateLobbyOptions.InvitePermissions"/>
    /// <seealso cref="LobbyData.InvitePermissions"/>
    public enum LobbyInvitePermissions
    {
        /// <summary>Only the Lobby's owner may invite players and create invite tokens. The default.</summary>
        OwnerOnly = 0,

        /// <summary>Every member of the Lobby may invite players and create invite tokens, its owner included.</summary>
        AllMembers = 1,
    }
}
