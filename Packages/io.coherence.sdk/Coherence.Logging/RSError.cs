// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Log
{
    /// <summary>
    /// Exact copy of error ids and error names from RS (in Go).
    /// Since the ids will not change in the future (only new ones will be added) this list is stable.
    /// Only errors which were of importance to the client were added (for integration-tests).
    /// </summary>
    public enum RSError
    {
        HTTPAPIResponse = 6,
        HTTPAPIStatusError = 7,
        ErrorBriskMissingAck = 23,
        ErrorBriskInvalidData = 28,
        ErrorBriskInvalidPingSequence = 29,
        ErrorEntityManagerCreateEntityRequest = 37,
        EntityManagerDestroyEntityRequest = 38,
        ErrorClientWorldFailedToUpdateOwnEntity = 108,
        ErrorReplicationPushToConnectionFull = 112,
        ErrorReplicationFailedToReadPacket = 120,
        ReplicationRunError = 122,
        ErrorSerializerDeprecatedComponentState = 131,
        ErrorSerializerInvalidComponent = 132,
        ErrorQueryManagerFailedToInsert = 160,
        ErrorReplicationCommandAuthorityOnlyConnectionGone = 170,
    }
}
