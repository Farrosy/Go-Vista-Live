// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Editor.Tests
{
    using Coherence.Tests;
    using Coherence.Toolkit;
    using Moq;
    using NUnit.Framework;

    public class CoherenceSyncConfigProviderTests : CoherenceTest
    {
        [Test]
        [Description("DirectReferenceProvider is a built-in provider.")]
        public void ProviderIsBuiltIn_DirectReferenceProvider_ReturnsTrue() =>
            Assert.That(CoherenceSyncConfigUtils.ProviderIsBuiltIn(new DirectReferenceProvider()), Is.True);

        [Test]
        [Description("ResourcesProvider is a built-in provider.")]
        public void ProviderIsBuiltIn_ResourcesProvider_ReturnsTrue() =>
            Assert.That(CoherenceSyncConfigUtils.ProviderIsBuiltIn(new ResourcesProvider()), Is.True);

        [Test]
        [Description("A custom INetworkObjectProvider implementation is not a built-in provider.")]
        public void ProviderIsBuiltIn_CustomProvider_ReturnsFalse() =>
            Assert.That(CoherenceSyncConfigUtils.ProviderIsBuiltIn(new Mock<INetworkObjectProvider>().Object), Is.False);

        [Test]
        [Description("DirectReferenceProvider is not addressable or custom.")]
        public void ProviderIsAddressableOrCustom_DirectReferenceProvider_ReturnsFalse() =>
            Assert.That(CoherenceSyncConfigUtils.ProviderIsAddressableOrCustom(new DirectReferenceProvider()), Is.False);

        [Test]
        [Description("ResourcesProvider is not addressable or custom.")]
        public void ProviderIsAddressableOrCustom_ResourcesProvider_ReturnsFalse() =>
            Assert.That(CoherenceSyncConfigUtils.ProviderIsAddressableOrCustom(new ResourcesProvider()), Is.False);

        [Test]
        [Description("A custom INetworkObjectProvider implementation is addressable or custom.")]
        public void ProviderIsAddressableOrCustom_CustomProvider_ReturnsTrue() =>
            Assert.That(CoherenceSyncConfigUtils.ProviderIsAddressableOrCustom(new Mock<INetworkObjectProvider>().Object), Is.True);

        [Test]
        [Description("CanLink throws ArgumentNullException when config is null.")]
        public void CanLink_NullConfig_ThrowsArgumentNullException() =>
            Assert.That(() => CoherenceSyncConfigUtils.CanLink(null), Throws.ArgumentNullException);

        [Test]
        [Description("Link throws ArgumentNullException when config is null.")]
        public void Link_NullConfig_ThrowsArgumentNullException() =>
            Assert.That(() => CoherenceSyncConfigUtils.Link(null), Throws.ArgumentNullException);
    }
}
