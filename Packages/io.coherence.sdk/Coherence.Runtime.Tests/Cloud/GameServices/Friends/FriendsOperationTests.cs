// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Runtime.Tests
{
    using System;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Coherence.Cloud;
    using Coherence.Tests;
    using NUnit.Framework;

    public class FriendsOperationTests : CoherenceTest
    {
        [Test]
        public async Task FriendsOperation_ReturnsItsResult_WhenAwaitedAfterSuccess()
        {
            var operation = await new FriendsOperation<int>(Task.FromResult(7));

            Assert.That(operation.IsCompletedSuccessfully, Is.True);
            Assert.That(operation.Result, Is.EqualTo(7));
        }

        [Test]
        [Description("Awaiting never throws: the caller is expected to check HasFailed, read Error, or use OnFail.")]
        public async Task FriendsOperation_DoesNotThrow_WhenAwaitedAfterFailure()
        {
            var operation = await new FriendsOperation<int>(Task.FromException<int>(RequestFailed()));

            Assert.That(operation.HasFailed, Is.True);
            Assert.That(operation.Result, Is.Zero);
        }

        [Test]
        [Description("When a friends request fails, a FriendsErrorType and a RequestException are passed along.")]
        public void Error_SurfacesTheErrorTypeAndRequestException_WhenTheRequestFailed()
        {
            var operation = new FriendsOperation<int>(Task.FromException<int>(RequestFailed()));

            var error = operation.Error;

            Assert.That(error, Is.Not.Null);
            Assert.That(error.Type, Is.EqualTo(FriendsErrorType.TooManyRequests));
            Assert.That(error.RequestException, Is.Not.Null);
            Assert.That(error.RequestException.HttpStatusCode, Is.EqualTo(HttpStatusCode.TooManyRequests));
            Assert.That(error.RequestException.ErrorCode, Is.EqualTo(ErrorCode.TooManyRequests));
        }

        [TestCase(ErrorCode.FriendsUnsupportedSource, FriendsErrorType.UnsupportedSource)]
        [TestCase(ErrorCode.FriendsRateLimited, FriendsErrorType.TooManyRequests)]
        [TestCase(ErrorCode.PresenceRateLimited, FriendsErrorType.TooManyRequests)]
        [TestCase(ErrorCode.TooManyRequests, FriendsErrorType.TooManyRequests)]
        [TestCase(ErrorCode.PresenceNotConnected, FriendsErrorType.PresenceNotConnected)]
        [TestCase(ErrorCode.PresencePayloadInvalid, FriendsErrorType.PresenceInvalid)]
        [TestCase(ErrorCode.PlayerSessionRequired, FriendsErrorType.NotLoggedIn)]
        [TestCase(ErrorCode.InvalidInput, FriendsErrorType.InvalidInput)]
        [TestCase(ErrorCode.LobbyNotCompatible, FriendsErrorType.RequestException,
            Description = "Backend error codes map to the proper FriendsErrorType.")]
        public void Error_MapsTheBackendCodeToAnErrorType_WhenTheRequestFailed(ErrorCode code,
            FriendsErrorType expected)
        {
            var operation = new FriendsOperation<int>(
                Task.FromException<int>(new RequestException(code, HttpStatusCode.BadRequest)));

            Assert.That(operation.Error.Type, Is.EqualTo(expected));
        }

        [Test]
        public void Error_HasNoRequestException_WhenTheFailureNeverReachedTheServer()
        {
            var operation = new FriendsOperation<int>(Task.FromException<int>(new InvalidOperationException("nope")));

            var error = operation.Error;

            Assert.That(error, Is.Not.Null);
            Assert.That(error.Type, Is.EqualTo(FriendsErrorType.InternalException));
            Assert.That(error.RequestException, Is.Null);
            Assert.That(error.Message, Does.Contain("nope"));
        }

        [Test]
        public void OnFail_RunsAndOnSuccessDoesNot_WhenTheOperationFailed()
        {
            var failed = false;
            var succeeded = false;

            new FriendsOperation<int>(Task.FromException<int>(RequestFailed()))
                .OnFail(_ => failed = true)
                .OnSuccess(() => succeeded = true);

            Assert.That(failed, Is.True);
            Assert.That(succeeded, Is.False);
        }

        [Test]
        [Description("A canceled operation is canceled, not failed, so it produces no error to report.")]
        public void Error_IsNull_WhenTheOperationWasCanceled()
        {
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            var operation = new FriendsOperation<int>(Task.FromCanceled<int>(cancellation.Token), cancellation.Token);

            Assert.That(operation.IsCanceled, Is.True);
            Assert.That(operation.HasFailed, Is.False);
            Assert.That(operation.Error, Is.Null);
        }

        [Test]
        [Description("Coroutines yield the operation, which keeps waiting until the task completes.")]
        public void KeepWaiting_BecomesFalse_WhenTheTaskCompletes()
        {
            var completionSource = new TaskCompletionSource<int>();
            var operation = new FriendsOperation<int>(completionSource.Task);

            Assert.That(operation.keepWaiting, Is.True);

            completionSource.SetResult(1);

            Assert.That(operation.keepWaiting, Is.False);
        }

        private static RequestException RequestFailed()
            => new(ErrorCode.TooManyRequests, HttpStatusCode.TooManyRequests);
    }
}
