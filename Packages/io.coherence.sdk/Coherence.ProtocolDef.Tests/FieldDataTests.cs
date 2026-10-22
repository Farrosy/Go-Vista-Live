// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.ProtocolDef.Tests
{
    using System.Numerics;
    using Coherence.Common;
    using Coherence.Connection;
    using Coherence.Entities;
    using Coherence.Schema;
    using Coherence.SimulationFrame;
    using NUnit.Framework;

    public class FieldDataTests
    {
        private const string FieldName = "TestField";

        private ManagedTypesTables managedTypesTables;

        [SetUp]
        public void SetUp()
        {
            managedTypesTables = new ManagedTypesTables();
        }

        private FieldData CreateFieldData(SchemaType type, bool hasSimFrames = false, int fieldIndex = 0)
        {
            var fieldDefinition = new ProtocolFieldDefinition(FieldName, type, default, hasSimFrames);

            return new FieldData(
                fieldDefinition,
                managedTypesTables,
                new byte[fieldDefinition.Size],
                hasSimFrames ? new AbsoluteSimulationFrame[1] : null,
                fieldIndex);
        }

        [Test]
        [Description("GetInt and SetInt should store and retrieve int values correctly")]
        public void GetInt_SetInt_StoresAndRetrievesValue()
        {
            // Arrange
            var fieldData = CreateFieldData(SchemaType.Int);
            var expectedValue = 42;

            // Act
            fieldData.SetInt(expectedValue);
            var actualValue = fieldData.GetInt();

            // Assert
            Assert.That(actualValue, Is.EqualTo(expectedValue));
        }

        [Test]
        [Description("GetInt64 and SetInt64 should store and retrieve long values correctly")]
        public void GetInt64_SetInt64_StoresAndRetrievesValue()
        {
            // Arrange
            var fieldData = CreateFieldData(SchemaType.Int64);
            var expectedValue = long.MaxValue;

            // Act
            fieldData.SetInt64(expectedValue);
            var actualValue = fieldData.GetInt64();

            // Assert
            Assert.That(actualValue, Is.EqualTo(expectedValue));
        }

        [Test]
        [Description("GetBool and SetBool should store and retrieve bool values correctly")]
        public void GetBool_SetBool_StoresAndRetrievesValue()
        {
            // Arrange
            var fieldData = CreateFieldData(SchemaType.Bool);
            var expectedValue = true;

            // Act
            fieldData.SetBool(expectedValue);
            var actualValue = fieldData.GetBool();

            // Assert
            Assert.That(actualValue, Is.EqualTo(expectedValue));
        }

        [Test]
        [Description("GetFloat and SetFloat should store and retrieve float values correctly")]
        public void GetFloat_SetFloat_StoresAndRetrievesValue()
        {
            // Arrange
            var fieldData = CreateFieldData(SchemaType.Float);
            var expectedValue = 3.14f;

            // Act
            fieldData.SetFloat(expectedValue);
            var actualValue = fieldData.GetFloat();

            // Assert
            Assert.That(actualValue, Is.EqualTo(expectedValue));
        }

        [Test]
        [Description("GetString and SetString should store and retrieve string values correctly")]
        public void GetString_SetString_StoresAndRetrievesValue()
        {
            // Arrange
            var fieldData = CreateFieldData(SchemaType.String);
            var expectedValue = "Hello World";

            // Act
            fieldData.SetString(expectedValue);
            var actualValue = fieldData.GetString();

            // Assert
            Assert.That(actualValue, Is.EqualTo(expectedValue));
        }

        [Test]
        [Description("GetString and SetString should handle null values correctly")]
        public void GetString_SetString_HandlesNullValue()
        {
            // Arrange
            var fieldData = CreateFieldData(SchemaType.String);

            // Act
            fieldData.SetString(null);
            var actualValue = fieldData.GetString();

            // Assert
            Assert.That(actualValue, Is.Null);
        }

        [Test]
        [Description("GetVector2 and SetVector2 should store and retrieve Vector2 values correctly")]
        public void GetVector2_SetVector2_StoresAndRetrievesValue()
        {
            // Arrange
            var fieldData = CreateFieldData(SchemaType.Vector2);
            var expectedValue = new Vector2(1.5f, 2.5f);

            // Act
            fieldData.SetVector2(expectedValue);
            var actualValue = fieldData.GetVector2();

            // Assert
            Assert.That(actualValue, Is.EqualTo(expectedValue));
        }

        [Test]
        [Description("GetVector3 and SetVector3 should store and retrieve Vector3d values correctly")]
        public void GetVector3_SetVector3_StoresAndRetrievesValue()
        {
            // Arrange
            var fieldData = CreateFieldData(SchemaType.Vector3);
            var expectedValue = new Vector3d(1.0, 2.0, 3.0);

            // Act
            fieldData.SetVector3(expectedValue);
            var actualValue = fieldData.GetVector3();

            // Assert
            Assert.That(actualValue, Is.EqualTo(expectedValue));
        }

        [Test]
        [Description("GetQuaternion and SetQuaternion should store and retrieve Quaternion values correctly")]
        public void GetQuaternion_SetQuaternion_StoresAndRetrievesValue()
        {
            // Arrange
            var fieldData = CreateFieldData(SchemaType.Quaternion);
            var expectedValue = new Quaternion(0.0f, 0.0f, 0.707f, 0.707f);

            // Act
            fieldData.SetQuaternion(expectedValue);
            var actualValue = fieldData.GetQuaternion();

            // Assert
            Assert.That(actualValue, Is.EqualTo(expectedValue));
        }

        [Test]
        [Description("GetEntity and SetEntity should store and retrieve Entity values correctly")]
        public void GetEntity_SetEntity_StoresAndRetrievesValue()
        {
            // Arrange
            var fieldData = CreateFieldData(SchemaType.Entity);
            var expectedValue = new Entity((Entities.Index)123, 1, true);

            // Act
            fieldData.SetEntity(expectedValue);
            var actualValue = fieldData.GetEntity();

            // Assert
            Assert.That(actualValue, Is.EqualTo(expectedValue));
        }

        [Test]
        [Description("GetBytes and SetBytes should store and retrieve byte array values correctly")]
        public void GetBytes_SetBytes_StoresAndRetrievesValue()
        {
            // Arrange
            var fieldData = CreateFieldData(SchemaType.Bytes);
            var expectedValue = new byte[] { 1, 2, 3, 4, 5 };

            // Act
            fieldData.SetBytes(expectedValue);
            var actualValue = fieldData.GetBytes();

            // Assert
            Assert.That(actualValue, Is.EqualTo(expectedValue));
        }

        [Test]
        [Description("GetColor and SetColor should store and retrieve Vector4 color values correctly")]
        public void GetColor_SetColor_StoresAndRetrievesValue()
        {
            // Arrange
            var fieldData = CreateFieldData(SchemaType.Color);
            var expectedValue = new Vector4(1.0f, 0.5f, 0.25f, 1.0f);

            // Act
            fieldData.SetColor(expectedValue);
            var actualValue = fieldData.GetColor();

            // Assert
            Assert.That(actualValue, Is.EqualTo(expectedValue));
        }

        [Test]
        [Description("GetUInt and SetUInt should store and retrieve uint values correctly")]
        public void GetUInt_SetUInt_StoresAndRetrievesValue()
        {
            // Arrange
            var fieldData = CreateFieldData(SchemaType.UInt);
            var expectedValue = uint.MaxValue;

            // Act
            fieldData.SetUInt(expectedValue);
            var actualValue = fieldData.GetUInt();

            // Assert
            Assert.That(actualValue, Is.EqualTo(expectedValue));
        }

        [Test]
        [Description("GetUInt64 and SetUInt64 should store and retrieve ulong values correctly")]
        public void GetUInt64_SetUInt64_StoresAndRetrievesValue()
        {
            // Arrange
            var fieldData = CreateFieldData(SchemaType.UInt64);
            var expectedValue = ulong.MaxValue;

            // Act
            fieldData.SetUInt64(expectedValue);
            var actualValue = fieldData.GetUInt64();

            // Assert
            Assert.That(actualValue, Is.EqualTo(expectedValue));
        }

        [Test]
        [Description("GetFloat64 and SetFloat64 should store and retrieve double values correctly")]
        public void GetFloat64_SetFloat64_StoresAndRetrievesValue()
        {
            // Arrange
            var fieldData = CreateFieldData(SchemaType.Float64);
            var expectedValue = 3.141592653589793;

            // Act
            fieldData.SetFloat64(expectedValue);
            var actualValue = fieldData.GetFloat64();

            // Assert
            Assert.That(actualValue, Is.EqualTo(expectedValue));
        }

        [Test]
        [Description("GetInt8 and SetInt8 should store and retrieve sbyte values correctly")]
        public void GetInt8_SetInt8_StoresAndRetrievesValue()
        {
            // Arrange
            var fieldData = CreateFieldData(SchemaType.Int8);
            var expectedValue = sbyte.MinValue;

            // Act
            fieldData.SetInt8(expectedValue);
            var actualValue = fieldData.GetInt8();

            // Assert
            Assert.That(actualValue, Is.EqualTo(expectedValue));
        }

        [Test]
        [Description("GetUInt8 and SetUInt8 should store and retrieve byte values correctly")]
        public void GetUInt8_SetUInt8_StoresAndRetrievesValue()
        {
            // Arrange
            var fieldData = CreateFieldData(SchemaType.UInt8);
            var expectedValue = byte.MaxValue;

            // Act
            fieldData.SetUInt8(expectedValue);
            var actualValue = fieldData.GetUInt8();

            // Assert
            Assert.That(actualValue, Is.EqualTo(expectedValue));
        }

        [Test]
        [Description("GetInt16 and SetInt16 should store and retrieve short values correctly")]
        public void GetInt16_SetInt16_StoresAndRetrievesValue()
        {
            // Arrange
            var fieldData = CreateFieldData(SchemaType.Int16);
            var expectedValue = short.MaxValue;

            // Act
            fieldData.SetInt16(expectedValue);
            var actualValue = fieldData.GetInt16();

            // Assert
            Assert.That(actualValue, Is.EqualTo(expectedValue));
        }

        [Test]
        [Description("GetUInt16 and SetUInt16 should store and retrieve ushort values correctly")]
        public void GetUInt16_SetUInt16_StoresAndRetrievesValue()
        {
            // Arrange
            var fieldData = CreateFieldData(SchemaType.UInt16);
            var expectedValue = ushort.MaxValue;

            // Act
            fieldData.SetUInt16(expectedValue);
            var actualValue = fieldData.GetUInt16();

            // Assert
            Assert.That(actualValue, Is.EqualTo(expectedValue));
        }

        [Test]
        [Description("GetChar and SetChar should store and retrieve char values correctly")]
        public void GetChar_SetChar_StoresAndRetrievesValue()
        {
            // Arrange
            var fieldData = CreateFieldData(SchemaType.Char);
            var expectedValue = 'A';

            // Act
            fieldData.SetChar(expectedValue);
            var actualValue = fieldData.GetChar();

            // Assert
            Assert.That(actualValue, Is.EqualTo(expectedValue));
        }

        [Test]
        [Description("GetEnum and SetEnum should store and retrieve enum values as int correctly")]
        public void GetEnum_SetEnum_StoresAndRetrievesValue()
        {
            // Arrange
            var fieldData = CreateFieldData(SchemaType.Enum);
            var expectedValue = 5;

            // Act
            fieldData.SetEnum(expectedValue);
            var actualValue = fieldData.GetEnum();

            // Assert
            Assert.That(actualValue, Is.EqualTo(expectedValue));
        }

        [Test]
        [Description("GetClientID and SetClientID should store and retrieve ClientID values correctly")]
        public void GetClientID_SetClientID_StoresAndRetrievesValue()
        {
            // Arrange
            var fieldData = CreateFieldData(SchemaType.ClientID);
            var expectedValue = new ClientID(12345);

            // Act
            fieldData.SetClientID(expectedValue);
            var actualValue = fieldData.GetClientID();

            // Assert
            Assert.That(actualValue, Is.EqualTo(expectedValue));
        }

        [Test]
        [Description("GetSimFrame and SetSimFrame should store and retrieve simulation frame values correctly")]
        public void GetSimFrame_SetSimFrame_StoresAndRetrievesValue()
        {
            // Arrange
            var fieldData = CreateFieldData(SchemaType.Int, hasSimFrames: true);
            var expectedValue = new AbsoluteSimulationFrame { Frame = 1234 };

            // Act
            fieldData.SetSimFrame(expectedValue);
            var actualValue = fieldData.GetSimFrame();

            // Assert
            Assert.That(actualValue, Is.EqualTo(expectedValue));
        }

        [Test]
        [Description("Set generic method should store int values correctly")]
        public void Set_GenericInt_StoresValue()
        {
            // Arrange
            var fieldData = CreateFieldData(SchemaType.Int);
            var expectedValue = 100;

            // Act
            fieldData.Set(expectedValue);
            var actualValue = fieldData.GetInt();

            // Assert
            Assert.That(actualValue, Is.EqualTo(expectedValue));
        }

        [Test]
        [Description("Get generic method should retrieve int values correctly")]
        public void Get_GenericInt_RetrievesValue()
        {
            // Arrange
            var fieldData = CreateFieldData(SchemaType.Int);
            var expectedValue = 200;
            fieldData.SetInt(expectedValue);

            // Act
            var actualValue = fieldData.Get<int>();

            // Assert
            Assert.That(actualValue, Is.EqualTo(expectedValue));
        }

        [Test]
        [Description("Set generic method should store string values correctly")]
        public void Set_GenericString_StoresValue()
        {
            // Arrange
            var fieldData = CreateFieldData(SchemaType.String);
            var expectedValue = "TestString";

            // Act
            fieldData.Set(expectedValue);
            var actualValue = fieldData.GetString();

            // Assert
            Assert.That(actualValue, Is.EqualTo(expectedValue));
        }

        [Test]
        [Description("Get generic method should retrieve string values correctly")]
        public void Get_GenericString_RetrievesValue()
        {
            // Arrange
            var fieldData = CreateFieldData(SchemaType.String);
            var expectedValue = "GenericTest";
            fieldData.SetString(expectedValue);

            // Act
            var actualValue = fieldData.Get<string>();

            // Assert
            Assert.That(actualValue, Is.EqualTo(expectedValue));
        }

        [Test]
        [Description("CopyFrom should copy value data from another FieldData")]
        public void CopyFrom_Int_CopiesValue()
        {
            // Arrange
            var sourceField = CreateFieldData(SchemaType.Int);
            var targetField = CreateFieldData(SchemaType.Int);
            var expectedValue = 999;
            sourceField.SetInt(expectedValue);

            // Act
            targetField.CopyFrom(sourceField);

            // Assert
            Assert.That(targetField.GetInt(), Is.EqualTo(expectedValue));
            Assert.That(sourceField.GetInt(), Is.EqualTo(expectedValue));
        }

        [Test]
        [Description("CopyFrom should copy string values from another FieldData")]
        public void CopyFrom_String_CopiesValue()
        {
            // Arrange
            var sourceField = CreateFieldData(SchemaType.String);
            var targetField = CreateFieldData(SchemaType.String);
            var expectedValue = "CopiedString";
            sourceField.SetString(expectedValue);

            // Act
            targetField.CopyFrom(sourceField);

            // Assert
            Assert.That(targetField.GetString(), Is.EqualTo(expectedValue));
            Assert.That(sourceField.GetString(), Is.EqualTo(expectedValue));
        }

        [Test]
        [Description("MoveFrom should move value data and clear source")]
        public void MoveFrom_Int_MovesValueAndClearsSource()
        {
            // Arrange
            var sourceField = CreateFieldData(SchemaType.Int);
            var targetField = CreateFieldData(SchemaType.Int);
            var expectedValue = 777;
            sourceField.SetInt(expectedValue);

            // Act
            targetField.MoveFrom(sourceField);

            // Assert
            Assert.That(targetField.GetInt(), Is.EqualTo(expectedValue));
            Assert.That(sourceField.GetInt(), Is.EqualTo(0));
        }

        [Test]
        [Description("Equals should return true for equal int fields")]
        public void Equals_EqualIntFields_ReturnsTrue()
        {
            // Arrange
            var field1 = CreateFieldData(SchemaType.Int);
            var field2 = CreateFieldData(SchemaType.Int);
            var value = 555;
            field1.SetInt(value);
            field2.SetInt(value);

            // Act
            var result = field1.Equals(field2);

            // Assert
            Assert.That(result, Is.True);
        }

        [Test]
        [Description("Equals should return false for different int fields")]
        public void Equals_DifferentIntFields_ReturnsFalse()
        {
            // Arrange
            var field1 = CreateFieldData(SchemaType.Int);
            var field2 = CreateFieldData(SchemaType.Int);
            field1.SetInt(100);
            field2.SetInt(200);

            // Act
            var result = field1.Equals(field2);

            // Assert
            Assert.That(result, Is.False);
        }

        [Test]
        [Description("Equals should return true for equal string fields")]
        public void Equals_EqualStringFields_ReturnsTrue()
        {
            // Arrange
            var field1 = CreateFieldData(SchemaType.String);
            var field2 = CreateFieldData(SchemaType.String);
            var value = "TestString";
            field1.SetString(value);
            field2.SetString(value);

            // Act
            var result = field1.Equals(field2);

            // Assert
            Assert.That(result, Is.True);
        }

        [Test]
        [Description("Equals should return true for equal byte array fields")]
        public void Equals_EqualByteArrayFields_ReturnsTrue()
        {
            // Arrange
            var field1 = CreateFieldData(SchemaType.Bytes);
            var field2 = CreateFieldData(SchemaType.Bytes);
            var value = new byte[] { 1, 2, 3 };
            field1.SetBytes(value);
            field2.SetBytes(value);

            // Act
            var result = field1.Equals(field2);

            // Assert
            Assert.That(result, Is.True);
        }

        [Test]
        [Description("Equals should return false for different types")]
        public void Equals_DifferentTypes_ReturnsFalse()
        {
            // Arrange
            var intField = CreateFieldData(SchemaType.Int);
            var floatField = CreateFieldData(SchemaType.Float);

            // Act
            var result = intField.Equals(floatField);

            // Assert
            Assert.That(result, Is.False);
        }

        [Test]
        [Description("Definition properties should return correct values")]
        public void Properties_Definition_ReturnsCorrectValues()
        {
            // Arrange
            var type = SchemaType.Float;
            var hasSimFrames = true;
            var index = 2;
            var mask = 1u << index;

            var fieldData = CreateFieldData(type, hasSimFrames, index);

            // Act & Assert
            Assert.That(fieldData.Type, Is.EqualTo(type));
            Assert.That(fieldData.DefinitionName, Is.EqualTo(FieldName));
            Assert.That(fieldData.HasSimFrames, Is.EqualTo(hasSimFrames));
            Assert.That(fieldData.Index, Is.EqualTo(index));
            Assert.That(fieldData.Mask, Is.EqualTo(mask));
        }
    }
}
