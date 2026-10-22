// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Cloud
{
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;
    using System.Runtime.Serialization;

    /// <summary>
    /// Specifies the category options for
    /// <see cref="ReportsService.SubmitReportAsync">reports submitted against player accounts</see>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Use <see cref="Other"/> with the optional <c>notes</c> argument when submitting a report when no listed category fits.
    /// </para>
    /// <para>
    /// Categories are coherence-defined and consumed by the project operator's report-management UI.
    /// They are not forwarded to external platforms (PSN, Xbox, Switch, etc.).
    /// Platform-native reporting is handled by the platform's own client UI, separately.
    /// </para>
    /// </remarks>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum ReportCategory
    {
        /// <summary>Targeted abuse, threats, or repeated unwanted contact.</summary>
        [EnumMember(Value = "harassment")]
        Harassment = 1,

        /// <summary>Content attacking people on the basis of a protected characteristic.</summary>
        [EnumMember(Value = "hate_speech")]
        HateSpeech = 2,

        /// <summary>Use of exploits, third-party tools, or other unfair-play behavior.</summary>
        [EnumMember(Value = "cheating")]
        Cheating = 3,

        /// <summary>Unsolicited bulk messaging or advertising.</summary>
        [EnumMember(Value = "spam")]
        Spam = 4,

        /// <summary>Sexual, violent, or otherwise inappropriate user-generated content.</summary>
        [EnumMember(Value = "inappropriate_content")]
        InappropriateContent = 5,

        /// <summary>Pretending to be another player, staff member, or organization.</summary>
        [EnumMember(Value = "impersonation")]
        Impersonation = 6,

        /// <summary>Account appears to belong to a user below the minimum age for the project.</summary>
        [EnumMember(Value = "underage_account")]
        UnderageAccount = 7,

        /// <summary>Other policy violation not covered by the listed categories. Use the
        /// <c>notes</c> argument to <see cref="ReportsService.SubmitReportAsync"/> to provide context.</summary>
        [EnumMember(Value = "other")]
        Other = 8,
    }
}
