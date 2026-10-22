// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Runtime.Tests
{
    using Coherence.Cloud;
    using Coherence.Tests;
    using Newtonsoft.Json;
    using NUnit.Framework;

    /// <summary>
    /// Tests the lobby creation flags <see cref="FindLobbyRequest"/> carries for the lobby it may create.
    /// </summary>
    /// <remarks>A lobby that is found instead keeps its own; they are read back off <see cref="LobbyData"/>.</remarks>
    public class FindLobbyRequestCreationFlagsTests : CoherenceTest
    {
        [Test]
        [Description("Find-or-create may end up creating the lobby. Check that creation options follow through.")]
        public void GetRequestBody_CarriesTheCreationFlags_WhenTheCallerSetThem()
        {
            var createOptions = new CreateLobbyOptions
            {
                Name = "My Lobby",
                MaxPlayers = 4,
                HideFromPresence = true,
                JoinCode = "JOIN17",
                InvitePermissions = LobbyInvitePermissions.AllMembers
            };

            var requestBody = Deserialize(createOptions);

            Assert.That(requestBody.Name, Is.EqualTo("My Lobby"));
            Assert.That(requestBody.HideFromPresence, Is.True);
            Assert.That(requestBody.JoinCode, Is.EqualTo("JOIN17"));
            Assert.That(requestBody.InvitePermissions, Is.EqualTo(LobbyData.InvitePermissionsAllMembers));
        }

        [Test]
        [Description("An empty name means no name, the same as null: the lobby is named after its room.")]
        public void GetRequestBody_NamesTheLobbyAfterTheRoom_WhenForRoomIsGivenAnEmptyName()
        {
            var room = new RoomData { KV = new() { [RoomData.RoomNameKey] = "Arena" } };

            var requestBody = Deserialize(CreateLobbyOptions.ForRoom(room, name: ""));

            Assert.That(requestBody.Name, Is.EqualTo("Arena"));
        }

        private static FindLobbyRequest Deserialize(CreateLobbyOptions createOptions)
            => JsonConvert.DeserializeObject<FindLobbyRequest>(
                FindLobbyRequest.GetRequestBody(new FindLobbyOptions(), createOptions));
    }
}
