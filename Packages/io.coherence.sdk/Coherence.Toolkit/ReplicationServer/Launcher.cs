// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Toolkit.ReplicationServer
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Text;
    using Coherence.Common;
    using Coherence.Log.Targets;
    using Coherence.ProtocolDef;
    using Plugins.NativeLauncher;
    using UnityEngine;

    /// <summary>
    /// Specifies the shell type for command formatting.
    /// </summary>
    public enum ShellType
    {
        /// <summary>
        /// Bash or Windows Command Prompt (cmd.exe).
        /// </summary>
        BashAndCmd,

        /// <summary>
        /// Windows PowerShell.
        /// </summary>
        PowerShell,
    }

    public static class Launcher
    {
        internal static IConfigProvider ConfigProvider = new StandaloneLauncherConfig();

        internal static IReplicationServer Start(ReplicationServerConfig serverConfig, bool startInTerminal,
            string additionalArguments = null)
        {
            var executablePath = ConfigProvider.ExecutablePath;
            var arguments = GenerateArguments(serverConfig, additionalArguments);

            ProcessUtil.FixUnixPermissions(executablePath);

            var command = ProcessUtil.CommandFromExecutableAndArguments(executablePath, arguments);
            var process = startInTerminal
                ? ProcessUtil.RunInTerminal(command)
                : ProcessUtil.RunOutsideTerminal(executablePath, arguments);

            return new ReplicationServer(process, serverConfig.APIPort);
        }

        /// <summary>
        /// Create a new `ReplicationServer` instance with the given configuration.
        /// Has to be started using `Start`, and allows `OnLog` and `OnExit` events to be subscribed to.
        /// </summary>
        /// <param name="serverConfig">Configuration to use when starting the server.</param>
        /// <param name="additionalArguments">Optional flags to add when starting the server.</param>
        /// <returns>A new `ReplicationServer`, to be started with `Start`.</returns>
        public static IReplicationServer Create(ReplicationServerConfig serverConfig, string additionalArguments = null)
        {
            switch (Application.platform)
            {
                case RuntimePlatform.PS4:
                case RuntimePlatform.PS5:
                case RuntimePlatform.Switch:
                case RuntimePlatform.XboxOne:
                case RuntimePlatform.GameCoreXboxOne:
                case RuntimePlatform.GameCoreXboxSeries:
                case RuntimePlatform.Android:
                case RuntimePlatform.IPhonePlayer:
#if !COHERENCE_HAS_RSL
                    throw new InvalidOperationException("Console builds require special integration for client hosting. Please contact the coherence team for details.");
#else
                    serverConfig.UseLite = true;
                    break;
#endif
            }
#if COHERENCE_HAS_RSL
            if (serverConfig.UseLite)
            {
                if (ReplicationServerOverride.LaunchRSLite != null)
                {
                    var protocolDefinition = ProtocolSchemaParser.Parse(
                        RuntimeSettings.Instance.SchemaID,
                        RuntimeSettings.Instance.DefaultSchemas.Select(s => s.SchemaDefinition),
                        includeRsSchema: true);

                    return ReplicationServerOverride.LaunchRSLite.Invoke(protocolDefinition, serverConfig, additionalArguments);
                }

                throw new InvalidOperationException("Replication Server Lite not ready yet. Make sure to rebake after adding the Replication Server Lite package, and contact the coherence team if the issue persists.");
            }
#endif

            var executablePath = ConfigProvider.ExecutablePath;
            var arguments = GenerateArguments(serverConfig, additionalArguments);

            var startupInfo = new NlProcessStartupInfo(executablePath, arguments)
            {
                RaiseOnExit = true,
            };
            startupInfo.EnvironmentVariables.Add("RS_UNITY_VERSION", Application.unityVersion);

            var process = new NlProcess(startupInfo);

            if (process.IsProcessAlreadyRunning())
            {
                throw new System.Exception("RS is already running.");
            }

            return new ReplicationServer(process, serverConfig.APIPort);
        }

        private static string GenerateArguments(ReplicationServerConfig serverConfig, string additionalArguments = null)
        {
            additionalArguments ??= "";

            var typeArgument = serverConfig.Mode switch
            {
                Mode.World => "world",
                Mode.Rooms => "rooms",
                _ => throw new ArgumentException("Unsupported mode", nameof(serverConfig.Mode)),
            };

            var webPortArgument = serverConfig.WebPort.HasValue
                ? $" --web-port {serverConfig.WebPort}"
                : "";

            var timeoutArgument = serverConfig.DisconnectTimeout.HasValue
                ? $" --disconnect-timeout {serverConfig.DisconnectTimeout.Value}"
                : "";

            var debugStreamsArgument = (serverConfig.Mode == Mode.World && serverConfig.UseDebugStreams)
                ? " --debug-streams"
                : "";

            var disableThrottlingArgument = (serverConfig.DisableThrottling)
                ? " --disable-throttling"
                : "";

            var roomSecretArgument = (serverConfig.Mode == Mode.World && !string.IsNullOrEmpty(serverConfig.RoomSecret))
                ? $" --room-secret {serverConfig.RoomSecret}"
                : "";

            var logTargetsArguments = GenerateLogTargetArguments(serverConfig.LogTargets);

            var schemas = string.Join(",", ConfigProvider.GatherSchemaPaths());

            var arguments = typeArgument +
                            $" --api-port {serverConfig.APIPort}" +
                            $" --udp-port {serverConfig.UDPPort}" +
                            $" --signalling-port {serverConfig.SignallingPort}" +
                            webPortArgument +
                            $" --stats-port {serverConfig.StatsPort}" +
                            $" --send-frequency {serverConfig.SendFrequency}" +
                            $" --recv-frequency {serverConfig.ReceiveFrequency}" +
                            (serverConfig.PingPort != 0
                                ? $" --ping-port {serverConfig.PingPort}"
                                : string.Empty) +
                            (serverConfig.Mode == Mode.World
                                ? $" --schema \"{schemas}\""
                                : $" --default-schema \"{schemas}\"") +
                            debugStreamsArgument +
                            disableThrottlingArgument +
                            timeoutArgument +
                            logTargetsArguments +
                            roomSecretArgument +
                            $" {additionalArguments}";

            if (serverConfig.AutoShutdownTimeout != 0)
            {
                arguments += $" --auto-shutdown-timeout-ms {serverConfig.AutoShutdownTimeout}";
            }

            if (serverConfig.HostAuthority != 0 && serverConfig.Mode == Mode.World)
            {
                arguments += $" --host-authority-features {serverConfig.HostAuthority.GetCommaSeparatedFeatures()}";
            }

            if (serverConfig.MaxClients > 0)
            {
                arguments += $" --max-clients {serverConfig.MaxClients}";
            }

            if (serverConfig.MaxEntities > 0)
            {
                arguments += $" --max-entities {serverConfig.MaxEntities}";
            }

            if (serverConfig.MaxQueriesPerClient > 0)
            {
                arguments += $" --max-queries-per-client {serverConfig.MaxQueriesPerClient}";
            }

            if (serverConfig.RoomsCleanupIntervalSec > 0)
            {
                arguments += $" --cleanup-interval-sec {serverConfig.RoomsCleanupIntervalSec}";
            }

            if (!string.IsNullOrEmpty(serverConfig.UnlockToken))
            {
                arguments += $" --unlock-token {serverConfig.UnlockToken}";
            }

            return arguments;
        }

        internal static string GenerateLogTargetArguments(LogTargetConfig[] logTargets)
        {
            if (logTargets == null || logTargets.Length == 0)
            {
                return "";
            }

            var stringBuilder = new StringBuilder();

            foreach (var logTarget in logTargets)
            {
                _ = stringBuilder.Append(" --log-target ");
                _ = stringBuilder.Append(logTarget.ToString());
            }

            return stringBuilder.ToString();
        }

        internal static string ToCommand(ReplicationServerConfig serverConfig, string additionalArguments = null, ShellType shell = ShellType.BashAndCmd)
        {
            var executablePath = ConfigProvider.ExecutablePath;
            var arguments = GenerateArguments(serverConfig, additionalArguments);

            return shell == ShellType.PowerShell
                ? $"& \"{executablePath}\" {arguments}"
                : $"\"{executablePath}\" {arguments}";
        }
    }

    public class StandaloneLauncherConfig : IConfigProvider
    {
        public string ExecutablePath
        {
            get
            {
#if UNITY_STANDALONE_WIN
                return Path.GetFullPath($"{Application.streamingAssetsPath}/replication-server.exe");
#else
                return Path.GetFullPath($"{Application.streamingAssetsPath}/replication-server");
#endif
            }
        }

        public string[] GatherSchemaPaths() =>
            new[]
            {
                Path.GetFullPath($"{Application.streamingAssetsPath}/combined.schema"),
            };
    }
}
