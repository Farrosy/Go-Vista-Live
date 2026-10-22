// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Toolkit.Tests
{
    using System;
    using Coherence.Tests;
    using NUnit.Framework;
    using UnityEngine;
    using Object = UnityEngine.Object;

    /// <summary>
    /// Edit mode unit tests for <see cref="NetworkPool"/>.
    /// </summary>
    public class NetworkPoolTests : CoherenceTest
    {
        private const int InitialSize = 3;

        private NetworkPool pool;
        private GameObject prefab;
        private CoherenceSync prefabSync;
        private GameObject container;
        private CoherenceSyncConfig config;
        private TestableBridgeBuilder bridgeBuilder;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();

            prefab = new($"{nameof(NetworkPoolTests)}_Prefab", typeof(CoherenceSync));
            prefab.SetActive(false);
            prefabSync = prefab.GetComponent<CoherenceSync>();

            config = ScriptableObject.CreateInstance<CoherenceSyncConfig>();
            config.name = $"{nameof(NetworkPoolTests)}_Config";
            container = new($"{nameof(NetworkPoolTests)}_Container");
            pool = new(InitialSize, container, Quaternion.identity);
        }

        [TearDown]
        public override void TearDown()
        {
            bridgeBuilder?.Dispose();

            if (container)
            {
                // Destroying the container also destroys every warmed up instance in the pool.
                Object.DestroyImmediate(container);
            }

            if (prefab)
            {
                Object.DestroyImmediate(prefab);
            }

            if (config)
            {
                Object.DestroyImmediate(config);
            }

            base.TearDown();
        }

        [Test]
        [Description("Regression test: WarmUpInstantiator must not throw a NullReferenceException when the " +
                     "CoherenceBridge is destroyed while the asset is still loading, and must still warm the " +
                     "pool up to initialSize.")]
        public void WarmUpInstantiator_WhenBridgeDestroyedDuringLoad_DoesNotThrow_AndStillWarmsPool()
        {
            var bridge = new GameObject($"{nameof(NetworkPoolTests)}_Bridge").AddComponent<CoherenceBridge>();
            var loader = new FakeNetworkObjectProvider();
            pool.WarmUpInstantiator(bridge, config, loader);
            Assert.That(pool.CountAll, Is.Zero, "Pool should be empty before the asset completes loading.");
            Assert.That(loader.LoadAssetInProgress, Is.True, "WarmUpInstantiator should have requested the asset.");
            Object.DestroyImmediate(bridge.gameObject); // <- Destroy the bridge before the asset finishes loading.

            Assert.DoesNotThrow(() => loader.CompleteLoadAsset(prefabSync));

            Assert.That(pool.CountAll, Is.EqualTo(InitialSize));
        }

        [Test]
        [Description("Sanity check for the happy path: with a live, initialized bridge the same flow warms " +
                     "the pool up to initialSize.")]
        public void WarmUpInstantiator_WhenBridgeAliveDuringLoad_WarmsPool()
        {
            bridgeBuilder = new();
            bridgeBuilder.SetupClient(client => client.SetIsConnected(false));
            var bridge = bridgeBuilder.Build();
            var loader = new FakeNetworkObjectProvider();
            pool.WarmUpInstantiator(bridge, config, loader);

            Assert.DoesNotThrow(() => loader.CompleteLoadAsset(prefabSync));

            Assert.That(pool.CountAll, Is.EqualTo(InitialSize));
        }

        private sealed class FakeNetworkObjectProvider : INetworkObjectProvider
        {
            private Action<ICoherenceSync> onLoaded;

            public bool LoadAssetInProgress => onLoaded != null;

            public void CompleteLoadAsset(ICoherenceSync loadedPrefab)
            {
                var callback = onLoaded;
                onLoaded = null;
                callback?.Invoke(loadedPrefab);
            }

            public void LoadAsset(string networkAssetId, Action<ICoherenceSync> onLoaded) => this.onLoaded = onLoaded;
            public ICoherenceSync LoadAsset(string networkAssetId) => null;
            public void Release(ICoherenceSync obj) { }
            public void OnApplicationQuit() { }
            public void Initialize(CoherenceSyncConfig entry) { }
            public bool Validate(CoherenceSyncConfig entry) => true;
        }
    }
}
