namespace Coherence.Utils
{
    using System;
    using System.Collections;
    using System.Collections.Generic;

    /// <summary>
    /// A <see cref="SortedList{TKey,TValue}"/> subclass whose <c>foreach</c> loop
    /// allocates zero heap memory.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The BCL <see cref="SortedList{TKey,TValue}.GetEnumerator"/> returns
    /// <c>IEnumerator&lt;KeyValuePair&lt;TKey, TValue&gt;&gt;</c> — an interface — which
    /// forces the runtime to box the internal <c>Enumerator</c> struct onto the heap on
    /// every <c>foreach</c>. By contrast, <see cref="Dictionary{TKey,TValue}"/> returns
    /// its concrete <c>Enumerator</c> struct directly, so iteration is allocation-free.
    /// This class brings the same behaviour to a sorted collection by hiding the base
    /// <c>GetEnumerator</c> with a new overload that returns the concrete
    /// <see cref="Enumerator"/> struct.
    /// </para>
    /// <para>
    /// The struct enumerator walks the list via <see cref="SortedList{TKey,TValue}.Keys"/>
    /// and <see cref="SortedList{TKey,TValue}.Values"/>, which are array-backed
    /// <c>IList&lt;T&gt;</c> properties cached after first access. Each element access
    /// costs one virtual dispatch — not a heap allocation.
    /// </para>
    /// <para>
    /// <b>Important:</b> the zero-alloc guarantee holds only when the variable is typed
    /// as <see cref="SortedListEx{TKey,TValue}"/>. If the list is upcast to
    /// <see cref="SortedList{TKey,TValue}"/> or <c>IEnumerable&lt;T&gt;</c> before
    /// iterating, the base <c>GetEnumerator</c> is called and boxing resumes. Keep
    /// fields and locals typed as <see cref="SortedListEx{TKey,TValue}"/>.
    /// </para>
    /// </remarks>
    public class SortedListEx<TKey, TValue> : SortedList<TKey, TValue>
    {
        public SortedListEx() { }

        public SortedListEx(int capacity) : base(capacity) { }

        public SortedListEx(IComparer<TKey> comparer) : base(comparer) { }

        public SortedListEx(int capacity, IComparer<TKey> comparer) : base(capacity, comparer) { }

        public SortedListEx(IDictionary<TKey, TValue> dictionary) : base(dictionary) { }

        public SortedListEx(IDictionary<TKey, TValue> dictionary, IComparer<TKey> comparer)
            : base(dictionary, comparer) { }

        /// <summary>
        /// Returns a struct enumerator that iterates the list in ascending key order
        /// without allocating heap memory.
        /// </summary>
        /// <remarks>
        /// This method hides (via <c>new</c>) the base
        /// <see cref="SortedList{TKey,TValue}.GetEnumerator"/>, which returns an
        /// interface and causes boxing. The C# <c>foreach</c> statement uses duck-typing:
        /// it resolves <c>GetEnumerator</c> against the compile-time type, so a variable
        /// typed as <see cref="SortedListEx{TKey,TValue}"/> will always reach this
        /// allocation-free overload.
        /// </remarks>
        public new Enumerator GetEnumerator() => new Enumerator(this);

        /// <summary>
        /// A value-type enumerator for <see cref="SortedListEx{TKey,TValue}"/>.
        /// Returned by <see cref="GetEnumerator"/> and used implicitly by <c>foreach</c>.
        /// </summary>
        public struct Enumerator : IEnumerator<KeyValuePair<TKey, TValue>>
        {
            private readonly IList<TKey> _keys;
            private readonly IList<TValue> _values;
            private readonly SortedList<TKey, TValue> _list;
            private readonly int _count;
            private int _index;

            internal Enumerator(SortedList<TKey, TValue> list)
            {
                // Keys and Values are array-backed IList<T> properties that are lazily
                // created and then cached on the SortedList instance, so accessing them
                // here does not allocate after the first call.
                _keys = list.Keys;
                _values = list.Values;
                _list = list;
                _count = list.Count;
                _index = -1;
            }

            /// <inheritdoc/>
            public bool MoveNext()
            {
                if (_list.Count != _count)
                {
                    throw new InvalidOperationException("Collection was modified; enumeration operation may not execute.");
                }

                return ++_index < _count;
            }

            /// <inheritdoc/>
            public KeyValuePair<TKey, TValue> Current
                => new KeyValuePair<TKey, TValue>(_keys[_index], _values[_index]);

            object IEnumerator.Current => Current;

            /// <inheritdoc/>
            public void Reset() => _index = -1;

            /// <inheritdoc/>
            public void Dispose() { }
        }
    }
}
