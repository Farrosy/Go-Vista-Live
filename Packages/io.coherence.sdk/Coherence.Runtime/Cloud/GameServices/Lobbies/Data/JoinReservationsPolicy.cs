// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Cloud
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using Newtonsoft.Json;
    using Runtime.Utils;

    /// <summary>
    /// Wire representation of <see cref="CreateLobbyOptions.MaxReservationsPerRequest"/> and
    /// <see cref="CreateLobbyOptions.MaxReservationTimeout"/>, sent and read back as <c>join_reservations</c>.
    /// </summary>
    internal struct JoinReservationsPolicy : IEquatable<JoinReservationsPolicy>
    {
        [JsonProperty("max_slots")]
        public int MaxSlots;

        [JsonProperty("ttl_seconds", NullValueHandling = NullValueHandling.Ignore)]
        public int? TimeoutSeconds;

        /// <summary>Left out of the request when nothing was set, so the lobby allows no reservations.</summary>
        [return: MaybeNull]
        internal static JoinReservationsPolicy? FromOptions(CreateLobbyOptions options)
            => options.MaxReservationsPerRequest > 0 || options.MaxReservationTimeout.HasValue
                ? new JoinReservationsPolicy
                {
                    MaxSlots = options.MaxReservationsPerRequest,
                    TimeoutSeconds = options.MaxReservationTimeout?.ToWholeSeconds(),
                }
                : null;

        public bool Equals(JoinReservationsPolicy other)
            => MaxSlots == other.MaxSlots && TimeoutSeconds == other.TimeoutSeconds;

        public override bool Equals(object obj) => obj is JoinReservationsPolicy other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(MaxSlots, TimeoutSeconds);
    }
}
