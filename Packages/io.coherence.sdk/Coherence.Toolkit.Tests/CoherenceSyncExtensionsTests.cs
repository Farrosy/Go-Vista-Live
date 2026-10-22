// Copyright (c) coherence ApS.
// See the license file in the project root for more information.

namespace Coherence.Toolkit.Tests
{
    using Coherence.Tests;
    using NUnit.Framework;
    using UnityEngine;
    using Object = UnityEngine.Object;
    using static CoherenceSync;

    /// <summary>
    /// Edit mode unit tests for <see cref="CoherenceSyncExtensions"/>.
    /// </summary>
    public class CoherenceSyncExtensionsTests : CoherenceTest
    {
        // MapToType

        [Test]
        [Description("MapToType maps NotTransferable config to NotTransferable type.")]
        public void MapToType_NotTransferable_MapsToNotTransferableType() =>
            Assert.That(AuthorityTransferConfig.NotTransferable.MapToType(), Is.EqualTo(AuthorityTransferType.NotTransferable));

        [Test]
        [Description("MapToType maps Request config to Request type.")]
        public void MapToType_Request_MapsToRequestType() =>
            Assert.That(AuthorityTransferConfig.Request.MapToType(), Is.EqualTo(AuthorityTransferType.Request));

        [Test]
        [Description("MapToType maps Stealing config to Stealing type.")]
        public void MapToType_Stealing_MapsToStealingType() =>
            Assert.That(AuthorityTransferConfig.Stealing.MapToType(), Is.EqualTo(AuthorityTransferType.Stealing));

        [Test]
        [Description("MapToType throws for an unrecognised AuthorityTransferConfig value.")]
        public void MapToType_UnknownValue_Throws() =>
            Assert.That(() => ((AuthorityTransferConfig)(-99)).MapToType(), Throws.Exception);

        // MapToConfig

        [Test]
        [Description("MapToConfig maps NotTransferable type to NotTransferable config.")]
        public void MapToConfig_NotTransferable_MapsToNotTransferableConfig() =>
            Assert.That(AuthorityTransferType.NotTransferable.MapToConfig(), Is.EqualTo(AuthorityTransferConfig.NotTransferable));

        [Test]
        [Description("MapToConfig maps Request type to Request config.")]
        public void MapToConfig_Request_MapsToRequestConfig() =>
            Assert.That(AuthorityTransferType.Request.MapToConfig(), Is.EqualTo(AuthorityTransferConfig.Request));

        [Test]
        [Description("MapToConfig maps Stealing type to Stealing config.")]
        public void MapToConfig_Stealing_MapsToStealingConfig() =>
            Assert.That(AuthorityTransferType.Stealing.MapToConfig(), Is.EqualTo(AuthorityTransferConfig.Stealing));

        [Test]
        [Description("MapToConfig throws for an unrecognised AuthorityTransferType value.")]
        public void MapToConfig_UnknownValue_Throws() =>
            Assert.That(() => ((AuthorityTransferType)(-99)).MapToConfig(), Throws.Exception);

        // MapToType / MapToConfig roundtrip

        [Test]
        [Description("MapToType and MapToConfig are inverses for NotTransferable.")]
        public void MapToType_ThenMapToConfig_Roundtrip_NotTransferable() =>
            Assert.That(AuthorityTransferConfig.NotTransferable.MapToType().MapToConfig(), Is.EqualTo(AuthorityTransferConfig.NotTransferable));

        [Test]
        [Description("MapToType and MapToConfig are inverses for Request.")]
        public void MapToType_ThenMapToConfig_Roundtrip_Request() =>
            Assert.That(AuthorityTransferConfig.Request.MapToType().MapToConfig(), Is.EqualTo(AuthorityTransferConfig.Request));

        [Test]
        [Description("MapToType and MapToConfig are inverses for Stealing.")]
        public void MapToType_ThenMapToConfig_Roundtrip_Stealing() =>
            Assert.That(AuthorityTransferConfig.Stealing.MapToType().MapToConfig(), Is.EqualTo(AuthorityTransferConfig.Stealing));

        // TryGetValueBinding / GetValueBinding

        [Test]
        [Description("TryGetValueBinding returns false and a null binding when no bindings are configured.")]
        public void TryGetValueBinding_WhenNoBindings_ReturnsFalseAndNullBinding()
        {
            var go = new GameObject();
            var sync = go.AddComponent<CoherenceSync>();

            var found = sync.TryGetValueBinding<Transform, Vector3>("position", out var binding);

            Object.DestroyImmediate(go);

            Assert.That(found, Is.False);
            Assert.That(binding, Is.Null);
        }

        [Test]
        [Description("GetValueBinding throws when no binding matching the name exists on the sync.")]
        public void GetValueBinding_WhenNoMatchingBinding_Throws()
        {
            var go = new GameObject();
            var sync = go.AddComponent<CoherenceSync>();

            Assert.That(() => sync.GetValueBinding<Transform, Vector3>("position"), Throws.Exception);

            Object.DestroyImmediate(go);
        }

        // GetInstance scene overload

        [Test]
        [Description("GetInstance returns null when no CoherenceBridge is registered in the active scene.")]
        public void GetInstance_WhenNoBridgeInActiveScene_ReturnsNull()
        {
            var go = new GameObject();
            var sync = go.AddComponent<CoherenceSync>();

            var result = sync.GetInstance();

            Object.DestroyImmediate(go);

            Assert.That(result, Is.Null);
        }
    }
}
