// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Serializer
{
    using System;
    using Coherence.Common;
    using Coherence.Log;
    using Coherence.ProtocolDef;
    using Coherence.Schema;
    using Coherence.Utils;

    public static class FieldSerializer
    {
        public static void SerializeFieldData(OutProtocolBitStream stream, FieldData fieldData, Logger logger)
        {
            switch (fieldData.Type)
            {
                case SchemaType.Int:
                    SerializeInt(stream, fieldData, logger);
                    break;
                case SchemaType.Int64:
                    SerializeInt64(stream, fieldData, logger);
                    break;
                case SchemaType.Bool:
                    SerializeBool(stream, fieldData, logger);
                    break;
                case SchemaType.Float:
                    SerializeFloat(stream, fieldData, logger);
                    break;
                case SchemaType.String:
                    SerializeString(stream, fieldData, logger);
                    break;
                case SchemaType.Vector2:
                    SerializeVector2(stream, fieldData, logger);
                    break;
                case SchemaType.Vector3:
                    SerializeVector3(stream, fieldData, logger);
                    break;
                case SchemaType.Quaternion:
                    SerializeQuaternion(stream, fieldData, logger);
                    break;
                case SchemaType.Entity:
                    SerializeEntity(stream, fieldData, logger);
                    break;
                case SchemaType.Bytes:
                    SerializeBytes(stream, fieldData, logger);
                    break;
                case SchemaType.Color:
                    SerializeColor(stream, fieldData, logger);
                    break;
                case SchemaType.UInt:
                    SerializeUInt(stream, fieldData, logger);
                    break;
                case SchemaType.UInt64:
                    SerializeUInt64(stream, fieldData, logger);
                    break;
                case SchemaType.Float64:
                    SerializeFloat64(stream, fieldData, logger);
                    break;
                case SchemaType.Int8:
                    SerializeInt8(stream, fieldData, logger);
                    break;
                case SchemaType.UInt8:
                    SerializeUInt8(stream, fieldData, logger);
                    break;
                case SchemaType.Int16:
                    SerializeInt16(stream, fieldData, logger);
                    break;
                case SchemaType.UInt16:
                    SerializeUInt16(stream, fieldData, logger);
                    break;
                case SchemaType.Char:
                    SerializeChar(stream, fieldData, logger);
                    break;
                case SchemaType.Enum:
                    SerializeEnum(stream, fieldData, logger);
                    break;
                case SchemaType.ClientID:
                    SerializeClientID(stream, fieldData, logger);
                    break;
                default:
                    throw new Exception($"Unsupported schema type {fieldData.Type} for field {fieldData.DefinitionName}");
            }
        }

        public static void DeserializeFieldData(InProtocolBitStream stream, FieldData fieldData)
        {
            switch (fieldData.Type)
            {
                case SchemaType.Int:
                    DeserializeInt(stream, fieldData);
                    break;
                case SchemaType.Int64:
                    DeserializeInt64(stream, fieldData);
                    break;
                case SchemaType.Bool:
                    DeserializeBool(stream, fieldData);
                    break;
                case SchemaType.Float:
                    DeserializeFloat(stream, fieldData);
                    break;
                case SchemaType.String:
                    DeserializeString(stream, fieldData);
                    break;
                case SchemaType.Vector2:
                    DeserializeVector2(stream, fieldData);
                    break;
                case SchemaType.Vector3:
                    DeserializeVector3(stream, fieldData);
                    break;
                case SchemaType.Quaternion:
                    DeserializeQuaternion(stream, fieldData);
                    break;
                case SchemaType.Entity:
                    DeserializeEntity(stream, fieldData);
                    break;
                case SchemaType.Bytes:
                    DeserializeBytes(stream, fieldData);
                    break;
                case SchemaType.Color:
                    DeserializeColor(stream, fieldData);
                    break;
                case SchemaType.UInt:
                    DeserializeUInt(stream, fieldData);
                    break;
                case SchemaType.UInt64:
                    DeserializeUInt64(stream, fieldData);
                    break;
                case SchemaType.Float64:
                    DeserializeFloat64(stream, fieldData);
                    break;
                case SchemaType.Int8:
                    DeserializeInt8(stream, fieldData);
                    break;
                case SchemaType.UInt8:
                    DeserializeUInt8(stream, fieldData);
                    break;
                case SchemaType.Int16:
                    DeserializeInt16(stream, fieldData);
                    break;
                case SchemaType.UInt16:
                    DeserializeUInt16(stream, fieldData);
                    break;
                case SchemaType.Char:
                    DeserializeChar(stream, fieldData);
                    break;
                case SchemaType.Enum:
                    DeserializeEnum(stream, fieldData);
                    break;
                case SchemaType.ClientID:
                    DeserializeClientID(stream, fieldData);
                    break;
                default:
                    throw new Exception($"Unsupported schema type {fieldData.Type} for field {fieldData.DefinitionName}");
            }
        }

        private static void SerializeInt(OutProtocolBitStream stream, FieldData fieldData, Logger logger)
        {
            var value = fieldData.GetInt();
            var meta = fieldData.Meta;

            Bounds.Check(value, meta.Minimum, meta.Maximum, fieldData.DefinitionName, logger);
            value = Bounds.Clamp(value, (int)meta.Minimum, (int)meta.Maximum);

            stream.WriteIntegerRange(value, meta.BitCount, (int)meta.Minimum);
        }

        private static void DeserializeInt(InProtocolBitStream stream, FieldData fieldData)
        {
            var meta = fieldData.Meta;
            var value = stream.ReadIntegerRange(meta.BitCount, (int)meta.Minimum);
            fieldData.SetInt(value);
        }

        private static void SerializeInt64(OutProtocolBitStream stream, FieldData fieldData, Logger logger)
        {
            stream.WriteLong(fieldData.GetInt64());
        }

        private static void DeserializeInt64(InProtocolBitStream stream, FieldData fieldData)
        {
            var value = stream.ReadLong();
            fieldData.SetInt64(value);
        }

        private static void SerializeBool(OutProtocolBitStream stream, FieldData fieldData, Logger logger)
        {
            stream.WriteBool(fieldData.GetBool());
        }

        private static void DeserializeBool(InProtocolBitStream stream, FieldData fieldData)
        {
            var value = stream.ReadBool();
            fieldData.SetBool(value);
        }

        private static void SerializeFloat(OutProtocolBitStream stream, FieldData fieldData, Logger logger)
        {
            var value = fieldData.GetFloat();
            var meta = fieldData.Meta;

            if (meta.Compression == FloatCompression.FixedPoint)
            {
                Bounds.Check(value, meta.Minimum, meta.Maximum, fieldData.DefinitionName, logger);
            }

            stream.WriteFloat(value, fieldData.Meta);
        }

        private static void DeserializeFloat(InProtocolBitStream stream, FieldData fieldData)
        {
            var meta = fieldData.Meta;
            var value = stream.ReadFloat(meta);
            fieldData.SetFloat(value);
        }

        private static void SerializeString(OutProtocolBitStream stream, FieldData fieldData, Logger logger)
        {
            stream.WriteString(fieldData.GetString());
        }

        private static void DeserializeString(InProtocolBitStream stream, FieldData fieldData)
        {
            var value = stream.ReadString();
            fieldData.SetString(value);
        }

        private static void SerializeVector2(OutProtocolBitStream stream, FieldData fieldData, Logger logger)
        {
            var meta = fieldData.Meta;
            var value = fieldData.GetVector2();

            if (meta.Compression == FloatCompression.FixedPoint)
            {
                Bounds.Check(value.X, meta.Minimum, meta.Maximum, fieldData.DefinitionName, logger);
                Bounds.Check(value.Y, meta.Minimum, meta.Maximum, fieldData.DefinitionName, logger);
            }

            stream.WriteVector2(value, fieldData.Meta);
        }

        private static void DeserializeVector2(InProtocolBitStream stream, FieldData fieldData)
        {
            var meta = fieldData.Meta;
            var value = stream.ReadVector2(meta);
            fieldData.SetVector2(value);
        }

        private static void SerializeVector3(OutProtocolBitStream stream, FieldData fieldData, Logger logger)
        {
            var meta = fieldData.Meta;
            var value = fieldData.GetVector3().ToCoreVector3();

            if (meta.Compression == FloatCompression.FixedPoint)
            {
                Bounds.Check(value.X, meta.Minimum, meta.Maximum, fieldData.DefinitionName, logger);
                Bounds.Check(value.Y, meta.Minimum, meta.Maximum, fieldData.DefinitionName, logger);
                Bounds.Check(value.Z, meta.Minimum, meta.Maximum, fieldData.DefinitionName, logger);
            }

            stream.WriteVector3(value, fieldData.Meta);
        }

        private static void DeserializeVector3(InProtocolBitStream stream, FieldData fieldData)
        {
            var meta = fieldData.Meta;
            var value = stream.ReadVector3(meta);
            fieldData.SetVector3(value.ToVector3d());
        }

        private static void SerializeQuaternion(OutProtocolBitStream stream, FieldData fieldData, Logger logger)
        {
            stream.WriteQuaternion(fieldData.GetQuaternion(), fieldData.Meta.BitCount);
        }

        private static void DeserializeQuaternion(InProtocolBitStream stream, FieldData fieldData)
        {
            var meta = fieldData.Meta;
            var value = stream.ReadQuaternion(meta.BitCount);
            fieldData.SetQuaternion(value);
        }

        private static void SerializeEntity(OutProtocolBitStream stream, FieldData fieldData, Logger logger)
        {
            stream.WriteEntity(fieldData.GetEntity());
        }

        private static void DeserializeEntity(InProtocolBitStream stream, FieldData fieldData)
        {
            var value = stream.ReadEntity();
            fieldData.SetEntity(value);
        }

        private static void SerializeBytes(OutProtocolBitStream stream, FieldData fieldData, Logger logger)
        {
            stream.WriteBytes(fieldData.GetBytes());
        }

        private static void DeserializeBytes(InProtocolBitStream stream, FieldData fieldData)
        {
            var value = stream.ReadBytes();
            fieldData.SetBytes(value);
        }

        private static void SerializeColor(OutProtocolBitStream stream, FieldData fieldData, Logger logger)
        {
            stream.WriteColor(fieldData.GetColor(), fieldData.Meta);
        }

        private static void DeserializeColor(InProtocolBitStream stream, FieldData fieldData)
        {
            var meta = fieldData.Meta;
            var value = stream.ReadColor(meta);
            fieldData.SetColor(value);
        }

        private static void SerializeUInt(OutProtocolBitStream stream, FieldData fieldData, Logger logger)
        {
            var value = fieldData.GetUInt();
            var meta = fieldData.Meta;

            Bounds.Check(value, meta.Minimum, meta.Maximum, fieldData.DefinitionName, logger);
            value = Bounds.Clamp(value, (uint)meta.Minimum, (uint)meta.Maximum);

            stream.WriteUIntegerRange(value, meta.BitCount, (uint)meta.Minimum);
        }

        private static void DeserializeUInt(InProtocolBitStream stream, FieldData fieldData)
        {
            var meta = fieldData.Meta;
            var value = stream.ReadUIntegerRange(meta.BitCount, (uint)meta.Minimum);
            fieldData.SetUInt(value);
        }

        private static void SerializeUInt64(OutProtocolBitStream stream, FieldData fieldData, Logger logger)
        {
            stream.WriteULong(fieldData.GetUInt64());
        }

        private static void DeserializeUInt64(InProtocolBitStream stream, FieldData fieldData)
        {
            var value = stream.ReadULong();
            fieldData.SetUInt64(value);
        }

        private static void SerializeFloat64(OutProtocolBitStream stream, FieldData fieldData, Logger logger)
        {
            stream.WriteDouble(fieldData.GetFloat64());
        }

        private static void DeserializeFloat64(InProtocolBitStream stream, FieldData fieldData)
        {
            var value = stream.ReadDouble();
            fieldData.SetFloat64(value);
        }

        private static void SerializeInt8(OutProtocolBitStream stream, FieldData fieldData, Logger logger)
        {
            stream.WriteSByte(fieldData.GetInt8());
        }

        private static void DeserializeInt8(InProtocolBitStream stream, FieldData fieldData)
        {
            var value = stream.ReadSByte();
            fieldData.SetInt8(value);
        }

        private static void SerializeUInt8(OutProtocolBitStream stream, FieldData fieldData, Logger logger)
        {
            stream.WriteByte(fieldData.GetUInt8());
        }

        private static void DeserializeUInt8(InProtocolBitStream stream, FieldData fieldData)
        {
            var value = stream.ReadByte();
            fieldData.SetUInt8(value);
        }

        private static void SerializeInt16(OutProtocolBitStream stream, FieldData fieldData, Logger logger)
        {
            stream.WriteShort(fieldData.GetInt16());
        }

        private static void DeserializeInt16(InProtocolBitStream stream, FieldData fieldData)
        {
            var value = stream.ReadShort();
            fieldData.SetInt16(value);
        }

        private static void SerializeUInt16(OutProtocolBitStream stream, FieldData fieldData, Logger logger)
        {
            stream.WriteUShort(fieldData.GetUInt16());
        }

        private static void DeserializeUInt16(InProtocolBitStream stream, FieldData fieldData)
        {
            var value = stream.ReadUShort();
            fieldData.SetUInt16(value);
        }

        private static void SerializeChar(OutProtocolBitStream stream, FieldData fieldData, Logger logger)
        {
            stream.WriteChar(fieldData.GetChar());
        }

        private static void DeserializeChar(InProtocolBitStream stream, FieldData fieldData)
        {
            var value = stream.ReadChar();
            fieldData.SetChar(value);
        }

        private static void SerializeEnum(OutProtocolBitStream stream, FieldData fieldData, Logger logger)
        {
            var value = fieldData.GetEnum();
            var meta = fieldData.Meta;

            Bounds.Check(value, meta.Minimum, meta.Maximum, fieldData.DefinitionName, logger);
            value = Bounds.Clamp(value, (int)meta.Minimum, (int)meta.Maximum);

            stream.WriteIntegerRange(value, meta.BitCount, (int)meta.Minimum);
        }

        private static void DeserializeEnum(InProtocolBitStream stream, FieldData fieldData)
        {
            var meta = fieldData.Meta;
            var value = stream.ReadIntegerRange(meta.BitCount, (int)meta.Minimum);
            fieldData.SetEnum(value);
        }

        private static void SerializeClientID(OutProtocolBitStream stream, FieldData fieldData, Logger logger)
        {
            stream.WriteClientID(fieldData.GetClientID());
        }

        private static void DeserializeClientID(InProtocolBitStream stream, FieldData fieldData)
        {
            var value = stream.ReadClientID();
            fieldData.SetClientID(value);
        }
    }
}
