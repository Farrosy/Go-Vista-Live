// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Toolkit.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using Toolkit;
    using Toolkit.Bindings;
    using NUnit.Framework;
    using Coherence.Schema;
    using UnityEngine;

    /// <summary>
    /// Edit mode unit tests for <see cref="TypeUtils"/>.
    /// </summary>
    public class TypeUtilsTests
    {
        [Test]
        public void TypeUtils_GetFieldValue_ReturnsDefaultIfObjectIsNull()
        {
            var resultInt = TypeUtils.GetFieldValue<int>(null, "armour", BindingFlags.Instance | BindingFlags.NonPublic);
            var resultString = TypeUtils.GetFieldValue<string>(null, "name", BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(resultInt, Is.Zero);
            Assert.That(resultString, Is.Null);
        }

        [Test]
        public void TypeUtils_GetFieldValue_ReturnsDefaultIfFieldNotFound()
        {
            var sample = new TypeUtilsSample();
            var resultInt = TypeUtils.GetFieldValue<int>(sample, "health", BindingFlags.Instance | BindingFlags.NonPublic);
            var resultString = TypeUtils.GetFieldValue<string>(sample, "nameOfCharacter", BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(resultInt, Is.Zero);
            Assert.That(resultString, Is.Null);
        }

        [Test]
        public void TypeUtils_GetFieldValue_ReturnsDefaultValueIfWrongFlag()
        {
            var armour = 99;
            var name = "A Knight";
            var sample = new TypeUtilsSample(name, armour);
            var resultInt = TypeUtils.GetFieldValue<int>(sample, "armour", BindingFlags.Instance);
            var resultString = TypeUtils.GetFieldValue<string>(sample, "name", BindingFlags.Instance);

            Assert.That(resultInt, Is.Zero);
            Assert.That(resultString, Is.Null);
        }

        [Test]
        public void TypeUtils_GetFieldValue_ReturnsCorrectValue()
        {
            var armour = 99;
            var name = "A Knight";
            var sample = new TypeUtilsSample(name, armour);
            var resultInt = TypeUtils.GetFieldValue<int>(sample, "armour", BindingFlags.Instance | BindingFlags.NonPublic);
            var resultString = TypeUtils.GetFieldValue<string>(sample, "name", BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(resultInt, Is.EqualTo(armour));
            Assert.That(resultString, Is.EqualTo(name));
        }

        [Test]
        public void GetImplementingTypes_Returns_All_Implementing_Types()
        {
            var results = TypeUtils.GetImplementingTypes<IInternalInterface>();
            Assert.That(results, Is.EquivalentTo(new[]
            {
                typeof(InternalClass_InternalInterface_Implicit_Direct),
                typeof(InternalClass_InternalInterface_Implicit_Derived),
                typeof(PublicClass_InternalInterface_Implicit_Direct),
                typeof(PublicClass_InternalInterface_Implicit_Derived),
                typeof(PublicClass_InternalInterface_Explicit_Direct),
                typeof(PublicClass_InternalInterface_Explicit_Derived),
            }));
        }

        [TestCase(typeof(PublicClass_PublicInterface_Implicit_Direct), new[]
        {
                nameof(PublicClass_PublicInterface_Implicit_Direct.publicField),
                nameof(PublicClass_PublicInterface_Implicit_Direct.Property),
                nameof(PublicClass_PublicInterface_Implicit_Direct.Method)
        })]
        [TestCase(typeof(PublicClass_PublicInterface_Explicit_Direct), new[]
        {
                nameof(PublicClass_PublicInterface_Explicit_Direct.publicField),
                nameof(IPublicInterface.Property),
                nameof(IPublicInterface.Method)
        })]
        [TestCase(typeof(PublicClass_PublicInterface_Implicit_Derived), new[]
        {
                nameof(PublicClass_PublicInterface_Implicit_Derived.publicField),
                nameof(PublicClass_PublicInterface_Implicit_Derived.Property),
                nameof(PublicClass_PublicInterface_Implicit_Derived.Method)
        })]
        [TestCase(typeof(PublicClass_PublicInterface_Explicit_Derived), new[]
        {
                nameof(PublicClass_PublicInterface_Explicit_Derived.publicField),
                nameof(IPublicInterface.Property),
                nameof(IPublicInterface.Method)
        })]
        [TestCase(typeof(PublicClass_InternalInterface_Implicit_Direct), new[]
        {
                nameof(PublicClass_InternalInterface_Implicit_Direct.publicField),
                nameof(PublicClass_InternalInterface_Implicit_Direct.Property),
                nameof(PublicClass_InternalInterface_Implicit_Direct.Method)
        })]
        [TestCase(typeof(PublicClass_InternalInterface_Implicit_Derived), new[]
        {
                nameof(PublicClass_InternalInterface_Implicit_Derived.publicField),
                nameof(PublicClass_InternalInterface_Implicit_Derived.Property),
                nameof(PublicClass_InternalInterface_Implicit_Derived.Method)
        })]
        [TestCase(typeof(InternalClass_PublicInterface_Implicit_Derived), new[]
        {
            nameof(IPublicInterface.Property),
            nameof(IPublicInterface.Method)
        })]
        [TestCase(typeof(InternalClass_PublicInterface_Implicit_Direct), new[]
        {
            nameof(IPublicInterface.Property),
            nameof(IPublicInterface.Method)
        })]
        [TestCase(typeof(InternalClass_InternalInterface_Implicit_Derived), new string[] { })]
        [TestCase(typeof(InternalClass_InternalInterface_Implicit_Direct), new string[] { })]
        [TestCase(typeof(PublicClass_InternalInterface_Explicit_Derived), new[] { nameof(PublicClass_InternalInterface_Explicit_Derived.publicField) })]
        [TestCase(typeof(PublicClass_InternalInterface_Explicit_Direct), new[] { nameof(PublicClass_InternalInterface_Explicit_Direct.publicField) })]
        public void GetBindableMembers_Returns_All_Bindable_Members(Type type, string[] expected)
        {
            var actual = type.GetBindableMembers().ToArray();
            Assert.That(actual, Is.EquivalentTo(expected.Select(name => GetMember(type, name))));
        }

        private static MemberInfo GetMember(Type type, string name)
        {
            do
            {
                var members = type.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                foreach (var x in members)
                {
                    if (x.Name == name || x.Name.EndsWith("." + name))
                    {
                        return x;
                    }
                }
            }
            while ((type = type.BaseType) is not null);

            return null;
        }

        internal interface IInternalInterface
        {
            bool Property { get; set; }
            bool GetOnlyProperty { get; }
            bool SetOnlyProperty { set; }
            void Method();
        }

        internal class InternalClass_InternalInterface_Implicit_Direct : IInternalInterface
        {
            internal bool internalField;
            public int publicField;
            public bool Property { get => false; set { } }
            public bool GetOnlyProperty => false;
            public bool SetOnlyProperty { set { } }
            public void Method() { }
        }

        internal class InternalClass_InternalInterface_Implicit_Derived : InternalClass_InternalInterface_Implicit_Direct { }

        public class PublicClass_InternalInterface_Implicit_Direct : IInternalInterface
        {
            internal bool internalField;
            public int publicField;
            public bool Property { get => false; set { } }
            public bool GetOnlyProperty => false;
            public bool SetOnlyProperty { set { } }
            public void Method() { }
        }

        public class PublicClass_InternalInterface_Implicit_Derived : PublicClass_InternalInterface_Implicit_Direct { }

        public interface IPublicInterface
        {
            bool Property { get; set; }
            bool GetOnlyProperty { get; }
            bool SetOnlyProperty { set; }
            void Method();
        }

        internal class InternalClass_PublicInterface_Implicit_Direct : IPublicInterface
        {
            internal bool internalField;
            public int publicField;
            public bool Property { get => false; set { } }
            public bool GetOnlyProperty => false;
            public bool SetOnlyProperty { set { } }
            public void Method() { }
        }

        internal class InternalClass_PublicInterface_Implicit_Derived : InternalClass_PublicInterface_Implicit_Direct { }

        public class PublicClass_PublicInterface_Implicit_Direct : IPublicInterface
        {
            internal bool internalField;
            public int publicField;
            public bool Property { get => false; set { } }
            public bool GetOnlyProperty => false;
            public bool SetOnlyProperty { set { } }
            public void Method() { }
        }

        public class PublicClass_PublicInterface_Implicit_Derived : PublicClass_PublicInterface_Implicit_Direct { }

        public class PublicClass_PublicInterface_Explicit_Direct : IPublicInterface
        {
            internal bool internalField;
            public int publicField;
            bool IPublicInterface.Property { get => false; set { } }
            bool IPublicInterface.GetOnlyProperty => false;
            bool IPublicInterface.SetOnlyProperty { set { } }
            void IPublicInterface.Method() { }
        }

        public class PublicClass_PublicInterface_Explicit_Derived : PublicClass_PublicInterface_Explicit_Direct { }

        public class PublicClass_InternalInterface_Explicit_Direct : IInternalInterface
        {
            internal bool internalField;
            public int publicField;
            bool IInternalInterface.Property { get => false; set { } }
            bool IInternalInterface.GetOnlyProperty => false;
            bool IInternalInterface.SetOnlyProperty { set { } }
            void IInternalInterface.Method() { }
        }

        public class PublicClass_InternalInterface_Explicit_Derived : PublicClass_InternalInterface_Explicit_Direct { }
    }

    public class TypeUtils_UnityReferenceTypeTests
    {
        [TestCase(typeof(GameObject))]
        [TestCase(typeof(Component))]
        [TestCase(typeof(MonoBehaviour))]
        [TestCase(typeof(Transform))]
        [TestCase(typeof(Rigidbody))]
        [TestCase(typeof(ArbitraryComponent))]
        public void IsUnityReferenceType_ReturnsTrue_ForGameObjectAndComponents(Type type)
        {
            Assert.That(TypeUtils.IsUnityReferenceType(type), Is.True);
        }

        // CoherenceSync is a Component but is explicitly excluded: it is the entity
        // itself, not a reference to some other Unity object.
        [Test]
        public void IsUnityReferenceType_ReturnsFalse_ForCoherenceSync()
        {
            Assert.That(TypeUtils.IsUnityReferenceType(typeof(CoherenceSync)), Is.False);
        }

        [TestCase(typeof(int))]
        [TestCase(typeof(string))]
        [TestCase(typeof(bool))]
        [TestCase(typeof(object))]
        [TestCase(typeof(Vector3))]
        [TestCase(typeof(ScriptableObject))]
        [TestCase(typeof(PlainClass))]
        public void IsUnityReferenceType_ReturnsFalse_ForNonUnityReferenceTypes(Type type)
        {
            Assert.That(TypeUtils.IsUnityReferenceType(type), Is.False);
        }

        [Test]
        public void IsUnityReferenceType_ReturnsFalse_ForNull()
        {
            Assert.That(TypeUtils.IsUnityReferenceType(null), Is.False);
        }

        [Test]
        public void UsesUnityReferenceType_ReturnsTrue_WhenMemberTypeIsUnityReference()
        {
            var descriptor = FieldDescriptor(nameof(ReferenceSample.componentRef));
            Assert.That(TypeUtils.UsesUnityReferenceType(descriptor), Is.True);
        }

        [Test]
        public void UsesUnityReferenceType_ReturnsTrue_WhenMemberTypeIsGameObject()
        {
            var descriptor = FieldDescriptor(nameof(ReferenceSample.gameObjectRef));
            Assert.That(TypeUtils.UsesUnityReferenceType(descriptor), Is.True);
        }

        [Test]
        public void UsesUnityReferenceType_ReturnsFalse_WhenMemberTypeIsPrimitive()
        {
            var descriptor = FieldDescriptor(nameof(ReferenceSample.number));
            Assert.That(TypeUtils.UsesUnityReferenceType(descriptor), Is.False);
        }

        [Test]
        public void UsesUnityReferenceType_ReturnsTrue_WhenAnyCommandParameterIsUnityReference()
        {
            var descriptor = MethodDescriptor(nameof(ReferenceSample.CommandWithComponent));
            Assert.That(TypeUtils.UsesUnityReferenceType(descriptor), Is.True);
        }

        [Test]
        public void UsesUnityReferenceType_ReturnsFalse_WhenNoCommandParameterIsUnityReference()
        {
            var descriptor = MethodDescriptor(nameof(ReferenceSample.CommandWithPrimitives));
            Assert.That(TypeUtils.UsesUnityReferenceType(descriptor), Is.False);
        }

        private static Descriptor FieldDescriptor(string fieldName) =>
            new Descriptor(typeof(ReferenceSample), typeof(ReferenceSample).GetField(fieldName));

        private static Descriptor MethodDescriptor(string methodName) =>
            new Descriptor(typeof(ReferenceSample), typeof(ReferenceSample).GetMethod(methodName));

        private class PlainClass { }

        private class ReferenceSample
        {
            public int number;
            public Rigidbody componentRef;
            public GameObject gameObjectRef;

            public void CommandWithComponent(int value, Rigidbody body) { }
            public void CommandWithPrimitives(int value, string name) { }
        }
    }

    // A custom Component subtype used as an arbitrary entity-ref type in tests.
    internal class ArbitraryComponent : MonoBehaviour { }

    public class TypeUtils_ArbitraryComponentTypeTests
    {
        [TestCase(typeof(Rigidbody))]
        [TestCase(typeof(ArbitraryComponent))]
        [TestCase(typeof(Transform))]   // already in the explicit list — must still pass
        public void IsTypeSupported_ReturnsTrue_ForComponentSubtypes(Type type)
        {
            Assert.IsTrue(TypeUtils.IsTypeSupported(type));
        }

        [TestCase(typeof(Rigidbody))]
        [TestCase(typeof(ArbitraryComponent))]
        [TestCase(typeof(Transform))]
        public void GetSchemaType_ReturnsEntity_ForComponentSubtypes(Type type)
        {
            Assert.AreEqual(SchemaType.Entity, TypeUtils.GetSchemaType(type));
        }

        [TestCase(typeof(Rigidbody))]
        [TestCase(typeof(ArbitraryComponent))]
        [TestCase(typeof(Transform))]
        public void GetBindingType_ReturnsReferenceBinding_ForComponentSubtypes(Type type)
        {
            var bindingType = TypeUtils.GetBindingType(type);
            Assert.IsNotNull(bindingType);
            Assert.IsTrue(
                typeof(Coherence.Toolkit.Bindings.ValueBindings.ReferenceBinding).IsAssignableFrom(bindingType),
                $"Expected a ReferenceBinding subtype, got {bindingType}");
        }

        [TestCase(typeof(int))]
        [TestCase(typeof(string))]
        [TestCase(typeof(bool))]
        public void IsTypeSupported_StillReturnsTrue_ForPreviouslySupportedPrimitiveTypes(Type type)
        {
            Assert.IsTrue(TypeUtils.IsTypeSupported(type));
        }
    }

    public class TypeUtils_CommandTypeNameTests
    {
        [Test]
        public void CommandTypeName_Can_Handle_Nested_And_Generic_Types()
        {
            var result = TypeUtils.CommandTypeName(typeof(Outer<int, string>.Middle<float, bool>.Inner<List<long>, double>));
            Assert.That(result, Is.EqualTo("Coherence.Toolkit.Tests.TypeUtils_CommandTypeNameTests.Outer<System.Int32,System.String>.Middle<System.Single,System.Boolean>.Inner<System.Collections.Generic.List<System.Int64>,System.Double>"));
        }

        [Test]
        public void CommandName_Can_Handle_Nested_And_Generic_Types()
        {
            var result = TypeUtils.CommandName(typeof(Outer<int, string>.Middle<float, bool>.Inner<long, double>), nameof(Outer<int, string>.Middle<float, bool>.Inner<long, double>.Method));
            Assert.That(result, Is.EqualTo("Coherence.Toolkit.Tests.TypeUtils_CommandTypeNameTests.Outer<System.Int32,System.String>.Middle<System.Single,System.Boolean>.Inner<System.Int64,System.Double>.Method"));
        }

        [Test]
        public void CommandTypeName_Returns_TypeParam_Name_For_Generic_Type_Definition()
        {
            var result = TypeUtils.CommandTypeName(typeof(Outer<,>));
            Assert.That(result, Is.EqualTo("Coherence.Toolkit.Tests.TypeUtils_CommandTypeNameTests.Outer<T1,T2>"));
        }

        private class Outer<T1, T2>
        {
            public class Middle<T3, T4>
            {
                public class Inner<T5, T6>
                {
                    public void Method() { }
                }
            }
        }
    }

    internal class TypeUtilsSample
    {
        private string name;
        private int armour;

        public TypeUtilsSample()
        {

        }

        public TypeUtilsSample(string nameValue, int armourValue)
        {
            name = nameValue;
            armour = armourValue;
        }
    }
}
