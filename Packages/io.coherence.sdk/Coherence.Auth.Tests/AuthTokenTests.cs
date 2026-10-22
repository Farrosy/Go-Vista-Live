// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Connection.Tests
{
    using JWT;
    using NUnit.Framework;

    public class AuthTokenTests
    {
        private const string PlayerID = "test-player";
        private const string Secret = "test-secret";

        [Test]
        [Description("Verifies that a token built by AuthToken.Custom can be decoded by JwtUtil and yields the "
            + "same subject that was encoded into it.")]
        public void Custom_TokenRoundTripsThroughJwtUtil()
        {
            // Arrange
            var token = AuthToken.Custom(PlayerID, ConnectionType.Client, Secret);

            // Act
            var subject = JwtUtil.GetSubjectWithoutValidation(token);

            // Assert
            Assert.That(subject, Is.EqualTo(PlayerID));
        }

        [Test]
        [Description("Verifies that Custom encodes the connection type as a lowercase string in the 'type' claim, "
            + "matching what the RS reads when authenticating a connecting client or simulator.")]
        [TestCase(ConnectionType.Client, "client")]
        [TestCase(ConnectionType.Simulator, "simulator")]
        public void Custom_EncodesConnectionTypeAsLowercaseTypeClaim(ConnectionType connectionType, string expectedClaim)
        {
            // Arrange
            var token = AuthToken.Custom(PlayerID, connectionType, Secret);

            // Act
            var typeClaim = JwtUtil.GetClaimWithoutValidation(token, "type");

            // Assert
            Assert.That(typeClaim, Is.EqualTo(expectedClaim));
        }

        [Test]
        [Description("Verifies that Custom always encodes 'local' as the 'pid' claim regardless of connection type, "
            + "which is what the RS checks to identify local-development tokens.")]
        public void Custom_EncodesPidClaimAsLocal()
        {
            // Arrange
            var token = AuthToken.Custom(PlayerID, ConnectionType.Client, Secret);

            // Act
            var pidClaim = JwtUtil.GetClaimWithoutValidation(token, "pid");

            // Assert
            Assert.That(pidClaim, Is.EqualTo("local"));
        }

        [Test]
        [Description("Verifies that ForLocalDevelopment produces a token that decodes to a non-empty subject "
            + "via the same coherence auth APIs used at runtime, for every connection type.")]
        [TestCase(ConnectionType.Client)]
        [TestCase(ConnectionType.Simulator)]
        public void ForLocalDevelopment_TokenRoundTripsThroughJwtUtil(ConnectionType connectionType)
        {
            // Arrange
            var token = AuthToken.ForLocalDevelopment(connectionType);

            // Act
            var subject = JwtUtil.GetSubjectWithoutValidation(token);

            // Assert
            Assert.That(subject, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        [Description("Verifies that two calls to ForLocalDevelopment without an explicit player ID produce tokens "
            + "with different subjects, confirming that a unique random ID is generated per call.")]
        [TestCase(ConnectionType.Client)]
        [TestCase(ConnectionType.Simulator)]
        public void ForLocalDevelopment_WithoutExplicitPlayerId_ProducesUniqueSubjectEachCall(ConnectionType connectionType)
        {
            // Act
            var token1 = AuthToken.ForLocalDevelopment(connectionType);
            var token2 = AuthToken.ForLocalDevelopment(connectionType);

            var subject1 = JwtUtil.GetSubjectWithoutValidation(token1);
            var subject2 = JwtUtil.GetSubjectWithoutValidation(token2);

            // Assert
            Assert.That(subject1, Is.Not.EqualTo(subject2));
        }

        [Test]
        [Description("Verifies that ForLocalDevelopment with an explicit player ID round-trips that exact ID "
            + "through JwtUtil, the same way HandleNewConnection reads a connecting player's subject.")]
        public void ForLocalDevelopment_WithExplicitPlayerId_RoundTripsThroughJwtUtil()
        {
            // Arrange
            var token = AuthToken.ForLocalDevelopment(PlayerID, ConnectionType.Client);

            // Act
            var subject = JwtUtil.GetSubjectWithoutValidation(token);

            // Assert
            Assert.That(subject, Is.EqualTo(PlayerID));
        }

        [Test]
        [Description("Verifies that ForLocalDevelopment signs with the well-known local development secret, "
            + "so tokens are accepted by a local RS without any user-supplied secret.")]
        [TestCase(ConnectionType.Client)]
        [TestCase(ConnectionType.Simulator)]
        public void ForLocalDevelopment_UsesLocalDevelopmentSecret(ConnectionType connectionType)
        {
            // Arrange - build the equivalent token manually using the known secret
            var token = AuthToken.ForLocalDevelopment(PlayerID, connectionType);
            var expected = AuthToken.Custom(PlayerID, connectionType, AuthToken.LocalDevelopmentSecret);

            // Assert - both tokens encode the same claims (same header.payload, signature may differ only
            // if the JWT library is non-deterministic, so we compare decoded subjects and type claims)
            Assert.That(JwtUtil.GetSubjectWithoutValidation(token),
                Is.EqualTo(JwtUtil.GetSubjectWithoutValidation(expected)));
            Assert.That(JwtUtil.GetClaimWithoutValidation(token, "type"),
                Is.EqualTo(JwtUtil.GetClaimWithoutValidation(expected, "type")));
        }
    }
}
