// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Toolkit
{
    using Log;

    /// <summary>
    /// Represents the result of a <see cref="CoherenceSync.AdoptAsync">request to adopt an orphaned entity</see>.
    /// </summary>
    public readonly struct AdoptResult
    {
        internal static readonly AdoptResult Success = new(AdoptResultType.Success, null, null);

        public AdoptResultType Type { get; }
        public string FailureMessage { get; }
        internal Warning? Warning { get; }

        public AdoptResult(AdoptResultType type, Warning? warning, string failureMessage)
        {
            Type = type;
            Warning = warning;
            FailureMessage = failureMessage;
        }

        public static implicit operator AdoptResultType(AdoptResult result) => result.Type;
        public static implicit operator bool(AdoptResult result) => result.Type is AdoptResultType.Success;

        internal void LogFailure(Logger logger)
        {
            if (Warning is { } warning)
            {
                logger?.Warning(warning, FailureMessage);
            }
            else if (FailureMessage is not null)
            {
                logger?.Debug(FailureMessage);
            }
        }
    }

    /// <summary>
    /// Specifies the possible outcomes of a <see cref="CoherenceSync.AdoptAsync">request to adopt an orphaned entity</see>.
    /// </summary>
    public enum AdoptResultType
    {
        /// <summary>The adoption request succeeded and authority has been granted.</summary>
        Success = 1,
        /// <summary>The adoption request was canceled via a <see cref="System.Threading.CancellationToken"/> or because the entity was destroyed.</summary>
        Canceled = 2,
        /// <summary>The entity is not orphaned and therefore cannot be adopted.</summary>
        EntityNotOrphanedError = 3,
        /// <summary>The entity is not synchronized with the network.</summary>
        EntityNotSynchronizedWithNetwork = 4,
        /// <summary>The adoption request timed out without receiving a response.</summary>
        TimeoutError = 5,
        /// <summary>The adoption request was rejected by the replication server or current authority.</summary>
        RequestRejectedError = 6
    }
}

