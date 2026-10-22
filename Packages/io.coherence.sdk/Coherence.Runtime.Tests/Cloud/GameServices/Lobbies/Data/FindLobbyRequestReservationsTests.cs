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

    public class FindLobbyRequestReservationsTests : CoherenceTest
    {
        [Test]
        [Description("Reservations on the CreateLobbyOptions passed to FindOrCreate are sent with the match request too, so a found lobby must fit the whole party.")]
        public void GetRequestBody_Includes_Reservations_From_CreateOptions_When_Provided()
        {
            var findOptions = new FindLobbyOptions();
            var createOptions = new CreateLobbyOptions
            {
                MaxPlayers = 4,
                Reservations = new LobbyReservations(new PlayerAccountId[] { "p1" }, TimeSpan.FromSeconds(15))
            };

            var requestBodyJson = FindLobbyRequest.GetRequestBody(findOptions, createOptions);
            var requestBody = JsonConvert.DeserializeObject<FindLobbyRequest>(requestBodyJson);

            Assert.That(requestBody.Reservations, Is.Not.Null);
            Assert.That(requestBody.Reservations.Value.PlayerIds, Is.EqualTo(new List<string> { "p1" }));
            Assert.That(requestBody.Reservations.Value.TotalSeconds, Is.EqualTo(15));
        }

        [Test]
        [Description("A CreateLobbyOptions without Reservations omits the field from the match request instead of sending an empty object.")]
        public void GetRequestBody_Omits_Reservations_When_Not_Provided()
        {
            var findOptions = new FindLobbyOptions();
            var createOptions = new CreateLobbyOptions { MaxPlayers = 4 };

            var requestBodyJson = FindLobbyRequest.GetRequestBody(findOptions, createOptions);
            var requestBody = JsonConvert.DeserializeObject<FindLobbyRequest>(requestBodyJson);

            Assert.That(requestBody.Reservations, Is.Null);
        }
    }
}
