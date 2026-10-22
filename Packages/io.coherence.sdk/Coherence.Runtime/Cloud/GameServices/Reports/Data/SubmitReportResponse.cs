// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Cloud
{
    using Newtonsoft.Json;

    /// <summary>
    /// Wire-level response body for <c>POST /social/reports/submit</c>.
    /// </summary>
    internal struct SubmitReportResponse
    {
        [JsonProperty("report_id")]
        public string ReportId;
    }
}
