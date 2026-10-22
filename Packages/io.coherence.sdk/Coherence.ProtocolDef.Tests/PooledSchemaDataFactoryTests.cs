// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.ProtocolDef.Tests
{
    using System.Collections.Generic;
    using Coherence.Tests;
    using Entities;
    using NUnit.Framework;
    using Schema;
    using SimulationFrame;

    public class PooledSchemaDataFactoryTests
    {
        private const uint ComponentID = 1;
        private const uint CommandID = 1;
        private const uint InputID = 1;

        private PooledSchemaDataFactory factory;

        [SetUp]
        public void SetUp()
        {
            var definition = new ProtocolDefinition(
                "test-schema",
                new Dictionary<uint, ProtocolComponentDefinition>
                {
                    {
                        ComponentID, new ProtocolComponentDefinition(ComponentID, "TestComponent",
                            new List<ProtocolFieldDefinition>
                            {
                                new("IntField", SchemaType.Int, default),
                                new("FloatField", SchemaType.Float, default),
                            })
                    }
                },
                new Dictionary<uint, ProtocolCommandDefinition>
                {
                    {
                        CommandID, new ProtocolCommandDefinition(CommandID, "TestCommand",
                            new List<ProtocolFieldDefinition>
                            {
                                new("ArgField", SchemaType.Int, default),
                            })
                    }
                },
                new Dictionary<uint, ProtocolInputDefinition>
                {
                    {
                        InputID, new ProtocolInputDefinition(InputID, "TestInput",
                            new List<ProtocolFieldDefinition>
                            {
                                new("AxisField", SchemaType.Float, default),
                            })
                    }
                },
                new Dictionary<uint, ProtocolArchetypeDefinition>());

            factory = new PooledSchemaDataFactory(definition, threadSafe: false, poolInputs: true);
        }

        [Test]
        public void CreateComponent_NoAllocations()
        {
            AllocCounter.AssertNoAllocs(() =>
            {
                factory.CreateComponent(ComponentID).Dispose();
            });
        }

        [Test]
        public void CreateCommand_NoAllocations()
        {
            AllocCounter.AssertNoAllocs(() =>
            {
                factory.CreateCommand(CommandID, Entity.InvalidRelative, MessageTarget.All).Dispose();
            });
        }

        [Test]
        public void CreateInput_NoAllocations()
        {
            AllocCounter.AssertNoAllocs(() =>
            {
                factory.CreateInput(InputID, Entity.InvalidRelative, MessageTarget.All, default(AbsoluteSimulationFrame)).Dispose();
            });
        }
    }
}
