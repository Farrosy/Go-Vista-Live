// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Cloud
{
    using System;
    using Newtonsoft.Json;

    /// <summary>Represents a single <see cref="LobbyData">Lobby</see> slot reserved for a specific player.</summary>
    /// <remarks>
    /// The slot stays reserved until the player identified by <see cref="PlayerAccountId"/> joins, or until
    /// <see cref="ExpiresAt"/> passes, at which point it becomes available to anyone and
    /// <see cref="LobbySession.OnReservationExpired"/> fires.
    /// </remarks>
    public struct LobbyReservation : IEquatable<LobbyReservation>
    {
        /// <summary>The <see cref="PlayerAccountId"/> of the player for whom this slot is reserved.</summary>
        [JsonIgnore]
        public PlayerAccountId PlayerAccountId => playerId;

        [JsonProperty("player_id")]
        internal string playerId;

        /// <summary>
        /// The time at which this reservation expires if the reserved player hasn't joined yet.
        /// </summary>
        [JsonIgnore]
        public DateTimeOffset ExpiresAt => DateTimeOffset.FromUnixTimeSeconds(expiresAt);

        [JsonProperty("expires_at")]
        internal long expiresAt;

        public bool Equals(LobbyReservation other) => PlayerAccountId.Equals(other.PlayerAccountId) && expiresAt == other.expiresAt;
        public override bool Equals(object obj) => obj is LobbyReservation other && Equals(other);
        public override int GetHashCode() => PlayerAccountId.GetHashCode();
    }
}
