// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.ProtocolDef
{
    using System;
    using System.Numerics;
    using Coherence.Common;
    using Coherence.Entities;
    using Coherence.Schema;

    public class ProtocolFieldDefinition
    {
        public string Name { get; private set; }
        public SchemaType Type { get; private set; }
        public FieldMeta Meta { get; private set; }
        public bool HasSimFrames { get; private set; }

        public int Size => GetFieldTypeSize(Type);

        public ProtocolFieldDefinition(string name, SchemaType type, FieldMeta meta, bool hasSimFrames = false)
        {
            Name = name;
            Type = type;
            Meta = meta;
            HasSimFrames = hasSimFrames;
        }

        public static int GetFieldTypeSize(SchemaType type)
        {
            return type switch
            {
                SchemaType.Int => sizeof(int),
                SchemaType.Int64 => sizeof(long),
                SchemaType.Bool => sizeof(bool),
                SchemaType.Float => sizeof(float),
                SchemaType.String => Unsafe.Utils.SizeOf<HandleTable<string>.Handle>(),
                SchemaType.Vector2 => Unsafe.Utils.SizeOf<Vector2>(),
                SchemaType.Vector3 => Unsafe.Utils.SizeOf<Vector3d>(),
                SchemaType.Quaternion => Unsafe.Utils.SizeOf<Quaternion>(),
                SchemaType.Entity => Unsafe.Utils.SizeOf<Entity>(),
                SchemaType.Bytes => Unsafe.Utils.SizeOf<HandleTable<byte[]>.Handle>(),
                SchemaType.Color => Unsafe.Utils.SizeOf<Vector4>(),
                SchemaType.UInt => sizeof(uint),
                SchemaType.UInt64 => sizeof(ulong),
                SchemaType.Float64 => sizeof(double),
                SchemaType.Int8 => sizeof(sbyte),
                SchemaType.UInt8 => sizeof(byte),
                SchemaType.Int16 => sizeof(short),
                SchemaType.UInt16 => sizeof(ushort),
                SchemaType.Char => sizeof(char),
                SchemaType.Enum => sizeof(int),
                SchemaType.ClientID => sizeof(uint),
                _ => throw new ArgumentException("Unknown SchemaType"),
            };
        }
    }
}
