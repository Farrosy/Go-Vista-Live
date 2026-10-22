// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Cloud
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using Runtime;

    /// <summary>
    /// Options for <see cref="LobbiesService.CreateLobby">creating a lobby</see>
    /// in the coherence Cloud.
    /// </summary>
    public sealed class CreateLobbyOptions
    {
        public static CreateLobbyOptions Default => new();

        /// <summary>
        /// The name for the lobby.
        /// </summary>
        /// <remarks>
        /// Names are unique. Creating a lobby with a name that's already taken fails with
        /// <see cref="ErrorCode.LobbyNameAlreadyExists"/>, including when
        /// <see cref="LobbiesService.FindOrCreateLobbyAsync"/> creates it. There, a <see langword="null"/> or empty
        /// name gets the lobby a generated one.
        /// </remarks>
        public string Name;

        /// <summary>
        /// Tag for the lobby.
        /// </summary>
        [MaybeNull]
        public string Tag;

        /// <summary>
        /// Maximum number of players that the lobby can hold.
        /// </summary>
        public int MaxPlayers;

        /// <summary>
        /// <para>
        /// Specifies whether the lobby should be unlisted or not.
        /// </para>
        /// <para>
        /// Public Lobbies can be matched/searched and then joined by anybody.
        /// Unlisted Lobbies can be joined only if the player knows the Lobby ID.
        /// </para>
        /// </summary>
        /// <remarks>Pair it with <see cref="JoinCode"/> for a lobby only people with the code can find.</remarks>
        public bool Unlisted;

        /// <summary>
        /// Password needed to access the lobby.
        /// </summary>
        /// <remarks>
        /// If <see langword="null"/> or empty, the lobby is accessible without a password.
        /// </remarks>
        /// <seealso cref="JoinCode"/>
        /// <seealso cref="InvitePermissions"/>
        [MaybeNull]
        public string Password;

        /// <summary>
        /// Who may <see cref="LobbySession.InviteAsync">invite players</see> to the lobby, and
        /// <see cref="LobbySession.CreateInviteTokenAsync">create invite tokens</see> for it.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Defaults to <see cref="LobbyInvitePermissions.OwnerOnly"/>.
        /// Fixed for the lobby's lifetime; it cannot be changed after creation.
        /// </para>
        /// </remarks>
        /// <seealso cref="LobbySession.CreateInviteTokenAsync"/>
        /// <seealso cref="LobbyData.InvitePermissions"/>
        public LobbyInvitePermissions InvitePermissions;

        /// <summary>
        /// A code that players can use to
        /// <see cref="LobbiesService.JoinLobbyByCodeAsync">join this lobby</see>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Password verification is skipped when joining through a join code.
        /// </para>
        /// <para>
        /// Codes are unique among your project's open lobbies. Creating a lobby with a join code that
        /// already exists fails with <see cref="ErrorCode.LobbyJoinCodeTaken"/>.
        /// The code is fixed for the lobby's lifetime, and freed for reuse once the lobby closes.
        /// </para>
        /// <para>
        /// coherence Cloud currently accepts codes from 6 to 32 characters long, without null characters, and fails
        /// creating the lobby with <see cref="ErrorCode.LobbyJoinCodeInvalid"/> otherwise. It doesn't check what the
        /// characters are, so pick an alphabet and a length that make guessing an open lobby's code impractical.
        /// </para>
        /// <para>Pair it with <see cref="Unlisted"/> for a lobby only people with the code can find.</para>
        /// </remarks>
        /// <seealso cref="LobbiesService.JoinLobbyByCodeAsync"/>
        /// <seealso cref="LobbyData.JoinCode"/>
        [MaybeNull]
        public string JoinCode;

        /// <summary>
        /// Hides this Lobby from online presence.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Someone in a hidden lobby still reads as online to their friends, they just do not read as
        /// being in a lobby: it never shows up in <see cref="Friend.LobbyIds"/>.
        /// </para>
        /// <para>Fixed for the lobby's lifetime; it cannot be changed after creation.</para>
        /// </remarks>
        /// <seealso cref="Friend.LobbyIds"/>
        public bool HideFromPresence;

        /// <summary>
        /// The most new slots a single request may reserve for the rest of a party, once this Lobby exists.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Above 0, a player can join and reserve slots for the rest of their party in a single step, so that the
        /// party either gets in together or not at all. Members can also reserve slots through
        /// <see cref="LobbySession.ReserveSlotsAsync"/>. The limit is per request, not per member.
        /// Players already in the lobby, or who already have a reservation, don't count towards it.
        /// </para>
        /// <para>
        /// At 0 (the default), a join or <see cref="LobbySession.ReserveSlotsAsync"/> that would add new reservations
        /// fails with <see cref="ErrorCode.LobbyReservationsNotAllowed"/>, even for the owner. Players can still join
        /// into slots reserved for them, and <see cref="Reservations"/> made at creation are not limited by it.
        /// </para>
        /// <para>
        /// Fixed for the lobby's lifetime; it cannot be changed after creation.
        /// Ignored by <see cref="LobbiesService.FindOrCreateLobbyAsync"/> when an existing lobby is found.
        /// </para>
        /// <para>coherence Cloud currently accepts from 0 to 99, and fails creating the lobby otherwise.</para>
        /// </remarks>
        /// <seealso cref="MaxReservationTimeout"/>
        /// <seealso cref="LobbyData.MaxReservationsPerRequest"/>
        public int MaxReservationsPerRequest;

        /// <summary>
        /// (Optional) How long a slot reserved once this Lobby exists lasts, at most, if the reserved player doesn't
        /// join in time.
        /// </summary>
        /// <remarks>
        /// <para>
        /// A request that doesn't set <see cref="LobbyReservations.Timeout"/> gets this timeout, and one that sets a
        /// longer one is shortened to it. An existing reservation keeps its original expiry.
        /// <see cref="Reservations"/> made at creation are not limited by it.
        /// </para>
        /// <para>
        /// <see langword="null"/> (the default) leaves it to coherence Cloud's default, currently two minutes.
        /// Fixed for the lobby's lifetime; it cannot be changed after creation.
        /// </para>
        /// <para>
        /// coherence Cloud currently accepts from a minute up to 30 minutes, and fails creating the lobby otherwise.
        /// </para>
        /// </remarks>
        /// <seealso cref="MaxReservationsPerRequest"/>
        /// <seealso cref="LobbyData.MaxReservationTimeout"/>
        [MaybeNull]
        public TimeSpan? MaxReservationTimeout;

        /// <summary>
        /// Custom attributes associated with the Lobby.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Public attributes will be returned and visible to all Players.
        /// </para>
        /// <para>
        /// Only the owner of the lobby can change Lobby attributes.
        /// </para>
        /// </remarks>
        [MaybeNull]
        public List<CloudAttribute> LobbyAttributes;

        /// <summary>
        /// Player attributes.
        /// </summary>
        /// <remarks>
        /// Public attributes will be returned and visible to all Players.
        /// </remarks>
        [MaybeNull]
        public List<CloudAttribute> PlayerAttributes;

        /// <summary>Slots to reserve for specific players.</summary>
        /// <remarks>
        /// <para>
        /// Reserved slots count against <see cref="MaxPlayers"/>, and are kept free until the reserved player joins or
        /// the reservation times out.
        /// </para>
        /// <para>
        /// With <see cref="LobbiesService.FindOrCreateLobbyAsync"/>, you join a found lobby and its slots are reserved
        /// in a single step. Lobbies that can't fit the whole party, or whose
        /// <see cref="LobbyData.MaxReservationsPerRequest"/> is too low, are skipped, and a new lobby is created with
        /// the slots reserved when none qualifies.
        /// </para>
        /// </remarks>
        /// <seealso cref="LobbyReservations"/>
        [MaybeNull]
        public LobbyReservations Reservations;

        /// <summary>
        /// The region for the lobby.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This can be used to <see cref="LobbyFilter.WithRegion">filter lobbies by region</see>.
        /// </para>
        /// <para>
        /// If the lobby is specific to a <see cref="RoomData">room</see>,
        /// this will default to the region of the room's host.
        /// </para>
        /// </remarks>
        [MaybeNull]
        public string Region;

        /// <summary>
        /// The Simulator Slug associated with the lobby.
        /// </summary>
        /// <remarks>
        /// If the lobby is not specific to a <see cref="RoomData">room</see>,
        /// or the room does not have a Simulator associated with it,
        /// this may be left <see langword="null"/> or empty.
        /// </remarks>
        [MaybeNull]
        public string SimulatorSlug;

        [Obsolete("CreateLobbyOptions.Secret will be removed in a future version. Use CreateLobbyOptions.Password instead.")]
        [Deprecated("08/2025", 2, 1, 0, Reason = "Renamed to Password to differentiate from RoomData.Secret.")]
        public string Secret
        {
            get => Password;
            set => Password = value;
        }

        public CreateLobbyOptions() { }

        /// <summary>
        /// Initializes a new instance of the <see cref="CreateLobbyOptions"/> class.
        /// </summary>
        /// <param name="name">
        /// The name for the lobby. Names are unique. Pass <see langword="null"/> or an empty string to have
        /// <see cref="LobbiesService.FindOrCreateLobbyAsync"/> generate one. See <see cref="Name"/>.
        /// </param>
        /// <param name="region">
        /// <para>
        /// The region for the lobby.
        /// </para>
        /// <para>
        /// This can be used to <see cref="LobbyFilter.WithRegion">filter lobbies by region</see>.
        /// </para>
        /// <para>
        /// If the lobby is associated with a <paramref name="room"/>, this will default to the region of the room's host.
        /// </para>
        /// </param>
        /// <param name="tag">Tag for the lobby. </param>
        /// <param name="maxPlayers">Maximum number of players that the lobby can hold. </param>
        /// <param name="unlisted">
        /// <para>
        /// Specifies whether the lobby should be unlisted or not.
        /// </para>
        /// <para>
        /// Public Lobbies can be matched/searched and then joined by anybody.
        /// Unlisted Lobbies can be joined only if the player knows the Lobby ID.
        /// </para>
        /// </param>
        /// <param name="password">
        /// <para>
        /// Password needed to access the lobby.
        /// </para>
        /// <para>
        /// If <see langword="null"/> or empty, the lobby is accessible without a password.
        /// </para>
        /// </param>
        /// <param name="lobbyAttributes">
        /// <para>
        /// Attributes for the Lobby.
        /// </para>
        /// <para>
        /// Only the owner of the lobby can change Lobby attributes.
        /// </para>
        /// </param>
        /// <param name="playerAttributes">Player attributes. </param>
        /// <param name="room">
        /// The room for which the lobby is created, if any; otherwise, <see langword="null"/>.
        /// </param>
        /// <param name="reservations">Slots to reserve for specific players. </param>
        public CreateLobbyOptions(string name, string region, string tag = null, int maxPlayers = 0, bool unlisted = false, string password = null, List<CloudAttribute> lobbyAttributes = null, List<CloudAttribute> playerAttributes = null, RoomData? room = null, LobbyReservations reservations = null)
        {
            Name = name;
            Tag = tag;
            MaxPlayers = maxPlayers;
            Unlisted = unlisted;
            Password = password;
            Region = region;
            if (room is { } roomData)
            {
                SimulatorSlug = roomData.SimSlug;
                Region ??= roomData.Host.Region;
            }

            LobbyAttributes = lobbyAttributes;
            PlayerAttributes = playerAttributes;
            Reservations = reservations;
        }

        /// <summary>
        /// Creates options for a lobby associated with a <see cref="RoomData">room</see>.
        /// </summary>
        /// <param name="room">The room for which the lobby is created. </param>
        /// <param name="name">
        /// The name for the lobby. If <see langword="null"/> or empty, defaults to the name of the room.
        /// See <see cref="Name"/>.
        /// </param>
        /// <param name="tag">Tag for the lobby. </param>
        /// <param name="maxPlayers">
        /// Maximum number of players that the lobby can hold.
        /// If none is provided defaults to the maximum players of the room.
        /// </param>
        /// <param name="unlisted">
        /// <para>
        /// Specifies whether the lobby should be unlisted or not.
        /// </para>
        /// <para>
        /// Public Lobbies can be matched/searched and then joined by anybody.
        /// Unlisted Lobbies can be joined only if the player knows the Lobby ID.
        /// </para>
        /// </param>
        /// <param name="password">
        /// <para>
        /// Password needed to access the lobby.
        /// </para>
        /// <para>
        /// If <see langword="null"/> or empty, the lobby is accessible without a password.
        /// </para>
        /// </param>
        /// <param name="lobbyAttributes">
        /// <para>
        /// Attributes for the Lobby.
        /// </para>
        /// <para>
        /// Only the owner can change Lobby attributes
        /// </para>
        /// </param>
        /// <param name="playerAttributes">Player attributes. </param>
        /// <param name="reservations">Slots to reserve for specific players. </param>
        /// <returns>
        /// A new instance of <see cref="CreateLobbyOptions"/> initialized with the provided room data and other parameters.
        /// </returns>
        public static CreateLobbyOptions ForRoom(RoomData room, string name = null, string tag = null, int maxPlayers = 0, bool unlisted = false, string password = null, List<CloudAttribute> lobbyAttributes = null, List<CloudAttribute> playerAttributes = null, LobbyReservations reservations = null)
            => new(name: string.IsNullOrEmpty(name) ? room.RoomName : name, region: room.Host.Region, tag: tag, maxPlayers: maxPlayers > 0 ? maxPlayers: room.MaxPlayers, unlisted, password: password, lobbyAttributes: lobbyAttributes, playerAttributes: playerAttributes, room, reservations);
    }
}
