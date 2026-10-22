// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Toolkit.ReplicationServer
{
    using System;
    using System.Collections.Concurrent;
    using System.Diagnostics;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Log;
    using Plugins.NativeLauncher;
    using Plugins.NativeUtils;
    using Debug = UnityEngine.Debug;

    public class ReplicationServer : IReplicationServer
    {
        private Process process;
        private NlProcess nlProcess;
        private readonly bool nativeProcess;
        private readonly ushort apiPort;

        private CancellationTokenSource cancellationTokenSource;
        private readonly ConcurrentQueue<string> logQueue = new();
        private ThreadResumer threadResumer;

        private volatile bool isReady;

        private readonly Logger logger = Log.GetLogger<ReplicationServer>();

        /// <summary>
        /// Occurs when the server creates a log entry.
        /// </summary>
        public event LogHandler OnLog;
        /// <summary>
        /// Occurs when the server has shut down for any reason.
        /// </summary>
        public event ExitHandler OnExit;

        /// <summary>
        /// Returns true when the Replication Server API is ready to accept connections.
        /// </summary>
        public bool IsReady => isReady;

        public bool HasExited => nativeProcess ? nlProcess?.HasExited ?? true : process?.HasExited ?? true;

        internal ReplicationServer(Process process, ushort apiPort)
        {
            this.process = process;
            this.apiPort = apiPort;
            nativeProcess = false;
            cancellationTokenSource = new CancellationTokenSource();

            process.OutputDataReceived += OnDataReceivedEventHandler;
            process.ErrorDataReceived += OnDataReceivedEventHandler;
            process.Exited += OnProcessExited;
        }

        internal ReplicationServer(NlProcess process, ushort apiPort)
        {
            nlProcess = process;
            this.apiPort = apiPort;
            nativeProcess = true;
            cancellationTokenSource = new CancellationTokenSource();

            process.OutputDataReceived += OnDataReceivedEventHandler;
            process.ErrorDataReceived += OnDataReceivedEventHandler;
            process.Exited += OnProcessExited;
        }

        private void OnDataReceivedEventHandler(object sender, DataReceivedEventArgs args)
        {
            if (!string.IsNullOrEmpty(args.Data))
            {
                logQueue.Enqueue(args.Data);
            }
        }

        private void OnDataReceivedEventHandler(object sender, StreamDataReceivedEvent args)
        {
            if (!string.IsNullOrEmpty(args.Data))
            {
                logQueue.Enqueue(args.Data);
            }
        }

        private async Task PollHealthEndpoint(CancellationToken token)
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
            var url = $"http://127.0.0.1:{apiPort}/health";

            while (!token.IsCancellationRequested)
            {
                try
                {
                    var response = await client.GetAsync(url, token);
                    if (response.IsSuccessStatusCode)
                    {
                        isReady = true;
                        logger.Debug("Replication Server is ready to accept connections");
                        return;
                    }
                }
                catch (Exception) when (!token.IsCancellationRequested)
                {
                    // Server not up yet, retry
                }

                await Task.Delay(200, token);
            }
        }

        private async Task ForwardLogs(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                while (logQueue.TryDequeue(out var log))
                {
                    try
                    {
                        OnLog?.Invoke(log);
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e);
                    }
                }

                await Task.Yield();
            }
        }

        private void OnProcessExited(object sender, EventArgs args)
        {
            try
            {
                var exitCode = nativeProcess ? nlProcess.ExitCode : process.ExitCode;
                OnExit?.Invoke(exitCode);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            OnLog = null;
            OnExit = null;

            CleanupOnExit();
        }

        /// <summary>
        /// Start the Replication Server.
        /// </summary>
        /// <returns>`true` if the server has started; `false` if something went wrong.</returns>
        public bool Start()
        {
            var success = nativeProcess
                ? nlProcess?.Start() ?? false
                : process?.Start() ?? false;

            if (!success)
            {
                logger.Error(Error.ReplicationServerFailedToStart);
                return false;
            }

            threadResumer = new ThreadResumer(
                nativeProcess ? nlProcess.Id : process.Id,
                cancellationTokenSource.Token,
                RuntimeSettings.Instance.Advanced.ThreadResumer);

            _ = ForwardLogs(cancellationTokenSource.Token);
            _ = PollHealthEndpoint(cancellationTokenSource.Token);
            if (nativeProcess)
            {
                nlProcess.BeginOutputReadLine();
                nlProcess.BeginErrorReadLine();
            }
            else
            {
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
            }

            return true;
        }

        /// <summary>
        /// Stop the Replication Server. Optionally wait for the server to shut down entirely.
        /// </summary>
        /// <param name="timeoutMs"></param>
        /// <returns>`true` if the server has shut down; `false` if the server is still in progress of shutting down after waiting.</returns>
        public bool Stop(int timeoutMs = 1000) => nativeProcess ? StopNlProcess(timeoutMs) : StopProcess(timeoutMs);

        private bool StopProcess(int timeoutMs)
        {
            if (process == null)
            {
                return true;
            }

            process.Kill();
            var success = process.WaitForExit(timeoutMs);

            CleanupOnExit();

            return success;
        }

        private bool StopNlProcess(int timeoutMs)
        {
            if (nlProcess == null)
            {
                return true;
            }

            var success = nlProcess.Terminate(timeoutMs);

            CleanupOnExit();

            return success;
        }

        private void CleanupOnExit()
        {
            isReady = false;

            if (!nativeProcess && process is not null)
            {
                process.Exited -= OnProcessExited;
                process = null;
            }

            if (nativeProcess && nlProcess is not null)
            {
                nlProcess.Exited -= OnProcessExited;
                nlProcess.Dispose();
                nlProcess = null;
            }

            cancellationTokenSource.Cancel();
        }
    }
}
