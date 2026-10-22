// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Common.Tests
{
    using NUnit.Framework;

    public class HostAuthorityTests
    {
        [TestCase(0, "")]
        [TestCase(HostAuthority.CreateEntities, "create-entities")]
        [TestCase(HostAuthority.ValidateConnection, "validate-connection")]
        [TestCase(HostAuthority.CreateEntities | HostAuthority.ValidateConnection, "create-entities,validate-connection")]
        public void GetCommaSeparatedFeaturesWorks(HostAuthority option, string expectedResult)
        {
            var result = option.GetCommaSeparatedFeatures();

            Assert.That(result, Is.EqualTo(expectedResult));
        }

        [TestCase("create-entities", true, HostAuthority.CreateEntities)]
        [TestCase("validate-connection", true, HostAuthority.ValidateConnection)]
        [TestCase("create-entities,validate-connection", true, HostAuthority.CreateEntities | HostAuthority.ValidateConnection)]
        [TestCase("", true, 0)]
        [TestCase("create-entities,", true, HostAuthority.CreateEntities)]   // trailing comma
        [TestCase(",create-entities", true, HostAuthority.CreateEntities)]   // leading comma
        [TestCase("create-entities,,validate-connection", true, HostAuthority.CreateEntities | HostAuthority.ValidateConnection)] // repeated comma
        [TestCase("create-entities, validate-connection", true, HostAuthority.CreateEntities | HostAuthority.ValidateConnection)] // whitespace
        [TestCase("invalid-feature", false, 0)]
        public void FromCommaSeparatedFeaturesWorks(string features, bool expectedSuccess, HostAuthority expectedResult)
        {
            var success = HostAuthorityEx.FromCommaSeparatedFeatures(features, out var result);

            Assert.That(success, Is.EqualTo(expectedSuccess));
            Assert.That(result, Is.EqualTo((HostAuthority)expectedResult));
        }
    }
}
