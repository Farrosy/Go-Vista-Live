// Copyright (c) coherence ApS.
// See the license file in the project root for more information.

namespace Coherence.Toolkit.Tests
{
    using System;
    using System.Collections.Generic;
    using Coherence.Tests;
    using NUnit.Framework;
    using UnityEngine;
    using Object = UnityEngine.Object;

    /// <summary>
    /// Edit mode unit tests for <see cref="AssetDatabaseProvider"/>.
    /// </summary>
    public class AssetDatabaseProviderTests : CoherenceTest
    {
        private CoherenceSyncConfigRegistry registry;
        private AssetDatabaseProvider provider;
        private readonly List<CoherenceSyncConfig> testConfigs = new();
        private readonly List<GameObject> testGameObjects = new();

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();

            registry = CoherenceSyncConfigRegistry.Instance;
            registry.ResetState();

            provider = new AssetDatabaseProvider();
        }

        [TearDown]
        public override void TearDown()
        {
            registry.ResetState();

            foreach (var config in testConfigs)
            {
                if (config)
                {
                    Object.DestroyImmediate(config);
                }
            }

            testConfigs.Clear();

            foreach (var gameObject in testGameObjects)
            {
                if (gameObject)
                {
                    Object.DestroyImmediate(gameObject);
                }
            }

            testGameObjects.Clear();

            base.TearDown();
        }

        // Registers a CoherenceSyncConfig under a unique id, targeting editorTarget.
        private CoherenceSyncConfig CreateRegisteredConfig(GameObject editorTarget)
        {
            var config = ScriptableObject.CreateInstance<CoherenceSyncConfig>();
            config.Init(Guid.NewGuid().ToString());
            config.EditorTarget = editorTarget;
            testConfigs.Add(config);

            Assert.That(registry.Register(config), Is.True);

            return config;
        }

        [Test]
        [Description("LoadAsset (sync overload) always returns null, regardless of the registry's contents.")]
        public void LoadAsset_ReturnsNull_ForSyncOverload()
        {
            Assert.That(provider.LoadAsset(Guid.NewGuid().ToString()), Is.Null);
        }

        [Test]
        [Description("LoadAsset (callback overload) invokes the callback with the registered config target's sync.")]
        public void LoadAsset_InvokesCallbackWithSync_WhenRegisteredConfigTargetsPrefabWithSync()
        {
            var mockPrefab = MockPrefab.New();
            testGameObjects.Add(mockPrefab.sync.gameObject);
            var config = CreateRegisteredConfig(mockPrefab.sync.gameObject);

            ICoherenceSync loaded = null;
            provider.LoadAsset(config.ID, sync => loaded = sync);

            Assert.That(loaded, Is.SameAs(mockPrefab.sync));
        }

        [Test]
        [Description("LoadAsset (callback overload) does not invoke the callback when no config is registered.")]
        public void LoadAsset_DoesNotInvokeCallback_WhenNoConfigRegisteredForId()
        {
            var invoked = false;

            provider.LoadAsset(Guid.NewGuid().ToString(), _ => invoked = true);

            Assert.That(invoked, Is.False);
        }

        [Test]
        [Description("LoadAsset (callback overload) does not invoke the callback when the target has no sync.")]
        public void LoadAsset_DoesNotInvokeCallback_WhenRegisteredConfigTargetHasNoSyncComponent()
        {
            var plainGameObject = new GameObject();
            testGameObjects.Add(plainGameObject);
            var config = CreateRegisteredConfig(plainGameObject);

            var invoked = false;
            provider.LoadAsset(config.ID, _ => invoked = true);

            Assert.That(invoked, Is.False);
        }

        [Test]
        [Description("Validate always returns true, regardless of the config passed to it.")]
        public void Validate_ReturnsTrue_Always()
        {
            var config = ScriptableObject.CreateInstance<CoherenceSyncConfig>();
            testConfigs.Add(config);

            Assert.That(provider.Validate(config), Is.True);
        }

        [Test]
        [Description("Initialize, Release, and OnApplicationQuit are no-ops and never throw.")]
        public void Initialize_And_Release_And_OnApplicationQuit_DoNotThrow()
        {
            var mockPrefab = MockPrefab.New();
            testGameObjects.Add(mockPrefab.sync.gameObject);
            var config = CreateRegisteredConfig(mockPrefab.sync.gameObject);

            Assert.DoesNotThrow(() => provider.Initialize(config));
            Assert.DoesNotThrow(() => provider.Release(mockPrefab.sync));
            Assert.DoesNotThrow(() => provider.OnApplicationQuit());
        }
    }
}
