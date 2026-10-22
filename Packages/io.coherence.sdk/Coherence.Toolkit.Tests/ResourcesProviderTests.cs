// Copyright (c) coherence ApS.
// See the license file in the project root for more information.

namespace Coherence.Toolkit.Tests
{
    using System.Reflection;
    using Coherence.Tests;
    using Moq;
    using NUnit.Framework;

    public class ResourcesProviderTests : CoherenceTest
    {

        // Seeds syncObject via reflection so LoadAsset won't call Resources.Load.
        private static ResourcesProvider CreateProviderWithLoadedAsset()
        {
            var provider = new ResourcesProvider
            {
                syncObject = Mock.Of<ICoherenceSync>(),
            };
            return provider;
        }

        [Test]
        [Description("Resource path resolution should locate the asset on the Resources system properly.")]
        [TestCase("Assets/Resources/A/B.asset", "A/B")]
        [TestCase("Assets/Resources/Foo/Bar/A/B.prefab", "Foo/Bar/A/B")]
        [TestCase("Assets/Resources/A/Resources/B/Resources/C/Foo/Bar.prefab", "A/Resources/B/Resources/C/Foo/Bar")]
        [TestCase("Assets/Resources/A.B.C.D.prefab", "A.B.C.D")]
        [TestCase("Assets/Resources/Foo/Bar/Resources/A.B.C.D.prefab", "Foo/Bar/Resources/A.B.C.D")]
        [TestCase("Resources/A/B.prefab", "")] // not a resource
        [TestCase("Foo/Bar/A/B.prefab", "")] // not a resource
        public void GetResourcesPath_WhenResourcesPath_ReturnsValid(string input, string expected)
        {
            Assert.AreEqual(expected, ResourcesProvider.GetResourcesPath(input));
        }

        [Test]
        [Description("When the path is an empty string, returns an empty string.")]
        public void GetResourcesPath_WhenEmpty_ReturnsEmpty()
            => Assert.That(ResourcesProvider.GetResourcesPath(string.Empty), Is.Empty);

        [Test]
        [Description("Resources folder matching is case-sensitive — lowercase 'resources' is not matched.")]
        public void GetResourcesPath_WhenResourcesFolderIsLowercase_ReturnsEmpty()
            => Assert.That(ResourcesProvider.GetResourcesPath("Assets/resources/A/B.prefab"), Is.Empty);

        [Test]
        [Description("Before any assets are loaded, References is zero.")]
        public void References_BeforeAnyLoad_IsZero()
            => Assert.That(new ResourcesProvider().References, Is.Zero);

        [Test]
        [Description("LoadAsset increments References by one.")]
        public void LoadAsset_IncreasesReferences()
        {
            var provider = CreateProviderWithLoadedAsset();

            provider.LoadAsset("any");

            Assert.That(provider.References, Is.EqualTo(1));
        }

        [Test]
        [Description("Each LoadAsset call increments References by one, so three calls yield References equal to three.")]
        public void LoadAsset_CalledMultipleTimes_AccumulatesReferences()
        {
            var provider = CreateProviderWithLoadedAsset();

            provider.LoadAsset("a");
            provider.LoadAsset("b");
            provider.LoadAsset("c");

            Assert.That(provider.References, Is.EqualTo(3));
        }

        [Test]
        [Description("LoadAsset with a callback invokes the callback with the loaded sync object.")]
        public void LoadAsset_WithCallback_InvokesCallbackWithSync()
        {
            var provider = CreateProviderWithLoadedAsset();
            ICoherenceSync received = null;

            provider.LoadAsset("any", sync => received = sync);

            Assert.That(received, Is.Not.Null);
        }

        [Test]
        [Description("LoadAsset with a callback increments References by one.")]
        public void LoadAsset_WithCallback_IncreasesReferences()
        {
            var provider = CreateProviderWithLoadedAsset();

            provider.LoadAsset("any", _ => { });

            Assert.That(provider.References, Is.EqualTo(1));
        }

        [Test]
        [Description("Release decrements References by one.")]
        public void Release_DecreasesReferences()
        {
            var provider = CreateProviderWithLoadedAsset();
            var sync = provider.LoadAsset("any");

            provider.Release(sync);

            Assert.That(provider.References, Is.EqualTo(0));
        }

        [Test]
        [Description("When References reaches zero after Release, the cached sync object is cleared so the next load reloads from Resources.")]
        public void Release_WhenReferencesReachZero_ClearsCachedSyncObject()
        {
            var provider = CreateProviderWithLoadedAsset();
            var sync = provider.LoadAsset("any");

            provider.Release(sync);

            Assert.That(provider.syncObject, Is.Null);
        }

        [Test]
        [Description("When References is still above zero after Release, the cached sync object is retained.")]
        public void Release_WhenReferencesStillAboveZero_RetainsCachedSyncObject()
        {
            var provider = CreateProviderWithLoadedAsset();
            provider.LoadAsset("a");
            var sync = provider.LoadAsset("b");

            provider.Release(sync);

            Assert.That(provider.syncObject, Is.Not.Null);
        }

        [Test]
        [Description("OnApplicationQuit resets References to zero regardless of how many times LoadAsset was called.")]
        public void OnApplicationQuit_ResetsReferencesToZero()
        {
            var provider = CreateProviderWithLoadedAsset();
            provider.LoadAsset("a");
            provider.LoadAsset("b");

            provider.OnApplicationQuit();

            Assert.That(provider.References, Is.Zero);
        }

        [Test]
        [Description("OnApplicationQuit clears the cached sync object.")]
        public void OnApplicationQuit_ClearsCachedSyncObject()
        {
            var provider = CreateProviderWithLoadedAsset();
            provider.LoadAsset("any");

            provider.OnApplicationQuit();

            Assert.That(provider.syncObject, Is.Null);
        }
    }
}
