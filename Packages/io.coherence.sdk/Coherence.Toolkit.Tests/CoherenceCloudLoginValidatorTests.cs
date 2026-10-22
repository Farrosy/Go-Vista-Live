// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Toolkit.Tests
{
    using Cloud;
    using Coherence.Tests;
    using NUnit.Framework;
    using static CoherenceCloudLogin;

    /// <summary>
    /// Edit mode unit tests for <see cref="CoherenceCloudLogin.Validate"/>.
    /// </summary>
    public sealed class CoherenceCloudLoginValidatorTests : CoherenceTest
    {
        [TestCase(null, CredentialsIssue.UsernameNotProvided)]
        [TestCase("", CredentialsIssue.UsernameEmpty)]
        [TestCase("A", CredentialsIssue.UsernameInvalidCharacters)]
        [TestCase("å", CredentialsIssue.UsernameInvalidCharacters)]
        [TestCase(" ", CredentialsIssue.UsernameInvalidCharacters)]
        [TestCase("abcdefghi-jklmnopqr-stuvwxyzz-123456789_", CredentialsIssue.None)]
        [TestCase("123456789_123456789_123456789_123456789_1", CredentialsIssue.UsernameTooLong)]
        [TestCase("123456789_123456789_123456789_123456789_A", CredentialsIssue.UsernameTooLong | CredentialsIssue.UsernameInvalidCharacters)]
        public void Validate_Username(string username, CredentialsIssue expectedIssue)
        {
            var cloudLogin = Create(username, "password");
            var issues = cloudLogin.Validate();
            Assert.That(issues, Is.EqualTo(expectedIssue));
        }

        [TestCase(null, CredentialsIssue.PasswordNotProvided)]
        [TestCase("", CredentialsIssue.PasswordEmpty)]
        [TestCase(" ", CredentialsIssue.None)]
        [TestCase("123456789_123456789_123456789_123456789_A!@ä*", CredentialsIssue.None)]
        public void Validate_Password(string password, CredentialsIssue expectedIssue)
        {
            var cloudLogin = Create("username", password);
            var issues = cloudLogin.Validate();
            Assert.That(issues, Is.EqualTo(expectedIssue));
        }

        [TestCase(null, CredentialsIssue.OneTimeCodeNotProvided)]
        [TestCase("", CredentialsIssue.OneTimeCodeEmpty)]
        [TestCase("1", CredentialsIssue.None)]
        public void Validate_OneTimeCode(string oneTimeCode, CredentialsIssue expectedIssue)
        {
            var cloudLogin = Create(Login.WithOneTimeCode, oneTimeCode);
            var issues = cloudLogin.Validate();
            Assert.That(issues, Is.EqualTo(expectedIssue));
        }

        [TestCase(null, CredentialsIssue.JwtTokenNotProvided)]
        [TestCase("", CredentialsIssue.JwtTokenEmpty)]
        [TestCase("1", CredentialsIssue.None)]
        public void Validate_JwtToken(string jwtToken, CredentialsIssue expectedIssue)
        {
            var cloudLogin = Create(Login.WithJwt, jwtToken);
            var issues = cloudLogin.Validate();
            Assert.That(issues, Is.EqualTo(expectedIssue));
        }

        [TestCase(null, CredentialsIssue.SteamTicketNotProvided)]
        [TestCase("", CredentialsIssue.SteamTicketEmpty)]
        [TestCase("1", CredentialsIssue.None)]
        public void Validate_SteamTicket(string steamTicket, CredentialsIssue expectedIssue)
        {
            var cloudLogin = Create(Login.WithSteam, steamTicket);
            var issues = cloudLogin.Validate();
            Assert.That(issues, Is.EqualTo(expectedIssue));
        }

        [TestCase(null, CredentialsIssue.EpicGamesTokenNotProvided)]
        [TestCase("", CredentialsIssue.EpicGamesTokenEmpty)]
        [TestCase("1", CredentialsIssue.None)]
        public void Validate_EpicGamesToken(string epicGamesToken, CredentialsIssue expectedIssue)
        {
            var cloudLogin = Create(Login.WithEpicGames, epicGamesToken);
            var issues = cloudLogin.Validate();
            Assert.That(issues, Is.EqualTo(expectedIssue));
        }

        [TestCase(null, CredentialsIssue.PlayStationTokenNotProvided)]
        [TestCase("", CredentialsIssue.PlayStationTokenEmpty)]
        [TestCase("1", CredentialsIssue.None)]
        public void Validate_PlayStationToken(string playStationToken, CredentialsIssue expectedIssue)
        {
            var cloudLogin = Create(Login.WithPlayStation, playStationToken);
            var issues = cloudLogin.Validate();
            Assert.That(issues, Is.EqualTo(expectedIssue));
        }

        [TestCase(null, CredentialsIssue.XboxTokenNotProvided)]
        [TestCase("", CredentialsIssue.XboxTokenEmpty)]
        [TestCase("1", CredentialsIssue.None)]
        public void Validate_XboxToken(string xboxToken, CredentialsIssue expectedIssue)
        {
            var cloudLogin = Create(Login.WithXbox, xboxToken);
            var issues = cloudLogin.Validate();
            Assert.That(issues, Is.EqualTo(expectedIssue));
        }

        [TestCase(null, CredentialsIssue.NintendoTokenNotProvided)]
        [TestCase("", CredentialsIssue.NintendoTokenEmpty)]
        [TestCase("1", CredentialsIssue.None)]
        public void Validate_NintendoToken(string nintendoToken, CredentialsIssue expectedIssue)
        {
            var cloudLogin = Create(Login.WithNintendo, nintendoToken);
            var issues = cloudLogin.Validate();
            Assert.That(issues, Is.EqualTo(expectedIssue));
        }

        [Test]
        public void Validate_GameCenterCredentials_Not_Provided()
        {
            var cloudLogin = Create(Login.WithGameCenter);
            var issues = cloudLogin.Validate();
            Assert.That(issues, Is.EqualTo(CredentialsIssue.GameCenterCredentialsNotProvided));
        }

        [Test]
        public void Validate_GameCenterCredentials_Complete()
        {
            var cloudLogin = Create(Login.WithGameCenter);
            cloudLogin.GameCenterCredentials = new("T:_teamPlayerId", "io.coherence.firststeps",
                "https://static.gc.apple.com/public-key/gc-prod-12.cer", new byte[] { 1, 2, 3 }, new byte[] { 4, 5 }, 1755595000123);

            var issues = cloudLogin.Validate();
            Assert.That(issues, Is.EqualTo(CredentialsIssue.None));
        }

        [Test]
        [Description("The property mirrors the other credential properties: it reads back only while 'Login' "
                     + "is 'With Game Center', and assigning it selects that login type.")]
        public void Setting_GameCenterCredentials_Selects_GameCenter_And_Reads_Back()
        {
            var cloudLogin = Create(Login.AsGuest);
            var credentials = new GameCenterCredentials("T:_teamPlayerId", "io.coherence.firststeps",
                "https://static.gc.apple.com/public-key/gc-prod-12.cer", new byte[] { 1, 2, 3 }, new byte[] { 4, 5 }, 1755595000123);

            cloudLogin.GameCenterCredentials = credentials;

            Assert.That(cloudLogin.login, Is.EqualTo(Login.WithGameCenter));
            Assert.That(cloudLogin.GameCenterCredentials, Is.EqualTo(credentials));
        }

        [Test]
        public void GameCenterCredentials_Is_Null_For_Other_Login_Types()
        {
            var cloudLogin = Create(Login.WithGameCenter);
            cloudLogin.GameCenterCredentials = new("T:_teamPlayerId", "bundleId", "publicKeyUrl",
                new byte[] { 1 }, new byte[] { 2 }, 1);

            cloudLogin.JwtToken = "token";

            Assert.That(cloudLogin.login, Is.EqualTo(Login.WithJwt));
            Assert.That(cloudLogin.GameCenterCredentials, Is.Null);
        }

        [TestCase("", "bundleId", "publicKeyUrl", 1, 1, 1UL, TestName = "Validate_GameCenterCredentials_Incomplete_TeamPlayerId")]
        [TestCase("teamPlayerId", "", "publicKeyUrl", 1, 1, 1UL, TestName = "Validate_GameCenterCredentials_Incomplete_BundleId")]
        [TestCase("teamPlayerId", "bundleId", "", 1, 1, 1UL, TestName = "Validate_GameCenterCredentials_Incomplete_PublicKeyUrl")]
        [TestCase("teamPlayerId", "bundleId", "publicKeyUrl", 0, 1, 1UL, TestName = "Validate_GameCenterCredentials_Incomplete_Signature")]
        [TestCase("teamPlayerId", "bundleId", "publicKeyUrl", 1, 0, 1UL, TestName = "Validate_GameCenterCredentials_Incomplete_Salt")]
        [TestCase("teamPlayerId", "bundleId", "publicKeyUrl", 1, 1, 0UL, TestName = "Validate_GameCenterCredentials_Incomplete_Timestamp")]
        public void Validate_GameCenterCredentials_Incomplete(string teamPlayerId, string bundleId, string publicKeyUrl,
            int signatureLength, int saltLength, ulong timestamp)
        {
            var cloudLogin = Create(Login.WithGameCenter);
            cloudLogin.GameCenterCredentials = new(teamPlayerId, bundleId, publicKeyUrl,
                new byte[signatureLength], new byte[saltLength], timestamp);

            var issues = cloudLogin.Validate();
            Assert.That(issues, Is.EqualTo(CredentialsIssue.GameCenterCredentialsIncomplete));
        }
    }
}
