// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Runtime.Tests
{
    using System;
    using System.Collections.Generic;
    using Coherence.Cloud;
    using Coherence.Tests;
    using Coherence.Utils;
    using NUnit.Framework;

    public class FriendTests : CoherenceTest
    {
        private const string FullFriend =
            "{\"player_id\":\"1\",\"username\":\"bob\",\"display_name\":\"Bob\"," +
            "\"avatar_url\":\"https://coherence.io/apple-touch-icon.png\"," +
            "\"source_ids\":{\"gamecenter\":\"G:1\",\"steam\":\"76561\"}," +
            "\"online\":true,\"lobby_ids\":[\"lby_8f3a\",\"lby_1c2d\"]," +
            "\"presence\":\"outfit:pirate_03\",\"last_online_at\":null}";

        [Test]
        public void Friend_ReadsEveryField_WhenDeserialized()
        {
            var friend = CoherenceJson.DeserializeObject<Friend>(FullFriend);

            Assert.That(friend.Id, Is.EqualTo((PlayerAccountId)"1"));
            Assert.That(friend.Username, Is.EqualTo("bob"));
            Assert.That(friend.DisplayName, Is.EqualTo("Bob"));
            Assert.That(friend.AvatarUrl, Is.EqualTo("https://coherence.io/apple-touch-icon.png"));
            Assert.That(friend.SourceIds["gamecenter"], Is.EqualTo("G:1"));
            Assert.That(friend.SourceIds["steam"], Is.EqualTo("76561"));
            Assert.That(friend.IsOnline, Is.True);
            Assert.That(friend.LobbyIds, Is.EqualTo(new[] { "lby_8f3a", "lby_1c2d" }));
            Assert.That(friend.Presence, Is.EqualTo("outfit:pirate_03"));
            Assert.That(friend.LastOnlineAt, Is.Null);
        }

        [Test]
        public void Friend_ReadsUnsetFieldsAsEmpty_WhenTheyAreMissingFromThePayload()
        {
            var friend = CoherenceJson.DeserializeObject<Friend>("{\"player_id\":\"1\"}");

            Assert.That(friend.Username, Is.Empty);
            Assert.That(friend.DisplayName, Is.Empty);
            Assert.That(friend.AvatarUrl, Is.Empty);
            Assert.That(friend.SourceIds, Is.Empty);
            Assert.That(friend.IsOnline, Is.False);
            Assert.That(friend.LobbyIds, Is.Empty);
            Assert.That(friend.Presence, Is.Empty);
            Assert.That(friend.LastOnlineAt, Is.Null);
        }

        [Test]
        [Description("Don't allow modifying source ids (when there's none).")]
        public void SourceIds_CannotBeModified_WhenTheFriendHasNone()
        {
            var friend = CoherenceJson.DeserializeObject<Friend>("{\"player_id\":\"1\"}");

            Assert.That(() => ((IDictionary<string, string>)friend.SourceIds)["evil"] = "x",
                Throws.TypeOf<NotSupportedException>());
            Assert.That(friend.SourceIds, Is.Empty);
        }

        [Test]
        [Description("Don't allow modifying source ids (when there's any).")]
        public void SourceIds_CannotBeModified_WhenTheFriendHasSome()
        {
            var friend = CoherenceJson.DeserializeObject<Friend>(FullFriend);

            Assert.That(() => ((IDictionary<string, string>)friend.SourceIds)["evil"] = "x",
                Throws.TypeOf<NotSupportedException>());
            Assert.That(friend.SourceIds["gamecenter"], Is.EqualTo("G:1"));
        }

        [Test]
        [Description("Don't allow modifying lobby ids (when there's friends).")]
        public void LobbyIds_CannotBeModified_WhenTheFriendIsInLobbies()
        {
            var friend = CoherenceJson.DeserializeObject<Friend>(
                "{\"player_id\":\"1\",\"lobby_ids\":[\"lby_8f3a\"]}");

            Assert.That(() => ((IList<string>)friend.LobbyIds).Add("evil"),
                Throws.TypeOf<NotSupportedException>());
            Assert.That(friend.LobbyIds, Is.EqualTo(new[] { "lby_8f3a" }));
        }

        [Test]
        [Description("Don't allow modifying lobby ids (when there's no friends).")]
        public void LobbyIds_CannotBeModified_WhenTheFriendIsInNoLobby()
        {
            var friend = CoherenceJson.DeserializeObject<Friend>("{\"player_id\":\"1\"}");

            Assert.That(() => ((IList<string>)friend.LobbyIds).Add("evil"),
                Throws.TypeOf<NotSupportedException>());
            Assert.That(friend.LobbyIds, Is.Empty);
        }

        [Test]
        [Description("Friend identity/equality is tied to player_id.")]
        public void Equals_ComparesIdOnly_WhenTheNamesDiffer()
        {
            var friend = CoherenceJson.DeserializeObject<Friend>("{\"player_id\":\"1\",\"display_name\":\"Bob\"}");
            var renamed = CoherenceJson.DeserializeObject<Friend>("{\"player_id\":\"1\",\"display_name\":\"Bobby\"}");
            var somebodyElse = CoherenceJson.DeserializeObject<Friend>("{\"player_id\":\"2\",\"display_name\":\"Bob\"}");

            Assert.That(friend, Is.EqualTo(renamed));
            Assert.That(friend, Is.Not.EqualTo(somebodyElse));
            Assert.That(friend.GetHashCode(), Is.EqualTo(renamed.GetHashCode()));
        }

        [Test]
        public void LastOnlineAt_IsReadAsATime_WhenTheFriendIsOffline()
        {
            var friend = CoherenceJson.DeserializeObject<Friend>(
                "{\"player_id\":\"1\",\"online\":false,\"last_online_at\":1754330000}");

            Assert.That(friend.LastOnlineAt,
                Is.EqualTo(DateTimeOffset.FromUnixTimeSeconds(1754330000)));
        }
    }
}
