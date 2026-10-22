// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Runtime.Tests
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Coherence.Cloud;
    using Coherence.Tests;
    using NUnit.Framework;

    public class LoginOperationTests : CoherenceTest
    {
        [Test]
        [Description("A login completes on a thread-pool thread. Its OnSuccess callback must still run on the " +
            "caller's thread, so that it can safely call into other Cloud services.")]
        public async Task OnSuccess_RunsTheCallbackOnTheCallersThread_WhenTheLoginCompletesOnAThreadPoolThread()
        {
            var callerThreadId = Thread.CurrentThread.ManagedThreadId;
            Assume.That(SynchronizationContext.Current, Is.Not.Null, "The test runs without a context to return to.");
            var login = new TaskCompletionSource<PlayerAccount>(TaskCreationOptions.RunContinuationsAsynchronously);
            var callbackThreadId = new TaskCompletionSource<int>();
            _ = new LoginOperation(login.Task).OnSuccess((LoginOperation _) =>
                callbackThreadId.SetResult(Thread.CurrentThread.ManagedThreadId));

            await Task.Run(() => login.SetResult(null));
            var completed = await Task.WhenAny(callbackThreadId.Task, Task.Delay(TimeSpan.FromSeconds(5)));

            Assert.That(completed, Is.SameAs(callbackThreadId.Task), "The callback never ran.");
            Assert.That(await callbackThreadId.Task, Is.EqualTo(callerThreadId));
        }
    }
}
