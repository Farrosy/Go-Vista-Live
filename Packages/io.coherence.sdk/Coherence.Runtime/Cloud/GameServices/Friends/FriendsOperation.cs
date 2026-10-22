// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Cloud
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Runtime;
    using Utils;

    /// <summary>A <see cref="FriendsService"/> request.</summary>
    /// <remarks>
    /// <para>
    /// <see langword="await"/> it in an <see langword="async"/> method,
    /// <see langword="yield"/> it in a coroutine, or hook up <c>OnSuccess</c> and <c>OnFail</c>.
    /// </para>
    /// <para>
    /// Awaiting never throws. Check <c>HasFailed</c>, read <c>Error</c>, or use <c>OnFail</c>.
    /// If you never look at the error, coherence logs it to the Console for you.
    /// </para>
    /// </remarks>
    /// <typeparam name="TResult">What the request gives you back when it succeeds.</typeparam>
    public sealed class FriendsOperation<TResult> : CloudOperation<TResult, FriendsError>
    {
        internal FriendsOperation(Task<TResult> task) : base(task) { }
        internal FriendsOperation(Task<TResult> task, CancellationToken cancellationToken) : base(task, cancellationToken) { }

        internal override FriendsError CreateError(Exception exception, object args = null)
            => FriendsError.From(exception, errorHasBeenObserved);
    }

    /// <inheritdoc cref="FriendsOperation{TResult}"/>
    public sealed class FriendsOperation : CloudOperation<FriendsError>
    {
        internal FriendsOperation(Task task) : base(task) { }
        internal FriendsOperation(Task task, CancellationToken cancellationToken) : base(task, cancellationToken) { }

        internal override FriendsError CreateError(Exception exception, object args = null)
            => FriendsError.From(exception, errorHasBeenObserved);
    }
}
