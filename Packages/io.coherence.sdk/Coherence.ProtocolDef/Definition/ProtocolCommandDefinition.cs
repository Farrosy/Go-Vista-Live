// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.ProtocolDef
{
    using System.Collections.Generic;

    public class ProtocolCommandDefinition
    {
        public uint CommandID { get; private set; }
        public string Name { get; private set; }
        public MessageTarget Routing { get; private set; }
        public IReadOnlyList<ProtocolFieldDefinition> Fields { get; private set; }
        public IReadOnlyList<int> FieldOffsets { get; private set; }
        public int DataSize { get; private set; }

        public ProtocolCommandDefinition(uint commandID, string name, List<ProtocolFieldDefinition> fields, MessageTarget routing = MessageTarget.All)
        {
            CommandID = commandID;
            Name = name;
            Fields = fields;
            Routing = routing;
            FieldOffsets = CalculateFieldOffsets(fields);
            DataSize = CalculateDataSize(fields);
        }

        public override string ToString()
        {
            return $"{Name}(T: {CommandID})";
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
