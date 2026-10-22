// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Cloud
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Threading;
    using Newtonsoft.Json;

    /// <summary>A friend player account.</summary>
    /// <remarks>
    /// Friendships are mutual (both player accounts need to befriend each other).
    /// Friends hold online presence, so you can see whether they are online, if
    /// they carry presence (i.e., a custom status message), and which lobbies are
    /// they on.
    /// </remarks>
    /// <seealso cref="FriendsService.Friends"/>
    public struct Friend : IEquatable<Friend>
    {
        [JsonIgnore]
        public PlayerAccountId Id => playerId;

        /// <summary>Player account username.</summary>
        /// <remarks>
        /// Players who log in with a platform account (Game Center, Steam, and so on)
        /// have no username unless your game gives them one, so fall back to
        /// <see cref="DisplayName"/> or to the platform's own profile,
        /// via <see cref="SourceIds"/>.
        /// </remarks>
        [JsonIgnore]
        public string Username => username ?? "";

        /// <summary>The name this friend chose to be shown as.</summary>
        /// <remarks>Empty string if it was never set.</remarks>
        [JsonIgnore]
        public string DisplayName => displayName ?? "";

        /// <summary>The image URL for the friend's avatar.</summary>
        /// <remarks>Empty string if it was never set.</remarks>
        [JsonIgnore]
        public string AvatarUrl => avatarUrl ?? "";

        /// <summary>This friend's id on each platform they were synced from.</summary>
        /// <remarks>
        /// <para>
        /// Only accounts for the platforms the player synced them under, not every account the friend has linked.
        /// </para>
        /// <para>
        /// Keyed by the same source passed to <see cref="FriendsService.SyncFriendsAsync"/>, so
        /// <c>friend.SourceIds["steam"]</c> is this friend's Steam id.
        /// </para>
        /// </remarks>
        [JsonIgnore]
        public IReadOnlyDictionary<string, string> SourceIds
            => sourceIds is { Count: > 0 } ? new ReadOnlyDictionary<string, string>(sourceIds) : EmptySourceIds;

        /// <summary>Whether this friend is playing right now.</summary>
        /// <remarks>
        /// A friend who chose to appear offline will be reported as not online,
        /// with no <see cref="Presence"/> and no <see cref="LobbyIds"/>.
        /// </remarks>
        [JsonIgnore]
        public bool IsOnline => isOnline;

        /// <summary>The lobbies this friend is in, the one they joined last first.</summary>
        /// <remarks>
        /// <para>
        /// Starting from the most recent Lobby joined.
        /// </para>
        /// <para>
        /// Empty when they are in none, when they are offline, and for lobbies created with
        /// <see cref="CreateLobbyOptions.HideFromPresence"/>.
        /// </para>
        /// <para>
        /// Hand one to
        /// <see cref="LobbiesService.JoinLobbyAsync(string, List{CloudAttribute}, string, LobbyReservations,
        /// CancellationToken)"/>
        /// to play along.
        /// The lobby has to have enough space and will require password (if set).
        /// </para>
        /// </remarks>
        [JsonIgnore]
        public IReadOnlyList<string> LobbyIds
            => lobbyIds is { Count: > 0 } ? new ReadOnlyCollection<string>(lobbyIds) : EmptyLobbyIds;

        /// <summary>Custom message reporting friend's online presence.</summary>
        /// <remarks>
        /// <para>Empty string when they never set one, and while they are offline.</para>
        /// </remarks>
        /// <seealso cref="FriendsService.SetPresenceAsync"/>
        [JsonIgnore]
        public string Presence => presence ?? "";

        /// <summary>When this friend was last seen playing.</summary>
        /// <remarks>
        /// <see langword="null"/> while they are online, and for a friend who has never played.
        /// Read <see cref="IsOnline"/> to tell those two apart.
        /// </remarks>
        [JsonIgnore]
        public DateTimeOffset? LastOnlineAt
            => lastOnlineAt is { } seconds ? DateTimeOffset.FromUnixTimeSeconds(seconds) : null;

        private static readonly IReadOnlyDictionary<string, string> EmptySourceIds
            = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>());

        private static readonly IReadOnlyList<string> EmptyLobbyIds = Array.Empty<string>();

        [JsonProperty("player_id")]
        internal string playerId;

        [JsonProperty("username")]
        internal string username;

        [JsonProperty("display_name")]
        internal string displayName;

        [JsonProperty("avatar_url")]
        internal string avatarUrl;

        [JsonProperty("source_ids")]
        internal Dictionary<string, string> sourceIds;

        [JsonProperty("online")]
        internal bool isOnline;

        [JsonProperty("lobby_ids")]
        internal List<string> lobbyIds;

        [JsonProperty("presence")]
        internal string presence;

        [JsonProperty("last_online_at")]
        internal long? lastOnlineAt;

        public override string ToString() => $"Friend(Id:{Id}, Username:{Username}, DisplayName:{DisplayName})";

        public bool Equals(Friend other) => Id == other.Id;

        public override bool Equals(object obj) => obj is Friend other && Equals(other);
        public override int GetHashCode() => Id.GetHashCode();

        public static bool operator ==(Friend left, Friend right) => left.Equals(right);
        public static bool operator !=(Friend left, Friend right) => !left.Equals(right);
    }
}
