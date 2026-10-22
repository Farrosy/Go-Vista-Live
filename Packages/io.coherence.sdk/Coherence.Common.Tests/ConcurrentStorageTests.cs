namespace Coherence.Common.Tests
{
    using System.Threading;
    using System.Threading.Tasks;
    using Coherence.Tests;
    using NUnit.Framework;
    using Pooling.Storage;

    /// <summary>
    /// Edit Mode unit tests for <see cref="ConcurrentStorage{T}"/>
    /// </summary>
    public class ConcurrentStorageTests : CoherenceTest
    {
        [Test, Description("TryTake returns false when storage is empty.")]
        public void TryTake_WhenEmpty_ReturnsFalse()
        {
            var storage = new ConcurrentStorage<string>();

            var result = storage.TryTake(out var item);

            Assert.That(result, Is.False);
            Assert.That(item, Is.Null);
        }

        [Test, Description("TryTake returns true and the item after Add.")]
        public void TryTake_AfterAdd_ReturnsTrueAndItem()
        {
            var storage = new ConcurrentStorage<string>();
            storage.Add("hello");

            var result = storage.TryTake(out var item);

            Assert.That(result, Is.True);
            Assert.That(item, Is.EqualTo("hello"));
        }

        [Test, Description("TryTake retrieves items in FIFO order (queue behavior).")]
        public void TryTake_MultipleItems_ReturnsInFifoOrder()
        {
            var storage = new ConcurrentStorage<int>();
            storage.Add(1);
            storage.Add(2);
            storage.Add(3);

            storage.TryTake(out var item1);
            Assert.That(item1, Is.EqualTo(1));

            storage.TryTake(out var item2);
            Assert.That(item2, Is.EqualTo(2));

            storage.TryTake(out var item3);
            Assert.That(item3, Is.EqualTo(3));
        }

        [Test, Description("TryTake returns false after all items have been taken.")]
        public void TryTake_AfterAllItemsTaken_ReturnsFalse()
        {
            var storage = new ConcurrentStorage<string>();
            storage.Add("item");
            storage.TryTake(out _);

            var result = storage.TryTake(out var item);

            Assert.That(result, Is.False);
            Assert.That(item, Is.Null);
        }

        [Test, Description("Add allows the same item to be stored multiple times.")]
        public void Add_SameItemMultipleTimes_AllCanBeRetrieved()
        {
            var storage = new ConcurrentStorage<string>();
            storage.Add("duplicate");
            storage.Add("duplicate");

            Assert.That(storage.TryTake(out var first), Is.True);
            Assert.That(first, Is.EqualTo("duplicate"));

            Assert.That(storage.TryTake(out var second), Is.True);
            Assert.That(second, Is.EqualTo("duplicate"));

            Assert.That(storage.TryTake(out _), Is.False);
        }

        [Test, Description("Storage works with value types.")]
        public void Add_ValueType_WorksCorrectly()
        {
            var storage = new ConcurrentStorage<int>();

            storage.Add(42);
            var result = storage.TryTake(out var item);

            Assert.That(result, Is.True);
            Assert.That(item, Is.EqualTo(42));
        }

        [Test, Description("TryTake returns default value for value types when empty.")]
        public void TryTake_EmptyWithValueType_ReturnsDefault()
        {
            var storage = new ConcurrentStorage<int>();

            var result = storage.TryTake(out var item);

            Assert.That(result, Is.False);
            Assert.That(item, Is.EqualTo(default(int)));
        }

        [Test, Description("Parallel Add and TryTake operations are thread-safe.")]
        public void ParallelAddAndRemove_IsThreadSafe()
        {
            const int itemsToAdd = 10_000;
            var storage = new ConcurrentStorage<int>();
            var addedCount = 0;
            var removedCount = 0;

            Parallel.For(0, itemsToAdd, i =>
            {
                storage.Add(i);
                Interlocked.Increment(ref addedCount);
            });

            Assert.That(addedCount, Is.EqualTo(itemsToAdd));

            Parallel.For(0, itemsToAdd, _ =>
            {
                if (storage.TryTake(out var _))
                {
                    Interlocked.Increment(ref removedCount);
                }
            });

            Assert.That(removedCount, Is.EqualTo(itemsToAdd));

            Assert.That(storage.TryTake(out _), Is.False);
        }

        [Test, Description("Concurrent Add and TryTake operations interleaved are thread-safe.")]
        public void ParallelInterleavedAddAndRemove_NoItemsLost()
        {
            const int itemsToRemove = 10_000;
            var storage = new ConcurrentStorage<int>();
            var removedCount = 0;
            Parallel.For(0, itemsToRemove, i =>
            {
                storage.Add(i);
                if (storage.TryTake(out var item))
                {
                    Interlocked.Increment(ref removedCount);
                }
            });

            Assert.That(removedCount, Is.EqualTo(itemsToRemove));
        }

        [Test, Description("Verifies that Add operation doesn't allocate.")]
        public void Add_NoAllocations()
        {
            var storage = new ConcurrentStorage<int>();

            AllocCounter.AssertNoAllocs(() => storage.Add(1));
        }

        [Test, Description("Verifies that TryTake operation (while there are items in the storage) doesn't allocate.")]
        public void TryTake_NotEmpty_NoAllocations()
        {
            var storage = new ConcurrentStorage<int>();
            for (var i = 0; i < 100; i++)
            {
                storage.Add(i);
            }

            var failed = false;

            AllocCounter.AssertNoAllocs(() => failed |= !storage.TryTake(out _));

            Assert.That(!failed);
        }

        [Test, Description("Verifies that TryTake operation (while there are no items in the storage) doesn't allocate.")]
        public void TryTake_Empty_NoAllocations()
        {
            var storage = new ConcurrentStorage<int>();

            var failed = false;

            AllocCounter.AssertNoAllocs(() => failed |= storage.TryTake(out _));

            Assert.That(!failed);
        }
    }
}
