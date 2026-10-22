// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Runtime.Tests
{
    using System.Collections.Generic;
    using Coherence.Cloud;
    using Coherence.Tests;
    using NUnit.Framework;

    public class AttributesExtensionsTests : CoherenceTest
    {
        private static CloudAttribute Attr(string key, string value) => new CloudAttribute(key, value);

        [Test]
        [Description("When the new attributes list is empty, lobby attributes remain unchanged.")]
        public void MergeAttributesWith_WhenNewAttributesIsEmpty_LobbyAttributesUnchanged()
        {
            // Arrange
            var existing = Attr("color", "red");
            var lobby = new LobbyData { lobbyAttributes = new List<CloudAttribute> { existing } };

            // Act
            lobby.MergeAttributesWith(new List<CloudAttribute>());

            // Assert
            Assert.That(lobby.Attributes, Is.EquivalentTo(new[] { existing }));
        }

        [Test]
        [Description("When the lobby has no existing attributes, all new attributes are appended.")]
        public void MergeAttributesWith_WhenLobbyAttributesIsEmpty_AppendsAllNewAttributes()
        {
            // Arrange
            var lobby = new LobbyData { lobbyAttributes = new List<CloudAttribute>() };
            var newAttrs = new List<CloudAttribute> { Attr("score", "100"), Attr("level", "5") };

            // Act
            lobby.MergeAttributesWith(newAttrs);

            // Assert
            Assert.That(lobby.Attributes, Has.Count.EqualTo(2));
        }

        [Test]
        [Description("When a new attribute key matches an existing one, the existing attribute is updated.")]
        public void MergeAttributesWith_WhenKeyMatches_UpdatesExistingAttribute()
        {
            // Arrange
            var lobby = new LobbyData { lobbyAttributes = new List<CloudAttribute> { Attr("color", "red") } };
            var updated = Attr("color", "blue");

            // Act
            lobby.MergeAttributesWith(new List<CloudAttribute> { updated });

            // Assert
            Assert.That(lobby.Attributes[0], Is.EqualTo(updated));
        }

        [Test]
        [Description("When no new attribute key matches an existing one, new attributes are appended.")]
        public void MergeAttributesWith_WhenNoKeyMatches_AppendsNewAttribute()
        {
            // Arrange
            var existing = Attr("color", "red");
            var lobby = new LobbyData { lobbyAttributes = new List<CloudAttribute> { existing } };
            var newAttr = Attr("size", "large");

            // Act
            lobby.MergeAttributesWith(new List<CloudAttribute> { newAttr });

            // Assert
            Assert.That(lobby.Attributes, Is.EquivalentTo(new[] { existing, newAttr }));
        }

        [Test]
        [Description("When some new attribute keys match and some are new, matching ones are updated and new ones are appended.")]
        public void MergeAttributesWith_WithMixedAttributes_UpdatesMatchingAndAppendsNew()
        {
            // Arrange
            var mode = Attr("mode", "deathmatch");
            var lobby = new LobbyData
            {
                lobbyAttributes = new List<CloudAttribute>
                {
                    Attr("color", "red"),
                    mode,
                }
            };
            var updated = Attr("color", "blue");
            var added = Attr("difficulty", "hard");

            // Act
            lobby.MergeAttributesWith(new List<CloudAttribute> { updated, added });

            // Assert
            Assert.That(lobby.Attributes, Has.Count.EqualTo(3));
            Assert.That(lobby.Attributes, Contains.Item(updated));
            Assert.That(lobby.Attributes, Contains.Item(added));
            Assert.That(lobby.Attributes, Contains.Item(mode));
        }

        [Test]
        [Description("When a player attribute key matches a new attribute, the existing attribute is updated.")]
        public void MergeAttributesWith_WhenPlayerHasMatchingAttribute_UpdatesIt()
        {
            // Arrange
            var original = Attr("rank", "bronze");
            var player = new LobbyPlayer("player1", "Alice", new List<CloudAttribute> { original });
            var updated = Attr("rank", "gold");

            // Act
            player.MergeAttributesWith(new List<CloudAttribute> { updated });

            // Assert
            Assert.That(player.Attributes[0], Is.EqualTo(updated));
        }

        [Test]
        [Description("When no player attribute key matches a new attribute, the new attribute is appended.")]
        public void MergeAttributesWith_WhenPlayerHasNoMatchingAttribute_AppendsNewAttribute()
        {
            // Arrange
            var existing = Attr("rank", "bronze");
            var player = new LobbyPlayer("player1", "Alice", new List<CloudAttribute> { existing });
            var newAttr = Attr("kills", "42");

            // Act
            player.MergeAttributesWith(new List<CloudAttribute> { newAttr });

            // Assert
            Assert.That(player.Attributes, Is.EquivalentTo(new[] { existing, newAttr }));
        }

        [Test]
        [Description("When the player has no existing attributes, all new attributes are appended.")]
        public void MergeAttributesWith_WhenPlayerAttributesIsEmpty_AppendsAllNewAttributes()
        {
            var player = new LobbyPlayer("player1", "Alice", attributes: new());

            player.MergeAttributesWith(newAttributes: new() { Attr("rank", "gold"), Attr("kills", "42") });

            Assert.That(player.Attributes, Is.EquivalentTo(new[] { Attr("rank", "gold"), Attr("kills", "42") }));
        }

        [Test]
        [Description("When some player attribute keys match and some are new, matching ones are updated and new ones are appended.")]
        public void MergeAttributesWith_WithMixedPlayerAttributes_UpdatesMatchingAndAppendsNew()
        {
            var player = new LobbyPlayer("player1", "Alice", new() { Attr("rank", "bronze"), Attr("kills", "10") });

            player.MergeAttributesWith(new() { Attr("rank", "gold"), Attr("assists", "5") });

            Assert.That(player.Attributes, Is.EquivalentTo(new[] { Attr("rank", "gold"), Attr("kills", "10"), Attr("assists", "5") }));
        }

        [Test]
        [Description("When the new attributes list is empty, player attributes remain unchanged.")]
        public void MergeAttributesWith_WhenNewAttributesIsEmpty_PlayerAttributesUnchanged()
        {
            // Arrange
            var existing = Attr("rank", "bronze");
            var player = new LobbyPlayer("player1", "Alice", new List<CloudAttribute> { existing });

            // Act
            player.MergeAttributesWith(new List<CloudAttribute>());

            // Assert
            Assert.That(player.Attributes, Is.EquivalentTo(new[] { existing }));
        }
    }
}
