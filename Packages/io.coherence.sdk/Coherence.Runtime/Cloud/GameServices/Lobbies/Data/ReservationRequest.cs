// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Cloud
{
    using System;
    using Newtonsoft.Json;
    using Runtime.Utils;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;

    /// <summary>
    /// Wire representation of a <see cref="LobbyReservations"/>, sent as the <c>reservations</c> field on the
    /// Create, FindOrCreate ("match") and Join Lobby requests.
    /// </summary>
    internal struct ReservationRequest
    {
        [JsonProperty("player_ids")]
        public List<string> PlayerIds;

        [JsonProperty("ttl_seconds", NullValueHandling = NullValueHandling.Ignore)]
        public int? TotalSeconds;

        internal static ReservationRequest? FromOptions([MaybeNull] LobbyReservations reservations)
            => reservations?.PlayerAccountIds is { Count: > 0 } ids
                ? new ReservationRequest
                {
                    PlayerIds = new(ids.Select(x => (string)x)),
                    TotalSeconds = reservations.Timeout is { } timeout
                        ? timeout.ToWholeSeconds()
                        : null
                } : null;
    }
}
