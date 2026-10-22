// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Runtime.Tests
{
    using System.Collections.Generic;
    using Coherence.Cloud;
    using Coherence.Tests;
    using Newtonsoft.Json;
    using NUnit.Framework;

    public class JoinLobbyByCodeRequestTests : CoherenceTest
    {
        [Test]
        public void GetRequestBody_Sends_The_Code_As_join_code()
        {
            var requestBodyJson = JoinLobbyByCodeRequest.GetRequestBody("JOIN17", null);
            var requestBody = JsonConvert.DeserializeObject<JoinLobbyByCodeRequest>(requestBodyJson);

            Assert.That(requestBody.JoinCode, Is.EqualTo("JOIN17"));
        }

        [Test]
        [Description("Joining by code takes player attributes, the same as joining by id does.")]
        public void GetRequestBody_Sends_PlayerAttributes()
        {
            var attributes = new List<CloudAttribute> { new("rank", "bronze") };

            var requestBodyJson = JoinLobbyByCodeRequest.GetRequestBody("JOIN17", attributes);
            var requestBody = JsonConvert.DeserializeObject<JoinLobbyByCodeRequest>(requestBodyJson);

            Assert.That(requestBody.PlayerAttributes, Has.Count.EqualTo(1));
            Assert.That(requestBody.PlayerAttributes[0].Key, Is.EqualTo("rank"));
        }
    }
}
