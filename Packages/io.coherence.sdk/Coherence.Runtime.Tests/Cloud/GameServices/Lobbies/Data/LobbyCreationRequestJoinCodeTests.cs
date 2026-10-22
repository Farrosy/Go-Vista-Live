// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Runtime.Tests
{
    using Coherence.Cloud;
    using Coherence.Tests;
    using Newtonsoft.Json;
    using NUnit.Framework;

    public class LobbyCreationRequestJoinCodeTests : CoherenceTest
    {
        [Test]
        public void GetRequestBody_Includes_JoinCode_When_Provided()
        {
            var options = new CreateLobbyOptions { MaxPlayers = 4, JoinCode = "JOIN17" };

            var requestBodyJson = LobbyCreationRequest.GetRequestBody(options);
            var requestBody = JsonConvert.DeserializeObject<LobbyCreationRequest>(requestBodyJson);

            Assert.That(requestBody.JoinCode, Is.EqualTo("JOIN17"));
        }

        [TestCase(null)]
        [TestCase("")]
        [Description("A lobby created without a join code asks for none, however the caller expressed it.")]
        public void GetRequestBody_Sends_No_JoinCode_When_Not_Provided(string joinCode)
        {
            var options = new CreateLobbyOptions { MaxPlayers = 4, JoinCode = joinCode };

            var requestBodyJson = LobbyCreationRequest.GetRequestBody(options);

            var requestBody = JsonConvert.DeserializeObject<LobbyCreationRequest>(requestBodyJson);

            Assert.That(requestBody.JoinCode, Is.Null);
        }
    }
}
