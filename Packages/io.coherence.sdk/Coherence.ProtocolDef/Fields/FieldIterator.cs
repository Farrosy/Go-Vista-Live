// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.ProtocolDef
{
    using Coherence.Schema;

    public interface IFieldDataCollection
    {
        int FieldCount { get; }
        FieldData GetField(int fieldIndex);
    }

    public ref struct FieldIterator<T> where T : IFieldDataCollection
    {
        public delegate bool FieldFilter(FieldData fieldData);

        public static readonly FieldFilter EntityFieldFilter = fieldData => fieldData.Type == SchemaType.Entity;

        private readonly T collection;
        private int currentIndex;
        private FieldFilter fieldFilter;

        public FieldData Current => collection.GetField(currentIndex);

        public FieldIterator(T collection, FieldFilter fieldFilter = null)
        {
            this.collection = collection;
            this.currentIndex = -1;
            this.fieldFilter = fieldFilter;
        }

        public FieldIterator<T> GetEnumerator() => this;

        public bool MoveNext()
        {
            do
            {
                currentIndex++;
                if (currentIndex >= collection.FieldCount)
                {
                    return false;
                }
            } while (!AcceptField(Current));

            return true;
        }

        private bool AcceptField(FieldData field) => fieldFilter == null || fieldFilter(field);
    }
}
