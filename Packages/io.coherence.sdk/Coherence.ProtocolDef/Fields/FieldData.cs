// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.ProtocolDef
{
    using System;
    using System.Numerics;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using System.Text;
    using Coherence.Common;
    using Coherence.Connection;
    using Coherence.Debugging;
    using Coherence.Entities;
    using Coherence.Schema;
    using Coherence.SimulationFrame;

    public ref struct FieldData
    {
        private ProtocolFieldDefinition definition;
        private ManagedTypesTables managedTypesTables;
        private Span<byte> data;
        private Span<AbsoluteSimulationFrame> simFrame;
        private int index;

        public ProtocolFieldDefinition Definition => definition;
        public SchemaType Type => definition.Type;
        public string DefinitionName => definition.Name;
        public FieldMeta Meta => definition.Meta;
        public bool HasSimFrames => definition.HasSimFrames;
        public int Index => index;
        public uint Mask => 1u << index;

        internal FieldData(ProtocolFieldDefinition definition, ManagedTypesTables managedTypesTables, Span<byte> data, Span<AbsoluteSimulationFrame> simFrame, int fieldIndex)
        {
            if (definition.HasSimFrames)
            {
                DbgAssert.ThatFmt(simFrame.Length == 1, "Field should reference only one simulation frame, but got {0}!", simFrame.Length);
            }
            else
            {
                DbgAssert.ThatFmt(simFrame.Length == 0, "Field should not reference any simulation frames, but got {0}!", simFrame.Length);
            }

            this.definition = definition;
            this.managedTypesTables = managedTypesTables;
            this.data = data;
            this.simFrame = simFrame;
            this.index = fieldIndex;
        }

        /// <summary>
        /// Moves the value from the other FieldData into this one, leaving the other in a cleared state.
        /// </summary>
        public void MoveFrom(FieldData other)
        {
            AssertType(other.Type);
            DbgAssert.That(data != other.data, "Cannot move from the same FieldData instance!");

            other.data.CopyTo(data);
            other.data.Clear();

            if (HasSimFrames)
            {
                SetSimFrame(other.GetSimFrame());
                other.SetSimFrame(default);
            }
        }

        /// <summary>
        /// Copies the value from the other FieldData into this one.
        /// </summary>
        public void CopyFrom(FieldData other)
        {
            AssertType(other.Type);

            switch (Type)
            {
                case SchemaType.String:
                    SetString(other.GetString());
                    break;
                case SchemaType.Bytes:
                    SetBytes(other.GetBytes());
                    break;
                default:
                    other.data.CopyTo(data);
                    break;
            }

            if (HasSimFrames)
            {
                SetSimFrame(other.GetSimFrame());
            }
        }

        public void Set<T>(T value)
        {
            switch (value)
            {
                case int intValue:
                    SetInt(intValue);
                    break;
                case long longValue:
                    SetInt64(longValue);
                    break;
                case bool boolValue:
                    SetBool(boolValue);
                    break;
                case float floatValue:
                    SetFloat(floatValue);
                    break;
                case string stringValue:
                    SetString(stringValue);
                    break;
                case Vector2 vector2Value:
                    SetVector2(vector2Value);
                    break;
                case Vector3 vector3Value:
                    SetVector3(vector3Value.ToVector3d());
                    break;
                case Vector3d vector3dValue:
                    SetVector3(vector3dValue);
                    break;
                case Quaternion quaternionValue:
                    SetQuaternion(quaternionValue);
                    break;
                case Entity entityValue:
                    SetEntity(entityValue);
                    break;
                case byte[] bytesValue:
                    SetBytes(bytesValue);
                    break;
                case Vector4 colorValue:
                    SetColor(colorValue);
                    break;
                case uint uintValue:
                    SetUInt(uintValue);
                    break;
                case ulong ulongValue:
                    SetUInt64(ulongValue);
                    break;
                case double doubleValue:
                    SetFloat64(doubleValue);
                    break;
                case sbyte sbyteValue:
                    SetInt8(sbyteValue);
                    break;
                case byte byteValue:
                    SetUInt8(byteValue);
                    break;
                case short shortValue:
                    SetInt16(shortValue);
                    break;
                case ushort ushortValue:
                    SetUInt16(ushortValue);
                    break;
                case char charValue:
                    SetChar(charValue);
                    break;
                // Enum is handled as int
                // case int enumValue:
                //    SetEnum(enumValue);
                //    break;
                case ClientID clientIDValue:
                    SetClientID(clientIDValue);
                    break;
                default:
                    throw new ArgumentException($"Unsupported type {typeof(T)} for FieldData.Set<T>()");
            }
        }

        public T Get<T>()
        {
            if (typeof(T) == typeof(int))
            {
                return (T)(object)GetInt();
            }
            else if (typeof(T) == typeof(long))
            {
                return (T)(object)GetInt64();
            }
            else if (typeof(T) == typeof(bool))
            {
                return (T)(object)GetBool();
            }
            else if (typeof(T) == typeof(float))
            {
                return (T)(object)GetFloat();
            }
            else if (typeof(T) == typeof(string))
            {
                return (T)(object)GetString();
            }
            else if (typeof(T) == typeof(Vector2))
            {
                return (T)(object)GetVector2();
            }
            else if (typeof(T) == typeof(Vector3))
            {
                return (T)(object)GetVector3().ToCoreVector3();
            }
            else if (typeof(T) == typeof(Vector3d))
            {
                return (T)(object)GetVector3();
            }
            else if (typeof(T) == typeof(Quaternion))
            {
                return (T)(object)GetQuaternion();
            }
            else if (typeof(T) == typeof(Entity))
            {
                return (T)(object)GetEntity();
            }
            else if (typeof(T) == typeof(byte[]))
            {
                return (T)(object)GetBytes();
            }
            else if (typeof(T) == typeof(Vector4))
            {
                return (T)(object)GetColor();
            }
            else if (typeof(T) == typeof(uint))
            {
                return (T)(object)GetUInt();
            }
            else if (typeof(T) == typeof(ulong))
            {
                return (T)(object)GetUInt64();
            }
            else if (typeof(T) == typeof(double))
            {
                return (T)(object)GetFloat64();
            }
            else if (typeof(T) == typeof(sbyte))
            {
                return (T)(object)GetInt8();
            }
            else if (typeof(T) == typeof(byte))
            {
                return (T)(object)GetUInt8();
            }
            else if (typeof(T) == typeof(short))
            {
                return (T)(object)GetInt16();
            }
            else if (typeof(T) == typeof(ushort))
            {
                return (T)(object)GetUInt16();
            }
            else if (typeof(T) == typeof(char))
            {
                return (T)(object)GetChar();
            }
            else if (typeof(T) == typeof(ClientID))
            {
                return (T)(object)GetClientID();
            }

            throw new ArgumentException($"Unsupported type {typeof(T)} for FieldData.Get<T>()");
        }

        public int GetInt()
        {
            AssertType(SchemaType.Int);
            return MemoryMarshal.Read<int>(data);
        }

        public void SetInt(int value)
        {
            AssertType(SchemaType.Int);
            MemoryMarshal.Write(data, ref value);
        }

        public long GetInt64()
        {
            AssertType(SchemaType.Int64);
            return MemoryMarshal.Read<long>(data);
        }

        public void SetInt64(long value)
        {
            AssertType(SchemaType.Int64);
            MemoryMarshal.Write(data, ref value);
        }

        public bool GetBool()
        {
            AssertType(SchemaType.Bool);
            return MemoryMarshal.Read<bool>(data);
        }

        public void SetBool(bool value)
        {
            AssertType(SchemaType.Bool);
            MemoryMarshal.Write(data, ref value);
        }

        public float GetFloat()
        {
            AssertType(SchemaType.Float);
            return MemoryMarshal.Read<float>(data);
        }

        public void SetFloat(float value)
        {
            AssertType(SchemaType.Float);
            MemoryMarshal.Write(data, ref value);
        }

        public string GetString()
        {
            AssertType(SchemaType.String);

            var handle = MemoryMarshal.Read<HandleTable<string>.Handle>(data);
            return managedTypesTables.StringsTable.GetValue(handle);
        }

        public void SetString(string value)
        {
            AssertType(SchemaType.String);

            var existingHandle = MemoryMarshal.Read<HandleTable<string>.Handle>(data);
            managedTypesTables.StringsTable.Delete(existingHandle);

            var newIndex = managedTypesTables.StringsTable.Add(value);
            MemoryMarshal.Write(data, ref newIndex);
        }

        public Vector2 GetVector2()
        {
            AssertType(SchemaType.Vector2);
            return MemoryMarshal.Read<Vector2>(data);
        }

        public void SetVector2(Vector2 value)
        {
            AssertType(SchemaType.Vector2);
            MemoryMarshal.Write(data, ref value);
        }

        public Vector3d GetVector3()
        {
            AssertType(SchemaType.Vector3);
            return MemoryMarshal.Read<Vector3d>(data);
        }

        public void SetVector3(Vector3d value)
        {
            AssertType(SchemaType.Vector3);
            MemoryMarshal.Write(data, ref value);
        }

        public Quaternion GetQuaternion()
        {
            AssertType(SchemaType.Quaternion);
            return MemoryMarshal.Read<Quaternion>(data);
        }

        public void SetQuaternion(Quaternion value)
        {
            AssertType(SchemaType.Quaternion);
            MemoryMarshal.Write(data, ref value);
        }

        public Entity GetEntity()
        {
            AssertType(SchemaType.Entity);
            return MemoryMarshal.Read<Entity>(data);
        }

        public void SetEntity(Entity value)
        {
            AssertType(SchemaType.Entity);
            MemoryMarshal.Write(data, ref value);
        }

        public byte[] GetBytes()
        {
            AssertType(SchemaType.Bytes);

            var handle = MemoryMarshal.Read<HandleTable<byte[]>.Handle>(data);
            return managedTypesTables.ByteArraysTable.GetValue(handle);
        }

        public void SetBytes(byte[] value)
        {
            AssertType(SchemaType.Bytes);

            var existingHandle = MemoryMarshal.Read<HandleTable<byte[]>.Handle>(data);
            managedTypesTables.ByteArraysTable.Delete(existingHandle);

            var newIndex = managedTypesTables.ByteArraysTable.Add(value);
            MemoryMarshal.Write(data, ref newIndex);
        }

        public Vector4 GetColor()
        {
            AssertType(SchemaType.Color);
            return MemoryMarshal.Read<Vector4>(data);
        }

        public void SetColor(Vector4 value)
        {
            AssertType(SchemaType.Color);
            MemoryMarshal.Write(data, ref value);
        }

        public uint GetUInt()
        {
            AssertType(SchemaType.UInt);
            return MemoryMarshal.Read<uint>(data);
        }

        public void SetUInt(uint value)
        {
            AssertType(SchemaType.UInt);
            MemoryMarshal.Write(data, ref value);
        }

        public ulong GetUInt64()
        {
            AssertType(SchemaType.UInt64);
            return MemoryMarshal.Read<ulong>(data);
        }

        public void SetUInt64(ulong value)
        {
            AssertType(SchemaType.UInt64);
            MemoryMarshal.Write(data, ref value);
        }

        public double GetFloat64()
        {
            AssertType(SchemaType.Float64);
            return MemoryMarshal.Read<double>(data);
        }

        public void SetFloat64(double value)
        {
            AssertType(SchemaType.Float64);
            MemoryMarshal.Write(data, ref value);
        }

        public sbyte GetInt8()
        {
            AssertType(SchemaType.Int8);
            return MemoryMarshal.Read<sbyte>(data);
        }

        public void SetInt8(sbyte value)
        {
            AssertType(SchemaType.Int8);
            MemoryMarshal.Write(data, ref value);
        }

        public byte GetUInt8()
        {
            AssertType(SchemaType.UInt8);
            return MemoryMarshal.Read<byte>(data);
        }

        public void SetUInt8(byte value)
        {
            AssertType(SchemaType.UInt8);
            MemoryMarshal.Write(data, ref value);
        }

        public short GetInt16()
        {
            AssertType(SchemaType.Int16);
            return MemoryMarshal.Read<short>(data);
        }

        public void SetInt16(short value)
        {
            AssertType(SchemaType.Int16);
            MemoryMarshal.Write(data, ref value);
        }

        public ushort GetUInt16()
        {
            AssertType(SchemaType.UInt16);
            return MemoryMarshal.Read<ushort>(data);
        }

        public void SetUInt16(ushort value)
        {
            AssertType(SchemaType.UInt16);
            MemoryMarshal.Write(data, ref value);
        }

        public char GetChar()
        {
            AssertType(SchemaType.Char);
            return MemoryMarshal.Read<char>(data);
        }

        public void SetChar(char value)
        {
            AssertType(SchemaType.Char);
            MemoryMarshal.Write(data, ref value);
        }

        public int GetEnum()
        {
            AssertType(SchemaType.Enum);
            return MemoryMarshal.Read<int>(data);
        }

        public void SetEnum(int value)
        {
            AssertType(SchemaType.Enum);
            MemoryMarshal.Write(data, ref value);
        }

        public ClientID GetClientID()
        {
            AssertType(SchemaType.ClientID);
            return MemoryMarshal.Read<ClientID>(data);
        }

        public void SetClientID(ClientID value)
        {
            AssertType(SchemaType.ClientID);
            MemoryMarshal.Write(data, ref value);
        }

        public AbsoluteSimulationFrame GetSimFrame()
        {
            DbgAssert.ThatFmt(HasSimFrames, "Field '{0}' does not have simulation frames!", DefinitionName);
            return simFrame[0];
        }

        public void SetSimFrame(AbsoluteSimulationFrame frame)
        {
            DbgAssert.ThatFmt(HasSimFrames, "Field '{0}' does not have simulation frames!", DefinitionName);
            simFrame[0] = frame;
        }

        public bool Equals(FieldData other)
        {
            if (Type != other.Type)
            {
                return false;
            }

            switch (Type)
            {
                case SchemaType.String:
                    return GetString() == other.GetString();
                case SchemaType.Bytes:
                    var a = GetBytes();
                    var b = other.GetBytes();

                    if (a.Length != b.Length)
                    {
                        return false;
                    }

                    for (var i = 0; i < a.Length; i++)
                    {
                        if (a[i] != b[i])
                        {
                            return false;
                        }
                    }

                    return true;
                default:
                    return data.SequenceEqual(other.data);
            }
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            ToString(sb);
            return sb.ToString();
        }

        public void ToString(StringBuilder sb)
        {
            sb.Append(DefinitionName);
            sb.Append(": ");

            switch (Type)
            {
                case SchemaType.Int:
                    sb.Append(GetInt());
                    break;
                case SchemaType.Int64:
                    sb.Append(GetInt64());
                    break;
                case SchemaType.Bool:
                    sb.Append(GetBool());
                    break;
                case SchemaType.Float:
                    sb.Append(GetFloat());
                    break;
                case SchemaType.String:
                    sb.AppendFormat("\"{0}\"", GetString());
                    break;
                case SchemaType.Vector2:
                    sb.Append(GetVector2());
                    break;
                case SchemaType.Vector3:
                    sb.Append(GetVector3());
                    break;
                case SchemaType.Quaternion:
                    sb.Append(GetQuaternion());
                    break;
                case SchemaType.Entity:
                    sb.Append(GetEntity());
                    break;
                case SchemaType.Bytes:
                    var bytes = GetBytes();
                    if (bytes == null)
                    {
                        sb.Append("null-bytes");
                    }
                    else
                    {
                        sb.AppendFormat("Byte[{0}]", bytes.Length);
                    }
                    break;
                case SchemaType.Color:
                    sb.Append(GetColor());
                    break;
                case SchemaType.UInt:
                    sb.Append(GetUInt());
                    break;
                case SchemaType.UInt64:
                    sb.Append(GetUInt64());
                    break;
                case SchemaType.Float64:
                    sb.Append(GetFloat64());
                    break;
                case SchemaType.Int8:
                    sb.Append(GetInt8());
                    break;
                case SchemaType.UInt8:
                    sb.Append(GetUInt8());
                    break;
                case SchemaType.Int16:
                    sb.Append(GetInt16());
                    break;
                case SchemaType.UInt16:
                    sb.Append(GetUInt16());
                    break;
                case SchemaType.Char:
                    sb.Append(GetChar());
                    break;
                case SchemaType.Enum:
                    sb.Append(GetEnum());
                    break;
                case SchemaType.ClientID:
                    sb.Append(GetClientID());
                    break;
                default:
                    sb.Append("<Unknown Field Type>");
                    break;
            }

            if (HasSimFrames)
            {
                sb.Append("@");
                sb.Append(GetSimFrame());
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AssertType(SchemaType type)
        {
            DbgAssert.ThatFmt(definition.Type == type, "Wrong field type! Expected {0}, but got {1}", type, definition.Type);
        }
    }
}
