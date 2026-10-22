// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Cloud
{
    using System.Collections.Generic;
    using Newtonsoft.Json;

    /// <summary>Body of the <c>/lobbies/reservations</c> push: the lobby's full live reservation list.</summary>
    internal struct ReservationsChangedPayload
    {
        [JsonProperty("lobby_id")]
        public string LobbyId;

        [JsonProperty("reservations")]
        public List<LobbyReservation> Reservations;
    }
}
