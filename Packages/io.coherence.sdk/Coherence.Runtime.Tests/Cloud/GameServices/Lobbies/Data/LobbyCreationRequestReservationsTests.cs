// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Runtime.Tests
{
    using System;
    using System.Collections.Generic;
    using Coherence.Cloud;
    using Coherence.Tests;
    using Newtonsoft.Json;
    using NUnit.Framework;

    public class LobbyCreationRequestReservationsTests : CoherenceTest
    {
        [Test]
        [Description("Reservations on CreateLobbyOptions are sent as { player_ids, ttl_seconds } on the request body.")]
        public void GetRequestBody_Includes_Reservations_When_Provided()
        {
            var options = new CreateLobbyOptions
            {
                MaxPlayers = 4,
                Reservations = new LobbyReservations(new PlayerAccountId[] { "p1", "p2" }, TimeSpan.FromSeconds(30))
            };

            var requestBodyJson = LobbyCreationRequest.GetRequestBody(options);
            var requestBody = JsonConvert.DeserializeObject<LobbyCreationRequest>(requestBodyJson);

            Assert.That(requestBody.Reservations, Is.Not.Null);
            Assert.That(requestBody.Reservations.Value.PlayerIds, Is.EqualTo(new List<string> { "p1", "p2" }));
            Assert.That(requestBody.Reservations.Value.TotalSeconds, Is.EqualTo(30));
        }

        [Test]
        [Description("A null Reservations, or one with no PlayerIds, is omitted from the request body instead of being sent as an empty object.")]
        public void GetRequestBody_Omits_Reservations_When_Not_Provided()
        {
            var options = new CreateLobbyOptions { MaxPlayers = 4 };

            var requestBodyJson = LobbyCreationRequest.GetRequestBody(options);
            var requestBody = JsonConvert.DeserializeObject<LobbyCreationRequest>(requestBodyJson);

            Assert.That(requestBody.Reservations, Is.Null);
        }
    }
}
