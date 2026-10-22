// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Runtime.Tests
{
    using Coherence.Cloud;
    using Coherence.Tests;
    using Newtonsoft.Json;
    using NUnit.Framework;

    public class LobbyCreationRequestInvitePermissionsTests : CoherenceTest
    {
        [TestCase(LobbyInvitePermissions.AllMembers, "members",
            Description = "AllMembers sends invite_permissions \"members\".")]
        [TestCase(LobbyInvitePermissions.OwnerOnly, "owner",
            Description = "By default only the owner can invite and create invite tokens.")]
        public void GetRequestBody_Sends_invite_permissions(LobbyInvitePermissions invitePermissions, string expected)
        {
            var options = new CreateLobbyOptions
            {
                MaxPlayers = 4,
                InvitePermissions = invitePermissions,
            };

            var requestBodyJson = LobbyCreationRequest.GetRequestBody(options);
            var requestBody = JsonConvert.DeserializeObject<LobbyCreationRequest>(requestBodyJson);

            Assert.That(requestBody.InvitePermissions, Is.EqualTo(expected));
        }
    }
}
