// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.ProtocolDef.Tests
{
    using NUnit.Framework;

    public class HandleTableTests
    {
        private class TestObject
        {
            public int Value { get; set; }

            public TestObject(int value)
            {
                Value = value;
            }
        }

        private HandleTable<TestObject> handleTable;

        [SetUp]
        public void SetUp()
        {
            handleTable = new HandleTable<TestObject>();
        }

        [Test, Description("Adding a valid object returns a non-empty handle")]
        public void Add_ValidObject_ReturnsNonEmptyHandle()
        {
            // Arrange
            var obj = new TestObject(42);

            // Act
            var handle = handleTable.Add(obj);

            // Assert
            Assert.That(handle, Is.Not.EqualTo(HandleTable<TestObject>.Handle.Empty));
            Assert.That(handle.GetArrayIndex(), Is.GreaterThanOrEqualTo(0));
            Assert.That(handle.GetVersion(), Is.EqualTo(0));
        }

        [Test, Description("Adding null returns an empty handle")]
        public void Add_NullObject_ReturnsEmptyHandle()
        {
            // Arrange & Act
            var handle = handleTable.Add(null);

            // Assert
            Assert.That(handle, Is.EqualTo(HandleTable<TestObject>.Handle.Empty));
        }

        [Test, Description("Count is zero when table is empty")]
        public void Count_EmptyTable_ReturnsZero()
        {
            // Arrange & Act
            var count = handleTable.Count;

            // Assert
            Assert.That(count, Is.EqualTo(0));
        }

        [Test, Description("Count increments when objects are added")]
        public void Count_AfterAddingObjects_ReturnsCorrectCount()
        {
            // Arrange
            var obj1 = new TestObject(1);
            var obj2 = new TestObject(2);
            var obj3 = new TestObject(3);

            // Act
            handleTable.Add(obj1);
            handleTable.Add(obj2);
            handleTable.Add(obj3);

            // Assert
            Assert.That(handleTable.Count, Is.EqualTo(3));
        }

        [Test, Description("Count decrements when objects are deleted")]
        public void Count_AfterDeletingObjects_ReturnsCorrectCount()
        {
            // Arrange
            var obj1 = new TestObject(1);
            var obj2 = new TestObject(2);
            var handle1 = handleTable.Add(obj1);
            var handle2 = handleTable.Add(obj2);

            // Act
            handleTable.Delete(handle1);

            // Assert
            Assert.That(handleTable.Count, Is.EqualTo(1));
        }

        [Test, Description("GetValue returns the correct object for a valid handle")]
        public void GetValue_ValidHandle_ReturnsCorrectObject()
        {
            // Arrange
            var obj = new TestObject(123);
            var handle = handleTable.Add(obj);

            // Act
            var retrieved = handleTable.GetValue(handle);

            // Assert
            Assert.That(retrieved, Is.SameAs(obj));
            Assert.That(retrieved.Value, Is.EqualTo(123));
        }

        [Test, Description("GetValue returns null for an empty handle")]
        public void GetValue_EmptyHandle_ReturnsNull()
        {
            // Arrange
            var handle = HandleTable<TestObject>.Handle.Empty;

            // Act
            var retrieved = handleTable.GetValue(handle);

            // Assert
            Assert.That(retrieved, Is.Null);
        }

        [Test, Description("Delete with empty handle does nothing")]
        public void Delete_EmptyHandle_DoesNothing()
        {
            // Arrange
            var obj = new TestObject(1);
            handleTable.Add(obj);
            var initialCount = handleTable.Count;

            // Act
            handleTable.Delete(HandleTable<TestObject>.Handle.Empty);

            // Assert
            Assert.That(handleTable.Count, Is.EqualTo(initialCount));
        }

        [Test, Description("Delete removes the object and updates count")]
        public void Delete_ValidHandle_RemovesObject()
        {
            // Arrange
            var obj = new TestObject(42);
            var handle = handleTable.Add(obj);

            // Act
            handleTable.Delete(handle);

            // Assert
            Assert.That(handleTable.Count, Is.EqualTo(0));
        }

        [Test, Description("Handle version increments after deletion")]
        public void Delete_ValidHandle_IncrementsVersion()
        {
            // Arrange
            var obj1 = new TestObject(1);
            var handle1 = handleTable.Add(obj1);
            var initialVersion = handle1.GetVersion();

            // Act
            handleTable.Delete(handle1);
            var obj2 = new TestObject(2);
            var handle2 = handleTable.Add(obj2);

            // Assert
            Assert.That(handle2.GetVersion(), Is.EqualTo(initialVersion + 1));
            Assert.That(handle2.GetArrayIndex(), Is.EqualTo(handle1.GetArrayIndex()));
        }

        [Test, Description("Adding multiple objects generates unique handles")]
        public void Add_MultipleObjects_GeneratesUniqueHandles()
        {
            // Arrange
            var obj1 = new TestObject(1);
            var obj2 = new TestObject(2);
            var obj3 = new TestObject(3);

            // Act
            var handle1 = handleTable.Add(obj1);
            var handle2 = handleTable.Add(obj2);
            var handle3 = handleTable.Add(obj3);

            // Assert
            Assert.That(handle1, Is.Not.EqualTo(handle2));
            Assert.That(handle2, Is.Not.EqualTo(handle3));
            Assert.That(handle1, Is.Not.EqualTo(handle3));
        }

        [Test, Description("Reusing a slot after deletion uses the free list")]
        public void Add_AfterDeletion_ReusesSlot()
        {
            // Arrange
            var obj1 = new TestObject(1);
            var handle1 = handleTable.Add(obj1);
            var arrayIndex1 = handle1.GetArrayIndex();
            handleTable.Delete(handle1);

            // Act
            var obj2 = new TestObject(2);
            var handle2 = handleTable.Add(obj2);
            var arrayIndex2 = handle2.GetArrayIndex();

            // Assert
            Assert.That(arrayIndex2, Is.EqualTo(arrayIndex1));
            Assert.That(handle2.GetVersion(), Is.EqualTo(handle1.GetVersion() + 1));
        }

        [Test, Description("Multiple add and delete operations maintain correct count")]
        public void AddAndDelete_MultipleOperations_MaintainsCorrectCount()
        {
            // Arrange
            var obj1 = new TestObject(1);
            var obj2 = new TestObject(2);
            var obj3 = new TestObject(3);

            // Act
            var handle1 = handleTable.Add(obj1);
            var handle2 = handleTable.Add(obj2);
            Assert.That(handleTable.Count, Is.EqualTo(2));

            handleTable.Delete(handle1);
            Assert.That(handleTable.Count, Is.EqualTo(1));

            var handle3 = handleTable.Add(obj3);
            Assert.That(handleTable.Count, Is.EqualTo(2));

            handleTable.Delete(handle2);
            handleTable.Delete(handle3);

            // Assert
            Assert.That(handleTable.Count, Is.EqualTo(0));
        }

        [Test, Description("GetValue for multiple handles returns correct objects")]
        public void GetValue_MultipleHandles_ReturnsCorrectObjects()
        {
            // Arrange
            var obj1 = new TestObject(10);
            var obj2 = new TestObject(20);
            var obj3 = new TestObject(30);
            var handle1 = handleTable.Add(obj1);
            var handle2 = handleTable.Add(obj2);
            var handle3 = handleTable.Add(obj3);

            // Act
            var retrieved1 = handleTable.GetValue(handle1);
            var retrieved2 = handleTable.GetValue(handle2);
            var retrieved3 = handleTable.GetValue(handle3);

            // Assert
            Assert.That(retrieved1.Value, Is.EqualTo(10));
            Assert.That(retrieved2.Value, Is.EqualTo(20));
            Assert.That(retrieved3.Value, Is.EqualTo(30));
        }

        [Test, Description("Handle equality operator works correctly")]
        public void HandleEquality_SameHandles_ReturnsTrue()
        {
            // Arrange
            var obj = new TestObject(1);
            var handle1 = handleTable.Add(obj);
            var handle2 = handle1;

            // Act & Assert
            Assert.That(handle1 == handle2, Is.True);
            Assert.That(handle1 != handle2, Is.False);
        }

        [Test, Description("Handle equality operator returns false for different handles")]
        public void HandleEquality_DifferentHandles_ReturnsFalse()
        {
            // Arrange
            var obj1 = new TestObject(1);
            var obj2 = new TestObject(2);
            var handle1 = handleTable.Add(obj1);
            var handle2 = handleTable.Add(obj2);

            // Act & Assert
            Assert.That(handle1 == handle2, Is.False);
            Assert.That(handle1 != handle2, Is.True);
        }

        [Test, Description("Handle Equals method works correctly")]
        public void HandleEquals_SameHandle_ReturnsTrue()
        {
            // Arrange
            var obj = new TestObject(1);
            var handle = handleTable.Add(obj);

            // Act & Assert
            Assert.That(handle.Equals(handle), Is.True);
        }

        [Test, Description("Handle Equals method returns false for different object type")]
        public void HandleEquals_DifferentObjectType_ReturnsFalse()
        {
            // Arrange
            var obj = new TestObject(1);
            var handle = handleTable.Add(obj);
            var differentObject = new object();

            // Act & Assert
            Assert.That(handle.Equals(differentObject), Is.False);
        }

        [Test, Description("Handle GetHashCode returns consistent values")]
        public void HandleGetHashCode_SameHandle_ReturnsSameHashCode()
        {
            // Arrange
            var obj = new TestObject(1);
            var handle = handleTable.Add(obj);

            // Act
            var hashCode1 = handle.GetHashCode();
            var hashCode2 = handle.GetHashCode();

            // Assert
            Assert.That(hashCode1, Is.EqualTo(hashCode2));
        }

        [Test, Description("Empty handle has array index of -1")]
        public void EmptyHandle_ArrayIndex_IsNegativeOne()
        {
            // Arrange
            var emptyHandle = HandleTable<TestObject>.Handle.Empty;

            // Act
            var arrayIndex = emptyHandle.GetArrayIndex();

            // Assert
            Assert.That(arrayIndex, Is.EqualTo(-1));
        }

        [Test, Description("Empty handle has version of 0")]
        public void EmptyHandle_Version_IsZero()
        {
            // Arrange
            var emptyHandle = HandleTable<TestObject>.Handle.Empty;

            // Act
            var version = emptyHandle.GetVersion();

            // Assert
            Assert.That(version, Is.EqualTo(0));
        }

        [Test, Description("Large number of operations maintains data integrity")]
        public void StressTest_ManyOperations_MaintainsIntegrity()
        {
            // Arrange
            var handles = new System.Collections.Generic.List<HandleTable<TestObject>.Handle>();
            const int operationCount = 1000;

            // Act
            for (int i = 0; i < operationCount; i++)
            {
                var obj = new TestObject(i);
                var handle = handleTable.Add(obj);
                handles.Add(handle);
            }

            Assert.That(handleTable.Count, Is.EqualTo(operationCount));

            for (int i = 0; i < handles.Count; i += 2)
            {
                handleTable.Delete(handles[i]);
            }

            Assert.That(handleTable.Count, Is.EqualTo(operationCount / 2));

            for (int i = 1; i < handles.Count; i += 2)
            {
                var retrieved = handleTable.GetValue(handles[i]);
                Assert.That(retrieved, Is.Not.Null);
                Assert.That(retrieved.Value, Is.EqualTo(i));
            }

            for (int i = 0; i < operationCount / 2; i++)
            {
                var obj = new TestObject(i + operationCount);
                handleTable.Add(obj);
            }

            // Assert
            Assert.That(handleTable.Count, Is.EqualTo(operationCount));
        }
    }
}

