// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Toolkit.Tests
{
    using Bindings;
    using Bindings.ValueBindings;
    using NUnit.Framework;
    using Coherence.Tests;

    public class DescriptorTests : CoherenceTest
    {
        private Descriptor descriptor1;
        private Descriptor descriptor2;
        private Descriptor descriptor1_1;
        private Descriptor descriptor2_1;

        private Descriptor nullDescriptor1 = null;
        private Descriptor nullDescriptor2 = null;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();

            descriptor1 = new Descriptor("Foo", typeof(FooType), typeof(BoolBinding));

            descriptor2 = new Descriptor("Bar", typeof(BarType), typeof(BoolBinding));

            descriptor1_1 = new Descriptor("Foo", typeof(FooType), typeof(BoolBinding));

            descriptor2_1 = new Descriptor("Bar", typeof(BarType), typeof(BoolBinding));
        }

        [Test]
        public void Should_ReturnTrue_When_ComparingEqualityAndBothDescriptorsAreNull()
        {
            Assert.IsTrue(nullDescriptor1 == nullDescriptor2);
        }

        [Test]
        public void Should_ReturnFalse_When_ComparingEqualityAndBothDescriptorsAreValidAndDifferent()
        {
            Assert.IsFalse(descriptor1 == descriptor2);
        }

        [Test]
        public void Should_ReturnFalse_When_ComparingEqualityAndOneDescriptorIsNull()
        {
            Assert.IsFalse(nullDescriptor1 == descriptor2);
        }

        [Test]
        public void Should_ReturnTrue_When_ComparingEqualityAndBothDescriptorsAreSameInstance()
        {
            var hashcode1 = descriptor1.GetHashCode();
            var hashcode2 = descriptor1.GetHashCode();

            Assert.IsTrue(hashcode1 == hashcode2);
        }

        [Test]
        public void Should_ReturnTrue_When_ComparingEqualityAndBothDescriptorsHaveSameHashCode()
        {
            var hashcode1 = descriptor1.GetHashCode();
            var hashcode2 = descriptor1_1.GetHashCode();

            Assert.IsTrue(hashcode1 == hashcode2);
            Assert.IsTrue(descriptor1 == descriptor1_1);
        }

        [Test]
        public void Should_ReturnFalse_When_ComparingNonEqualityAndBothDescriptorsHaveSameHashCode()
        {
            var hashcode1 = descriptor2.GetHashCode();
            var hashcode2 = descriptor2_1.GetHashCode();

            Assert.IsTrue(hashcode1 == hashcode2);
            Assert.IsFalse(descriptor2 != descriptor2_1);
        }
    }

    [TestFixture]
    public class IsDescriptorRelated_MethodTests : CoherenceTest
    {
        private class MethodOwner
        {
            public void MyMethod() { }
            public void MyMethod(int arg) { }
            public void OtherMethod() { }
        }

        [Test]
        public void IsDescriptorRelated_ReturnsTrue_WhenSameMethodButRequiredChanged()
        {
            // Simulates [Command] being removed: stored descriptor was required, fresh is not.
            var methodInfo = typeof(MethodOwner).GetMethod(nameof(MethodOwner.MyMethod), System.Type.EmptyTypes);
            var stored = new Descriptor(typeof(MethodOwner), methodInfo);
            stored.Required = true;
            var fresh = new Descriptor(typeof(MethodOwner), methodInfo);

            Assert.That(stored.IsDescriptorRelated(fresh), Is.True);
        }

        [Test]
        public void IsDescriptorRelated_ReturnsFalse_WhenBothDescriptorsAreIdentical()
        {
            // Identical descriptors: nothing has changed, no update needed.
            var methodInfo = typeof(MethodOwner).GetMethod(nameof(MethodOwner.MyMethod), System.Type.EmptyTypes);
            var a = new Descriptor(typeof(MethodOwner), methodInfo);
            var b = new Descriptor(typeof(MethodOwner), methodInfo);

            Assert.That(a.IsDescriptorRelated(b), Is.False);
        }

        [Test]
        public void IsDescriptorRelated_ReturnsFalse_WhenMethodNamesAreDifferent()
        {
            var myMethod = typeof(MethodOwner).GetMethod(nameof(MethodOwner.MyMethod), System.Type.EmptyTypes);
            var otherMethod = typeof(MethodOwner).GetMethod(nameof(MethodOwner.OtherMethod));
            var a = new Descriptor(typeof(MethodOwner), myMethod);
            var b = new Descriptor(typeof(MethodOwner), otherMethod);

            Assert.That(a.IsDescriptorRelated(b), Is.False);
        }

        [Test]
        public void IsDescriptorRelated_ReturnsFalse_WhenMethodNamesMatchButParametersDiffer()
        {
            var noParams = typeof(MethodOwner).GetMethod(nameof(MethodOwner.MyMethod), System.Type.EmptyTypes);
            var withParam = typeof(MethodOwner).GetMethod(nameof(MethodOwner.MyMethod), new[] { typeof(int) });
            var a = new Descriptor(typeof(MethodOwner), noParams);
            var b = new Descriptor(typeof(MethodOwner), withParam);

            Assert.That(a.IsDescriptorRelated(b), Is.False);
        }
    }

    public class FooType
    {
    }

    public class BarType
    {
    }
}
