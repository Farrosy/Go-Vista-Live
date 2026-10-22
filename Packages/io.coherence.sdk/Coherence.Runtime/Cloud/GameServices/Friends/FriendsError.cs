// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Cloud
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using Common;
    using Log;
    using Runtime;
    using Utils;

    /// <summary>What went wrong with a <see cref="FriendsService"/> request.</summary>
    /// <remarks>
    /// If you never look at the error, coherence logs it to the Console for you.
    /// </remarks>
    public sealed class FriendsError : CoherenceError<FriendsErrorType>
    {
        /// <summary>
        /// The failed request, or <see langword="null"/> if the problem came up before anything was sent.
        /// </summary>
        /// <remarks>
        /// Read <see cref="RequestException.HttpStatusCode"/> for the status the server replied with,
        /// and <see cref="RequestException.ErrorCode"/> for the code it sent back.
        /// </remarks>
        [MaybeNull] public RequestException RequestException { get; }

        private FriendsError(FriendsErrorType type, string message, bool hasBeenObserved = false)
            : base(type, message, Error.FriendsOperationError, hasBeenObserved) { }

        private FriendsError([DisallowNull] RequestException requestException, bool hasBeenObserved = false)
            : base(TypeOf(requestException.ErrorCode), requestException.Message, Error.FriendsOperationError,
                hasBeenObserved)
            => RequestException = requestException;

        // A faulted task hands over an AggregateException, so every case has to look inside rather
        // than at the exception it was given.
        internal static FriendsError From([MaybeNull] Exception exception, bool hasBeenObserved)
        {
            if (exception.TryExtract(out RequestException requestException))
            {
                return new(requestException, hasBeenObserved);
            }

            if (exception.TryExtract(out ResponseDeserializationException unreadable))
            {
                return new(FriendsErrorType.InvalidResponse, unreadable.Message, hasBeenObserved);
            }

            if (exception.TryExtract(out ArgumentException badArgument))
            {
                return new(FriendsErrorType.InvalidInput, badArgument.Message, hasBeenObserved);
            }

            return new(FriendsErrorType.InternalException, exception?.Message ?? "The request failed.",
                hasBeenObserved);
        }

        private static FriendsErrorType TypeOf(ErrorCode code) => code switch
        {
            ErrorCode.FriendsUnsupportedSource => FriendsErrorType.UnsupportedSource,
            ErrorCode.PresenceNotConnected => FriendsErrorType.PresenceNotConnected,
            ErrorCode.PresencePayloadInvalid => FriendsErrorType.PresenceInvalid,
            ErrorCode.PlayerSessionRequired => FriendsErrorType.NotLoggedIn,
            ErrorCode.InvalidInput => FriendsErrorType.InvalidInput,
            ErrorCode.FriendsRateLimited or ErrorCode.PresenceRateLimited or ErrorCode.TooManyRequests
                => FriendsErrorType.TooManyRequests,
            _ => FriendsErrorType.RequestException,
        };
    }
}
