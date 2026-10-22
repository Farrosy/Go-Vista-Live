// Copyright (c) coherence ApS.
// See the license file in the package root for more information.
namespace Coherence.Runtime.Utils
{
    using System;
    using System.Runtime.CompilerServices;
    using System.Threading.Tasks;
    using Awaiter = System.Runtime.CompilerServices.TaskAwaiter;

    /// <summary>
    /// Extension methods for <see cref="TimeSpan"/>.
    /// </summary>
    internal static class TimeSpanExtensions
    {
        /// <summary>Whole seconds, rounded up so a request never lasts less than asked for.</summary>
        /// <remarks>Clamped to 0 for negative spans, and to <see cref="int.MaxValue"/> for very long ones.</remarks>
        public static int ToWholeSeconds(this TimeSpan timeSpan)
            => (int)Math.Clamp(Math.Ceiling(timeSpan.TotalSeconds), 0d, int.MaxValue);

        /// <summary>
        /// Suspend execution of the calling async method for the specified amount of time.
        /// <remarks>
        /// <para>
        /// The wait is measured in real time, so it is not affected by <c>Time.timeScale</c> and still elapses
        /// while the game is slowed down or paused.
        /// </para>
        /// <para>
        /// This method is safe to use on WebGL platforms (unlike <see cref="Task.Delay(int)"/>).
        /// </para>
        /// </remarks>
        /// </summary>
        /// <param name="timeSpan"> The amount of time to wait. </param>
        /// <returns> An awaiter. </returns>
        /// <seealso cref="Wait"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Awaiter GetAwaiter(this TimeSpan timeSpan) => Wait.For(timeSpan).GetAwaiter();
    }
}
