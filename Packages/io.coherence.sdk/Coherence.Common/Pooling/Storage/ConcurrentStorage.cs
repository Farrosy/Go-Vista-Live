// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Common.Pooling.Storage
{
    using System.Collections.Concurrent;

    internal class ConcurrentStorage<T> : IPoolStorage<T>
    {
        private readonly ConcurrentQueue<T> queue = new();

        public bool TryTake(out T item) => queue.TryDequeue(out item);
        public void Add(T item) => queue.Enqueue(item);
    }
}
