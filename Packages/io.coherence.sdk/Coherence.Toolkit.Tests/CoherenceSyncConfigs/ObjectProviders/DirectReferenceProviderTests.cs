// Copyright (c) coherence ApS.
// See the license file in the project root for more information.

namespace Coherence.Toolkit.Tests
{
    using Coherence.Tests;
    using NUnit.Framework;
    using UnityEngine;
    using Object = UnityEngine.Object;

    /// <summary>
    /// Edit mode unit tests for <see cref="DirectReferenceProvider"/>.
    /// </summary>
    public class DirectReferenceProviderTests : CoherenceTest
    {
        private DirectReferenceProvider provider;
        private CoherenceSyncConfig config;
        private MockPrefab mockPrefab;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();

            provider = new DirectReferenceProvider();
            mockPrefab = MockPrefab.New();
            config = ScriptableObject.CreateInstance<CoherenceSyncConfig>();
            config.EditorTarget = mockPrefab.sync.gameObject;
        }

        [TearDown]
        public override void TearDown()
        {
            Object.DestroyImmediate(config);
            Object.DestroyImmediate(mockPrefab.sync.gameObject);

            base.TearDown();
        }

        [Test]
        [Description("LoadAsset (sync overload) returns the ICoherenceSync of the initialized prefab.")]
        public void LoadAsset_ReturnsInitializedPrefabSync_ForSyncOverload()
        {
            provider.Initialize(config);

            var sync = provider.LoadAsset("any-id");

            Assert.That(sync, Is.SameAs(mockPrefab.sync));
        }

        [Test]
        [Description("LoadAsset (callback overload) invokes the callback with the initialized prefab's sync.")]
        public void LoadAsset_InvokesCallbackWithInitializedPrefabSync_ForCallbackOverload()
        {
            provider.Initialize(config);

            ICoherenceSync loaded = null;
            provider.LoadAsset("any-id", sync => loaded = sync);

            Assert.That(loaded, Is.SameAs(mockPrefab.sync));
        }

        [Test]
        [Description("Validate returns true while the config's EditorTarget still matches the initialized prefab.")]
        public void Validate_ReturnsTrue_WhenEditorTargetStillMatchesInitializedPrefab()
        {
            provider.Initialize(config);

            Assert.That(provider.Validate(config), Is.True);
        }

        [Test]
        [Description("Validate returns false once the config's EditorTarget changes to a different prefab.")]
        public void Validate_ReturnsFalse_WhenEditorTargetChangedSinceInitialize()
        {
            provider.Initialize(config);

            var otherPrefab = MockPrefab.New();
            config.EditorTarget = otherPrefab.sync.gameObject;

            try
            {
                Assert.That(provider.Validate(config), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(otherPrefab.sync.gameObject);
            }
        }

        [Test]
        [Description("Release and OnApplicationQuit are no-ops and never throw.")]
        public void Release_And_OnApplicationQuit_DoNotThrow()
        {
            provider.Initialize(config);

            Assert.DoesNotThrow(() => provider.Release(mockPrefab.sync));
            Assert.DoesNotThrow(() => provider.OnApplicationQuit());
        }
    }
}
