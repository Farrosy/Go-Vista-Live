// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Toolkit.ReplicationServer
{
    using System;
    using Coherence.Common;
    using Coherence.Log;
    using Coherence.Log.Targets;

    public delegate void LogHandler(string log);
    public delegate void ExitHandler(int code);

    public interface IReplicationServer
    {
        public event LogHandler OnLog;
        public event ExitHandler OnExit;

        public bool IsReady { get; }
        public bool HasExited { get; }

        public bool Start();
        public bool Stop(int timeoutMs = 1000);
    }

    [Serializable]
    public struct ReplicationServerConfig
    {
#if COHERENCE_HAS_RSL
        public bool UseLite;
#endif
        public Mode Mode;
        public ushort APIPort;
        public ushort UDPPort;
        public ushort SignallingPort;
        [NonSerialized]
        public ushort? WebPort;
        public ushort PingPort;
        public ushort StatsPort;
        public int SendFrequency;
        public int ReceiveFrequency;
        public bool DisableThrottling;
        [NonSerialized]
        public uint? DisconnectTimeout;
        public LogTargetConfig[] LogTargets;
        public bool UseDebugStreams;
        public int AutoShutdownTimeout;
        public int RoomsCleanupIntervalSec;
        public HostAuthority HostAuthority;
        public uint MaxClients;
        public uint MaxEntities;
        public uint MaxQueriesPerClient;
        public string RoomSecret;
        public string UnlockToken;

        [NonSerialized]
        public Logger RSLLoggerOverride;
    }

    public enum Mode
    {
        World,
        Rooms
    }
}

namespace Coherence
{
#if COHERENCE_HAS_RSL
    using Coherence.Toolkit.ReplicationServer;
    using Coherence.ProtocolDef;

    public static class ReplicationServerOverride
    {
        public static System.Func<ProtocolDefinition, ReplicationServerConfig, string, IReplicationServer> LaunchRSLite = null;
    }
#endif
}
