namespace Coherence.Toolkit.Tests
{
    using System;
    using System.Collections.Generic;
    using Coherence.Tests;
    using NUnit.Framework;
    using UnityEditor;
    using UnityEngine;
    using Object = UnityEngine.Object;

    /// <summary>
    /// Edit mode unit tests for <see cref="CoherenceSyncConfigRegistry"/>.
    /// </summary>
    public class CoherenceSyncConfigRegistryTests : CoherenceTest
    {
        private const string DefaultPrefabName = nameof(CoherenceSyncConfigRegistryTests);
        private const string DefaultPrefabPath = "Assets/" + DefaultPrefabName + ".prefab";
        private string prefabPath;
        private GameObject prefab;
        private CoherenceSync sync;
        private CoherenceSyncConfigRegistry registry;
        private readonly List<CoherenceSyncConfig> testConfigs = new();

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();

            prefabPath = AssetDatabase.GenerateUniqueAssetPath(DefaultPrefabPath);
            prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(
                ObjectFactory.CreateGameObject(DefaultPrefabName, typeof(CoherenceSync)),
                prefabPath, InteractionMode.AutomatedAction);
            sync = prefab.GetComponent<CoherenceSync>();
            registry = CoherenceSyncConfigRegistry.Instance;
            registry.ResetState();
        }

        [TearDown]
        public override void TearDown()
        {
            AssetDatabase.DeleteAsset(prefabPath);
            registry.ResetState();

            foreach (var config in testConfigs)
            {
                if (config)
                {
                    Object.DestroyImmediate(config);
                }
            }

            testConfigs.Clear();

            base.TearDown();
        }

        // Creates an in-memory CoherenceSyncConfig with a unique ID. Not an asset — won't
        // survive registry.ResetState(), so it's safe to leave deregistration to TearDown.
        private CoherenceSyncConfig CreateConfig()
        {
            var config = ScriptableObject.CreateInstance<CoherenceSyncConfig>();
            using var so = new SerializedObject(config);
            using var idProp = so.FindProperty(CoherenceSyncConfig.Property.id);
            idProp.stringValue = Guid.NewGuid().ToString();
            so.ApplyModifiedPropertiesWithoutUndo();
            testConfigs.Add(config);
            return config;
        }

        [Test]
        [Description("Verifies that created CoherenceSyncConfig objects are registered using the ID.GetHashCode().")]
        public void TestConfigHashCodeUsed()
        {
            // Arrange / Act - create a CoherenceSyncPrefab which automatically registers.
            var config = sync.CoherenceSyncConfig;
            var networkID = config.GetNetworkAssetId();

            // Assert
            Assert.True(registry.GetFromNetworkId(networkID, out var testConfig));
            Assert.That(testConfig, Is.EqualTo(config));
        }

        [Test]
        [Description("Verifies that deleted configs are removed from the registry network IDs.")]
        public void TestDestroyedConfigsRemovedFromNetworkID()
        {
            // Arrange / Act - create a CoherenceSyncPrefab which automatically registers.
            var config = sync.CoherenceSyncConfig;
            var networkID = config.GetNetworkAssetId();

            // Act
            AssetDatabase.DeleteAsset(prefabPath);

            // Assert
            Assert.False(registry.GetFromNetworkId(networkID, out var testConfig));
        }

        [Test]
        public void CleanUp_Is_Idempotent()
        {
            var config = ScriptableObject.CreateInstance<CoherenceSyncConfig>();
            var instantiator = new FakeInstantiator();
            using var serializedObject = new SerializedObject(config);

            using var instantiatorProperty = serializedObject.FindProperty(CoherenceSyncConfig.Property.objectInstantiator);
            instantiatorProperty.managedReferenceValue = instantiator;

            using var idProperty = serializedObject.FindProperty(CoherenceSyncConfig.Property.id);
            idProperty.stringValue = Guid.NewGuid().ToString();

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            registry.Register(config);

            registry.CleanUp();
            registry.CleanUp();
            registry.CleanUp();

            Assert.That(instantiator.OnApplicationQuitCallCount, Is.EqualTo(1));

            registry.Deregister(config);
            Object.DestroyImmediate(config);
        }

        [Test]
        [Description("Registering a null config returns false.")]
        public void Register_WhenConfigIsNull_ReturnsFalse()
            => Assert.That(registry.Register(null), Is.False);

        [Test]
        [Description("Registering a config with an empty ID returns false.")]
        public void Register_WhenConfigIdIsEmpty_ReturnsFalse()
        {
            var config = ScriptableObject.CreateInstance<CoherenceSyncConfig>();
            testConfigs.Add(config);

            Assert.That(registry.Register(config), Is.False);
        }

        [Test]
        [Description("Registering the same config a second time returns false.")]
        public void Register_WhenAlreadyRegistered_ReturnsFalse()
        {
            var config = CreateConfig();
            registry.Register(config);

            Assert.That(registry.Register(config), Is.False);
        }

        [Test]
        [Description("Registering a valid config returns true.")]
        public void Register_WhenValid_ReturnsTrue()
        {
            var config = CreateConfig();

            Assert.That(registry.Register(config), Is.True);
        }

        [Test]
        [Description("Registering a valid config increments Count by one.")]
        public void Register_WhenValid_IncrementsCount()
        {
            var countBefore = registry.Count;
            var config = CreateConfig();

            registry.Register(config);

            Assert.That(registry.Count, Is.EqualTo(countBefore + 1));
        }

        [Test]
        [Description("Deregistering a null config returns false.")]
        public void Deregister_WhenConfigIsNull_ReturnsFalse()
            => Assert.That(registry.Deregister((CoherenceSyncConfig)null), Is.False);

        [Test]
        [Description("Deregistering with an empty GUID returns false.")]
        public void Deregister_WhenGuidIsEmpty_ReturnsFalse()
            => Assert.That(registry.Deregister(string.Empty), Is.False);

        [Test]
        [Description("Deregistering a config that was never registered returns false.")]
        public void Deregister_WhenNotRegistered_ReturnsFalse()
        {
            var config = CreateConfig();

            Assert.That(registry.Deregister(config), Is.False);
        }

        [Test]
        [Description("Deregistering a registered config returns true.")]
        public void Deregister_WhenRegistered_ReturnsTrue()
        {
            var config = CreateConfig();
            registry.Register(config);

            Assert.That(registry.Deregister(config), Is.True);
        }

        [Test]
        [Description("Deregistering a registered config decrements Count by one.")]
        public void Deregister_WhenRegistered_DecrementsCount()
        {
            var config = CreateConfig();
            registry.Register(config);
            var countAfterRegister = registry.Count;

            registry.Deregister(config);

            Assert.That(registry.Count, Is.EqualTo(countAfterRegister - 1));
        }

        [Test]
        [Description("After deregistering a config it can no longer be found by network ID.")]
        public void Deregister_WhenRegistered_ConfigNotRetrievableByNetworkId()
        {
            var config = CreateConfig();
            registry.Register(config);
            var networkId = config.GetNetworkAssetId();

            registry.Deregister(config);

            Assert.That(registry.GetFromNetworkId(networkId, out _), Is.False);
        }

        [Test]
        [Description("After deregistering a config it can no longer be found by asset ID.")]
        public void Deregister_WhenRegistered_ConfigNotRetrievableByAssetId()
        {
            var config = CreateConfig();
            registry.Register(config);
            var assetId = config.ID;

            registry.Deregister(config);

            Assert.That(registry.TryGetFromAssetId(assetId, out _), Is.False);
        }

        [Test]
        [Description("GetAt returns the config appended at the known index.")]
        public void GetAt_WhenRegistered_ReturnsConfig()
        {
            var config = CreateConfig();
            var indexAfterRegister = registry.Count;
            registry.Register(config);

            Assert.That(registry.GetAt(indexAfterRegister), Is.EqualTo(config));
        }

        [Test]
        [Description("TryGetFromAssetId returns true and the config when it is registered.")]
        public void TryGetFromAssetId_WhenRegistered_ReturnsTrueWithConfig()
        {
            var config = CreateConfig();
            registry.Register(config);

            var found = registry.TryGetFromAssetId(config.ID, out var result);

            Assert.That(found, Is.True);
            Assert.That(result, Is.EqualTo(config));
        }

        [Test]
        [Description("TryGetFromAssetId returns false when the asset ID is not registered.")]
        public void TryGetFromAssetId_WhenNotRegistered_ReturnsFalse()
            => Assert.That(registry.TryGetFromAssetId(Guid.NewGuid().ToString(), out _), Is.False);

        [Test]
        [Description("GetReferencedPrefabs skips configs whose networked prefab has been destroyed " +
                     "without the config getting deregistered (e.g. generated prefabs deleted mid-session). " +
                     "A destroyed GameObject still passes the 'is GameObject' pattern match, so without " +
                     "an explicit liveness check TryGetComponent throws MissingReferenceException. See #7144.")]
        public void GetReferencedPrefabs_WhenEditorTargetDestroyed_SkipsConfig()
        {
            var config = CreateConfig();
            var target = ObjectFactory.CreateGameObject(nameof(GetReferencedPrefabs_WhenEditorTargetDestroyed_SkipsConfig));
            config.EditorTarget = target;
            Assert.That(registry.Register(config), Is.True);

            // Destroy the target directly, leaving the config registered with a destroyed EditorTarget.
            Object.DestroyImmediate(target);

            var results = new List<CoherenceSync>();
            Assert.That(() => registry.GetReferencedPrefabs(results), Throws.Nothing);
            Assert.That(results, Has.All.Matches<CoherenceSync>(resultSync => (bool)resultSync));
        }

        private class FakeInstantiator : INetworkObjectInstantiator
        {
            public int OnApplicationQuitCallCount { get; private set; }

            public void OnUniqueObjectReplaced(ICoherenceSync instance) { }
            public ICoherenceSync Instantiate(SpawnInfo spawnInfo) => null;
            public void Destroy(ICoherenceSync obj) { }
            public void WarmUpInstantiator(CoherenceBridge bridge, CoherenceSyncConfig config, INetworkObjectProvider assetLoader) { }
            public void OnApplicationQuit() => OnApplicationQuitCallCount++;
        }
    }
}
