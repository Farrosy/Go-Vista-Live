// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.ProtocolDef
{
    using System.Collections.Generic;

    public class ProtocolInputDefinition
    {
        public uint InputID { get; private set; }
        public string Name { get; private set; }
        public IReadOnlyList<ProtocolFieldDefinition> Fields { get; private set; }
        public IReadOnlyList<int> FieldOffsets { get; private set; }
        public int DataSize { get; private set; }

        public ProtocolInputDefinition(uint inputID, string name, List<ProtocolFieldDefinition> fields)
        {
            InputID = inputID;
            Name = name;
            Fields = fields;
            FieldOffsets = CalculateFieldOffsets(fields);
            DataSize = CalculateDataSize(fields);
        }

        public override string ToString()
        {
            return $"{Name}(T: {InputID})";
        }

        private static List<int> CalculateFieldOffsets(List<ProtocolFieldDefinition> fields)
        {
            var offsets = new List<int>(fields.Count);

            var offset = 0;
            foreach (var field in fields)
            {
                offsets.Add(offset);
                offset += field.Size;
            }

            return offsets;
        }

        private static int CalculateDataSize(List<ProtocolFieldDefinition> fields)
        {
            var dataSize = 0;
            foreach (var field in fields)
            {
                dataSize += field.Size;
            }

            return dataSize;
        }
    }
}
