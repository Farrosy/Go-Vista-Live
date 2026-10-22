// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Cloud
{
    using Newtonsoft.Json;
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Diagnostics.CodeAnalysis;

    /// <summary>
    /// Contains information about a player that has joined a <see cref="LobbyData">lobby</see>.
    /// </summary>
    public struct LobbyPlayer : IEquatable<LobbyPlayer>, IComparable<LobbyPlayer>
    {
        [JsonProperty("player_id")]
        private string playerId;

#pragma warning disable CS0649
        [JsonProperty("id")]
        [Deprecated("09/2026", 2, 3, 0, Reason = "The backend sends player_id; read to understand older responses.")]
        private string id;
#pragma warning restore CS0649

        [JsonProperty("username")]
        public string username;

        [JsonProperty("display_name")]
        internal string displayName;

        [JsonProperty("avatar_url")]
        internal string avatarUrl;

        [JsonProperty("attributes")]
        internal List<CloudAttribute> attributes;

        [JsonIgnore]
        public PlayerAccountId Id => RawId;

        private string RawId => playerId ?? id;

        [JsonIgnore]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [Obsolete("Use " + nameof(Id) + " instead.")]
        [Deprecated("04/2025", 1, 6, 0, Reason = "Use " + nameof(Id) + " instead.")]
        public string UserId => RawId;

        /// <summary>This player's username.</summary>
        /// <remarks>
        /// Players who log in with a platform account (Game Center, Steam, and so on) have no username unless
        /// your game gives them one, so fall back to <see cref="DisplayName"/>. Empty string if it was never set.
        /// </remarks>
        [NotNull, JsonIgnore] public string Username => username ?? "";

        /// <summary>The name this player chose to be shown as.</summary>
        /// <remarks>Empty string if it was never set.</remarks>
        [NotNull, JsonIgnore] public string DisplayName => displayName ?? "";

        /// <summary>The image URL for this player's avatar.</summary>
        /// <remarks>Empty string if it was never set.</remarks>
        [NotNull, JsonIgnore] public string AvatarUrl => avatarUrl ?? "";
        [NotNull, JsonIgnore] public IReadOnlyList<CloudAttribute> Attributes => attributes ?? (IReadOnlyList<CloudAttribute>)Array.Empty<CloudAttribute>();

        bool IEquatable<LobbyPlayer>.Equals(LobbyPlayer other) => Equals(other);

        internal LobbyPlayer(string playerId) : this(playerId, "", new(0)) { }

        internal LobbyPlayer(string playerId, string username, List<CloudAttribute> attributes) : this()
        {
            this.playerId = playerId;
            this.username = username;
            this.attributes = attributes;
        }

        public bool Equals(in LobbyPlayer other) => Id == other.Id;
        public override bool Equals(object obj) => obj is LobbyPlayer other && Equals(other);
        public int CompareTo(LobbyPlayer other) => string.Compare(Id, other.Id, StringComparison.Ordinal);
        public static bool operator ==(in LobbyPlayer left, in LobbyPlayer right) => left.Equals(right);
        public static bool operator !=(in LobbyPlayer left, in LobbyPlayer right) => !left.Equals(right);

        public override int GetHashCode() => Id.GetHashCode();

        public CloudAttribute? GetAttribute(string key)
        {
            foreach (var attribute in Attributes)
            {
                if (attribute.Key.Equals(key))
                {
                    return attribute;
                }
            }

            return null;
        }
    }
}
