// Copyright (c) coherence ApS.
// See the license file in the project root for more information.

namespace Coherence.Toolkit.Tests
{
    using System;
    using Coherence.Tests;
    using Moq;
    using NUnit.Framework;
    using UnityEngine;
    using Object = UnityEngine.Object;

    /// <summary>
    /// Edit mode unit tests for <see cref="CoherenceSyncConfig"/>.
    /// </summary>
    public class CoherenceSyncConfigTests : CoherenceTest
    {
        private CoherenceSyncConfig config;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();
            config = ScriptableObject.CreateInstance<CoherenceSyncConfig>();
        }

        [TearDown]
        public override void TearDown()
        {
            Object.DestroyImmediate(config);
            base.TearDown();
        }

        [Test]
        [Description("GetNetworkAssetId returns the same value on repeated calls for the same ID.")]
        public void GetNetworkAssetId_IsIdempotent()
        {
            config.Init("some-test-id");

            var first = config.GetNetworkAssetId();
            var second = config.GetNetworkAssetId();

            Assert.That(second, Is.EqualTo(first));
        }

        [Test]
        [Description("GetNetworkAssetId returns different values for different IDs.")]
        public void GetNetworkAssetId_ReturnsDifferentValue_ForDifferentIds()
        {
            config.Init("id-one");
            var first = config.GetNetworkAssetId();

            config.Init("id-two");
            var second = config.GetNetworkAssetId();

            Assert.That(second, Is.Not.EqualTo(first));
        }

        [Test]
        [Description("GetNetworkAssetId handles an odd-length ID without throwing.")]
        public void GetNetworkAssetId_HandlesOddLengthId_WithoutThrowing()
        {
            config.Init("abc");

            Assert.DoesNotThrow(() => config.GetNetworkAssetId());
        }

        [Test]
        [Description("NetworkAssetIdToString formats zero as eight zero digits.")]
        public void NetworkAssetIdToString_FormatsZero_AsEightZeros() =>
            Assert.That(CoherenceSyncConfig.NetworkAssetIdToString(0), Is.EqualTo("00000000"));

        [Test]
        [Description("NetworkAssetIdToString formats -1 as eight F digits.")]
        public void NetworkAssetIdToString_FormatsNegativeOne_AsAllFs() =>
            Assert.That(CoherenceSyncConfig.NetworkAssetIdToString(-1), Is.EqualTo("FFFFFFFF"));

        [Test]
        [Description("NetworkAssetIdToString pads small values with leading zeros to eight characters.")]
        public void NetworkAssetIdToString_PadsToEightCharacters() =>
            Assert.That(CoherenceSyncConfig.NetworkAssetIdToString(0xFF), Is.EqualTo("000000FF"));

        [Test]
        [Description("GetNetworkAssetIdAsString matches NetworkAssetIdToString(GetNetworkAssetId()).")]
        public void GetNetworkAssetIdAsString_MatchesManualComposition()
        {
            config.Init("some-id");

            var expected = CoherenceSyncConfig.NetworkAssetIdToString(config.GetNetworkAssetId());

            Assert.That(config.GetNetworkAssetIdAsString(), Is.EqualTo(expected));
        }

        [Test]
        [Description("GetInstance returns null when the provider's LoadAsset returns null.")]
        public void GetInstance_WhenProviderReturnsNull_ReturnsNull()
        {
            using var bridgeBuilder = new MockBridgeBuilder();
            var providerMock = new Mock<INetworkObjectProvider>();
            providerMock.Setup(p => p.LoadAsset(It.IsAny<string>())).Returns((ICoherenceSync)null);
            config.Init("test-id");
            config.Provider = providerMock.Object;

            var result = config.GetInstance(bridgeBuilder.Build(), Vector3.zero, Quaternion.identity);

            Assert.That(result, Is.Null);
        }

        [Test]
        [Description("GetInstance returns null when the provider is null.")]
        public void GetInstance_WhenProviderIsNull_ReturnsNull()
        {
            using var bridgeBuilder = new MockBridgeBuilder();
            config.Init("test-id");
            config.Provider = null;

            var result = config.GetInstance(bridgeBuilder.Build(), Vector3.zero, Quaternion.identity);

            Assert.That(result, Is.Null);
        }

        [Test]
        [Description("GetInstance returns null when the instantiator is null.")]
        public void GetInstance_WhenInstantiatorIsNull_ReturnsNull()
        {
            using var bridgeBuilder = new MockBridgeBuilder();
            var providerMock = new Mock<INetworkObjectProvider>();
            providerMock.Setup(p => p.LoadAsset(It.IsAny<string>())).Returns(new Mock<ICoherenceSync>().Object);
            config.Init("test-id");
            config.Provider = providerMock.Object;
            config.Instantiator = null;

            var result = config.GetInstance(bridgeBuilder.Build(), Vector3.zero, Quaternion.identity);

            Assert.That(result, Is.Null);
        }

        [Test]
        [Description("GetInstance returns null when no CoherenceBridge is registered in the active scene.")]
        public void GetInstance_WhenNoBridgeInActiveScene_ReturnsNull()
        {
            config.Init("test-id");

            var result = config.GetInstance();

            Assert.That(result, Is.Null);
        }

        [Test]
        [Description("GetInstanceAsync invokes the callback with null when the provider loads null.")]
        public void GetInstanceAsync_WhenProviderLoadsNull_CallbackReceivesNull()
        {
            using var bridgeBuilder = new MockBridgeBuilder();
            var providerMock = new Mock<INetworkObjectProvider>();
            providerMock
                .Setup(p => p.LoadAsset(It.IsAny<string>(), It.IsAny<Action<ICoherenceSync>>()))
                .Callback<string, Action<ICoherenceSync>>((_, cb) => cb(null));
            config.Init("test-id");
            config.Provider = providerMock.Object;

            CoherenceSync received = default;
            config.GetInstanceAsync(bridgeBuilder.Build(), Vector3.zero, Quaternion.identity, sync => received = sync);

            Assert.That(received, Is.Null);
        }

        [Test]
        [Description("GetInstanceAsync invokes the callback with null when no CoherenceBridge is in the active scene.")]
        public void GetInstanceAsync_WhenNoBridgeInActiveScene_CallbackReceivesNull()
        {
            config.Init("test-id");

            CoherenceSync received = default;
            config.GetInstanceAsync(sync => received = sync);

            Assert.That(received, Is.Null);
        }

        [Test]
        [Description("ReleaseInstance calls Destroy on the instantiator with the given instance.")]
        public void ReleaseInstance_CallsDestroyOnInstantiator()
        {
            var instantiatorMock = new Mock<INetworkObjectInstantiator>();
            config.Instantiator = instantiatorMock.Object;

            config.ReleaseInstance(null);

            instantiatorMock.Verify(i => i.Destroy(null), Times.Once);
        }
    }
}
