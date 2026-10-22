// Copyright (c) coherence ApS.
// See the license file in the package root for more information.
namespace Coherence.Runtime.Tests
{
    using Coherence.Cloud;
    using Coherence.Tests;
    using NUnit.Framework;

    public class GuestIdTests : CoherenceTest
    {
        private const string ProjectId = "ProjectId";
        private CloudUniqueId CloudUniqueId => new("CloudUniqueId");

        [Test]
        [Description("A freshly generated guest id must be flushed to persistent storage immediately, " +
                     "so that it survives across sessions even on platforms that don't reliably flush prefs at shutdown (e.g. WebGL). ")]
        public void GetOrCreate_Flushes_Prefs_When_Generating_New_GuestId()
        {
            using var mock = new MockPrefsImplementationBuilder().Inject();

            _ = GuestId.GetOrCreate(ProjectId, CloudUniqueId);

            Assert.That(mock.SetStringWasCalled, Is.True, "Expected the generated guest id to be written to prefs.");
            Assert.That(mock.SaveWasCalled, Is.True, "Expected prefs to be flushed after generating a new guest id.");
        }

        [Test]
        [Description("Reading an already cached guest id must not generate or persist a new one.")]
        public void GetOrCreate_Returns_Cached_GuestId_Without_Generating_A_New_One()
        {
            const string cachedGuestId = "cachedGuestId";
            using var mock = new MockPrefsImplementationBuilder().GetStringReturns(cachedGuestId).Inject();

            var guestId = GuestId.GetOrCreate(ProjectId, CloudUniqueId);

            Assert.That((string)guestId, Is.EqualTo(cachedGuestId));
            Assert.That(mock.SetStringWasCalled, Is.False, "Expected the cached guest id to be reused as-is.");
        }

        [Test]
        [Description("Saving a guest id must flush it to persistent storage immediately.")]
        public void Save_Flushes_Prefs()
        {
            using var mock = new MockPrefsImplementationBuilder().Inject();

            GuestId.Save(ProjectId, CloudUniqueId, new GuestId("someGuestId"));

            Assert.That(mock.SetStringWasCalled, Is.True);
            Assert.That(mock.SaveWasCalled, Is.True, "Expected prefs to be flushed after saving a guest id.");
        }

        [Test]
        [Description("Deleting a guest id must flush the deletion to persistent storage immediately.")]
        public void Delete_Flushes_Prefs()
        {
            using var mock = new MockPrefsImplementationBuilder().Inject();

            GuestId.Delete(ProjectId, CloudUniqueId);

            Assert.That(mock.SaveWasCalled, Is.True, "Expected prefs to be flushed after deleting a guest id.");
        }

        [Test]
        public void Constructor_With_Null_Value_Equals_None()
        {
            var id = new GuestId(null);

            Assert.That(id.Equals(GuestId.None), Is.True);
        }

        [Test]
        public void Constructor_With_Null_Value_ToString_Returns_Empty_String()
        {
            var id = new GuestId(null);

            Assert.That(id.ToString(), Is.EqualTo(""));
        }

        [Test, Description("A default-initialized GuestId (e.g. a dictionary lookup miss, or an " +
            "uninitialized field) never goes through the constructor, so its backing field is null rather " +
            "than the constructor's null-coalesced empty string. Equals treats it the same as None regardless.")]
        public void Default_Equals_None()
        {
            var id = default(GuestId);

            Assert.That(id.Equals(GuestId.None), Is.True);
        }

        [Test, Description("GetHashCode must agree with Equals: since default(GuestId) equals None " +
            "(see Default_Equals_None), they must also hash the same.")]
        public void Default_GetHashCode_Equals_None_GetHashCode()
        {
            var id = default(GuestId);

            Assert.That(id.GetHashCode(), Is.EqualTo(GuestId.None.GetHashCode()));
        }

        [Test]
        public void Serialize_Then_Deserialize_Round_Trips_To_An_Equal_Id()
        {
            var original = new GuestId("some-guest-id");

            var serialized = GuestId.Serialize(original);
            var deserialized = GuestId.Deserialize(serialized);

            Assert.That(deserialized.Equals(original), Is.True);
        }

        [Test]
        public void Deserialize_Null_Returns_None()
        {
            Assert.That(GuestId.Deserialize(null).Equals(GuestId.None), Is.True);
        }

        [Test]
        public void Deserialize_Empty_String_Returns_None()
        {
            Assert.That(GuestId.Deserialize("").Equals(GuestId.None), Is.True);
        }

        [Test]
        public void Implicit_Conversion_To_And_From_String_Preserves_Value()
        {
            GuestId id = "some-guest-id"; // implicit string -> GuestId
            string value = id; // implicit GuestId -> string

            Assert.That(value, Is.EqualTo("some-guest-id"));
            Assert.That(id.Equals(new GuestId("some-guest-id")), Is.True);
        }
    }
}
