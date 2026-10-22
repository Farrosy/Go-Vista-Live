// Copyright (c) coherence ApS.
// See the license file in the package root for more information.
namespace Coherence.Cloud
{
    using System.Diagnostics.CodeAnalysis;
    using Runtime;

    /// <summary>
    /// Exposes additional members on top of the ones already offered by <see cref="IRequestFactory"/>
    /// for internal-only use.
    /// </summary>
    /// <inheritdoc/>
    internal interface IRequestFactoryInternal : IRequestFactory
    {
        RequestThrottle Throttle { get; }

        /// <summary>
        /// The <c>resume_id</c> of the push whose callbacks are running, or <see langword="null"/> when it carries
        /// none. Only meaningful when read from inside a push callback.
        /// </summary>
        /// <remarks>
        /// Within one lobby, a later full-state push carries a higher id, so it can be used to drop stale ones
        /// that arrive out of order.
        /// </remarks>
        [MaybeNull]
        string PushResumeId { get; }
    }
}
