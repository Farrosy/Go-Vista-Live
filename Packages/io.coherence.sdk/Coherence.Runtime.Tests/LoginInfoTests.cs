// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Runtime.Tests
{
    using System;
    using Coherence.Cloud;
    using Coherence.Tests;
    using NUnit.Framework;

    /// <summary>
    /// Unit tests for <see cref="LoginInfo"/>.
    /// </summary>
    public sealed class LoginInfoTests : CoherenceTest
    {
        private const string TeamPlayerId = "T:_teamPlayerId";
        private const string BundleId = "io.coherence.firststeps";
        private const string PublicKeyUrl = "https://static.gc.apple.com/public-key/gc-prod-12.cer";
        private const ulong Timestamp = 1755595000123;

        private static LoginInfo GameCenter(string teamPlayerId = TeamPlayerId, string bundleId = BundleId,
            string publicKeyUrl = PublicKeyUrl, byte[] signature = null, byte[] salt = null, ulong timestamp = Timestamp)
            => LoginInfo.WithGameCenter(new(teamPlayerId, bundleId, publicKeyUrl, signature ?? new byte[] { 1, 2, 3 }, salt ?? new byte[] { 4, 5 }, timestamp));

        [Test]
        public void WithGameCenter_Exposes_Provided_Credentials()
        {
            var signature = new byte[] { 0x00, 0xFF, 0xAB };
            var salt = new byte[] { 0xDE, 0xAD };

            var loginInfo = GameCenter(signature: signature, salt: salt);

            Assert.That(loginInfo.LoginType, Is.EqualTo(LoginType.GameCenter));
            Assert.That(loginInfo.GameCenter.TeamPlayerId, Is.EqualTo(TeamPlayerId));
            Assert.That(loginInfo.GameCenter.BundleId, Is.EqualTo(BundleId));
            Assert.That(loginInfo.GameCenter.PublicKeyUrl, Is.EqualTo(PublicKeyUrl));
            Assert.That(loginInfo.GameCenter.Signature, Is.EqualTo(Convert.ToBase64String(signature)));
            Assert.That(loginInfo.GameCenter.Salt, Is.EqualTo(Convert.ToBase64String(salt)));
            Assert.That(loginInfo.GameCenter.Timestamp, Is.EqualTo(Timestamp));
        }

        [Test]
        public void GameCenter_LoginInfos_With_Identical_Credentials_Are_Equal()
        {
            Assert.That(GameCenter(), Is.EqualTo(GameCenter()));
            Assert.That(GameCenter().GetHashCode(), Is.EqualTo(GameCenter().GetHashCode()));
        }

        /// <remarks>
        /// Game Center credentials live outside the two string fields that back every other provider,
        /// so equality has to reach into them. Otherwise all Game Center logins compare equal and
        /// looking a player account up by its login info hands back the wrong one.
        /// </remarks>
        [Test]
        public void GameCenter_LoginInfos_With_Differing_Credentials_Are_Not_Equal()
        {
            var loginInfo = GameCenter();

            Assert.That(GameCenter(teamPlayerId: "T:_other"), Is.Not.EqualTo(loginInfo));
            Assert.That(GameCenter(bundleId: "io.coherence.other"), Is.Not.EqualTo(loginInfo));
            Assert.That(GameCenter(publicKeyUrl: "https://static.gc.apple.com/public-key/gc-prod-11.cer"), Is.Not.EqualTo(loginInfo));
            Assert.That(GameCenter(signature: new byte[] { 9, 9, 9 }), Is.Not.EqualTo(loginInfo));
            Assert.That(GameCenter(salt: new byte[] { 9, 9 }), Is.Not.EqualTo(loginInfo));
            Assert.That(GameCenter(timestamp: Timestamp + 1), Is.Not.EqualTo(loginInfo));
        }

        [Test]
        public void GameCenter_LoginInfo_Is_Not_Equal_To_Other_LoginTypes()
        {
            Assert.That(GameCenter(), Is.Not.EqualTo(LoginInfo.WithNintendo("token")));
            Assert.That(GameCenter(), Is.Not.EqualTo(LoginInfo.WithJwt("token")));
        }

        [Test]
        [Description("GameCenterCredentials is a public struct, so a default instance bypasses the constructor. "
                     + "Its properties must still never return null - as they do for every login type other than "
                     + "Game Center, where the getter hands out a default instance.")]
        public void GameCenter_Credentials_Of_Other_LoginTypes_Are_Empty_And_Not_Null()
        {
            var credentials = LoginInfo.WithNintendo("token").GameCenter;

            Assert.That(credentials.TeamPlayerId, Is.Empty);
            Assert.That(credentials.BundleId, Is.Empty);
            Assert.That(credentials.PublicKeyUrl, Is.Empty);
            Assert.That(credentials.Signature, Is.Empty);
            Assert.That(credentials.Salt, Is.Empty);
            Assert.That(credentials.Timestamp, Is.Zero);
        }
    }
}
