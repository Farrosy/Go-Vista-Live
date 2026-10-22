// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Cloud
{
    using System;
    using System.Linq;
    using Newtonsoft.Json;
    using System.Collections.Generic;

    public struct LobbyData : IEquatable<LobbyData>
    {
        [JsonIgnore] public IReadOnlyList<CloudAttribute> Attributes => lobbyAttributes;
        [JsonIgnore] public IReadOnlyList<LobbyPlayer> Players => players;

        /// <summary>Slots reserved for specific players, until they join or their reservation expires.</summary>
        [JsonIgnore] public IReadOnlyList<LobbyReservation> Reservations => reservations;

        [JsonProperty("id")]
        public string Id;

        [JsonProperty("name")]
        public string Name;

        [JsonProperty("region")]
        public string Region;

        [JsonProperty("tag")]
        public string Tag;

        [JsonProperty("max_players")]
        public int MaxPlayers;

        [JsonProperty("closed")]
        public bool Closed;

        [JsonProperty("unlisted")]
        public bool Unlisted;

        [JsonProperty("private")]
        public bool IsPrivate;

        /// <summary>Who may invite players to this lobby, and create invite tokens for it.</summary>
        /// <remarks>
        /// Set through <see cref="CreateLobbyOptions.InvitePermissions"/> when the lobby is created,
        /// and fixed for its lifetime. The owner may always do both.
        /// </remarks>
        /// <seealso cref="CreateLobbyOptions.InvitePermissions"/>
        /// <seealso cref="LobbySession.CreateInviteTokenAsync"/>
        [JsonIgnore]
        public LobbyInvitePermissions InvitePermissions => invitePermissions == InvitePermissionsAllMembers
            ? LobbyInvitePermissions.AllMembers
            : LobbyInvitePermissions.OwnerOnly;

        internal const string InvitePermissionsAllMembers = "members";
        internal const string InvitePermissionsOwnerOnly = "owner";

        /// <summary>Who the backend lets invite players and create invite tokens for this lobby.</summary>
        /// <remarks>
        /// Either <see cref="InvitePermissionsAllMembers"/> or <see cref="InvitePermissionsOwnerOnly"/>.
        /// </remarks>
        /// <seealso cref="InvitePermissions"/>
        [JsonProperty("invite_permissions")]
        internal string invitePermissions;

        /// <summary>
        /// A code that players can use to
        /// <see cref="LobbiesService.JoinLobbyByCodeAsync">join this lobby</see> (if any).
        /// </summary>
        /// <remarks>
        /// Only carried by the <see cref="LobbySession.LobbyData"/> of a lobby you are in.
        /// <see cref="LobbySession.LobbyData"/> instances retrieved from
        /// <see cref="LobbiesService.FindLobbiesAsync"/> won't have this code,
        /// even when they describe a lobby you are already in.
        /// </remarks>
        /// <seealso cref="CreateLobbyOptions.JoinCode"/>
        /// <seealso cref="LobbiesService.JoinLobbyByCodeAsync"/>
        [JsonIgnore]
        public string JoinCode => joinCode ?? "";

        [JsonProperty("join_code")]
        internal string joinCode;

        /// <summary>Whether the lobby should be listed as part of the players' presence.</summary>
        /// <seealso cref="CreateLobbyOptions.HideFromPresence"/>
        [JsonIgnore]
        public bool HideFromPresence => hideFromPresence;

        [JsonProperty("hide_from_presence")]
        internal bool hideFromPresence;

        /// <summary>
        /// The most new slots a single request may reserve for the rest of a party, when
        /// <see cref="LobbiesService.JoinLobbyAsync(string, List{CloudAttribute}, string, LobbyReservations,
        /// System.Threading.CancellationToken)">joining</see> or as a member through
        /// <see cref="LobbySession.ReserveSlotsAsync"/>. 0 means players can't reserve slots.
        /// </summary>
        /// <seealso cref="CreateLobbyOptions.MaxReservationsPerRequest"/>
        [JsonIgnore]
        public int MaxReservationsPerRequest => joinReservations.MaxSlots;

        /// <summary>How long a slot reserved in this lobby after its creation lasts, at most.</summary>
        /// <seealso cref="CreateLobbyOptions.MaxReservationTimeout"/>
        [JsonIgnore]
        public TimeSpan MaxReservationTimeout => TimeSpan.FromSeconds(joinReservations.TimeoutSeconds ?? 0);

        [JsonProperty("join_reservations", NullValueHandling = NullValueHandling.Ignore)]
        internal JoinReservationsPolicy joinReservations;

        [JsonIgnore]
        public PlayerAccountId OwnerId => ownerId;
        [JsonProperty("owner_id")]
        internal string ownerId;

        [JsonProperty("sim_slug")]
        public string SimulatorSlug;

        [JsonProperty("room_id")]
        public long RoomId;

        [JsonProperty("room")]
        public RoomData? RoomData;

        [JsonProperty("players")]
        internal List<LobbyPlayer> players;

        [JsonProperty("attributes")]
        internal List<CloudAttribute> lobbyAttributes;

        [JsonProperty("reservations")]
        internal List<LobbyReservation> reservations;

        /// <summary>
        /// The date and time at which the lobby was created.
        /// </summary>
        [JsonIgnore]
        public DateTimeOffset CreatedAt => DateTimeOffset.FromUnixTimeSeconds(createdAt);

        [JsonProperty("created_at")]
        internal long createdAt;

        public CloudAttribute? GetAttribute(string key)
        {
            if (lobbyAttributes == null)
            {
                return null;
            }

            foreach (var attribute in lobbyAttributes)
            {
                if (attribute.Key.Equals(key))
                {
                    return attribute;
                }
            }

            return null;
        }

        public override string ToString() => $"LobbySession(Name:\"{Name}\", Id:\"{Id}\")";

        public bool Equals(LobbyData other)
        {
            return Id == other.Id
                && string.Equals(Name, other.Name)
                && string.Equals(Region, other.Region)
                && string.Equals(Tag, other.Tag)
                && MaxPlayers == other.MaxPlayers
                && Closed == other.Closed
                && Unlisted == other.Unlisted
                && IsPrivate == other.IsPrivate
                && string.Equals(invitePermissions, other.invitePermissions)
                && string.Equals(joinCode, other.joinCode)
                && hideFromPresence == other.hideFromPresence
                && joinReservations.Equals(other.joinReservations)
                && string.Equals(ownerId, other.ownerId)
                && string.Equals(SimulatorSlug, other.SimulatorSlug)
                && RoomId == other.RoomId
                && Nullable.Equals(RoomData, other.RoomData)
                && createdAt == other.createdAt
                && ListEqual(players, other.players)
                && ListEqual(lobbyAttributes, other.lobbyAttributes)
                && ListEqual(reservations, other.reservations);

            bool ListEqual<T>(List<T> a, List<T> b) => a is null ? b is null : b is not null && a.SequenceEqual(b);
        }

        public override bool Equals(object obj) => obj is LobbyData other && Equals(other);

        public override int GetHashCode() => Id?.GetHashCode() ?? 0;
    }
}
