// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Runtime.Tests
{
    using Coherence.Cloud;
    using Coherence.Tests;
    using Coherence.Utils;
    using NUnit.Framework;

    public class LobbyPlayerTests : CoherenceTest
    {
        [TestCase("{\"player_id\":\"p1\"}")]
        [TestCase("{\"id\":\"p1\"}", Description = "Older coherence Cloud sends only the deprecated id.")]
        public void Id_Is_Deserialized_From_Either_Wire_Name(string body)
        {
            var player = CoherenceJson.DeserializeObject<LobbyPlayer>(body);

            Assert.That(player.Id, Is.EqualTo((PlayerAccountId)"p1"));
        }

        [Test]
        public void DisplayName_And_AvatarUrl_Are_Deserialized()
        {
            var player = CoherenceJson.DeserializeObject<LobbyPlayer>(
                "{\"player_id\":\"p1\",\"username\":\"alice\",\"display_name\":\"Alice\"," +
                "\"avatar_url\":\"https://example.test/a.png\"}");

            Assert.That(player.Username, Is.EqualTo("alice"));
            Assert.That(player.DisplayName, Is.EqualTo("Alice"));
            Assert.That(player.AvatarUrl, Is.EqualTo("https://example.test/a.png"));
        }

        [Test]
        [Description("The name fields are sent only when set, and read back as empty strings rather than null.")]
        public void Names_Are_Empty_When_Not_Sent()
        {
            var player = CoherenceJson.DeserializeObject<LobbyPlayer>("{\"player_id\":\"p1\"}");

            Assert.That(player.Username, Is.Empty);
            Assert.That(player.DisplayName, Is.Empty);
            Assert.That(player.AvatarUrl, Is.Empty);
        }

        [Test]
        [Description("The same fields arrive on the /lobbies/player/join push, not just on lobby responses.")]
        public void PlayerJoinedPush_Carries_The_Players_Display_Data()
        {
            var payload = CoherenceJson.DeserializeObject<PlayerJoinedPayload>(
                "{\"lobby_id\":\"lobby1\",\"player\":{\"player_id\":\"p1\",\"display_name\":\"Alice\"}}");

            Assert.That(payload.Player.Id, Is.EqualTo((PlayerAccountId)"p1"));
            Assert.That(payload.Player.DisplayName, Is.EqualTo("Alice"));
        }
    }
}
