// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Core
{
    using System;
    using Brook;

    internal class SequenceBuffer<T>
    {
        private const int MinBufferSize = 64;
        private const int MaxBufferSize = 4096;

        public struct Slot
        {
            public static readonly Slot Empty = new(default, false);

            public T Value;
            public bool HasValue;

            public Slot(T message)
            {
                Value = message;
                HasValue = true;
            }

            private Slot(T message, bool isOccupied)
            {
                Value = message;
                HasValue = isOccupied;
            }
        }

        private readonly Slot[] messages;
        protected int Size => messages.Length;

        protected SequenceBuffer(int size)
        {
            AssertValidSize(size);

            messages = new Slot[size];
            for (var i = 0; i < size; i++)
            {
                messages[i] = Slot.Empty;
            }
        }

        protected Slot Find(MessageID id) => messages[Index(id)];

        protected void Insert(MessageID id, T data) => messages[Index(id)] = new(data);

        protected void Remove(MessageID id) => messages[Index(id)] = Slot.Empty;

        private int Index(MessageID id) => id.Value % Size;

        protected void ClearBuffer()
        {
            for (var i = 0; i < messages.Length; ++i)
            {
                messages[i] = Slot.Empty;
            }
        }

        private void AssertValidSize(int size)
        {
            if (size != NextPowerOfTwo(size))
            {
                throw new ArgumentException($"Invalid buffer size {size}. Must be power of two");
            }

            if (size < MinBufferSize || size > MaxBufferSize)
            {
                throw new ArgumentException($"Invalid buffer size {size}. Min={MinBufferSize}, Max={MaxBufferSize}");
            }
        }

        private int NextPowerOfTwo(int x) => (x > 2) ? (int)Math.Pow(2, (int)Math.Log(x - 1, 2) + 1) : 1;
    }
}
