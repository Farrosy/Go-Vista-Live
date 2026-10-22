// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Log
{
    /// <summary>
    /// Exact copy of warning ids and warning names from RS (in Go).
    /// Since the ids will not change in the future (only new ones will be added) this list is stable.
    /// Only warnings which were of importance to the client were added (for integration-tests).
    /// </summary>
    public enum RSWarning
    {
        WarningBriskLastSentReliablePacketNil = 5,
        BriskReceivedChangeSendFrequencyRequest = 7,
        BriskFrequencyHighExternalPressure = 9,
        BriskFrequencyMediumExternalPressure = 11,
        BriskFrequencyConnectionThrottled = 12,
        EntityManagerAuthTransferFailed = 19,
        WarningEntityManagerAuthFailedNoClient = 21,
        WarningSerializerMessageRoutingError = 50,
        WarningInBufferTooManyEntities = 67,
        WarningConnectionClosedTooManyWarnings = 68,
        ReplicationServerRestrictedMode = 71,
    }
}

