// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.ProtocolDef.Tests
{
    using System.Collections.Generic;
    using Coherence.Common;
    using Coherence.Connection;
    using Coherence.Entities;
    using Coherence.Schema;
    using Coherence.SimulationFrame;
    using Coherence.Tests;
    using NUnit.Framework;

    public class ComponentDataTests
    {
        private ProtocolComponentDefinition simpleComponentDefinition;
        private ProtocolComponentDefinition stringsComponentDefinition;
        private ProtocolComponentDefinition byteArraysComponentDefinition;
        private ProtocolComponentDefinition complexComponentDefinition;
        private ProtocolComponentDefinition simFramesComponentDefinition;
        private ProtocolComponentDefinition entityRefsComponentDefinition;
        private ManagedTypesTables managedTypesTables;

        [SetUp]
        public void SetUp()
        {
            simpleComponentDefinition = new ProtocolComponentDefinition(1, "SimpleComponent", new List<ProtocolFieldDefinition>
            {
                new("IntField", SchemaType.Int, default),
                new("FloatField", SchemaType.Float, default),
            });
            stringsComponentDefinition = new ProtocolComponentDefinition(2, "StringsComponent", new List<ProtocolFieldDefinition>
            {
                new("StringField1", SchemaType.String, default),
                new("StringField2", SchemaType.String, default),
            });
            byteArraysComponentDefinition = new ProtocolComponentDefinition(3, "ByteArraysComponent", new List<ProtocolFieldDefinition>
            {
                new("ByteArrayField1", SchemaType.Bytes, default),
                new("ByteArrayField2", SchemaType.Bytes, default),
            });
            complexComponentDefinition = new ProtocolComponentDefinition(4, "ComplexComponent", new List<ProtocolFieldDefinition>
            {
                new("ByteArrayField", SchemaType.Bytes, default),
                new("ClientIDField", SchemaType.ClientID, default),
                new("Vector3Field", SchemaType.Vector3, default),
                new("EntityField", SchemaType.Entity, default),
                new("StringField", SchemaType.String, default),
                new("BoolField", SchemaType.Bool, default),
                new("CharField", SchemaType.Char, default)
            });
            simFramesComponentDefinition = new ProtocolComponentDefinition(5, "SimFramesComponent", new List<ProtocolFieldDefinition>
            {
                new("Field1", SchemaType.Int, default, hasSimFrames: true),
                new("Field2", SchemaType.Float, default, hasSimFrames: true),
            });
            entityRefsComponentDefinition = new ProtocolComponentDefinition(6, "EntityRefsComponent", new List<ProtocolFieldDefinition>
            {
                new("EntityField1", SchemaType.Entity, default),
                new("EntityField2", SchemaType.Entity, default),
                new("EntityField3", SchemaType.Entity, default),
                new("NotEntityField", SchemaType.Int, default),
            });

            managedTypesTables = new ManagedTypesTables();
        }

        private ComponentData CreateComponentData(ProtocolComponentDefinition componentDefinition)
        {
            return new ComponentData(
                componentDefinition,
                managedTypesTables,
                new byte[componentDefinition.DataSize],
                new AbsoluteSimulationFrame[ProtocolComponentDefinition.MaxFields]);
        }

        [TestCase(0, 0f)]
        [TestCase(1, 1.1f)]
        [TestCase(-10, -3.14f)]
        [TestCase(int.MaxValue, float.MaxValue)]
        [TestCase(int.MinValue, float.MinValue)]
        public void TestSimpleComponent(int intValue, float floatValue)
        {
            var componentData = CreateComponentData(simpleComponentDefinition);
            var intField = componentData.GetField(0);
            var floatField = componentData.GetField(1);

            intField.SetInt(intValue);
            floatField.SetFloat(floatValue);

            Assert.That(intField.GetInt(), Is.EqualTo(intValue));
            Assert.That(floatField.GetFloat(), Is.EqualTo(floatValue));
        }

        [TestCase(null, null, "v1", "v2")]
        [TestCase("", "", "v1", "v2")]
        [TestCase("hello", "world", null, null)]
        public void TestStringsComponent(string str1, string str2, string str1New, string str2New)
        {
            var componentData = CreateComponentData(stringsComponentDefinition);
            var stringField1 = componentData.GetField(0);
            var stringField2 = componentData.GetField(1);

            stringField1.SetString(str1);
            stringField2.SetString(str2);

            Assert.That(stringField1.GetString(), Is.EqualTo(str1));
            Assert.That(stringField2.GetString(), Is.EqualTo(str2));

            stringField1.SetString(str1New);
            stringField2.SetString(str2New);

            Assert.That(stringField1.GetString(), Is.EqualTo(str1New));
            Assert.That(stringField2.GetString(), Is.EqualTo(str2New));
        }

        [TestCase(null, null, new byte[] { 1, 2 }, new byte[] { 2, 3 })]
        [TestCase(new byte[] { }, new byte[] { }, new byte[] { 3, 4 }, new byte[] { 4, 5 })]
        [TestCase(new byte[] { 10, 20, 30 }, new byte[] { 40, 50, 60 }, null, null)]
        public void TestByteArraysComponent(byte[] arr1, byte[] arr2, byte[] arr1New, byte[] arr2New)
        {
            var componentData = CreateComponentData(byteArraysComponentDefinition);
            var byteArrayField1 = componentData.GetField(0);
            var byteArrayField2 = componentData.GetField(1);

            byteArrayField1.SetBytes(arr1);
            byteArrayField2.SetBytes(arr2);

            Assert.That(byteArrayField1.GetBytes(), Is.EqualTo(arr1));
            Assert.That(byteArrayField2.GetBytes(), Is.EqualTo(arr2));

            byteArrayField1.SetBytes(arr1New);
            byteArrayField2.SetBytes(arr2New);

            Assert.That(byteArrayField1.GetBytes(), Is.EqualTo(arr1New));
            Assert.That(byteArrayField2.GetBytes(), Is.EqualTo(arr2New));
        }

        [Test]
        public void TestComplexComponent()
        {
            var componentData = CreateComponentData(complexComponentDefinition);
            var byteArrayField = componentData.GetField(0);
            var clientIDField = componentData.GetField(1);
            var vector3Field = componentData.GetField(2);
            var entityField = componentData.GetField(3);
            var stringField = componentData.GetField(4);
            var boolField = componentData.GetField(5);
            var charField = componentData.GetField(6);

            Assert.That(byteArrayField.GetBytes(), Is.Null);
            Assert.That(clientIDField.GetClientID(), Is.EqualTo(new ClientID(0)));
            Assert.That(vector3Field.GetVector3(), Is.EqualTo(Vector3d.zero));
            Assert.That(entityField.GetEntity(), Is.EqualTo(new Entity(0, 0, false)));
            Assert.That(stringField.GetString(), Is.Null);
            Assert.That(boolField.GetBool(), Is.False);
            Assert.That(charField.GetChar(), Is.EqualTo('\0'));

            var byteArrayValue = new byte[] { 1, 2, 3 };
            var clientIDValue = new ClientID(66443);
            var vector3Value = new Vector3d(1.0f, 2.0f, 3.0f);
            var entityValue = new Entity((Index)123, 1, true);
            var stringValue = "TestString";
            var boolValue = true;
            var charValue = '{';

            byteArrayField.SetBytes(byteArrayValue);
            clientIDField.SetClientID(clientIDValue);
            vector3Field.SetVector3(vector3Value);
            entityField.SetEntity(entityValue);
            stringField.SetString(stringValue);
            boolField.SetBool(boolValue);
            charField.SetChar(charValue);

            Assert.That(byteArrayField.GetBytes(), Is.EqualTo(byteArrayValue));
            Assert.That(clientIDField.GetClientID(), Is.EqualTo(clientIDValue));
            Assert.That(vector3Field.GetVector3(), Is.EqualTo(vector3Value));
            Assert.That(entityField.GetEntity(), Is.EqualTo(entityValue));
            Assert.That(stringField.GetString(), Is.EqualTo(stringValue));
            Assert.That(boolField.GetBool(), Is.EqualTo(boolValue));
            Assert.That(charField.GetChar(), Is.EqualTo(charValue));
        }

        [Test]
        public void TestManagedTypeHandling()
        {
            Assert.That(managedTypesTables.StringsTable.Count, Is.EqualTo(0));

            var componentData = CreateComponentData(stringsComponentDefinition);
            var stringField = componentData.GetField(0);

            stringField.SetString("Hello");

            Assert.That(managedTypesTables.StringsTable.Count, Is.EqualTo(1));

            stringField.SetString("World");

            Assert.That(managedTypesTables.StringsTable.Count, Is.EqualTo(1));

            stringField.SetString(null);

            Assert.That(managedTypesTables.StringsTable.Count, Is.EqualTo(0));

            stringField.SetString("NewString");

            Assert.That(managedTypesTables.StringsTable.Count, Is.EqualTo(1));

            componentData.Dispose();

            Assert.That(managedTypesTables.StringsTable.Count, Is.EqualTo(0));
        }

        [Test]
        public void TestIsInitialized()
        {
            var componentData = new ComponentData();
            Assert.That(componentData.IsInitialized, Is.False);

            componentData = CreateComponentData(simpleComponentDefinition);
            Assert.That(componentData.IsInitialized, Is.True);
        }

        [Test]
        public void TestFieldsMaskOperations()
        {
            var componentData = CreateComponentData(simpleComponentDefinition);

            Assert.That(componentData.FieldsMask, Is.EqualTo(0u));
            Assert.That(componentData.IsFieldSet(0), Is.False);
            Assert.That(componentData.IsFieldSet(1), Is.False);

            componentData.SetFieldSet(0, true);
            Assert.That(componentData.FieldsMask, Is.EqualTo(0b01u));
            Assert.That(componentData.IsFieldSet(0), Is.True);
            Assert.That(componentData.IsFieldSet(1), Is.False);

            componentData.SetFieldSet(1, true);
            Assert.That(componentData.FieldsMask, Is.EqualTo(0b11u));
            Assert.That(componentData.IsFieldSet(0), Is.True);
            Assert.That(componentData.IsFieldSet(1), Is.True);

            componentData.SetFieldSet(0, false);
            Assert.That(componentData.FieldsMask, Is.EqualTo(0b10u));
            Assert.That(componentData.IsFieldSet(0), Is.False);
            Assert.That(componentData.IsFieldSet(1), Is.True);
        }

        [Test]
        public void TestStoppedMask()
        {
            var componentData = CreateComponentData(simpleComponentDefinition);

            componentData.StoppedMask = 0b11u;
            Assert.That(componentData.StoppedMask, Is.EqualTo(0b11u));

            componentData.ClearStoppedMask(0b01u);
            Assert.That(componentData.StoppedMask, Is.EqualTo(0b10u));

            componentData.ClearStoppedMask(0b10u);
            Assert.That(componentData.StoppedMask, Is.EqualTo(0b00u));
        }

        [Test]
        public void TestClearFieldsMask()
        {
            var componentData = CreateComponentData(simpleComponentDefinition);

            componentData.FieldsMask = 0b11u;
            Assert.That(componentData.FieldsMask, Is.EqualTo(0b11u));

            componentData.ClearFieldsMask(0b01u);
            Assert.That(componentData.FieldsMask, Is.EqualTo(0b10u));

            componentData.ClearFieldsMask(0b10u);
            Assert.That(componentData.FieldsMask, Is.EqualTo(0b00u));
        }

        [Test]
        public void TestInitialFieldsMask()
        {
            var componentData = CreateComponentData(simpleComponentDefinition);
            Assert.That(componentData.InitialFieldsMask(), Is.EqualTo(0b11u));

            var componentData2 = CreateComponentData(complexComponentDefinition);
            Assert.That(componentData2.InitialFieldsMask(), Is.EqualTo(0b1111111u));
        }

        [Test]
        public void TestUpdate()
        {
            var componentData1 = CreateComponentData(simpleComponentDefinition);
            var componentData2 = CreateComponentData(simpleComponentDefinition);

            componentData1.GetField(0).SetInt(10);
            componentData1.GetField(1).SetFloat(1.5f);
            componentData1.FieldsMask = componentData1.InitialFieldsMask();

            componentData2.GetField(0).SetInt(20);
            componentData2.SetFieldSet(0, true);

            componentData1.Update(componentData2);

            Assert.That(componentData1.GetField(0).GetInt(), Is.EqualTo(20));
            Assert.That(componentData1.GetField(1).GetFloat(), Is.EqualTo(1.5f));
            Assert.That(componentData1.FieldsMask, Is.EqualTo(0b11u));
        }

        [Test]
        public void TestUpdateWithStoppedMask()
        {
            var componentData1 = CreateComponentData(simpleComponentDefinition);
            var componentData2 = CreateComponentData(simpleComponentDefinition);

            componentData1.StoppedMask = 0b11u;
            componentData2.FieldsMask = 0b01u;
            componentData2.StoppedMask = 0b10u;

            componentData1.Update(componentData2);

            Assert.That(componentData1.FieldsMask, Is.EqualTo(0b01u));
            Assert.That(componentData1.StoppedMask, Is.EqualTo(0b10u));
        }

        [Test]
        public void TestGetMinSimulationFrame()
        {
            var componentData = CreateComponentData(simFramesComponentDefinition);

            var result = componentData.GetMinSimulationFrame();
            Assert.That(result, Is.Null);

            componentData.GetField(0).SetSimFrame(new AbsoluteSimulationFrame { Frame = 100 });
            componentData.SetFieldSet(0, true);

            result = componentData.GetMinSimulationFrame();
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Value.Frame, Is.EqualTo(100));

            componentData.GetField(1).SetSimFrame(new AbsoluteSimulationFrame { Frame = 50 });
            componentData.SetFieldSet(1, true);

            result = componentData.GetMinSimulationFrame();
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Value.Frame, Is.EqualTo(50));
        }

        [Test]
        public void TestReplaceReferences()
        {
            var componentData = CreateComponentData(entityRefsComponentDefinition);

            var fromEntity = new Entity((Index)100, 1, false);
            var toEntity = new Entity((Index)200, 2, false);

            var entityField = componentData.GetField(1);
            entityField.SetEntity(fromEntity);

            var refsMask = componentData.ReplaceReferences(fromEntity, toEntity);

            Assert.That(entityField.GetEntity(), Is.EqualTo(toEntity));
            Assert.That(refsMask, Is.EqualTo(entityField.Mask));
        }

        [Test]
        public void TestReplaceReferences_NoMatch()
        {
            var componentData = CreateComponentData(entityRefsComponentDefinition);

            var fromEntity = new Entity((Index)100, 1, false);
            var toEntity = new Entity((Index)200, 2, false);
            var differentEntity = new Entity((Index)300, 3, false);

            var entityField = componentData.GetField(0);
            entityField.SetEntity(differentEntity);

            var refsMask = componentData.ReplaceReferences(fromEntity, toEntity);

            Assert.That(entityField.GetEntity(), Is.EqualTo(differentEntity));
            Assert.That(refsMask, Is.EqualTo(0u));
        }

        [Test]
        public void TestDispose_WithManagedTypes()
        {
            var componentData = CreateComponentData(stringsComponentDefinition);

            componentData.GetField(0).SetString("Test1");
            componentData.GetField(1).SetString("Test2");

            Assert.That(managedTypesTables.StringsTable.Count, Is.EqualTo(2));

            componentData.Dispose();

            Assert.That(managedTypesTables.StringsTable.Count, Is.EqualTo(0));
        }

        [Test]
        public void TestFields()
        {
            var componentData = CreateComponentData(simpleComponentDefinition);

            var count = 0;

            foreach (var field in componentData.Fields)
            {
                if (count == 0)
                {
                    Assert.That(field.DefinitionName, Is.EqualTo("IntField"));
                }
                else if (count == 1)
                {
                    Assert.That(field.DefinitionName, Is.EqualTo("FloatField"));
                }
                count++;
            }

            Assert.That(count, Is.EqualTo(2));
        }

        [Test]
        public void TestFields_NoAllocations()
        {
            var componentData = CreateComponentData(complexComponentDefinition);

            AllocCounter.AssertNoAllocs(() =>
            {
                foreach (var field in componentData.Fields)
                {
                }
            });
        }

        [Test]
        public void TestEntityFields()
        {
            var componentData = CreateComponentData(entityRefsComponentDefinition);
            var entity1 = new Entity((Index)1, 1, false);
            var entity2 = new Entity((Index)2, 1, false);
            var entity3 = new Entity((Index)3, 1, false);

            componentData.GetField(0).SetEntity(entity1);
            componentData.GetField(1).SetEntity(entity2);
            componentData.GetField(2).SetEntity(entity3);

            var count = 0;

            foreach (var field in componentData.EntityFields)
            {
                if (count == 0)
                {
                    Assert.That(field.GetEntity(), Is.EqualTo(entity1));
                }
                else if (count == 1)
                {
                    Assert.That(field.GetEntity(), Is.EqualTo(entity2));
                }
                else if (count == 2)
                {
                    Assert.That(field.GetEntity(), Is.EqualTo(entity3));
                }

                count++;
            }

            Assert.That(count, Is.EqualTo(3));
        }

        [Test]
        public void TestEntityFields_NoAllocations()
        {
            var componentData = CreateComponentData(entityRefsComponentDefinition);

            AllocCounter.AssertNoAllocs(() =>
            {
                foreach (var field in componentData.EntityFields)
                {
                }
            });
        }

        [Test]
        [Description("Verifies that GetMemorySize() returns zero for an uninitialized ComponentData.")]
        public void TestGetMemorySize_Uninitialized_ReturnsZero()
        {
            var componentData = new ComponentData();
            Assert.That(componentData.GetMemorySize(), Is.EqualTo(0));
        }

        [Test]
        [Description("Verifies that GetMemorySize() returns the exact field buffer and sim-frames array size for a value-type component.")]
        public void TestGetMemorySize_ValueTypeComponent_ReturnsPositiveSize()
        {
            var componentData = CreateComponentData(simpleComponentDefinition);
            var expectedSize = simpleComponentDefinition.DataSize +
                               ProtocolComponentDefinition.MaxFields * Unsafe.Utils.SizeOf<AbsoluteSimulationFrame>();
            Assert.That(componentData.GetMemorySize(), Is.EqualTo(expectedSize));
        }

        [Test]
        [Description("Verifies that GetMemorySize() increases by the byte array length when a bytes field is set.")]
        public void TestGetMemorySize_BytesComponent_GrowsWithByteArrayLength()
        {
            var componentData = CreateComponentData(byteArraysComponentDefinition);
            var baseSize = componentData.GetMemorySize();

            componentData.GetField(0).SetBytes(new byte[100]);

            Assert.That(componentData.GetMemorySize(), Is.EqualTo(baseSize + 100));
        }

        [Test]
        [Description("Verifies that GetMemorySize() decreases by the byte array length when a bytes field is cleared to null.")]
        public void TestGetMemorySize_BytesComponent_ShrinkWhenBytesCleared()
        {
            var componentData = CreateComponentData(byteArraysComponentDefinition);
            componentData.GetField(0).SetBytes(new byte[100]);
            var sizeWithBytes = componentData.GetMemorySize();

            componentData.GetField(0).SetBytes(null);

            Assert.That(componentData.GetMemorySize(), Is.EqualTo(sizeWithBytes - 100));
        }

        [Test]
        [Description("Verifies that GetMemorySize() increases by string.Length * sizeof(char) when a string field is set.")]
        public void TestGetMemorySize_StringComponent_GrowsWithStringLength()
        {
            var componentData = CreateComponentData(stringsComponentDefinition);
            var baseSize = componentData.GetMemorySize();

            var str = "HelloWorld"; // 10 chars
            componentData.GetField(0).SetString(str);

            Assert.That(componentData.GetMemorySize(), Is.EqualTo(baseSize + str.Length * sizeof(char)));
        }

        [Test]
        [Description("Verifies that GetMemorySize() decreases by string.Length * sizeof(char) when a string field is cleared to null.")]
        public void TestGetMemorySize_StringComponent_ShrinkWhenStringCleared()
        {
            var componentData = CreateComponentData(stringsComponentDefinition);
            var str = "HelloWorld"; // 10 chars
            componentData.GetField(0).SetString(str);
            var sizeWithString = componentData.GetMemorySize();

            componentData.GetField(0).SetString(null);

            Assert.That(componentData.GetMemorySize(), Is.EqualTo(sizeWithString - str.Length * sizeof(char)));
        }
    }
}

