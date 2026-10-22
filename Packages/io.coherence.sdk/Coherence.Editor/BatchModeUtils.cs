namespace Coherence.Editor
{
    using System;
    using System.Globalization;
    using System.Threading;
    using System.Threading.Tasks;
    using UnityEngine;
    using UnityEngine.Networking;

    internal static class BatchModeUtils
    {
        private const int DefaultTimeoutMinutes = 20;

        /// <summary>
        /// Blocks the main thread until the task has completed if <paramref name="shouldWait"/>.ShouldWait() returns true.
        /// </summary>
        /// <param name="shouldWait"> Determines whether or not we should block the main thread or not. </param>
        /// <param name="task"> Task to wait to complete. </param>
        /// <param name="timeout"> Maximum time to block the thread before throwing a <see cref="TimeoutException"/>. </param>
        /// <returns> The result of <paramref name="shouldWait"/>.ShouldWait(). </returns>
        /// <exception cref="TimeoutException"> Thrown if the thread remains blocked for longer than the maximum allowed <see cref="timeout"/>. </exception>
        public static bool WaitIf(ShouldWait shouldWait, Task task, TimeSpan? timeout = null)
        {
            if (!shouldWait.ShouldWait())
            {
                return false;
            }

            var wait = timeout ?? TimeSpan.FromMinutes(DefaultTimeoutMinutes);
            var waitUntil = DateTime.UtcNow + (wait);

            while (!task.IsCompleted)
            {
                if (waitUntil < DateTime.UtcNow)
                {
                    var timeoutMessage = $"Waiting for task in batch mode timed out after {(wait.TotalMinutes > 1d ? wait.TotalMinutes.ToString(CultureInfo.InvariantCulture) + " minutes" : wait.TotalSeconds.ToString(CultureInfo.InvariantCulture) + " seconds")}.";
                    Debug.LogError(timeoutMessage);
                    throw new TimeoutException(timeoutMessage);
                }

                #if UNITY_EDITOR
                UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
                #endif

                Thread.Sleep(1);
            }

            if (task.IsFaulted) throw task.Exception;
            if (task.IsCanceled) throw new OperationCanceledException("Task was canceled.");

            return true;
        }

        /// <summary>
        /// Blocks the main thread until the request has completed if <paramref name="shouldWait"/>.ShouldWait() returns true.
        /// </summary>
        /// <param name="shouldWait"> Determines whether or not we should block the main thread or not. </param>
        /// <param name="request"> Request to wait to complete. </param>
        /// <param name="timeout"> Maximum time to block the thread before throwing a <see cref="TimeoutException"/>. </param>
        /// <returns> The result of <paramref name="shouldWait"/>.ShouldWait(). </returns>
        /// <exception cref="TimeoutException"> Thrown if the thread remains blocked for longer than the maximum allowed <see cref="timeout"/>. </exception>
        public static bool WaitIf(ShouldWait shouldWait, UnityWebRequest request, TimeSpan? timeout = null)
        {
            if (!shouldWait.ShouldWait())
            {
                return false;
            }

            var wait = timeout ?? TimeSpan.FromMinutes(DefaultTimeoutMinutes);
            var waitUntil = DateTime.UtcNow + (wait);

            while (!request.isDone)
            {
                if (waitUntil < DateTime.UtcNow)
                {
                    var timeoutMessage = $"Waiting for portal request in batch mode timed out after {(wait.TotalMinutes > 1d ? wait.TotalMinutes.ToString(CultureInfo.InvariantCulture) + " minutes" : wait.TotalSeconds.ToString(CultureInfo.InvariantCulture) + " seconds")}.";
                    Debug.LogError(timeoutMessage);
                    throw new TimeoutException(timeoutMessage);
                }

                #if UNITY_EDITOR
                UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
                #endif

                Thread.Sleep(1);
            }

            return true;
        }

        /// <summary>
        /// Blocks the main thread until the task has completed if <see cref="Application.isBatchMode"/> is true.
        /// </summary>
        /// <param name="task"> Task to wait to complete. </param>
        /// <param name="timeout"> Maximum time to block the thread before throwing a <see cref="TimeoutException"/>. </param>
        /// <returns> True if batch mode is active; otherwise, false. </returns>
        /// <exception cref="TimeoutException"> Thrown if the thread remains blocked for longer than the maximum allowed <see cref="timeout"/>. </exception>
        public static bool WaitIfBatchMode(Task task, TimeSpan? timeout = null)
        {
            if (!Application.isBatchMode)
            {
                return false;
            }

            var wait = timeout ?? TimeSpan.FromMinutes(DefaultTimeoutMinutes);
            var waitUntil = DateTime.UtcNow + (wait);

            while (!task.IsCompleted)
            {
                if (waitUntil < DateTime.UtcNow)
                {
                    var timeoutMessage = $"Waiting for task in batch mode timed out after {(wait.TotalMinutes > 1d ? wait.TotalMinutes.ToString(CultureInfo.InvariantCulture) + " minutes" : wait.TotalSeconds.ToString(CultureInfo.InvariantCulture) + " seconds")}.";
                    Debug.LogError(timeoutMessage);
                    throw new TimeoutException(timeoutMessage);
                }

                #if UNITY_EDITOR
                UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
                #endif

                Thread.Sleep(1);
            }

            if (task.IsFaulted) throw task.Exception;
            if (task.IsCanceled) throw new OperationCanceledException("Task was canceled.");

            return true;
        }

        /// <summary>
        /// Blocks the main thread until the request has completed if <see cref="Application.isBatchMode"/> is true.
        /// </summary>
        /// <param name="request"> Request to wait to complete. </param>
        /// <param name="timeout"> Maximum time to block the thread before throwing a <see cref="TimeoutException"/>. </param>
        /// <returns> True if batch mode is active; otherwise, false. </returns>
        /// <exception cref="TimeoutException"> Thrown if the thread remains blocked for longer than the maximum allowed <see cref="timeout"/>. </exception>
        public static bool WaitIfBatchMode(UnityWebRequest request, TimeSpan? timeout = null)
        {
            if (!Application.isBatchMode)
            {
                return false;
            }

            var wait = timeout ?? TimeSpan.FromMinutes(DefaultTimeoutMinutes);
            var waitUntil = DateTime.UtcNow + (wait);

            while (!request.isDone)
            {
                if (waitUntil < DateTime.UtcNow)
                {
                    var timeoutMessage = $"Waiting for portal request in batch mode timed out after {(wait.TotalMinutes > 1d ? wait.TotalMinutes.ToString(CultureInfo.InvariantCulture) + " minutes" : wait.TotalSeconds.ToString(CultureInfo.InvariantCulture) + " seconds")}.";
                    Debug.LogError(timeoutMessage);
                    throw new TimeoutException(timeoutMessage);
                }

                #if UNITY_EDITOR
                UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
                #endif

                Thread.Sleep(1);
            }

            return true;
        }
    }
}
