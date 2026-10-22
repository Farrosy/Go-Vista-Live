// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

#if UNITY_5_3_OR_NEWER
// IMPORTANT: Used by the pure-dotnet client, DON'T REMOVE.
#define UNITY
#endif

namespace Coherence.Cloud
{
    using Common;
    using Log;
    using Runtime;
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Threading;
    using System.Threading.Tasks;
    using Logger = Log.Logger;

    /// <summary>
    /// Service that can be used to report <see cref="PlayerAccount">player accounts</see> for policy violations.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Reports are stored durably by coherence Cloud and surfaced to the project operator (game developer)
    /// via the Online Dashboard's report-management UI.
    /// </para>
    /// <para>
    /// Reports are <em>not</em> forwarded to external platforms (PSN, Xbox, Switch, etc.) - handle
    /// platform-native reporting through each platform's client UI instead.
    /// </para>
    /// <para>
    /// This service  satisfies the "in-game player report" requirement that platform certification
    /// (and many storefronts) impose on multiplayer titles.
    /// </para>
    /// </remarks>
    public class ReportsService : IDisposable
    {
        /// <summary>
        /// Raised when <see cref="SubmitReportAsync"/> has successfully submitted a report to the backend and received a report id back.
        /// </summary>
        internal event Action<ReportId, PlayerAccountId, ReportCategory, string> OnReportSubmitted;

        /// <summary>
        /// Raised when <see cref="SubmitReportAsync"/> has failed to submit a report or parse the backend response.
        /// </summary>
        internal event Action<Exception, PlayerAccountId, ReportCategory, string> OnReportSubmissionFailed;

        internal const string reportsSubmitEndpoint = "/reports";

        private readonly IRequestFactory requestFactory;
        private readonly IAuthClientInternal authClient;
        private readonly RequestThrottle throttle;
        private readonly Logger logger;
        private readonly bool shouldDisposeLogger;
        private readonly bool shouldDisposeRequestFactoryAndAuthClient;
        private readonly IPlayerAccountProvider playerAccountProviderToDispose;

        private bool disposed;

        internal ReportsService() { } // for mocking

        /// <summary>
        /// Initializes a new instance of the <see cref="ReportsService"/> class.
        /// </summary>
        /// <param name="runtimeSettings"> Optional runtime settings to use, or <see langword="null"/> to resolve automatically. </param>
        public ReportsService(IRuntimeSettings runtimeSettings = null)
            : this(null, runtimeSettings, playerAccountProvider: null, throttle: null) { }

        internal ReportsService([MaybeNull] CloudCredentialsPair credentialsPair, [MaybeNull] IRuntimeSettings runtimeSettings, [MaybeNull] IPlayerAccountProvider playerAccountProvider, [MaybeNull] RequestThrottle throttle, Logger logger = null)
        {
            if (logger is null)
            {
                shouldDisposeLogger = true;
                logger = Log.GetLogger<ReportsService>();
            }

            this.logger = logger;

            if (credentialsPair is null)
            {
                shouldDisposeRequestFactoryAndAuthClient = true;

                #if UNITY
                runtimeSettings ??= RuntimeSettings.Instance;
                #endif

                if (playerAccountProvider is null)
                {
                    playerAccountProvider = playerAccountProviderToDispose =
                        #if UNITY
                        SimulatorUtility.IsSimulator ? throw new ArgumentNullException(nameof(playerAccountProvider)) :
                        #endif
                        new NewPlayerAccountProvider(runtimeSettings: runtimeSettings);
                }

                credentialsPair =
                    #if UNITY
                    SimulatorUtility.IsSimulator ? CloudCredentialsFactory.ForSimulator(runtimeSettings, playerAccountProvider) :
                    #endif
                    CloudCredentialsFactory.ForClient(runtimeSettings, playerAccountProvider);
                credentialsPair.authClient.AutoLogin().Then(task =>
                {
                    if (task.IsFaulted)
                    {
                        logger.Warning(Warning.RuntimeCloudLoginFailedMsg, task.Exception?.ToString());
                    }
                    else if (task.IsCompletedSuccessfully && task.Result.Type is not Result.Success)
                    {
                        logger.Error(task.Result.Error, task.Result.ToString());
                    }
                });
            }

            this.requestFactory = credentialsPair.requestFactory;
            this.authClient = credentialsPair.authClient;
            this.throttle = throttle ?? credentialsPair.requestFactory.Throttle;
        }

        /// <summary>
        /// Submits a report against <paramref name="target"/> player account for a policy violation.
        /// </summary>
        /// <param name="target"> The id of the player account being reported. </param>
        /// <param name="category"> Category of the reported behavior. </param>
        /// <param name="notes">
        /// Optional player-supplied narrative. coherence Cloud currently accepts up to 500 Unicode code points, and
        /// fails longer notes with <see cref="ErrorCode.ReportsInvalidNotes"/>.
        /// </param>
        /// <param name="onRequestFinished"> Callback invoked with the resulting <see cref="ReportId"/> on
        /// success, or an <see cref="Exception"/> on failure. </param>
        /// <param name="cancellationToken"> Optional cancellation token. </param>
        /// <remarks>
        /// coherence Cloud limits how many reports a player can submit, currently 20 in any 24 hours, and how often
        /// they can report the same player account, currently once an hour.
        /// </remarks>
        public void SubmitReport(PlayerAccountId target, ReportCategory category, Action<RequestResponse<ReportId>> onRequestFinished, [MaybeNull] string notes = null, CancellationToken cancellationToken = default)
            => SubmitReportAsync(target, category, notes, cancellationToken).Then(task =>
                onRequestFinished?.Invoke(task.IsCompletedSuccessfully
                    ? new() { Status = RequestStatus.Success, Result = task.Result }
                    : new() { Status = RequestStatus.Fail, Exception = task.Exception?.GetBaseException() ?? new OperationCanceledException(cancellationToken.IsCancellationRequested ? cancellationToken : new(canceled: true)) }));

        /// <summary>
        /// Submits a report against <paramref name="target"/> player account for a policy violation.
        /// </summary>
        /// <param name="target"> The id of the player account being reported. </param>
        /// <param name="category"> Category of the reported behavior. </param>
        /// <param name="notes">
        /// Optional player-supplied narrative. coherence Cloud currently accepts up to 500 Unicode code points, and
        /// fails longer notes with <see cref="ErrorCode.ReportsInvalidNotes"/>.
        /// </param>
        /// <param name="cancellationToken"> Optional cancellation token. </param>
        /// <returns> The newly-generated <see cref="ReportId"/> on successful durable commit. </returns>
        /// <exception cref="RequestException">
        /// The backend rejected the report — see <see cref="RequestException.ErrorCode"/>
        /// for the specific cause (rate limit, invalid input, target not found, etc.). </exception>
        /// <remarks>
        /// coherence Cloud limits how many reports a player can submit, currently 20 in any 24 hours, and how often
        /// they can report the same player account, currently once an hour. Going over those limits fails the
        /// request with <see cref="ErrorCode.ReportsRateLimited"/> or <see cref="ErrorCode.ReportsTargetCooldown"/>
        /// respectively.
        /// </remarks>
        public async Task<ReportId> SubmitReportAsync(PlayerAccountId target, ReportCategory category, [MaybeNull] string notes = null, CancellationToken cancellationToken = default)
        {
            const string method = "POST";
            var body = SubmitReportRequest.GetRequestBody(target, category, notes);

            await throttle.WaitForCooldown(reportsSubmitEndpoint, method, cancellationToken);

            string textResponse;

            try
            {
                textResponse = await requestFactory.SendRequestAsync(reportsSubmitEndpoint, method, body, null, $"{nameof(ReportsService)}.{nameof(SubmitReportAsync)}", GetAuthToken());
            }
            catch (Exception exception)
            {
                OnReportSubmissionFailed?.Invoke(exception, target, category, notes);
                throw;
            }

            try
            {
                var parsed = Utils.CoherenceJson.DeserializeObject<SubmitReportResponse>(textResponse);
                var reportId = new ReportId(parsed.ReportId);
                OnReportSubmitted?.Invoke(reportId, target, category, notes);
                return reportId;
            }
            catch (Exception exception)
            {
                var deserializationException = new ResponseDeserializationException(Result.InvalidResponse, $"SubmitReportAsync failed to deserialize server response.\nException: {exception}\nResponse: \"{textResponse}\"");
                OnReportSubmissionFailed?.Invoke(deserializationException, target, category, notes);
                throw deserializationException;
            }
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;

            if (shouldDisposeRequestFactoryAndAuthClient)
            {
                CloudCredentialsPair.Dispose(authClient, requestFactory);
            }

            if (shouldDisposeLogger)
            {
                logger.Dispose();
            }

            playerAccountProviderToDispose?.Dispose();
        }

        private SessionToken GetAuthToken()
        {
#if UNITY
            if (!string.IsNullOrEmpty(SimulatorUtility.AuthToken))
            {
                return new(SimulatorUtility.AuthToken);
            }
#endif
            return authClient.SessionToken;
        }
    }
}
