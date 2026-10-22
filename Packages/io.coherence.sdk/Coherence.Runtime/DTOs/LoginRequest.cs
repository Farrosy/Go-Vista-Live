// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

#pragma warning disable 0649

namespace Coherence.Runtime
{
    using Newtonsoft.Json;

    internal struct GuestLoginRequest
    {
        [JsonProperty("guest_id")]
        public string GuestId;

        [JsonProperty("autosignup")]
        public bool AutoSignup;
    }

    internal struct PasswordLoginRequest
    {
        [JsonProperty("username")]
        public string Username;

        [JsonProperty("password")]
        public string Password;

        [JsonProperty("autosignup")]
        public bool Autosignup;
    }

    internal struct SteamLoginRequest
    {
        [JsonProperty("ticket")]
        public string Ticket;

        [JsonProperty("identity")]
        public string Identity;

        [JsonProperty("autosignup")]
        public bool AutoSignup;
    }

    internal struct EpicGamesLoginRequest
    {
        [JsonProperty("token")]
        public string Token;

        [JsonProperty("autosignup")]
        public bool AutoSignup;
    }

    internal struct PlayStationLoginRequest
    {
        [JsonProperty("token")]
        public string Token;

        [JsonProperty("autosignup")]
        public bool AutoSignup;
    }

    internal struct XboxLoginRequest
    {
        [JsonProperty("token")]
        public string Token;

        [JsonProperty("autosignup")]
        public bool AutoSignup;
    }

    internal struct NintendoLoginRequest
    {
        [JsonProperty("token")]
        public string Token;

        [JsonProperty("autosignup")]
        public bool AutoSignup;
    }

    internal struct GameCenterLoginRequest
    {
        [JsonProperty("team_player_id")]
        public string TeamPlayerId;

        [JsonProperty("bundle_id")]
        public string BundleId;

        [JsonProperty("public_key_url")]
        public string PublicKeyUrl;

        /// <summary>
        /// Base64-encoded signature.
        /// </summary>
        [JsonProperty("signature")]
        public string Signature;

        /// <summary>
        /// Base64-encoded salt.
        /// </summary>
        [JsonProperty("salt")]
        public string Salt;

        /// <summary>
        /// Signature creation time, in milliseconds since the Unix epoch.
        /// </summary>
        [JsonProperty("timestamp")]
        public ulong Timestamp;

        [JsonProperty("autosignup")]
        public bool AutoSignup;
    }

    internal struct OneTimeCodeLoginRequest
    {
        [JsonProperty("code")]
        public string Code;
    }

    internal struct JwtLoginRequest
    {
        [JsonProperty("token")]
        public string Token;

        [JsonProperty("autosignup")]
        public bool AutoSignup;
    }
}
