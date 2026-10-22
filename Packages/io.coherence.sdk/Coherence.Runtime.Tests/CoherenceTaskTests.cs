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

    public class CoherenceTaskTests : CoherenceTest
    {
        [Test]
        [Description("A task completes on a thread-pool thread. Its OnSuccess callback must still run on the " +
            "caller's thread, so that it can safely call into Cloud services.")]
        public async Task OnSuccess_RunsTheCallbackOnTheCallersThread_WhenTheTaskCompletesOnAThreadPoolThread()
        {
            var callerThreadId = Thread.CurrentThread.ManagedThreadId;
            Assume.That(SynchronizationContext.Current, Is.Not.Null, "The test runs without a context to return to.");
            var source = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            var callbackThreadId = new TaskCompletionSource<int>();
            _ = new CoherenceTask<int>(source.Task).OnSuccess(_ =>
                callbackThreadId.SetResult(Thread.CurrentThread.ManagedThreadId));

            await Task.Run(() => source.SetResult(7));
            var completed = await Task.WhenAny(callbackThreadId.Task, Task.Delay(TimeSpan.FromSeconds(5)));

            Assert.That(completed, Is.SameAs(callbackThreadId.Task), "The callback never ran.");
            Assert.That(await callbackThreadId.Task, Is.EqualTo(callerThreadId));
        }
    }
}
