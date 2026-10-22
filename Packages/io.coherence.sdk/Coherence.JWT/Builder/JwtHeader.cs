#if MODERN_DOTNET
using System.Text.Json.Serialization;
#endif

using Newtonsoft.Json;

namespace JWT.Builder
{
    /// <summary>
    /// JSON header model with predefined parameter names specified by RFC 7515, see https://tools.ietf.org/html/rfc7515
    /// </summary>
    public class JwtHeader
    {
        /// <summary>
        /// Declared explicitly, and for every target, because this type is only ever created by a
        /// deserializer. An implicit constructor is invisible to static analysis and gets removed by
        /// IL2CPP managed stripping, which makes decoding any token fail.
        /// </summary>
#if MODERN_DOTNET
        [System.Text.Json.Serialization.JsonConstructor]
#endif
        public JwtHeader() { }

        [JsonProperty("typ")]
#if MODERN_DOTNET
        [JsonPropertyName("typ")]
#endif
        public string Type { get; set; }

        [JsonProperty("cty")]
#if MODERN_DOTNET
        [JsonPropertyName("cty")]
#endif
        public string ContentType { get; set; }

        [JsonProperty("alg")]
#if MODERN_DOTNET
        [JsonPropertyName("alg")]
#endif
        public string Algorithm { get; set; }

        [JsonProperty("kid")]
#if MODERN_DOTNET
        [JsonPropertyName("kid")]
#endif
        public string KeyId { get; set; }

        [JsonProperty("x5u")]
#if MODERN_DOTNET
        [JsonPropertyName("x5u")]
#endif
        public string X5u { get; set; }

        [JsonProperty("x5c")]
#if MODERN_DOTNET
        [JsonPropertyName("x5c")]
#endif
        public string[] X5c { get; set; }

        [JsonProperty("x5t")]
#if MODERN_DOTNET
        [JsonPropertyName("x5t")]
#endif
        public string X5t { get; set; }
    }
}
