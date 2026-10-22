// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Runtime.Tests
{
    using Coherence.Cloud;
    using Coherence.Tests;
    using Newtonsoft.Json;
    using NUnit.Framework;

    public class LobbyCreationRequestHideFromPresenceTests : CoherenceTest
    {
        [TestCase(true)]
        [TestCase(false)]
        public void GetRequestBody_SendsHideFromPresence_WhenTheCallerSetIt(bool hideFromPresence)
        {
            var options = new CreateLobbyOptions { MaxPlayers = 4, HideFromPresence = hideFromPresence };

            var requestBodyJson = LobbyCreationRequest.GetRequestBody(options);
            var requestBody = JsonConvert.DeserializeObject<LobbyCreationRequest>(requestBodyJson);

            Assert.That(requestBody.HideFromPresence, Is.EqualTo(hideFromPresence));
        }

        [Test]
        [Description("HideFromPresence is sent even when set to false.")]
        public void GetRequestBody_SendsHideFromPresenceAsFalse_WhenTheCallerSetNothing()
        {
            var options = new CreateLobbyOptions { MaxPlayers = 4 };

            var requestBodyJson = LobbyCreationRequest.GetRequestBody(options);

            Assert.That(requestBodyJson, Does.Contain("hide_from_presence"),
                "The field was left out of the body instead of being sent as false.");
            Assert.That(JsonConvert.DeserializeObject<LobbyCreationRequest>(requestBodyJson).HideFromPresence,
                Is.False);
        }
    }
}
