// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.ProtocolDef
{
    using System;
    using System.Collections.Generic;
    using Coherence.Debugging;

    public class HandleTable<T> where T : class
    {
        private struct Slot
        {
            public T Value;
            public uint Version;
            public bool Occupied;
        }

        public struct Handle
        {
            public static readonly Handle Empty = new(-1, 0);

            private int value;
            private uint version;

            public Handle(int arrayIndex, uint version)
            {
                this.value = arrayIndex + 1;
                this.version = version;
            }

            public int GetArrayIndex() => value - 1;
            public uint GetVersion() => version;

            public static bool operator ==(Handle left, Handle right)
            {
                return left.value == right.value && left.version == right.version;
            }

            public static bool operator !=(Handle left, Handle right) => !(left == right);


            public override bool Equals(object obj)
            {
                if (obj is Handle index)
                {
                    return this == index;
                }

                return false;
            }

            public override int GetHashCode() => HashCode.Combine(value, version);
        }

        private List<Slot> slots = new();
        private Stack<int> freeList = new();

        public int Count => slots.Count - freeList.Count;

        public Handle Add(T value)
        {
            if (value == null)
            {
                return Handle.Empty;
            }

            int index;
            uint version;

            if (freeList.Count > 0)
            {
                index = freeList.Pop();

                var slot = slots[index];
                slot.Value = value;
                slot.Occupied = true;
                slots[index] = slot;

                version = slot.Version;
            }
            else
            {
                index = slots.Count;
                version = 0;

                slots.Add(new Slot
                {
                    Value = value,
                    Version = version,
                    Occupied = true
                });
            }

            return new Handle(index, version);
        }

        public void Delete(Handle handle)
        {
            if (handle == Handle.Empty)
            {
                return;
            }

            var index = handle.GetArrayIndex();

            DbgAssert.ThatFmt(index >= 0 && index < slots.Count, "Handle index out of range: {0}. Slots count: {1}", index, slots.Count);

            var slot = slots[index];

            DbgAssert.That(slot.Occupied, "Handle points to a free slot");
            DbgAssert.ThatFmt(slot.Version == handle.GetVersion(), "Handle version mismatch. {0} != {1}", slot.Version, handle.GetVersion());

            slot.Value = null;
            slot.Occupied = false;
            slot.Version++;

            slots[index] = slot;
            freeList.Push(index);
        }

        public T GetValue(Handle handle)
        {
            if (handle == Handle.Empty)
            {
                return null;
            }

            var index = handle.GetArrayIndex();

            DbgAssert.ThatFmt(index >= 0 && index < slots.Count, "Handle index out of range: {0}. Slots count: {1}", index, slots.Count);

            var slot = slots[index];

            DbgAssert.That(slot.Occupied, "Handle points to a free slot");
            DbgAssert.ThatFmt(slot.Version == handle.GetVersion(), "Handle version mismatch. {0} != {1}", slot.Version, handle.GetVersion());

            return slot.Value;
        }
    }
}
