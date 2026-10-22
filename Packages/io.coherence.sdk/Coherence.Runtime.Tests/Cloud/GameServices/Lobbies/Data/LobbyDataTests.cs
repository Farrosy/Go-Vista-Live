// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Runtime.Tests
{
    using System;
    using Coherence.Cloud;
    using Coherence.Tests;
    using Coherence.Utils;
    using NUnit.Framework;

    public class LobbyDataTests : CoherenceTest
    {
        [Test]
        [Description("created_at is surfaced as CreatedAt, interpreted as unix seconds.")]
        public void CreatedAt_Is_Deserialized_From_created_at()
        {
            var lobby = CoherenceJson.DeserializeObject<LobbyData>("{\"id\":\"a\",\"created_at\":1786451681}");

            Assert.That(lobby.CreatedAt, Is.EqualTo(DateTimeOffset.FromUnixTimeSeconds(1786451681)));
        }

        [Test]
        [Description("A response without created_at deserializes to the unix epoch instead of throwing.")]
        public void CreatedAt_Defaults_To_UnixEpoch_When_created_at_Is_Missing()
        {
            var lobby = CoherenceJson.DeserializeObject<LobbyData>("{\"id\":\"a\"}");

            Assert.That(lobby.CreatedAt, Is.EqualTo(DateTimeOffset.FromUnixTimeSeconds(0)));
        }

        [Test]
        public void Equals_Is_False_When_CreatedAt_Differs()
        {
            var lobby = CoherenceJson.DeserializeObject<LobbyData>("{\"id\":\"a\",\"created_at\":1786451681}");
            var updated = CoherenceJson.DeserializeObject<LobbyData>("{\"id\":\"a\",\"created_at\":1786451682}");

            Assert.That(lobby, Is.Not.EqualTo(updated));
        }

        [Test]
        public void Equals_Is_True_When_CreatedAt_Matches()
        {
            var lobby = CoherenceJson.DeserializeObject<LobbyData>("{\"id\":\"a\",\"created_at\":1786451681}");
            var same = CoherenceJson.DeserializeObject<LobbyData>("{\"id\":\"a\",\"created_at\":1786451681}");

            Assert.That(lobby, Is.EqualTo(same));
        }

        [Test]
        [Description("reservations is surfaced as Reservations, with player_id/expires_at mapped to Id/ExpiresAt.")]
        public void Reservations_Is_Deserialized_From_reservations()
        {
            var lobby = CoherenceJson.DeserializeObject<LobbyData>(
                "{\"id\":\"a\",\"reservations\":[{\"player_id\":\"p1\",\"expires_at\":1786451681}]}");

            Assert.That(lobby.Reservations, Has.Count.EqualTo(1));
            Assert.That(lobby.Reservations[0].PlayerAccountId, Is.EqualTo((PlayerAccountId)"p1"));
            Assert.That(lobby.Reservations[0].ExpiresAt, Is.EqualTo(DateTimeOffset.FromUnixTimeSeconds(1786451681)));
        }

        [Test]
        [Description("A response without reservations deserializes to a null list instead of throwing.")]
        public void Reservations_Is_Null_When_reservations_Is_Missing()
        {
            var lobby = CoherenceJson.DeserializeObject<LobbyData>("{\"id\":\"a\"}");

            Assert.That(lobby.Reservations, Is.Null);
        }

        [TestCase("\"invite_permissions\":\"members\"", LobbyInvitePermissions.AllMembers)]
        [TestCase("\"invite_permissions\":\"owner\"", LobbyInvitePermissions.OwnerOnly)]
        [TestCase("", LobbyInvitePermissions.OwnerOnly,
            Description = "No invite_permissions reads as owner-only, the default a lobby is created with.")]
        [TestCase("\"invite_permissions\":\"nobody\"", LobbyInvitePermissions.OwnerOnly,
            Description = "A value this version doesn't know reads as owner-only rather than throwing.")]
        public void InvitePermissions_Is_Read_From_invite_permissions(string invitePermissions,
            LobbyInvitePermissions expected)
        {
            var lobby = CoherenceJson.DeserializeObject<LobbyData>(Body(invitePermissions));

            Assert.That(lobby.InvitePermissions, Is.EqualTo(expected));
        }

        [TestCase("\"join_code\":\"JOIN17\"", "JOIN17")]
        [TestCase("", "", Description = "Search results carry no join_code, read as empty rather than null.")]
        public void JoinCode_Is_Read_From_join_code(string joinCode, string expected)
        {
            var lobby = CoherenceJson.DeserializeObject<LobbyData>(Body(joinCode));

            Assert.That(lobby.JoinCode, Is.EqualTo(expected));
        }

        [TestCase("\"hide_from_presence\":true", true)]
        [TestCase("\"hide_from_presence\":false", false)]
        [TestCase("", false, Description = "A lobby that says nothing about it is in its members' presence.")]
        public void HideFromPresence_Is_Read_From_hide_from_presence(string hideFromPresence, bool expected)
        {
            var lobby = CoherenceJson.DeserializeObject<LobbyData>(Body(hideFromPresence));

            Assert.That(lobby.HideFromPresence, Is.EqualTo(expected));
        }

        private static string Body(string extraField)
            => extraField.Length == 0 ? "{\"id\":\"a\"}" : "{\"id\":\"a\"," + extraField + "}";

        [Test]
        public void Equals_Is_False_When_invite_permissions_Differs()
        {
            var lobby = CoherenceJson.DeserializeObject<LobbyData>(Body("\"invite_permissions\":\"owner\""));
            var updated = CoherenceJson.DeserializeObject<LobbyData>(Body("\"invite_permissions\":\"members\""));

            Assert.That(lobby, Is.Not.EqualTo(updated));
        }

        [Test]
        public void Equals_Is_False_When_Reservations_Differ()
        {
            var lobby = CoherenceJson.DeserializeObject<LobbyData>(
                "{\"id\":\"a\",\"reservations\":[{\"player_id\":\"p1\",\"expires_at\":1786451681}]}");
            var updated = CoherenceJson.DeserializeObject<LobbyData>("{\"id\":\"a\",\"reservations\":[]}");

            Assert.That(lobby, Is.Not.EqualTo(updated));
        }
    }
}
