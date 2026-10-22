// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Runtime.Tests
{
    using Coherence.Cloud;
    using Coherence.Tests;
    using NUnit.Framework;

    /// <summary>
    /// Unit tests for <see cref="PlayerAccountId"/>.
    /// </summary>
    public sealed class PlayerAccountIdTests : CoherenceTest
    {
        [Test]
        public void Constructor_With_Null_Value_Equals_None()
        {
            var id = new PlayerAccountId(null);

            Assert.That(id.Equals(PlayerAccountId.None), Is.True);
            Assert.That(id == PlayerAccountId.None, Is.True);
        }

        [Test]
        public void Constructor_With_Null_Value_ToString_Returns_Empty_String()
        {
            var id = new PlayerAccountId(null);

            Assert.That(id.ToString(), Is.EqualTo(""));
        }

        [Test, Description("A default-initialized PlayerAccountId (e.g. a dictionary lookup miss, or an " +
            "uninitialized field) never goes through the constructor, so its backing field is null rather " +
            "than the constructor's null-coalesced empty string. Equals treats it the same as None regardless.")]
        public void Default_Equals_None()
        {
            var id = default(PlayerAccountId);

            Assert.That(id.Equals(PlayerAccountId.None), Is.True);
        }

        [Test, Description("GetHashCode must agree with Equals: since default(PlayerAccountId) equals None " +
            "(see Default_Equals_None), they must also hash the same.")]
        public void Default_GetHashCode_Equals_None_GetHashCode()
        {
            var id = default(PlayerAccountId);

            Assert.That(id.GetHashCode(), Is.EqualTo(PlayerAccountId.None.GetHashCode()));
        }

        [Test]
        public void Serialize_Then_Deserialize_Round_Trips_To_An_Equal_Id()
        {
            var original = new PlayerAccountId("some-player-id");

            var serialized = PlayerAccountId.Serialize(original);
            var deserialized = PlayerAccountId.Deserialize(serialized);

            Assert.That(deserialized.Equals(original), Is.True);
        }

        [Test]
        public void Deserialize_Null_Returns_None()
        {
            Assert.That(PlayerAccountId.Deserialize(null).Equals(PlayerAccountId.None), Is.True);
        }

        [Test]
        public void Deserialize_Empty_String_Returns_None()
        {
            Assert.That(PlayerAccountId.Deserialize("").Equals(PlayerAccountId.None), Is.True);
        }

        [Test]
        public void Implicit_Conversion_To_And_From_String_Preserves_Value()
        {
            PlayerAccountId id = "some-player-id"; // implicit string -> PlayerAccountId
            string value = id; // implicit PlayerAccountId -> string

            Assert.That(value, Is.EqualTo("some-player-id"));
            Assert.That(id.Equals(new PlayerAccountId("some-player-id")), Is.True);
        }
    }
}
