// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Common.Pooling.Modules
{
    using System;
    using System.Collections.Generic;

    internal class GuardModule<T> : IPoolModule<T>
    {
        private readonly bool threadSafe;
        private readonly bool enabled;
        private readonly HashSet<T> rented = new();

        public GuardModule(bool threadSafe = false, bool enabled = true)
        {
            this.threadSafe = threadSafe;
            this.enabled = enabled;
        }

        public void OnRent(in T item)
        {
            if (!enabled)
            {
                return;
            }

            if (!threadSafe)
            {
                VerifyRentedOnce(item);
                return;
            }

            lock (rented)
            {
                VerifyRentedOnce(item);
            }
        }

        public void OnReturning(in T item)
        {
            if (!enabled)
            {
                return;
            }

            if (!threadSafe)
            {
                VerifyReturnedWasRented(item);
                return;
            }

            lock (rented)
            {
                VerifyReturnedWasRented(item);
            }
        }

        public void OnReturn(in T item) { }

        public bool ShouldRestore(in T item)
        {
            return true;
        }

        public void Dispose()
        {
            if (rented.Count > 0)
            {
                throw new Exception($"There are still {rented.Count} rented objects of type {typeof(T)} that have not been returned. There is a leak!");
            }
        }

        private void VerifyRentedOnce(in T item)
        {
            if (rented.Add(item))
            {
                return;
            }

            throw new Exception($"Pooled object of type {typeof(T)} was rented twice: {item}");
        }

        private void VerifyReturnedWasRented(in T item)
        {
            if (rented.Remove(item))
            {
                return;
            }

            throw new Exception($"Pooled object of type {typeof(T)} was returned without being rented: {item}");
        }
    }
}
