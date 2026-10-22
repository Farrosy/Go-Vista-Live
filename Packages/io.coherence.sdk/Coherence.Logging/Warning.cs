// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Log
{
    public enum Warning
    {
        BoundsPositionInvalid,
        BoundsVariable,
        BriskFailedToSendDisconnect,
        BriskOutPacketStreamNull,
        BriskServerConnectionResendNull,
        BriskServerConnectionReceivedLargerThanMTU,
        BriskUnknownOOB,
        CodeGenWarning,
        CoreClientConnectFailed,
        CoreClientAuthTransferNoAuthority,
        CoreClientAuthInsufficient,
        CoreClientAuthCantOrphanInput,
        CoreClientAuthTransferFailed,
        CoreClientCommandUnownedEntity,
        CoreInConnectionFloatingOriginNaN,
        CoreInConnectionFloatingOriginDelta,
        CoreInConnectionPacketFullOfMessages,
        CoreInNetworkChannelUnknownMessage,
        EditorBakeUtilGenerateSchemaNoSchema,
        EditorBakeUtilInProgress,
        EditorBugReportCreateFile,
        EditorBugReportDisplayException,
        EditorBuildUploadValidator,
        EditorHubTrackerIssueWarning,
        EditorNetworkPrefabProcessorUpdateMissingScripts,
        EditorPortalFetchLogin,
        EditorPortalFetchParsingSDK,
        EditorPortalRequestCreditsExceeded,
        EditorPortalRequestFailed,
        EditorPortalRequestInvalidToken,
        EditorPortalRequestSDKDeprecated,
        EditorPortalRequestProjectDeleted,
        EditorPortalStartLogin,
        EditorPostprocessorGetSchemaAssetsFailure,
        EditorSchemaCreatorDuplicateNames,
        EditorSchemaCreatorInvalidBindings,
        EditorSchemaCreatorInvalidComponents,
        EditorSchemaCreatorNoLODs,
        EditorSchemaCreatorNoComponents,
        EditorSchemaCreatorInvalidLODs,
        EditorConfigurationWindowContextMenuWarning,
        EntityManagerAuthorityTransferFailed,
        EntityManagerAuthorityRequestClientNotFound,
        EntityManagerAuthorityRequestParticipantMismatch,
        EntityManagerAuthorityOrphanInput,
        EntityManagerAuthorityTransferFailedOwnerNotFound,
        EntityManagerConnectedEntityCycle,
        EntityManagerLiveQueryOutOfRange,
        EntityManagerRelativePositionOutOfRange,
        EntityManagerWorldPositionOutOfRange,
        FluxDisableAddressReuse,
        FluxInvalidRoomID,
        FluxReceivedFromInvalidIP,
        FluxWrongRoomID,
        InBitStreamEOS,
        InterpolationSampleBufferJitter,
        JSONSerialization,
        MissingDebugDefine,
        MissingTraceDefine,
        OutPacketStreamNull,
        PrefsFailedToSave,
        ReplicationManagerClientWorldCantAccessArchetype,
        ReplicationManagerClientWorldCantFindLOD,
        ReplicationManagerClientWorldMetaNotFound,
        ReplicationManagerClientWorldQueryFailedToResolveParent,
        ReplicationManagerOutConnectionUnknownClientMessage,
        ReplicationManagerOutConnectionSceneIndexChangeEntityNotFound,
        ReplicationManagerInPacketReaderFloatingOriginNaN,
        ReplicationManagerInPacketReaderUnsupportedMessage,
        ReplicationManagerInPacketReaderInvalidRouting,
        ReplicationManagerInputReceiverNotFound,
        ReplicationManagerOrderedMessageExpired,
        ReplicationServerEntityManagerUnknownRequestType,
        ReplicationServerAlreadyRunning,
        ReplicationServerHighMemoryPressure,
        ReplicationServerCriticalMemoryPressure,
        ReplicationServerHTTPAPIResponse,
        ReplicationServerRestrictedMode,
        RuntimeAnalyticsFailedToWrite,
        RuntimeCloudAuthClientMsg,
        RuntimeCloudDeserializationException,
        RuntimeCloudAuthDoubleLoginMsg,
        RuntimeCloudLoginFailedMsg,
        RuntimeCloudAuthSessionTokenRefreshingMsg,
        RuntimeCloudFriendsCatchUpFailed,
        RuntimeCloudGameServicesFailedKVSync,
        RuntimeCloudGameServicesInvalidKey,
        RuntimeCloudGameServicesResourceLeak,
        RuntimeCloudGameServicesShutdownError,
        RuntimeCloudGameServersTooManyRequests,
        RuntimeCloudRequestFactoryResourceLeak,
        RuntimeCloudRoomsInvalidRegion,
        RuntimeInternalException,
        RuntimeRequestFailed,
        RuntimeRequestNoKey,
        [System.Obsolete("RuntimeResponseFailed is no longer used and will be removed in a future version.")]
        RuntimeResponseFailed,
        RuntimeWebsocketClosedPrematurely,
        RuntimeWebsocketRequestFailed,
        RuntimeWebsocketCallbackNotFoundForFailedRequest,
        RuntimeWebsocketRequestTimedOut,
        RuntimeWebsocketWebError,
        RuntimeWebsocketResourceLeak,
        RuntimeWebsocketClosedByServer,
        RuntimeConnectionError,
        SerializeEntityTooBig,
        SerializeMessageTooBig,
        ServerTransportNewSessionID,
        SimulatorFrameRateChanged,
        SteamTransportAlreadyClosed,
        TCPTranportInvalidState,
        ThreadResumerSuspendedThreadFound,
        ThreadResumerLongSearch,
        ToolkitArchetypeComponentAlreadyBound,
        ToolkitAuthorityAbandon,
        ToolkitAuthorityAdopt,
        ToolkitAuthorityManagerRequest,
        ToolkitAuthorityManagerTransfer,
        ToolkitBakedSyncCreateEntityMissingArchetype,
        ToolkitBakedSyncReceiveCommandUnhandled,
        ToolkitBindingDescriptorAmbiguousOnValueSynced,
        ToolkitBindingDescriptorFailedToExtractParameterType,
        ToolkitBindingFailedToReloadComponentFieldInfo,
        ToolkitBindingFailedToReloadComponentFieldSimFrameInfo,
        ToolkitBindingFailedToReloadFieldInfo,
        ToolkitBindingFailedToReloadMethodInfo,
        ToolkitBindingFailedToCheckForMeta,
        ToolkitBindingFailedToReloadPropertyInfo,
        ToolkitBindingMissingComponent,
        ToolkitBindingObsolete,
        ToolkitBindingOnValueSyncedNotSynced,
        ToolkitBindingInterpolateException,
        ToolkitBindingIsDirtyException,
        ToolkitBindingInterpolateInvalidBinding,
        ToolkitBindingSampleInvalidBinding,
        ToolkitSyncUpdateInvalidBindingGroup,
        ToolkitSyncInstantiatorException,
        ToolkitSyncCommandReceiveException,
        ToolkitSyncCommandSendException,
        ToolkitSyncDuplicateConfig,
        ToolkitBridgeEnableRunInBackground,
        ToolkitBridgeNotInitialized,
        ToolkitBridgeReplacedByMainBridge,
        ToolkitBridgeDestroyed,
        ToolkitClientConnectionManagerQueryError,
        ToolkitClientConnectionSceneChangeWrongOwner,
        ToolkitCommandBridgeDisconnected,
        ToolkitCommandMissing,
        ToolkitCommandValidateArguments,
        ToolkitCommandValidateArgumentTypes,
        ToolkitEntitiesManagerMaxEntities,
        ToolkitEntitiesManagerMaxQueries,
        ToolkitEntitiesManagerUnauthorizedCreate,
        ToolkitEntitiesManagerMapperInvalid,
        ToolkitEntitiesManagerMapperDuplicate,
        ToolkitEntitiesManagerRemoteEntityInvalid,
        ToolkitBindingSampleException,
        ToolkitFloatingOriginOutOfRange,
        ToolkitGenericMessageReceiveUnknown,
        ToolkitGenericCommandSendMaxRefs,
        ToolkitGenericCommandSendNotExist,
        ToolkitGenericCommandSendToOwned,
        ToolkitInputNoBridge,
        ToolkitInputTooManyInputs,
        ToolkitInputServerNoAuth,
        ToolkitInputNotProducer,
        ToolkitInputUnexpectedReset,
        ToolkitInterfaceUnsupported,
        ToolkitNodeCantFindChildAtPath,
        ToolkitNodeFailedToParseSection,
        ToolkitNodeNotLocallySimulated,
        ToolkitNodePathTooLong,
        ToolkitSceneAlreadyExists,
        ToolkitSceneLoadSceneMoveNonRootEntity,
        ToolkitSceneLoaderMissingBridge,
        ToolkitSceneMissingBridge,
        ToolkitSceneMissingClient,
        ToolkitSceneMissingScene,
        ToolkitSceneNegativeIndex,
        ToolkitSceneReconnect,
        ToolkitSyncBakedBindingNoScript,
        ToolkitSyncBakedScriptFailedToLoad,
        ToolkitSyncBakedScriptMissingType,
        ToolkitSyncBakedScriptNoTypeStored,
        ToolkitSyncCommandInvalidRouting,
        ToolkitSyncCommandMissingBridge,
        ToolkitSyncCommandMissingEntity,
        ToolkitSyncComponentActionNull,
        ToolkitSyncDestroyNonAuthority,
        ToolkitSyncMissingProvider,
        ToolkitSyncSceneMissingBridge,
        ToolkitSyncSetLODNoLODSupport,
        ToolkitSyncUpdateBindingNull,
        ToolkitSyncUpdateConnectedEntityParentMissingBinds,
        ToolkitSyncUpdateConnectedEntityParentNoSync,
        ToolkitSyncUpdateMissingBridge,
        ToolkitSyncUpdateMissingComponent,
        ToolkitSyncUpdateDestroyNotSupported,
        ToolkitSyncUpdateUnsupportedReflection,
        ToolkitSyncValidateConnectedEntityParent,
        ToolkitSyncValidateLifetimeType,
        ToolkitSyncValidateTransferType,
        ToolkitSyncMissingConfig,
        ToolkitSyncGameObjectSetInactive,
        ToolkitSyncAuthorityRequestRejected,
        ToolkitTagQueryMissingTag,
        ToolkitBridgeLoginFailed,
        ToolkitBindingMarkAsDirtyNonManual,
        TransportReceivedFromInvalidIP,
        TransportWrongRoomID,
        ConfigurationWindowWarning,
        AnotherBridgeAlreadyConnectedToMainPlayerAccount,
        PlayerAccountProviderNotReady,

        PingServiceListenerCloseError,
        PingServiceSocketError,

        // Special warnings for integration tests
        IntegrationTest,
        IntegrationTestRS,

        ToolkitCommandValidateAuthorityOrphaned,
        ReplicationManagerCommandAuthorityOnlyOrphaned,
        ReplicationManagerInvalidKickRequest,
        TendLessThan1Byte,
        TendInvalidPacket,
        ToolkitBridgeMissingValidateConnectionRequest,
        ToolkitBridgeConnectionValidationMultipleListeners,
        ToolkitSyncAuthorityRequestMultipleListeners,
        ContentUtilsWarning,
        RuntimeRequestResponse,
        EditorPortalUpdateSyncStateFail,
        CoreFragmentationVeryBigData,
        InBufferDroppedExpiredEntityChange,
        InBufferDroppedExpiredMessage,
        InBufferTooManyEntities,
        BriskFrequencyConnectionThrottled,
        ConnectionClosedTooManyWarnings,
        ConnectionClosedTooManyErrors,
        TransportMuxerUnknownRoomID,
        RoomsHandlerSchemaMismatch,
        ReplicationServerAnalyticsEventFailed,
    }

    public static partial class LogTextMap
    {
        public static string GetText(this Warning id)
#pragma warning disable CS8524
            => id switch
#pragma warning restore CS8524
            {
                Warning.BoundsPositionInvalid => "Detected invalid position.",
                Warning.BoundsVariable => "Variable out of bounds.",
                Warning.BriskFailedToSendDisconnect => "Failed to send disconnect message.",
                Warning.BriskOutPacketStreamNull => "OutPacket.Stream is null; replacing.",
                Warning.BriskServerConnectionResendNull => "tried to resend 'null' lastSentReliablePacket",
                Warning.BriskServerConnectionReceivedLargerThanMTU => "Received packet larger than max MTU",
                Warning.BriskUnknownOOB => "Unknown signal.",
                Warning.CodeGenWarning => "", // Uses WarningMsg
                Warning.CoreClientConnectFailed => "Connection attempt failed",
                Warning.CoreClientAuthTransferNoAuthority => "Tried to transfer authority of an entity with no authority",
                Warning.CoreClientAuthInsufficient => "Insufficient authority for transfer",
                Warning.CoreClientAuthCantOrphanInput => "Input authority cannot be orphaned",
                Warning.CoreClientAuthTransferFailed => "", // Uses WarningMsg
                Warning.CoreClientCommandUnownedEntity => "Received command for non-owned entity",
                Warning.CoreInConnectionFloatingOriginNaN => "Received floating origin with NaN component(s).",
                Warning.CoreInConnectionFloatingOriginDelta => "Delta between received floating origin and current floating origin is larger than float.MaxValue.",
                Warning.CoreInConnectionPacketFullOfMessages => "We received a packet filled with messages (commands, inputs, etc.). This means that regular entity updates might become delayed.",
                Warning.CoreInNetworkChannelUnknownMessage => "Received unsupported message type. Please make sure that all changes are baked and both the client and the Replication Server use the latest schema. If the issue persists please contact the coherence development team.",
                Warning.EditorBakeUtilGenerateSchemaNoSchema => "Not using CoherenceSync Schemas in Project Settings or Extra Schemas in Runtime Settings. Bake aborted.",
                Warning.EditorBakeUtilInProgress => "Baking operation in process. Wait until it is completed.",
                Warning.EditorBugReportCreateFile => "Failed to create file with diagnostics information.",
                Warning.EditorBugReportDisplayException => "", // Uses WarningMsg
                Warning.EditorBuildUploadValidator => "", // Uses WarningMsg
                Warning.EditorHubTrackerIssueWarning => "", // Uses WarningMsg
                Warning.EditorNetworkPrefabProcessorUpdateMissingScripts => "", // Uses WarningMsg
                Warning.EditorPortalFetchLogin => "", // Uses WarningMsg
                Warning.EditorPortalFetchParsingSDK => "", // Uses WarningMsg
                Warning.EditorPortalRequestCreditsExceeded => "Credit limit exceeded. Some requests for resources may not work. Please visit https://coherence.io/dashboard to review your plan.",
                Warning.EditorPortalRequestFailed => "", // Uses WarningMsg
                Warning.EditorPortalRequestInvalidToken => "Login session token is invalid or has expired. Please open the 'Cloud' tab in the coherence Hub window and log in again.",
                Warning.EditorPortalRequestSDKDeprecated => "", // Uses WarningMsg
                Warning.EditorPortalRequestProjectDeleted => "Project has been deleted. Please select a new project in the online tab of coherence Hub.",
                Warning.EditorPortalStartLogin => "", // Uses WarningMsg
                Warning.EditorPostprocessorGetSchemaAssetsFailure => "", // Uses WarningMsg
                Warning.EditorSchemaCreatorDuplicateNames => "", // Uses WarningMsg
                Warning.EditorSchemaCreatorInvalidBindings => "", // Uses WarningMsg
                Warning.EditorSchemaCreatorInvalidComponents => "", // Uses WarningMsg
                Warning.EditorSchemaCreatorNoLODs => "", // Uses WarningMsg
                Warning.EditorSchemaCreatorNoComponents => "", // Uses WarningMsg
                Warning.EditorSchemaCreatorInvalidLODs => "", // Uses WarningMsg
                Warning.EditorConfigurationWindowContextMenuWarning => "", // Uses WarningMsg
                Warning.EntityManagerAuthorityRequestClientNotFound => "client requesting authority not found",
                Warning.EntityManagerAuthorityRequestParticipantMismatch => "client requesting the authority does not match sender participant",
                Warning.EntityManagerAuthorityTransferFailed => "authority transfer failed",
                Warning.EntityManagerAuthorityOrphanInput => "entity transfer failed: input cannot be orphaned",
                Warning.EntityManagerAuthorityTransferFailedOwnerNotFound => "entity transfer failed: new authority not found",
                Warning.EntityManagerConnectedEntityCycle => "connected entity cycle",
                Warning.EntityManagerLiveQueryOutOfRange => "live query position is out of range",
                Warning.EntityManagerRelativePositionOutOfRange => "relative position of an entity is out of supported range",
                Warning.EntityManagerWorldPositionOutOfRange => "World position is out of range",
                Warning.FluxDisableAddressReuse => "Failed to disable address reuse",
                Warning.FluxInvalidRoomID => "Received packet with missing roomId. Ignoring this packet.",
                Warning.FluxReceivedFromInvalidIP => "Received packet from wrong IPEndPoint. Ignoring.",
                Warning.FluxWrongRoomID => "Received packet with wrong roomId. Ignoring.",
                Warning.InBitStreamEOS => "Trying to read past end of stream.",
                Warning.InterpolationSampleBufferJitter => "Trying to get a sample from an empty sample buffer. This will return a default sample and cause the binding to jitter to 0 and back. This shouldn't happen, thus something went wrong.",
                Warning.JSONSerialization => "JSON serialization error.",
                Warning.MissingDebugDefine => $"Selected LogLevel includes debug logs, but no debug logs will be printed because scripting define is missing. Please add {LogConditionals.Debug} to your scripting define symbols.",
                Warning.MissingTraceDefine => $"Selected LogLevel includes trace logs, but no trace logs will be printed because scripting define is missing. Please add {LogConditionals.Trace} to your scripting define symbols.",
                Warning.OutPacketStreamNull => "OutPacket stream can not be null.",
                Warning.PrefsFailedToSave => "Failed to save preferences file on shutdown.",
                Warning.ReplicationManagerClientWorldCantAccessArchetype => "can't access archetype",
                Warning.ReplicationManagerClientWorldCantFindLOD => "couldn't find LODForDistance",
                Warning.ReplicationManagerClientWorldMetaNotFound => "meta not found",
                Warning.ReplicationManagerClientWorldQueryFailedToResolveParent => "query failed to resolve parent entity",
                Warning.ReplicationManagerOutConnectionUnknownClientMessage => "unknown client message",
                Warning.ReplicationManagerOutConnectionSceneIndexChangeEntityNotFound => "SceneIndexChangedCommand: entity not found or not owned by this client",
                Warning.ReplicationManagerInPacketReaderFloatingOriginNaN => "Received floating origin with NaN component(s).",
                Warning.ReplicationManagerInPacketReaderUnsupportedMessage => "", // Uses WarningMsg
                Warning.ReplicationManagerInPacketReaderInvalidRouting => "invalid command routing",
                Warning.ReplicationManagerInputReceiverNotFound => "message receiver not found",
                Warning.ReplicationManagerOrderedMessageExpired => "Ordered message expired",
                Warning.ReplicationServerEntityManagerUnknownRequestType => "unknown request type",
                Warning.RuntimeAnalyticsFailedToWrite => "Failed to send analytics event",
                Warning.RuntimeCloudAuthClientMsg => "", // Uses WarningMsg
                Warning.RuntimeCloudAuthDoubleLoginMsg => "", // Uses WarningMsg
                Warning.RuntimeCloudLoginFailedMsg => "", // Uses WarningMsg
                Warning.RuntimeCloudAuthSessionTokenRefreshingMsg => "", // Uses WarningMsg
                Warning.RuntimeCloudDeserializationException => "deserialization exception",
                Warning.RuntimeCloudFriendsCatchUpFailed => "Failed to catch up on friends after reconnecting.",
                Warning.RuntimeCloudGameServicesFailedKVSync => "Failed to perform key-value store sync",
                Warning.RuntimeCloudGameServicesInvalidKey => "Invalid KvStore entry key. Allowed characters: A-z0-9_-",
                Warning.RuntimeCloudGameServicesResourceLeak => "", // Uses WarningMsg
                Warning.RuntimeCloudGameServicesShutdownError => "", // Uses WarningMsg
                Warning.RuntimeCloudGameServersTooManyRequests => "Game server request throttled. Please slow down the rate of sending requests and try again.",
                Warning.RuntimeCloudRequestFactoryResourceLeak => "", // Uses WarningMsg
                Warning.RuntimeCloudRoomsInvalidRegion => "", // Uses WarningMsg
                Warning.RuntimeRequestFailed => "Request failed",
                Warning.RuntimeRequestNoKey => "Executing Play request without a runtime key. Please, set a valid runtime key.",
#pragma warning disable CS0618
                Warning.RuntimeResponseFailed => "Response failed",
#pragma warning restore CS0618
                Warning.RuntimeWebsocketClosedPrematurely => "Connection closed prematurely",
                Warning.RuntimeWebsocketRequestFailed => "Request failed",
                Warning.RuntimeWebsocketCallbackNotFoundForFailedRequest => "Callback not found for failed request",
                Warning.RuntimeWebsocketRequestTimedOut => "Request timed out",
                Warning.RuntimeWebsocketWebError => "", // Uses WarningMsg.
                Warning.RuntimeWebsocketResourceLeak => "", // Uses WarningMsg.
                Warning.RuntimeWebsocketClosedByServer => "", // Uses WarningMsg.
                Warning.RuntimeConnectionError => "", // Uses WarningMsg.
                Warning.RuntimeInternalException => "", // Uses WarningMsg.
                Warning.ReplicationServerAlreadyRunning => "Replication Server is already running.",
                Warning.ReplicationServerHighMemoryPressure => "Memory pressure is high.",
                Warning.ReplicationServerCriticalMemoryPressure => "Memory pressure is critical.",
                Warning.ReplicationServerHTTPAPIResponse => "Response",
                Warning.ReplicationServerRestrictedMode => "Unlock token isn't provided, the Replication Server will run in restricted mode with limited CCU. Unlock token can be retrieved from the coherence dashboard after registering at coherence.io.",
                Warning.SerializeEntityTooBig => "Failed to serialize entity because its data is too large to fit inside of an empty packet. This entity will probably never get synchronized. You can remove some bindings, send less data in binding with variable size (strings and byte arrays), or you can use Fragmented channel for synchronization of this entity - but Fragmented channels shouldn't be used for data requiring low latency.",
                Warning.SerializeMessageTooBig => "Failed to serialize an input/command because it's too large to fit inside of an empty packet. This input/command will probably never get sent. You can remove some parameters, send less data in parameters with variable size (strings and byte arrays), or you can send it over the Fragmented channel - but Fragmented channels shouldn't be used for data requiring low latency.",
                Warning.ServerTransportNewSessionID => "Got packet with wrong session ID for a new connection.",
                Warning.SimulatorFrameRateChanged => "", // Uses WarningMsg
                Warning.SteamTransportAlreadyClosed => "Steam Transport is already closed",
                Warning.TCPTranportInvalidState => "Tried to open connection in invalid state",
                Warning.ThreadResumerSuspendedThreadFound => "Suspended thread found, resuming",
                Warning.ThreadResumerLongSearch => "Finding suspended threads took too long",
                Warning.ToolkitArchetypeComponentAlreadyBound => "", // Uses WarningMsg
                Warning.ToolkitAuthorityAbandon => "", // Uses WarningMsg
                Warning.ToolkitAuthorityAdopt => "", // Uses WarningMsg
                Warning.ToolkitAuthorityManagerRequest => "", // Uses WarningMsg
                Warning.ToolkitAuthorityManagerTransfer => "", // Uses WarningMsg
                Warning.ToolkitBakedSyncCreateEntityMissingArchetype => "", // Uses WarningMsg
                Warning.ToolkitBakedSyncReceiveCommandUnhandled => "", // Uses WarningMsg
                Warning.ToolkitBindingDescriptorAmbiguousOnValueSynced => "", // Uses WarningMsg
                Warning.ToolkitBindingDescriptorFailedToExtractParameterType => "", // Uses WarningMsg
                Warning.ToolkitBindingFailedToReloadComponentFieldInfo => "", // Uses WarningMsg
                Warning.ToolkitBindingFailedToReloadComponentFieldSimFrameInfo => "", // Uses WarningMsg
                Warning.ToolkitBindingFailedToReloadFieldInfo => "", // Uses WarningMsg
                Warning.ToolkitBindingFailedToReloadMethodInfo => "", // Uses WarningMsg
                Warning.ToolkitBindingFailedToCheckForMeta => "", // Uses WarningMsg
                Warning.ToolkitBindingFailedToReloadPropertyInfo => "", // Uses WarningMsg
                Warning.ToolkitBindingMissingComponent => "", // Uses WarningMsg
                Warning.ToolkitBindingObsolete => "", // Uses WarningMsg
                Warning.ToolkitBindingOnValueSyncedNotSynced => "", // Uses WarningMsg
                Warning.ToolkitBindingInterpolateException => "", // Uses ErrorMsg.
                Warning.ToolkitSyncUpdateInvalidBindingGroup => "", // Uses ErrorMsg.
                Warning.ToolkitSyncInstantiatorException => "", // Uses ErrorMsg.
                Warning.ToolkitSyncCommandReceiveException => "", // Uses ErrorMsg
                Warning.ToolkitSyncCommandSendException => "", // Uses ErrorMsg
                Warning.ToolkitSyncDuplicateConfig => "", // Uses WarningMsg
                Warning.ToolkitBindingIsDirtyException => "", // Uses ErrorMsg.
                Warning.ToolkitBindingInterpolateInvalidBinding => "", // Uses ErrorMsg.
                Warning.ToolkitBindingSampleInvalidBinding => "", // Uses ErrorMsg.
                Warning.ToolkitBridgeEnableRunInBackground => "", // Uses WarningMsg
                Warning.ToolkitBridgeNotInitialized => "Cannot connect: Bridge is not initialized",
                Warning.ToolkitBridgeReplacedByMainBridge => "", // Uses WarningMsg
                Warning.ToolkitBridgeDestroyed => "", // Uses WarningMsg
                Warning.ToolkitBridgeLoginFailed => "", // Uses WarningMsg
                Warning.ToolkitClientConnectionManagerQueryError => "", // Uses WarningMsg
                Warning.ToolkitClientConnectionSceneChangeWrongOwner => "", // Uses WarningMsg
                Warning.ToolkitCommandBridgeDisconnected => "", // Uses WarningMsg
                Warning.ToolkitCommandMissing => "", // Uses WarningMsg
                Warning.ToolkitCommandValidateArguments => "", // Uses WarningMsg
                Warning.ToolkitCommandValidateArgumentTypes => "", // Uses WarningMsg
                Warning.ToolkitEntitiesManagerMaxEntities => "", // Uses WarningMsg
                Warning.ToolkitEntitiesManagerMaxQueries => "", // Uses WarningMsg
                Warning.ToolkitEntitiesManagerUnauthorizedCreate => "", // Uses WarningMsg
                Warning.ToolkitEntitiesManagerMapperInvalid => "", // Uses WarningMsg
                Warning.ToolkitEntitiesManagerMapperDuplicate => "", // Uses WarningMsg
                Warning.ToolkitEntitiesManagerRemoteEntityInvalid => "", // Uses WarningMsg
                Warning.ToolkitBindingSampleException => "", // Uses WarningMsg
                Warning.ToolkitFloatingOriginOutOfRange => "Setting your floating origin outside of 64-bit precise range will lead to bad precision. To have sub-1mm precision set your floating origin within a range of 10^13.",
                Warning.ToolkitGenericMessageReceiveUnknown => "", // Uses WarningMsg
                Warning.ToolkitGenericCommandSendMaxRefs => "", // Uses WarningMsg
                Warning.ToolkitGenericCommandSendNotExist => "", // Uses WarningMsg
                Warning.ToolkitGenericCommandSendToOwned => "", // Uses WarningMsg
                Warning.ToolkitInputNoBridge => "", // Uses WarningMsg
                Warning.ToolkitInputTooManyInputs => "", // Uses WarningMsg
                Warning.ToolkitInputServerNoAuth => "", // Uses WarningMsg
                Warning.ToolkitInputNotProducer => "", // Uses WarningMsg
                Warning.ToolkitInputUnexpectedReset => "", // Uses WarningMsg
                Warning.ToolkitInterfaceUnsupported => "", // Uses WarningMsg
                Warning.ToolkitNodeCantFindChildAtPath => "", // Uses WarningMsg
                Warning.ToolkitNodeFailedToParseSection => "", // Uses WarningMsg
                Warning.ToolkitNodeNotLocallySimulated => "", // Uses WarningMsg
                Warning.ToolkitNodePathTooLong => "The parent to child path is too long to be serialized.",
                Warning.ToolkitSceneAlreadyExists => "A CoherenceScene component is already registered for this scene. Disabling.",
                Warning.ToolkitSceneLoadSceneMoveNonRootEntity => "LoadScene is trying to bring along an entity which is not a root. Will ignore this entity.",
                Warning.ToolkitSceneLoaderMissingBridge => "Couldn't find a valid Bridge to attach to",
                Warning.ToolkitSceneMissingBridge => "Couldn't find a valid Bridge associated to scene",
                Warning.ToolkitSceneMissingClient => "Couldn't find a client in scene. Is there a CoherenceBridge associated with this scene?",
                Warning.ToolkitSceneMissingScene => "Scene has no active CoherenceScene. Instantiating one.",
                Warning.ToolkitSceneNegativeIndex => "Can't use negative scene index",
                Warning.ToolkitSceneReconnect => "", // Uses WarningMsg
                Warning.ToolkitSyncBakedBindingNoScript => "Cannot fetch baked binding for without having a instantiated baked script.",
                Warning.ToolkitSyncBakedScriptFailedToLoad => "Couldn't load baked script. Using reflection.",
                Warning.ToolkitSyncBakedScriptMissingType => "", // Uses WarningMsg
                Warning.ToolkitSyncBakedScriptNoTypeStored => "", // Uses WarningMsg
                Warning.ToolkitSyncCommandInvalidRouting => "Received message with invalid routing.",
                Warning.ToolkitSyncCommandMissingBridge => "", // Uses WarningMsg
                Warning.ToolkitSyncCommandMissingEntity => "", // Uses WarningMsg
                Warning.ToolkitSyncComponentActionNull => "", // Uses WarningMsg
                Warning.ToolkitSyncDestroyNonAuthority => "", // Uses WarningMsg
                Warning.ToolkitSyncMissingProvider => "Trying to instantiate prefab but is missing a provider or instantiator implementation.",
                Warning.ToolkitSyncSceneMissingBridge => "Trying to instantiate prefab in a Scene that is not synced with coherence via a CoherenceBridge.",
                Warning.ToolkitSyncSetLODNoLODSupport => "", // Uses WarningMsg
                Warning.ToolkitSyncUpdateBindingNull => "", // Uses WarningMsg
                Warning.ToolkitSyncUpdateConnectedEntityParentMissingBinds => "", // Uses WarningMsg
                Warning.ToolkitSyncUpdateConnectedEntityParentNoSync => "", // Uses WarningMsg
                Warning.ToolkitSyncUpdateMissingBridge => "", // Uses WarningMsg
                Warning.ToolkitSyncUpdateMissingComponent => "",
                Warning.ToolkitSyncUpdateDestroyNotSupported => "", // Uses WarningMsg
                Warning.ToolkitSyncUpdateUnsupportedReflection => "", // Uses WarningMsg
                Warning.ToolkitSyncValidateConnectedEntityParent => "", // Uses WarningMsg
                Warning.ToolkitSyncValidateLifetimeType => "", // Uses WarningMsg
                Warning.ToolkitSyncValidateTransferType => "", // Uses WarningMsg
                Warning.ToolkitSyncMissingConfig => "", // Uses WarningMsg
                Warning.ToolkitSyncAuthorityRequestRejected => "", // Uses WarningMsg
                Warning.ToolkitTagQueryMissingTag => "Tag query has no tag, will not be able to query.",
                Warning.ToolkitSyncGameObjectSetInactive => "", // Uses WarningMsg
                Warning.ToolkitBindingMarkAsDirtyNonManual => "", // Uses WarningMsg
                Warning.TransportReceivedFromInvalidIP => "Data from invalid endpoint.",
                Warning.TransportWrongRoomID => "Packet with wrong roomId.",
                Warning.ConfigurationWindowWarning => "", // Uses WarningMsg
                Warning.AnotherBridgeAlreadyConnectedToMainPlayerAccount => "", // Uses WarningMsg
                Warning.PlayerAccountProviderNotReady => "", // Uses WarningMsg

                Warning.PingServiceListenerCloseError => "Failed to close ping service listener.",
                Warning.PingServiceSocketError => "Ping service socket error.",

                // Special warnings for integration tests.
                Warning.IntegrationTest => "", // Uses WarningMsg.
                Warning.IntegrationTestRS => "", // Uses WarningMsg.

                Warning.ToolkitCommandValidateAuthorityOrphaned => "", // Uses WarningMsg
                Warning.ReplicationManagerCommandAuthorityOnlyOrphaned => "Trying to send a command to AuthorityOnly on an orphaned entity",
                Warning.ReplicationManagerInvalidKickRequest => "Invalid kick request",

                Warning.TendInvalidPacket => "Invalid tend packet - exception contains more information",
                Warning.TendLessThan1Byte => "Invalid tend packet - header too small",
                Warning.ToolkitBridgeMissingValidateConnectionRequest => $"Missing onValidateConnectionRequest callback. All connection requests will by accepted by default. Please subscribe to onValidateConnectionRequest event to validate connection requests, or disable HostAuthority.ValidateConnection feature if you aren't using it.",
                Warning.ToolkitBridgeConnectionValidationMultipleListeners => "onValidateConnectionRequest should have only one listener subscribed.",
                Warning.ToolkitSyncAuthorityRequestMultipleListeners => "OnAuthorityRequest should have only one listener subscribed.",
                Warning.ContentUtilsWarning => "", // Uses WarningMsg.
                Warning.RuntimeRequestResponse => "Failed to parse request error body",
                Warning.EditorPortalUpdateSyncStateFail => "", // Uses WarningMsg.
                Warning.CoreFragmentationVeryBigData => "", // Uses WarningMsg.
                Warning.InBufferDroppedExpiredEntityChange => "Dropped an expired entity change",
                Warning.InBufferDroppedExpiredMessage => "Dropped an expired entity message",
                Warning.InBufferTooManyEntities => "Too many entities",
                Warning.BriskFrequencyConnectionThrottled => "Connection throttled",
                Warning.ConnectionClosedTooManyWarnings => "Connection closed due to too many warnings",
                Warning.ConnectionClosedTooManyErrors => "Connection closed due to too many errors",
                Warning.TransportMuxerUnknownRoomID => "Received packet for unknown room ID",
                Warning.RoomsHandlerSchemaMismatch => "The provided schema's hash doesn't match the provided schemaID",
                Warning.ReplicationServerAnalyticsEventFailed => "Failed to send analytics event",
            };
    }
}
