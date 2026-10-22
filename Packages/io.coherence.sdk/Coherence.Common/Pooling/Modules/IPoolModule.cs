// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Common.Pooling.Modules
{
    using System;

    /// <summary>
    /// Pooling infrastructure for memory efficient packet management.
    /// </summary>
    public interface IPoolModule<T> : IDisposable
    {
        void OnRent(in T item);
        void OnReturning(in T item);
        void OnReturn(in T item);
        bool ShouldRestore(in T item);
    }
}
