// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.ProtocolDef
{
    using System;
    using System.Collections.Generic;
    using Coherence.Schema;

    public class ProtocolComponentDefinition
    {
        public const int MaxFields = 32;

        public uint ComponentID { get; private set; }
        public string Name { get; private set; }
        public IReadOnlyList<ProtocolFieldDefinition> Fields { get; private set; }
        public IReadOnlyList<int> FieldOffsets { get; private set; }
        public int DataSize { get; private set; }
        public int RefFieldsCount { get; private set; }
        public int PriorityLevel => Attributes.Priority switch
        {
            ComponentPriority.High => 1000,
            ComponentPriority.Normal => 100,
            _ => throw new ArgumentException("Unknown priority level"),
        };

        public ComponentAttributes Attributes { get; private set; }

        public ProtocolComponentDefinition(uint componentID, string name, List<ProtocolFieldDefinition> fields, ComponentAttributes attributes = default)
        {
            ComponentID = componentID;
            Name = name;
            Fields = fields;
            FieldOffsets = CalculateFieldOffsets(fields);
            DataSize = CalculateDataSize(fields);
            RefFieldsCount = CalculateRefFieldsCount(fields);
            Attributes = attributes;
        }

        public override string ToString()
        {
            return $"{Name}(T: {ComponentID})";
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

        private static int CalculateRefFieldsCount(List<ProtocolFieldDefinition> fields)
        {
            var count = 0;

            foreach (var field in fields)
            {
                if (field.Type == SchemaType.Entity)
                {
                    count++;
                }
            }

            return count;
        }
    }

    public enum ComponentPriority
    {
        Normal,
        High
    }

    public struct ComponentAttributes
    {
        public bool SendOrdered;
        public int ComponentOrder;
        public bool HasBaseComponent;
        public uint BaseComponentID;
        public ComponentPriority Priority;

        public ComponentAttributes(
            bool sendOrdered = false,
            int componentOrder = 0,
            bool hasBaseComponent = false,
            uint baseComponentID = 0,
            ComponentPriority priority = ComponentPriority.Normal)
        {
            SendOrdered = sendOrdered;
            ComponentOrder = componentOrder;
            HasBaseComponent = hasBaseComponent;
            BaseComponentID = baseComponentID;
            Priority = priority;
        }
    }
}
