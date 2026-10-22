// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Cloud
{
    using System;
    using System.Collections.Generic;

    /// <summary>Describes lobby slots reserved for specific players, such as the other members of a party.</summary>
    /// <remarks>
    /// <para>
    /// Reserved slots count against <see cref="LobbyData.MaxPlayers"/> and prevent other players from filling them,
    /// until either a reserved player joins (consuming their reservation) or <see cref="Timeout"/> elapses (which
    /// releases the slot back to anyone). A reservation only keeps a slot free: the reserved player still joins on
    /// their own, and still needs the lobby's password or an invite if it has one.
    /// </para>
    /// <para>
    /// Slots can be reserved when <see cref="CreateLobbyOptions.Reservations">creating</see>,
    /// <see cref="LobbiesService.FindOrCreateLobbyAsync">finding</see> or
    /// <see cref="LobbiesService.JoinLobbyAsync(string, List{CloudAttribute}, string,
    /// LobbyReservations, System.Threading.CancellationToken)">joining</see> a lobby, or later through
    /// <see cref="LobbySession.ReserveSlotsAsync"/>. When finding or joining, the caller joins and the slots are
    /// reserved in a single step: if the lobby lacks room for the whole party, the request fails with
    /// <see cref="ErrorCode.LobbyFull"/> and nobody joins. Reserving slots in an existing lobby is limited by its
    /// <see cref="LobbyData.MaxReservationsPerRequest"/>.
    /// </para>
    /// <para>
    /// Players already in the lobby, or who already have a reservation, are skipped, so every member of a party can
    /// send the same reservations at once, or retry, without reserving extra slots. An existing reservation keeps its
    /// original expiry.
    /// </para>
    /// </remarks>
    public sealed class LobbyReservations
    {
        /// <summary>Players for whom a slot will be reserved.</summary>
        /// <remarks>
        /// Your own <see cref="PlayerAccountId"/> is ignored if included,
        /// since the request already takes a slot for you.
        /// coherence Cloud currently accepts up to 99 players, and fails the request otherwise.
        /// </remarks>
        public IReadOnlyList<PlayerAccountId> PlayerAccountIds { get; }

        /// <summary>How long each reservation lasts, if the reserved player doesn't join in time.</summary>
        /// <remarks>
        /// <para>
        /// In a lobby that already exists, it can't be longer than the lobby's
        /// <see cref="LobbyData.MaxReservationTimeout"/>: a longer one is shortened, and <see langword="null"/>
        /// gets that timeout.
        /// </para>
        /// <para>
        /// When creating a lobby, <see langword="null"/> leaves it to coherence Cloud's default,
        /// currently two minutes.
        /// </para>
        /// <para>
        /// coherence Cloud currently accepts from a minute up to 30 minutes, and fails the request otherwise.
        /// </para>
        /// </remarks>
        public TimeSpan? Timeout { get; }

        public LobbyReservations(IReadOnlyList<PlayerAccountId> playerAccountIds, TimeSpan? timeout = null)
        {
            PlayerAccountIds = playerAccountIds ?? throw new ArgumentNullException(nameof(playerAccountIds));
            Timeout = timeout;
        }
    }
}
