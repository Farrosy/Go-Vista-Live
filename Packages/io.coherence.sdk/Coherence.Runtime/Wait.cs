// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

#if UNITY_5_3_OR_NEWER
// IMPORTANT: Used by the pure-dotnet client, DON'T REMOVE.
#define UNITY
#endif

namespace Coherence.Runtime.Utils
{
    using System;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;
#if UNITY
    using UnityEngine;
#endif

    /// <summary>
    /// Utility class providing methods for suspending the execution of the calling async method for a specified amount of time.
    /// <remarks>
    /// All methods in this class are safe to use on WebGL platforms (unlike <see cref="Task.Delay(int)"/>).
    /// </remarks>
    /// </summary>
    internal static class Wait
    {
        /// <summary>
        /// Suspend execution of the calling async method for the specified amount of time.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The wait is measured in real time, a frame at a time: it is not affected by <c>Time.timeScale</c>,
        /// so it still elapses while the game is slowed down or paused. Everything this waits for — a request
        /// cooldown, a connection timeout, an authority request — carries on regardless of the game clock.
        /// </para>
        /// <para>
        /// This method is safe to use on WebGL platforms (unlike <see cref="Task.Delay(int)"/>).
        /// </para>
        /// </remarks>
        /// <param name="timeSpan"> The amount of time to wait. </param>
        /// <returns> An awaitable. </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static async Task For(TimeSpan timeSpan)
        {
            var waitUntil = DateTime.Now + timeSpan;
            do
            {
                await ForNextFrame();
            }
            while (DateTime.Now < waitUntil);
        }

        /// <inheritdoc cref="For(TimeSpan)"/>
        /// <param name="timeSpan"> The amount of time to wait. </param>
        /// <param name="cancellationToken"> Used to cancel the operation. </param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static async Task For(TimeSpan timeSpan, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var waitUntil = DateTime.Now + timeSpan;
            do
            {
                await ForNextFrame();
                cancellationToken.ThrowIfCancellationRequested();
            }
            while (DateTime.Now < waitUntil);
        }

        /// <summary>
        /// Suspend execution of the calling async method until the next frame.
        /// </summary>
        /// <remarks>
        /// <para>
        /// NOTE: This is only guaranteed to wait for exactly one frame in Unity 6.0.0 and later.
        /// In earlier versions of Unity, it will wait for one frame or more.
        /// In non-Unity environments, it will wait for the duration of one Task.Yield.
        /// In Edit Mode it will also wait for the duration of one Task.Yield.
        /// </para>
        /// <para>
        /// This method is safe to use on WebGL platforms (unlike <see cref="Task.Delay(int)"/>).
        /// </para>
        /// </remarks>
        /// <returns> An awaitable. </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static
#if UNITY_6000_0_OR_NEWER
            async Awaitable
#elif UNITY
            async Task
#else
            YieldAwaitable
#endif
            ForNextFrame()
        {
#if UNITY

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                await Task.Yield();
                return;
            }
#endif

#if UNITY_6000_0_OR_NEWER
            await Awaitable.NextFrameAsync();
#else
            var frameCount = Time.frameCount;
            do
            {
                await Task.Yield();
            }
            while (Time.frameCount == frameCount);
#endif

#else
            return Task.Yield();
#endif
        }
    }
}
