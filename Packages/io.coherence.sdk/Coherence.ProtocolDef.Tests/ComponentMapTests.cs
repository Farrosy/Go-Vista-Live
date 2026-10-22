// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.ProtocolDef.Tests
{
    using System.Collections.Generic;
    using NUnit.Framework;

    public class ComponentMapTests
    {
        private PooledSchemaDataFactory factory;

        [SetUp]
        public void SetUp()
        {
            var definition = new ProtocolDefinition(
                "test-schema",
                new Dictionary<uint, ProtocolComponentDefinition>(),
                new Dictionary<uint, ProtocolCommandDefinition>(),
                new Dictionary<uint, ProtocolInputDefinition>(),
                new Dictionary<uint, ProtocolArchetypeDefinition>());

            factory = new PooledSchemaDataFactory(definition, threadSafe: false, poolInputs: false);
        }

        [TearDown]
        public void TearDown()
        {
            factory.AssertNoLeaks();
        }

        [Test]
        [Description("Verifies that Dispose invokes the onDisposedCallback")]
        public void Dispose_InvokesOnDisposedCallback()
        {
            bool invoked = false;
            var map = new ComponentMap(onDisposedCallback: _ => invoked = true);

            map.Dispose();

            Assert.That(invoked, Is.True);
        }

        [Test]
        [Description("Verifies that Reset does not invoke the onDisposedCallback")]
        public void Reset_DoesNotInvokeOnDisposedCallback()
        {
            bool invoked = false;
            var map = new ComponentMap(onDisposedCallback: _ => invoked = true);

            map.Reset();

            Assert.That(invoked, Is.False);
        }

        [Test]
        [Description("Verifies that TakeOwnership invokes the ownership callback with the given identifier")]
        public void TakeOwnership_InvokesOwnershipCallback()
        {
            string capturedOwner = null;
            var map = new ComponentMap(onOwnershipChangedCallback: (_, owner) => capturedOwner = owner);

            map.TakeOwnership("test-owner");

            Assert.That(capturedOwner, Is.EqualTo("test-owner"));
        }

        [Test]
        [Description("Verifies that ReleaseOwnership invokes the ownership callback with null")]
        public void ReleaseOwnership_InvokesOwnershipCallbackWithNull()
        {
            string capturedOwner = "initial";
            var map = new ComponentMap(onOwnershipChangedCallback: (_, owner) => capturedOwner = owner);

            map.ReleaseOwnership();

            Assert.That(capturedOwner, Is.Null);
        }

        [Test]
        [Description("Verifies that disposing a pooled ComponentMap returns it to the factory pool")]
        public void Dispose_ReturnsPooledMapToPool()
        {
            var map = factory.CreateComponentMap();

            map.Dispose();

            // AssertNoLeaks() in TearDown verifies the map was returned
        }

        [Test]
        [Description("Verifies that Update(ComponentMap) disposes the other map, returning it to the factory pool")]
        public void Update_DisposesOtherMap()
        {
            var map = factory.CreateComponentMap();
            var other = factory.CreateComponentMap();

            map.Update(other, out _);

            map.Dispose();
            // AssertNoLeaks() in TearDown verifies both maps were returned
        }
    }
}
