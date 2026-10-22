// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Runtime.Tests
{
    using Coherence.Cloud;
    using Coherence.Tests;
    using NUnit.Framework;

    /// <summary>
    /// Unit tests for <see cref="CloudUniqueId"/>.
    /// </summary>
    public sealed class CloudUniqueIdTests : CoherenceTest
    {
        [Test]
        public void Constructor_With_Null_Value_Equals_None()
        {
            var id = new CloudUniqueId(null);

            Assert.That(id.Equals(CloudUniqueId.None), Is.True);
            Assert.That(id == CloudUniqueId.None, Is.True);
        }

        [Test]
        public void Constructor_With_Null_Value_ToString_Returns_Empty_String()
        {
            var id = new CloudUniqueId(null);

            Assert.That(id.ToString(), Is.EqualTo(""));
        }

        [Test, Description("A default-initialized CloudUniqueId (e.g. a dictionary lookup miss, or an " +
            "uninitialized field) never goes through the constructor, so its backing field is null rather " +
            "than the constructor's null-coalesced empty string. Equals treats it the same as None regardless.")]
        public void Default_Equals_None()
        {
            var id = default(CloudUniqueId);

            Assert.That(id.Equals(CloudUniqueId.None), Is.True);
        }

        [Test, Description("GetHashCode must agree with Equals: since default(CloudUniqueId) equals None " +
            "(see Default_Equals_None), they must also hash the same.")]
        public void Default_GetHashCode_Equals_None_GetHashCode()
        {
            var id = default(CloudUniqueId);

            Assert.That(id.GetHashCode(), Is.EqualTo(CloudUniqueId.None.GetHashCode()));
        }

        [Test]
        public void Serialize_Then_Deserialize_Round_Trips_To_An_Equal_Id()
        {
            var original = new CloudUniqueId("some-cloud-unique-id");

            var serialized = CloudUniqueId.Serialize(original);
            var deserialized = CloudUniqueId.Deserialize(serialized);

            Assert.That(deserialized.Equals(original), Is.True);
        }

        [Test]
        public void Deserialize_Null_Returns_None()
        {
            Assert.That(CloudUniqueId.Deserialize(null).Equals(CloudUniqueId.None), Is.True);
        }

        [Test]
        public void Deserialize_Empty_String_Returns_None()
        {
            Assert.That(CloudUniqueId.Deserialize("").Equals(CloudUniqueId.None), Is.True);
        }

        [Test]
        public void Implicit_Conversion_To_And_From_String_Preserves_Value()
        {
            CloudUniqueId id = "some-cloud-unique-id"; // implicit string -> CloudUniqueId
            string value = id; // implicit CloudUniqueId -> string

            Assert.That(value, Is.EqualTo("some-cloud-unique-id"));
            Assert.That(id.Equals(new CloudUniqueId("some-cloud-unique-id")), Is.True);
        }
    }
}
