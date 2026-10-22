// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

using System;
using JWT.Serializers;

namespace JWT
{
    public static class JwtUtil
    {
        private static readonly IJwtDecoder Decoder = new JwtDecoder(new JsonNetSerializer(), new JwtBase64UrlEncoder());

        public static string GetClaimWithoutValidation(string token, string claimName)
        {
            if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(claimName))
            {
                return string.Empty;
            }

            try
            {
                var payload = Decoder.DecodeToObject(token, verify: false);

                return payload.TryGetValue(claimName, out var claim) ? claim?.ToString() ?? string.Empty : string.Empty;
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        public static string GetSubjectWithoutValidation(string token) => GetClaimWithoutValidation(token, "sub");
    }
}
