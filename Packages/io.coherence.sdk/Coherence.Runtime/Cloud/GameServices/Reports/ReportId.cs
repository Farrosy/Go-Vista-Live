// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Cloud
{
    using System;

    /// <summary>
    /// A globally unique identifier for a player report submitted via <see cref="ReportsService"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The id is generated on the coherence Cloud backend when the report is submitted using
    /// <see cref="ReportsService.SubmitReportAsync"/> and is returned to the caller.
    /// </para>
    /// <para>
    /// The id is stable for the lifetime of the report. It is the same value that appears in
    /// the operator's report-management dashboard. The reporter holds the id and nothing more; what the
    /// operator does with the report is not surfaced back to the client.
    /// </para>
    /// </remarks>
    [Serializable]
    public readonly struct ReportId : IEquatable<ReportId>
    {
        /// <summary>
        /// Represents the lack of an id.
        /// </summary>
        public static readonly ReportId None = new("");

        private readonly string value;

        internal ReportId(string value) => this.value = value ?? "";

        public override string ToString() => value ?? "";
        public bool Equals(ReportId other) => string.IsNullOrEmpty(value) ? string.IsNullOrEmpty(other.value) : value.Equals(other.value);
        public override bool Equals(object obj) => obj is ReportId other && Equals(other);
        public override int GetHashCode() => value?.GetHashCode() ?? 0;

        public static implicit operator string(ReportId id) => id.value;
        public static implicit operator ReportId(string id) => new(id);
        public static bool operator ==(ReportId left, ReportId right) => left.Equals(right);
        public static bool operator !=(ReportId left, ReportId right) => !left.Equals(right);
    }
}
