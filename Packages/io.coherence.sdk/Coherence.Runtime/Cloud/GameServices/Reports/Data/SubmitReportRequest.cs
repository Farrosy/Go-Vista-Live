// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Cloud
{
    using Newtonsoft.Json;
    using Utils;

    /// <summary>
    /// Wire-level body of <c>POST /reports</c>.
    /// </summary>
    internal struct SubmitReportRequest
    {
        [JsonProperty("target_player_id")]
        public string Target;

        [JsonProperty("category")]
        public ReportCategory Category;

        [JsonProperty("notes", NullValueHandling = NullValueHandling.Ignore)]
        public string Notes;

        public static string GetRequestBody(PlayerAccountId target, ReportCategory category, string notes)
        {
            return CoherenceJson.SerializeObject(new SubmitReportRequest
            {
                Target = target,
                Category = category,
                Notes = string.IsNullOrEmpty(notes) ? null : notes,
            });
        }
    }
}
