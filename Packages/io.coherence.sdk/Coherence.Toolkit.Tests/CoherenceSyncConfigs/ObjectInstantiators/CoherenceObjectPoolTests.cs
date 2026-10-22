// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Toolkit.Tests
{
    using System;
    using Coherence.Tests;
    using NUnit.Framework;

    public class CoherenceObjectPoolTests : CoherenceTest
    {
        private CoherenceObjectPool<object> CreatePool(
            Func<object> create = null,
            Func<object, bool> onGet = null,
            Action<object> onRelease = null,
            Action<object> onDestroy = null,
            bool collectionCheck = true,
            int maxSize = 100) =>
            new CoherenceObjectPool<object>(
                create ?? (() => new object()),
                onGet ?? (_ => true),
                onRelease,
                onDestroy,
                collectionCheck,
                maxSize);

        [Test]
        [Description("Constructor throws ArgumentException when maxSize is zero or negative.")]
        [TestCase(0)]
        [TestCase(-1)]
        public void Constructor_ThrowsArgumentException_WhenMaxSizeIsZeroOrNegative(int maxSize) =>
            Assert.Throws<ArgumentException>(() => CreatePool(maxSize: maxSize));

        [Test]
        [Description("Constructor throws ArgumentNullException when createFunc is null.")]
        public void Constructor_ThrowsArgumentNullException_WhenCreateFuncIsNull() =>
            Assert.Throws<ArgumentNullException>(() => new CoherenceObjectPool<object>(null, _ => true));

        [Test]
        [Description("Get creates a new object and increments CountAll when the pool is empty.")]
        public void Get_IncrementsCountAll_WhenPoolIsEmpty()
        {
            // Arrange
            var pool = CreatePool();

            // Act
            _ = pool.Get();

            // Assert
            Assert.That(pool.CountAll, Is.EqualTo(1));
        }

        [Test]
        [Description("CountActive reflects the number of objects currently checked out of the pool.")]
        public void CountActive_ReflectsObjectsInUse()
        {
            // Arrange
            var pool = CreatePool();

            // Act
            _ = pool.Get();
            _ = pool.Get();

            // Assert
            Assert.That(pool.CountActive, Is.EqualTo(2));
        }

        [Test]
        [Description("Release moves an object from active to inactive, updating both counts.")]
        public void Release_MovesObjectFromActiveToInactive()
        {
            // Arrange
            var pool = CreatePool();
            var obj = pool.Get();

            // Act
            pool.Release(obj);

            // Assert
            Assert.That(pool.CountActive, Is.EqualTo(0));
            Assert.That(pool.CountInactive, Is.EqualTo(1));
        }

        [Test]
        [Description("Get returns a previously released object instead of creating a new one.")]
        public void Get_ReturnsReleasedObject_WhenPoolHasAvailableObjects()
        {
            // Arrange
            var pool = CreatePool();
            var obj = pool.Get();
            pool.Release(obj);

            // Act
            var reused = pool.Get();

            // Assert
            Assert.That(reused, Is.SameAs(obj));
            Assert.That(pool.CountAll, Is.EqualTo(1));
        }

        [Test]
        [Description("Release invokes the onRelease callback with the released object.")]
        public void Release_InvokesOnReleaseCallback()
        {
            // Arrange
            object released = null;
            var pool = CreatePool(onRelease: obj => released = obj);
            var pooled = pool.Get();

            // Act
            pool.Release(pooled);

            // Assert
            Assert.That(released, Is.SameAs(pooled));
        }

        [Test]
        [Description("Release invokes onDestroy instead of pooling when CountInactive is at maxSize.")]
        public void Release_InvokesOnDestroy_WhenPoolIsAtMaxSize()
        {
            // Arrange
            object destroyed = null;
            var pool = CreatePool(onDestroy: obj => destroyed = obj, maxSize: 1);
            var first = pool.Get();
            var second = pool.Get();
            pool.Release(first);

            // Act
            pool.Release(second);

            // Assert
            Assert.That(destroyed, Is.SameAs(second));
        }

        [Test]
        [Description("Release throws InvalidOperationException when releasing an object already in the pool.")]
        public void Release_Throws_WhenReleasingAlreadyReleasedObject()
        {
            // Arrange
            var pool = CreatePool(collectionCheck: true);
            var obj = pool.Get();
            pool.Release(obj);

            // Assert
            Assert.Throws<InvalidOperationException>(() => pool.Release(obj));
        }

        [Test]
        [Description("Release does not throw on a double-release when collectionCheck is disabled.")]
        public void Release_DoesNotThrow_WhenCollectionCheckIsDisabled()
        {
            // Arrange
            var pool = CreatePool(collectionCheck: false);
            var obj = pool.Get();
            pool.Release(obj);

            // Assert
            Assert.DoesNotThrow(() => pool.Release(obj));
        }

        [Test]
        [Description("Clear resets CountAll to zero and invokes onDestroy for every pooled object.")]
        public void Clear_ResetsCountAll_AndInvokesOnDestroyForPooledObjects()
        {
            // Arrange
            var destroyCount = 0;
            var pool = CreatePool(onDestroy: _ => destroyCount++);
            var first = pool.Get();
            var second = pool.Get();
            pool.Release(first);
            pool.Release(second);

            // Act
            pool.Clear();

            // Assert
            Assert.That(pool.CountAll, Is.EqualTo(0));
            Assert.That(destroyCount, Is.EqualTo(2));
        }

        [Test]
        [Description("ForceGet removes a specific object from the pool even when it is not at the top of the stack.")]
        public void ForceGet_RemovesSpecificObjectFromPool()
        {
            // Arrange
            var pool = CreatePool();
            var first = pool.Get();
            var second = pool.Get();
            pool.Release(first);
            pool.Release(second); // second is now on top of the stack

            // Act — first is buried beneath second; ForceGet should still remove it
            pool.ForceGet(first);

            // Assert
            Assert.That(pool.CountInactive, Is.EqualTo(1));
        }

        [Test]
        [Description("Get decrements CountAll for objects rejected by onGet and creates a replacement object.")]
        public void Get_DecrementsCountAll_WhenOnGetRejectsObject()
        {
            // Arrange
            var objs = new[] { new object(), new object() };
            var idx = 0;
            var pool = new CoherenceObjectPool<object>(
                createFunc: () => objs[idx++],
                actionOnGet: obj => obj != objs[0]);

            // Act
            var result = pool.Get();

            // Assert — objs[0] was rejected (CountAll decremented), objs[1] accepted
            Assert.That(result, Is.SameAs(objs[1]));
            Assert.That(pool.CountAll, Is.EqualTo(1));
        }

        [Test]
        [Description("ForceGet does nothing when the object is not currently in the pool.")]
        public void ForceGet_DoesNothing_WhenObjectIsNotInPool()
        {
            // Arrange
            var pool = CreatePool();
            var inPool = pool.Get();
            pool.Release(inPool);
            var notInPool = new object();
            var countAllWas = pool.CountAll;
            var countActiveWas = pool.CountActive;
            var countInactiveWas = pool.CountInactive;

            // Act
            pool.ForceGet(notInPool);

            // Assert — pool state unchanged
            Assert.That(pool.CountAll, Is.EqualTo(countAllWas));
            Assert.That(pool.CountActive, Is.EqualTo(countActiveWas));
            Assert.That(pool.CountInactive, Is.EqualTo(countInactiveWas));
        }

        [Test]
        [Description("Clear does not throw when onDestroy is null.")]
        public void Clear_DoesNotThrow_WhenOnDestroyIsNull()
        {
            // Arrange
            var pool = CreatePool(onDestroy: null);
            var obj = pool.Get();
            pool.Release(obj);

            // Assert
            Assert.DoesNotThrow(() => pool.Clear());
        }

        [Test]
        [Description("Dispose clears the pool, resetting all counts to zero.")]
        public void Dispose_ClearsPool()
        {
            // Arrange
            var pool = CreatePool();
            var obj = pool.Get();
            pool.Release(obj);

            // Act
            pool.Dispose();

            // Assert
            Assert.That(pool.CountAll, Is.EqualTo(0));
            Assert.That(pool.CountInactive, Is.EqualTo(0));
        }
    }
}
