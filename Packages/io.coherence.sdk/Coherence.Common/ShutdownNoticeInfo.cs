// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Common
{
    using System;

    /// <summary>
    /// A scheduled Replication Server shutdown, as announced through a
    /// shutdown notice. The server keeps running until the scheduled time.
    /// </summary>
    public readonly struct ShutdownNoticeInfo
    {
        /// <summary>
        /// Scheduled shutdown time, always in UTC.
        /// </summary>
        public DateTimeOffset ShutdownTimeUtc { get; }

        /// <summary>
        /// Optional human readable reason for the shutdown. Empty when none
        /// was given.
        /// </summary>
        public string Reason { get; }

        /// <summary>
        /// Optional custom data set by whoever announced the shutdown, opaque
        /// to the server and interpreted by the game. Empty when none was
        /// given.
        /// </summary>
        public CustomPayload Payload { get; }

        public ShutdownNoticeInfo(DateTimeOffset shutdownTime, string reason, CustomPayload payload)
        {
            ShutdownTimeUtc = shutdownTime.ToUniversalTime();
            Reason = reason ?? string.Empty;
            Payload = payload;
        }

        public override string ToString() =>
            $"{nameof(ShutdownNoticeInfo)}: [ At: {ShutdownTimeUtc:O}, Reason: {Reason}, HasPayload: {!Payload.IsEmpty} ]";
    }
}
