// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Cloud
{
    using System;
    using System.Diagnostics.CodeAnalysis;

    /// <summary>
    /// The output of GameKit's <c>fetchItems(forIdentityVerificationSignature:)</c>, used to
    /// <see cref="CoherenceCloud.LoginWithGameCenter">log in to</see> and
    /// <see cref="PlayerAccount.LinkGameCenter">link</see> a Game Center player against coherence Cloud.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The signature attests <c>teamPlayerID ‖ bundleID ‖ timestamp ‖ salt</c>, so all four
    /// must be the exact values the signature was generated with, or verification fails.
    /// </para>
    /// <para>
    /// Being a struct, a <see langword="default"/> instance bypasses the constructor, so the getters
    /// coalesce as well: no property ever returns <see langword="null"/>, and callers can test for
    /// emptiness without a null check.
    /// </para>
    /// </remarks>
    public readonly struct GameCenterCredentials : IEquatable<GameCenterCredentials>
    {
        /// <summary>
        /// <c>GKLocalPlayer.teamPlayerID</c> — the only identifier the signature attests.
        /// </summary>
        [NotNull] public string TeamPlayerId => teamPlayerId ?? "";
        private readonly string teamPlayerId;

        /// <summary>
        /// Bundle identifier of the application the signature was generated for.
        /// </summary>
        [NotNull] public string BundleId => bundleId ?? "";
        private readonly string bundleId;

        /// <summary>
        /// URL of the Apple certificate the signature can be verified against.
        /// </summary>
        [NotNull] public string PublicKeyUrl => publicKeyUrl ?? "";
        private readonly string publicKeyUrl;

        /// <summary>
        /// The signature, Base64-encoded.
        /// </summary>
        [NotNull] public string Signature => signature ?? "";
        private readonly string signature;

        /// <summary>
        /// The salt the signature was generated with, Base64-encoded.
        /// </summary>
        [NotNull] public string Salt => salt ?? "";
        private readonly string salt;

        /// <summary>
        /// Time at which the signature was created, in milliseconds since the Unix epoch.
        /// </summary>
        public ulong Timestamp { get; }

        /// <summary>
        /// Creates the Game Center credentials that GameKit's
        /// <c>fetchItems(forIdentityVerificationSignature:)</c> returned.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Your title obtains these values itself — coherence never calls GameKit, and therefore cannot
        /// validate them. Two mistakes are only catchable on the calling side:
        /// </para>
        /// <para>
        /// Pass <c>GKLocalPlayer.teamPlayerID</c>, <b>not</b> <c>gamePlayerID</c>. Apple's signature only attests
        /// the <c>teamPlayerID</c>, and it is the identifier flavor the Game Center friends API returns, so
        /// anything else is rejected as invalid credentials.
        /// </para>
        /// <para>
        /// Only use the credentials when <c>GKPlayer.scopedIDsArePersistent()</c> is <see langword="true"/>. When it
        /// is <see langword="false"/> — for guest players and some identity-less states — GameKit returns
        /// session-scoped identifiers that change every session, so every login would silently create a
        /// fresh, orphaned player account. The signature still verifies, so neither coherence nor the
        /// backend can detect this; the check on the calling side is the only guard.
        /// </para>
        /// </remarks>
        /// <param name="teamPlayerId"> <c>GKLocalPlayer.teamPlayerID</c> — <b>not</b> <c>gamePlayerID</c>. </param>
        /// <param name="bundleId"> Bundle identifier of the application the signature was generated for. </param>
        /// <param name="publicKeyUrl"> Apple certificate URL returned by GameKit. </param>
        /// <param name="signature"> Signature returned by GameKit. </param>
        /// <param name="salt"> Salt returned by GameKit. </param>
        /// <param name="timestamp"> Signature creation time returned by GameKit, in milliseconds since the Unix epoch. </param>
        public GameCenterCredentials(string teamPlayerId, string bundleId, string publicKeyUrl, byte[] signature, byte[] salt, ulong timestamp)
            : this(teamPlayerId, bundleId, publicKeyUrl, ToBase64(signature), ToBase64(salt), timestamp) { }

        internal GameCenterCredentials(string teamPlayerId, string bundleId, string publicKeyUrl, string signature, string salt, ulong timestamp)
        {
            this.teamPlayerId = teamPlayerId ?? "";
            this.bundleId = bundleId ?? "";
            this.publicKeyUrl = publicKeyUrl ?? "";
            this.signature = signature ?? "";
            this.salt = salt ?? "";
            Timestamp = timestamp;
        }

        private static string ToBase64(byte[] bytes) => bytes is { Length: > 0 } ? Convert.ToBase64String(bytes) : "";

        public static bool operator ==(GameCenterCredentials x, GameCenterCredentials y) => x.Equals(y);
        public static bool operator !=(GameCenterCredentials x, GameCenterCredentials y) => !x.Equals(y);

        public bool Equals(GameCenterCredentials other)
            => string.Equals(TeamPlayerId, other.TeamPlayerId)
               && string.Equals(BundleId, other.BundleId)
               && string.Equals(PublicKeyUrl, other.PublicKeyUrl)
               && string.Equals(Signature, other.Signature)
               && string.Equals(Salt, other.Salt)
               && Timestamp == other.Timestamp;

        public override bool Equals(object obj) => obj is GameCenterCredentials other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(TeamPlayerId, BundleId, PublicKeyUrl, Signature, Salt, Timestamp);

        public override string ToString()
            => $"TeamPlayerId:\"{TeamPlayerId}\", BundleId:\"{BundleId}\", PublicKeyUrl:\"{PublicKeyUrl}\", Signature:\"{Signature}\", Salt:\"{Salt}\", Timestamp:{Timestamp}";
    }
}
