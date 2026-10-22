// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Runtime.Tests
{
    using Coherence.Cloud;
    using Coherence.Tests;
    using Newtonsoft.Json;
    using NUnit.Framework;

    public class JoinLobbyRequestInviteTokenTests : CoherenceTest
    {
        [Test]
        [Description("An invite token passed to a join is sent as invite_token, alongside any password given.")]
        public void GetRequestBody_Includes_InviteToken_When_Provided()
        {
            var requestBodyJson = JoinLobbyRequest.GetRequestBody(null, "a-password", "a-token");
            var requestBody = JsonConvert.DeserializeObject<JoinLobbyRequest>(requestBodyJson);

            Assert.That(requestBody.InviteToken, Is.EqualTo("a-token"));
            Assert.That(requestBody.Password, Is.EqualTo("a-password"));
        }

        [TestCase(null)]
        [TestCase("")]
        [Description("A join without an invite carries no token, however the caller expressed its absence.")]
        public void GetRequestBody_Sends_No_InviteToken_When_Not_Provided(string inviteToken)
        {
            var requestBodyJson = JoinLobbyRequest.GetRequestBody(null, "a-password", inviteToken);

            var requestBody = JsonConvert.DeserializeObject<JoinLobbyRequest>(requestBodyJson);

            Assert.That(requestBody.InviteToken, Is.Null.Or.Empty);
        }
    }
}
