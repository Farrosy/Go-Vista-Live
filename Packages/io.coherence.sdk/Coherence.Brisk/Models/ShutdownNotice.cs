// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Brisk.Models
{
    using System;
    using Brook;
    using Coherence.Brisk.Serializers;
    using Coherence.Common;

    /// <summary>
    /// Informs a client that the replication server is scheduled to shut down.
    /// Requires <see cref="ProtocolDef.Version.VersionIncludesShutdownNotice"/>.
    /// </summary>
    public class ShutdownNotice : IOobMessage
    {
        public bool IsReliable { get; set; } = true;
        public OobMessageType Type => OobMessageType.ShutdownNotice;

        /// <summary>Scheduled shutdown time, always in UTC.</summary>
        public DateTimeOffset ShutdownTimeUtc { get; set; }

        /// <summary>Optional human readable reason for the shutdown.</summary>
        public string Reason { get; set; }

        /// <summary>
        /// Optional custom data set by whoever announced the shutdown, opaque
        /// to the server and interpreted by the game.
        /// </summary>
        public CustomPayload Payload { get; set; }

        public ShutdownNotice(DateTimeOffset shutdownTime, string reason, CustomPayload payload)
        {
            ShutdownTimeUtc = shutdownTime.ToUniversalTime();
            Reason = reason ?? string.Empty;
            Payload = payload;
        }

        public override string ToString()
        {
            return $"{nameof(ShutdownNotice)}: [ At: {ShutdownTimeUtc:O}, Reason: {Reason}, HasPayload: {!Payload.IsEmpty} ]";
        }

        public void Serialize(IOutOctetStream stream, uint _)
        {
            stream.WriteUint64((ulong)ShutdownTimeUtc.ToUnixTimeMilliseconds());
            stream.WriteCustomPayload(new CustomPayload(Reason ?? string.Empty));
            stream.WriteCustomPayload(Payload);
        }

        public static ShutdownNotice Deserialize(IInOctetStream stream, uint _)
        {
            var unixMillis = (long)stream.ReadUint64();
            var reason = stream.ReadCustomPayload();
            var payload = stream.ReadCustomPayload();

            return new ShutdownNotice(DateTimeOffset.FromUnixTimeMilliseconds(unixMillis), reason.AsString, payload);
        }
    }
}
