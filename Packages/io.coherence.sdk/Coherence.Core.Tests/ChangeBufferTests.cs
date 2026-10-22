// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Core.Tests
{
    using System.Collections.Generic;
    using Entities;
    using NUnit.Framework;
    using ProtocolDef;
    using Serializer;
    using SimulationFrame;
    using Coherence.Tests;
    using Coherence.Common.Tests;

    public class ChangeBufferTests : CoherenceTest
    {
        private PooledSchemaDataFactory schemaDataFactory;
        private SendChangeBuffer changeBuffer;
        private AbsoluteSimulationFrame simulationFrame;
        private HashSet<Entity> ackedEntities;
        private Entity entityA;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();

            schemaDataFactory = new PooledSchemaDataFactory(TestSchema.Definition, threadSafe: false, poolInputs: true);
            changeBuffer = new SendChangeBuffer(TestSchema.Definition, schemaDataFactory, logger);
            simulationFrame = 0;
            ackedEntities = new HashSet<Entity>();
            entityA = new Entity((Index)1, 0, Entity.Relative);
        }

        [TearDown]
        public override void TearDown()
        {
            changeBuffer.Reset();
            schemaDataFactory.AssertNoLeaks();

            base.TearDown();
        }

        [Test]
        public void CreateEntity_ShouldBuffer()
        {
            // Arrange
            var intComponentValue = 10;
            var expectedState = GetEntityState(priority: SendChangeBuffer.CREATE_PRIORITY,
                operation: EntityOperation.Create, intComponentValue: intComponentValue);

            // Act
            CreateEntity(entityA, intComponentValue: intComponentValue);

            // Assert
            Assert_EntityStateSame(entityA, expectedState);

            // Clean up
            expectedState.Return();
        }

        [Test]
        public void DroppedCreate_ShouldRebuffer()
        {
            // Arrange
            var intComponentValue = 10;
            var expectedState = GetEntityState(priority: SendChangeBuffer.CREATE_PRIORITY,
                operation: EntityOperation.Create,
                intComponentValue: intComponentValue);

            // Act
            CreateEntity(entityA, intComponentValue: intComponentValue);

            var changesSent = SendChanges(expectedExistenceChanges: 1, expectedUpdateChanges: 0);

            DropChanges(changesSent);

            // Assert
            Assert_EntityStateSame(entityA, expectedState);

            // Clean up
            expectedState.Return();
        }

        [TestCase(false, false, false)] //Update without create like when updating entity created through auth change
        [TestCase(true, false, true)] //If don't send create, the update shouldn't override the IsCreate state
        [TestCase(true, true, false)] //If send create the IsCreate state is cleared by the update
        public void UpdateState_ShouldBuffer(bool createFirst, bool sendCreate, bool expectIsCreateTrue)
        {
            var intComponentValue = 10;
            var updatePriority = 2342; // random number
            var updateValue = 2;
            var updateData = GetComponentData(intComponentValue: updateValue);
            var updateChange = new EntityUpdateChange
            {
                ID = entityA,
                Data = ComponentUpdates.New(updateData),
                Priority = updatePriority,
            };
            var expectedPriority =
                ((createFirst && !sendCreate) ? SendChangeBuffer.CREATE_PRIORITY : 0) + updatePriority;
            var expectedState = GetEntityState(priority: expectedPriority,
                operation: expectIsCreateTrue ? EntityOperation.Create : EntityOperation.Update,
                intComponentValue: updateValue);

            // Act
            if (createFirst)
            {
                CreateEntity(entityA, intComponentValue: intComponentValue);
            }

            if (sendCreate)
            {
                var createChanges = SendChanges(expectedExistenceChanges: 1, expectedUpdateChanges: 0);
                foreach (var (_, change) in createChanges)
                {
                    change.Return();
                }
            }

            // Update with value of 2
            changeBuffer.UpdateEntity(updateChange);

            // Assert
            Assert_EntityStateSame(entityA, expectedState);

            // Clean up
            expectedState.Return();
        }

        [Test]
        public void DroppedUpdate_ShouldRebuffer()
        {
            var intComponentValue = 10;
            var updatePriority = 2355; // random value
            var updateValue = 20;
            var updateData = GetComponentData(intComponentValue: updateValue);
            var updateChange = new EntityUpdateChange
            {
                ID = entityA,
                Data = ComponentUpdates.New(updateData),
                Priority = updatePriority,
            };
            var expectedState = GetEntityState(priority: updatePriority, intComponentValue: updateValue);

            // Act
            CreateEntity(entityA, intComponentValue: intComponentValue);

            var pendingChanges = SendChanges(expectedExistenceChanges: 1, expectedUpdateChanges: 0);

            changeBuffer.UpdateEntity(updateChange);

            var changesSent = SendChanges(expectedExistenceChanges: 0, expectedUpdateChanges: 1);

            DropChanges(changesSent);

            // Assert
            Assert_EntityStateSame(entityA, expectedState);

            // Clean up
            expectedState.Return();
            foreach (var (_, change) in pendingChanges)
            {
                change.Return();
            }
        }

        [TestCase(true, true)]
        [TestCase(true, false)]
        [TestCase(false, false)]
        public void DestroyEntity_ShouldBuffer(bool createFirst, bool sendCreate)
        {
            var intComponentValue = 10;
            var expectedState = GetEntityState(operation: EntityOperation.Destroy,
                priority: SendChangeBuffer.DESTROY_PRIORITY);

            // Act
            if (createFirst)
            {
                CreateEntity(entityA, intComponentValue: intComponentValue);
            }

            if (sendCreate)
            {
                var sentChanges = SendChanges(expectedExistenceChanges: 1, expectedUpdateChanges: 0);
                foreach (var (_, change) in sentChanges)
                {
                    change.Return();
                }
            }

            DestroyEntity(entityA);

            // Assert
            if (sendCreate)
            {
                Assert_EntityStateSame(entityA, expectedState);
            }
            else
            {
                Assert.IsFalse(changeBuffer.Buffer.ContainsKey(entityA), "Have Entity in Buffer");
            }

            // Clean up
            expectedState.Return();
        }

        [Test]
        public void DestroyEntity_FromTransfer()
        {
            var expectedState = GetEntityState(priority: SendChangeBuffer.DESTROY_PRIORITY);
            expectedState.Operation = EntityOperation.Destroy;

            ackedEntities = new HashSet<Entity>
            {
                entityA
            }; // Created by a transfer

            // Act
            DestroyEntity(entityA);

            // Assert
            Assert_EntityStateSame(entityA, expectedState);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void DroppedDestroy_ShouldRebuffer(bool updateBeforeDrop)
        {
            var intComponentValue = 10;
            var expectedState = GetEntityState(operation: EntityOperation.Destroy,
                priority: SendChangeBuffer.DESTROY_PRIORITY);

            // Act
            CreateEntity(entityA, intComponentValue: intComponentValue);

            var createChanges = SendChanges(expectedExistenceChanges: 1, expectedUpdateChanges: 0);

            DestroyEntity(entityA);

            var changesSent = SendChanges(expectedExistenceChanges: 1, expectedUpdateChanges: 0);

            if (updateBeforeDrop)
            {
                UpdateEntity(entityA, intComponentValue: intComponentValue);
            }

            DropChanges(changesSent);

            // Assert
            Assert_EntityStateSame(entityA, expectedState);

            // Clean up
            expectedState.Return();
            foreach (var (_, change) in createChanges)
            {
                change.Return();
            }
        }

        [Test]
        [Description("Tests that a destroy is sent even though a create was dropped because a partial update" +
            "could be held on the receiving client.")]
        public void DroppedCreateAfterDestroy()
        {
            CreateEntity(entityA, intComponentValue: 10);
            var sentCreate = SendChanges(expectedExistenceChanges: 1, expectedUpdateChanges: 0);

            DestroyEntity(entityA);

            DropChanges(sentCreate);

            var sentChanges = SendChanges(expectedExistenceChanges: 1, expectedUpdateChanges: 0);

            Assert.That(sentChanges, Has.Count.EqualTo(1));
            Assert.That(sentChanges[entityA].Operation, Is.EqualTo(EntityOperation.Destroy));

            // Clean up
            foreach (var (_, change) in sentChanges)
            {
                change.Return();
            }
        }

        [Test]
        [Description("Tests that an update is fully dropped if there is an in-flight destroy.")]
        public void DroppedUpdateAfterDestroy()
        {
            CreateEntity(entityA, intComponentValue: 10);
            var sentChanges = SendChanges(expectedExistenceChanges: 1, expectedUpdateChanges: 0);
            foreach (var (_, change) in sentChanges)
            {
                change.Return();
            }

            UpdateEntity(entityA, intComponentValue: 20);
            var sentUpdate = SendChanges(expectedExistenceChanges: 0, expectedUpdateChanges: 1);

            DestroyEntity(entityA);
            var sentDestroy = SendChanges(expectedExistenceChanges: 1, expectedUpdateChanges: 0);

            DropChanges(sentUpdate, sentDestroy);

            // Nothing should be sent as the destroy is in-flight
            _ = SendChanges(expectedExistenceChanges: 0, expectedUpdateChanges: 0);

            // Clean up
            foreach (var (_, change) in sentDestroy)
            {
                change.Return();
            }
        }

        [TestCase(true)]
        [TestCase(false)]
        public void RemoveComponent_ShouldBuffer(bool sendCreate)
        {
            var intComponentValue = 10;
            var floatComponentValue = 10.5f;
            var removeChange = new EntityRemoveChange
            {
                ID = entityA,
                Remove = new[]
                {
                    FloatComponent.ComponentID
                },
            };

            OutgoingEntityUpdate expectedUpdate;
            if (sendCreate)
            {
                expectedUpdate = GetEntityState(destroys: new ComponentDestroyMap
                {
                    FloatComponent.ComponentID
                });
            }
            else
            {
                expectedUpdate = GetEntityState(priority: SendChangeBuffer.CREATE_PRIORITY,
                    operation: EntityOperation.Create,
                    intComponentValue: intComponentValue);
            }

            // Act
            CreateEntity(entityA, intComponentValue: intComponentValue, floatComponentValue: floatComponentValue);

            if (sendCreate)
            {
                var createChanges = SendChanges(expectedExistenceChanges: 1, expectedUpdateChanges: 0);
                foreach (var (_, change) in createChanges)
                {
                    change.Return();
                }
            }

            changeBuffer.RemoveComponent(removeChange);

            // Assert
            Assert_EntityStateSame(entityA, expectedUpdate);

            // Clean up
            expectedUpdate.Return();
        }

        [Test]
        public void DroppedRemoveComponent_ShouldRebuffer()
        {
            var intComponentValue = 10;
            var floatComponentValue = 10.5f;
            var removeChange = new EntityRemoveChange
            {
                ID = entityA,
                Remove = new[]
                {
                    FloatComponent.ComponentID
                },
            };

            var expectedState = GetEntityState(destroys: new ComponentDestroyMap
            {
                FloatComponent.ComponentID
            });

            // Act
            CreateEntity(entityA, intComponentValue: intComponentValue, floatComponentValue: floatComponentValue);

            var createChanges = SendChanges(expectedExistenceChanges: 1, expectedUpdateChanges: 0);

            changeBuffer.RemoveComponent(removeChange);

            var changesSent = SendChanges(expectedExistenceChanges: 0, expectedUpdateChanges: 1);

            DropChanges(changesSent);

            // Assert
            Assert_EntityStateSame(entityA, expectedState);

            // Clean up
            foreach (var (_, change) in createChanges)
            {
                change.Return();
            }
        }

        [Test]
        [Description(
            "Verifies that when component removal is sent, followed by an update being sent for this component," +
            "if removal is dropped, it doesn't get remerged back due to update being more recent.")]
        public void RemovedNotRemergedOnDropIfUpdateWasSent()
        {
            // Arrange
            var entity = new Entity((Index)1, 0, false);

            // Act
            changeBuffer.RemoveComponent(new EntityRemoveChange()
            {
                ID = entity,
                Remove = new uint[]
                {
                    IntComponent.ComponentID
                }
            });
            var sentRemove = SendChanges(expectedExistenceChanges: 0, expectedUpdateChanges: 1);

            UpdateEntity(entity, intComponentValue: 3);
            var sentUpdate = SendChanges(expectedExistenceChanges: 0, expectedUpdateChanges: 1);

            DropChanges(sentRemove, sentUpdate);

            // Assert
            var outgoingEntityUpdate = changeBuffer.CopyEntityUpdate(entity).Value;

            Assert.That(outgoingEntityUpdate.Components.Destroys, Is.Empty);
            Assert.That(outgoingEntityUpdate.Components.Updates.Count, Is.Zero);

            // Clean up
            outgoingEntityUpdate.Return();
            foreach (var change in sentUpdate.Values)
            {
                change.Return();
            }
        }

        [Test]
        [Description(
            "Verifies that when component removal is sent, followed by an update added to the buffer for this component," +
            "if removal is dropped, it doesn't get remerged back due to update being more recent.")]
        public void RemovedNotRemergedOnDropIfUpdateIsInTheBuffer()
        {
            // Arrange
            var entity = new Entity((Index)1, 0, false);

            // Act
            changeBuffer.RemoveComponent(new EntityRemoveChange()
            {
                ID = entity,
                Remove = new uint[]
                {
                    IntComponent.ComponentID
                }
            });
            var sentRemove = SendChanges(expectedExistenceChanges: 0, expectedUpdateChanges: 1);

            UpdateEntity(entity, intComponentValue: 3);

            DropChanges(sentRemove);

            // Assert
            var outgoingEntityUpdate = changeBuffer.CopyEntityUpdate(entity).Value;

            Assert.That(outgoingEntityUpdate.Components.Destroys, Is.Empty);
            Assert.That(outgoingEntityUpdate.Components.Updates.Store.ContainsKey(IntComponent.ComponentID));

            // Clean up
            outgoingEntityUpdate.Return();
        }

        [Test]
        [Description(
            "Verifies that when component removal is sent, followed by another same remove being sent for this component," +
            "if removal is dropped, it doesn't get remerged back due to remove for same component being in-flight.")]
        public void RemovedNotRemergedOnDropIfAnotherRemoveWasSent()
        {
            // Arrange
            var entity = new Entity((Index)1, 0, false);

            // Act
            changeBuffer.RemoveComponent(new EntityRemoveChange()
            {
                ID = entity,
                Remove = new uint[]
                {
                    IntComponent.ComponentID
                }
            });
            var sentRemove = SendChanges(expectedExistenceChanges: 0, expectedUpdateChanges: 1);

            changeBuffer.RemoveComponent(new EntityRemoveChange()
            {
                ID = entity,
                Remove = new uint[]
                {
                    IntComponent.ComponentID
                }
            });
            var sentUpdate = SendChanges(expectedExistenceChanges: 0, expectedUpdateChanges: 1);

            DropChanges(sentRemove, sentUpdate);

            // Assert
            var outgoingEntityUpdate = changeBuffer.CopyEntityUpdate(entity).Value;

            Assert.That(outgoingEntityUpdate.Components.Destroys, Is.Empty);
            Assert.That(outgoingEntityUpdate.Components.Updates.Count, Is.Zero);
        }

        [Test]
        public void Test_ResendingPreservesOperation()
        {
            // Verifies that when both create and update gets dropped,
            // when both are re-merged into the change buffer, the create operation is preserved.

            // Arrange
            // Add and send a creation change
            var entity = new Entity((Index)1, 0, false);
            CreateEntity(entity);

            var createChanges = SendChanges(expectedExistenceChanges: 1, expectedUpdateChanges: 0);

            // Add and send an update
            UpdateEntity(entity);

            var updateChanges = SendChanges(expectedExistenceChanges: 0, expectedUpdateChanges: 1);

            // Act
            DropChanges(createChanges);
            DropChanges(updateChanges);

            // Assert
            var metas = changeBuffer.GetEntityMeta();
            var entityState = metas[entity];
            Assert.That(entityState.Operation, Is.EqualTo(EntityOperation.Create));
        }

        [Test]
        public void Test_DroppedSendMergesCorrectly()
        {
            // Verifies that when a dropped create is remerged that the
            // component is correct.

            // Arrange
            // Add and send a creation change
            var createVal = 123;
            var updateVal = 321;

            var entity = new Entity((Index)1, 0, false);
            CreateEntity(entity, intComponentValue: createVal);

            var createChanges = SendChanges(expectedExistenceChanges: 1, expectedUpdateChanges: 0);

            // Add and send an update
            UpdateEntity(entity, intComponentValue: updateVal);

            // Act
            DropChanges(createChanges);

            // Assert
            var metas = changeBuffer.GetEntityMeta();
            var entityState = metas[entity];
            Assert.That(entityState.Operation, Is.EqualTo(EntityOperation.Create));
            Assert.That(entityState.Components.Updates.Store.ContainsKey(IntComponent.ComponentID));

            var intComp = IntComponent.From(entityState.Components.Updates.Store[IntComponent.ComponentID]);
            Assert.That(intComp.Number, Is.EqualTo(updateVal));
        }

        [Test]
        public void Test_UpdateMergesOverDroppedSendCorrectly()
        {
            // Verifies that when an update merges over a dropped
            // and remerged send that the component is correct.

            // Arrange
            // Add and send a creation change
            var createIntVal = 123;
            var createFloatVal = 0.999f;
            var updateVal = 0.4f;

            var entity = new Entity((Index)1, 0, false);
            CreateEntity(entity, intComponentValue: createIntVal, floatComponentValue: createFloatVal);

            var createChanges = SendChanges(expectedExistenceChanges: 1, expectedUpdateChanges: 0);

            // Add and send an update
            UpdateEntity(entity, floatComponentValue: updateVal);

            var updateChanges = SendChanges(expectedExistenceChanges: 0, expectedUpdateChanges: 1);

            // Act
            DropChanges(createChanges, updateChanges);
            DropChanges(updateChanges);

            // Assert
            var metas = changeBuffer.GetEntityMeta();
            var entityState = metas[entity];
            Assert.That(entityState.Operation, Is.EqualTo(EntityOperation.Create));
            Assert.That(entityState.Components.Updates.Store.ContainsKey(IntComponent.ComponentID));

            var intComp = IntComponent.From(entityState.Components.Updates.Store[IntComponent.ComponentID]);
            Assert.That(intComp.Number, Is.EqualTo(createIntVal));
            var floatComp = FloatComponent.From(entityState.Components.Updates.Store[FloatComponent.ComponentID]);
            Assert.That(floatComp.Number, Is.EqualTo(updateVal));
        }

        [Test]
        [Description("Verifies that if a create is dropped followed by updates with changes " +
                     "to the same components as in the create that the create remerges back into " +
                     "the buffer with the complete changes and not devoid of components.")]
        public void Test_DroppedCreateAfterUpdateWithSameComponentsChangedIsNotEmpty()
        {
            // Arrange
            var droppedFloat = 0.5f;
            var droppedInt = 99;
            var updateFloat = 123.456f;
            var updateFloat2 = 333.333f;
            var updateInt = 202;

            var entity = new Entity((Index)1, 0, false);
            CreateEntity(entity, intComponentValue: droppedInt, floatComponentValue: droppedFloat);

            var createChanges = SendChanges(expectedExistenceChanges: 1, expectedUpdateChanges: 0);

            // add an update that changes the float
            UpdateEntity(entity, floatComponentValue: updateFloat);

            var update1 = SendChanges(expectedExistenceChanges: 0, expectedUpdateChanges: 1);

            // add an update that changes the int and float
            UpdateEntity(entity, intComponentValue: updateInt, floatComponentValue: updateFloat2);

            var update2 = SendChanges(expectedExistenceChanges: 0, expectedUpdateChanges: 1);

            // add an update with a component not in the original
            UpdateEntity(entity, addOrderedComp: true);

            var update3 = SendChanges(expectedExistenceChanges: 0, expectedUpdateChanges: 1);

            // Act
            // Drop the original create
            DropChanges(createChanges, update1, update2, update3);

            // Assert
            // The create should contain all the changes.
            var metas = changeBuffer.GetEntityMeta();
            var entityState = metas[entity];

            Assert.That(entityState.Operation, Is.EqualTo(EntityOperation.Create));
            Assert.That(entityState.Components.Updates.Store.ContainsKey(FloatComponent.ComponentID));
            var floatComp = FloatComponent.From(entityState.Components.Updates.Store[FloatComponent.ComponentID]);
            Assert.That(floatComp.Number, Is.EqualTo(updateFloat2));

            Assert.That(entityState.Components.Updates.Store.ContainsKey(IntComponent.ComponentID));
            var intComp = IntComponent.From(entityState.Components.Updates.Store[IntComponent.ComponentID]);
            Assert.That(intComp.Number, Is.EqualTo(updateInt));

            Assert.That(entityState.Components.Updates.Store.ContainsKey(OrderedComponent.ComponentID));

            // Clean up
            foreach (var change in update1.Values)
            {
                change.Return();
            }
            foreach (var change in update2.Values)
            {
                change.Return();
            }
            foreach (var change in update3.Values)
            {
                change.Return();
            }
        }

        [Test]
        [Description("Verifies that the stopped mask is set correctly.")]
        public void Test_StoppedComponentMaskIsSet()
        {
            // Arrange
            var entity = new Entity((Index)1, 0, false);

            // Act - add an entity change with a stopped mask set
            UpdateMultiCompEntity(entity, 1, 2f, 3f, 0b101);

            // Assert
            var metas = changeBuffer.GetEntityMeta();
            var entityState = metas[entity];
            Assert.That(entityState.Components.Updates.Store.ContainsKey(MultiComponent.ComponentID));
            var multiComp = MultiComponent.From(entityState.Components.Updates.Store[MultiComponent.ComponentID]);
            Assert.That(multiComp.Data.StoppedMask, Is.EqualTo(0b101));
        }

        [Test]
        [Description("Verifies that an existing non-stopped compononet merges with a stopped one correctly.")]
        public void Test_StoppedComponentMaskMergesIntoNonStopped()
        {
            // Arrange
            var entity = new Entity((Index)1, 0, false);

            UpdateMultiCompEntity(entity, 1, 2f, 3f, 0b000);

            // Act
            UpdateMultiCompEntity(entity, 4, 5f, 6f, 0b010);

            // Assert
            var metas = changeBuffer.GetEntityMeta();
            var entityState = metas[entity];
            Assert.That(entityState.Components.Updates.Store.ContainsKey(MultiComponent.ComponentID));
            var multiComp = MultiComponent.From(entityState.Components.Updates.Store[MultiComponent.ComponentID]);
            Assert.That(multiComp.Data.StoppedMask, Is.EqualTo(0b010));
        }

        [Test]
        [Description("Verifies that when a new sample is added to the change buffer after one with a stopped mask that the " +
            "mask is correctly updated along with the value.")]
        public void Test_StoppedComponentMergedWithNewUnstoppedSample()
        {
            // Arrange
            var entity = new Entity((Index)1, 0, false);

            // add an entity change with a stopped mask set
            UpdateMultiCompEntity(entity, 1, 2f, 3f, 0b100);

            // Act - add an entity change with the same component restarted
            UpdateMultiCompEntity(entity, 4, 5f, 6f, 0b000);

            // Assert
            var metas = changeBuffer.GetEntityMeta();
            var entityState = metas[entity];
            Assert.That(entityState.Components.Updates.Store.ContainsKey(MultiComponent.ComponentID));
            var multiComp = MultiComponent.From(entityState.Components.Updates.Store[MultiComponent.ComponentID]);
            Assert.That(multiComp.Data.StoppedMask, Is.EqualTo(0b000));
        }

        [Test]
        [Description("Verifies that when a new sample is added to the change buffer after one with a stopped mask that the " +
            "mask is correctly updated along with the value even if some of the fields are unchanged.")]
        public void Test_StoppedComponentMergedWithNewUnstoppedSampleSomeUnchanged()
        {
            // Arrange
            var entity = new Entity((Index)1, 0, false);

            // add an entity change with a stopped mask set
            UpdateMultiCompEntity(entity, 1, 2f, 3f, 0b101);

            // Act - add an entity change with the same component restarted
            UpdateMultiCompEntity(entity, 4, 5f, null, 0b000);

            // Assert - 0b100 means that the float1 value is still stopped since it was
            // not chaned in the update.
            var metas = changeBuffer.GetEntityMeta();
            var entityState = metas[entity];
            Assert.That(entityState.Components.Updates.Store.ContainsKey(MultiComponent.ComponentID));
            var multiComp = MultiComponent.From(entityState.Components.Updates.Store[MultiComponent.ComponentID]);
            Assert.That(multiComp.Data.StoppedMask, Is.EqualTo(0b100));
        }

        [Test]
        [Description("Verifies that if an unstopped sample is dropped and a stopped sample is in the buffer when " +
            "the drop is remerged that the stopped mask is correctly set.")]
        public void Test_StoppedInBufferWithDroppedUnstoppedMergedBackIn()
        {
            // Arrange
            var entity = new Entity((Index)1, 0, false);

            // add an entity change with a stopped mask set
            UpdateMultiCompEntity(entity, 1, 2f, 3f, 0b100);

            var changesSent = SendChanges(expectedExistenceChanges: 0, expectedUpdateChanges: 1);

            UpdateMultiCompEntity(entity, 4, 5f, 6f, 0b101);

            // Act
            DropChanges(changesSent);

            // Assert
            var metas = changeBuffer.GetEntityMeta();
            var entityState = metas[entity];
            Assert.That(entityState.Components.Updates.Store.ContainsKey(MultiComponent.ComponentID));
            var multiComp = MultiComponent.From(entityState.Components.Updates.Store[MultiComponent.ComponentID]);
            Assert.That(multiComp.Data.StoppedMask, Is.EqualTo(0b101));
        }

        [Test]
        [Description("verifies that if an unstopped sample is dropped and a stopped sample is in the buffer when " +
            "the drop is remerged that the stopped mask is correctly set even if some of the fields are unchanged.")]
        public void Test_StoppedInBufferWithDroppedUnstoppedMergedBackInSomeUnchanged()
        {
            // Arrange
            var entity = new Entity((Index)1, 0, false);

            // add an entity change with a stopped mask set
            UpdateMultiCompEntity(entity, 1, 2f, 3f, 0b100);

            var changesSent = SendChanges(expectedExistenceChanges: 0, expectedUpdateChanges: 1);

            UpdateMultiCompEntity(entity, 4, 5f, null, 0b001);

            // Act
            DropChanges(changesSent);

            // Assert
            var metas = changeBuffer.GetEntityMeta();
            var entityState = metas[entity];
            Assert.That(entityState.Components.Updates.Store.ContainsKey(MultiComponent.ComponentID));
            var multiComp = MultiComponent.From(entityState.Components.Updates.Store[MultiComponent.ComponentID]);
            Assert.That(multiComp.Data.StoppedMask, Is.EqualTo(0b101));
        }

        [Test]
        [Description("Verifies that if there is an update to an in-flight stopped field and that update unstops " +
            "the field that the dropped merge of the stopped field correctly clears the stop " +
            "bit so the sent field is not stopped.")]
        public void Test_StoppedMergesCorrectlyWithSentChanges()
        {
            // Arrange
            var entity = new Entity((Index)1, 0, false);

            // send update with a stopped int value
            UpdateMultiCompEntity(entity, 1, 2f, 3f, 0b001);

            var droppedChanges = SendChanges(expectedExistenceChanges: 0, expectedUpdateChanges: 1);

            // update the int value so it is no longer stopped and send it.
            UpdateMultiCompEntity(entity, 4, 5f, null, 0b000);

            var updateChanges = SendChanges(expectedExistenceChanges: 0, expectedUpdateChanges: 1);

            // Act
            DropChanges(droppedChanges, updateChanges);

            // Assert
            var metas = changeBuffer.GetEntityMeta();
            var entityState = metas[entity];
            Assert.That(entityState.Components.Updates.Store.ContainsKey(MultiComponent.ComponentID));
            var multiComp = MultiComponent.From(entityState.Components.Updates.Store[MultiComponent.ComponentID]);
            Assert.That(multiComp.Data.StoppedMask, Is.EqualTo(0b000));

            // Clean up
            foreach (var (_, change) in updateChanges)
            {
                change.Return();
            }
        }

        [Test]
        [Description("Verifies that the tests create expected fields masks when adding changes to the change buffer.")]
        public void Test_FieldsMaskBasic()
        {
            // Arrange
            var entity = new Entity((Index)1, 0, false);

            // Act
            UpdateEntity(entity, 1, 2f, true);
            UpdateMultiCompEntity(entity, 3, 4f, 5f, 0b000);

            // Assert
            var metas = changeBuffer.GetEntityMeta();
            var entityState = metas[entity];
            Assert.That(entityState.Components.Updates.Store[IntComponent.ComponentID].FieldsMask, Is.EqualTo((uint)0b1));
            Assert.That(entityState.Components.Updates.Store[FloatComponent.ComponentID].FieldsMask, Is.EqualTo((uint)0b1));
            Assert.That(entityState.Components.Updates.Store[OrderedComponent.ComponentID].FieldsMask, Is.EqualTo((uint)0b0));
            Assert.That(entityState.Components.Updates.Store[MultiComponent.ComponentID].FieldsMask, Is.EqualTo((uint)0b111));
        }

        [Test]
        [Description("Verifies that two updates with different fields masked merge the masks correctly.")]
        public void Test_FieldsMaskMergeChange()
        {
            // Arrange
            var entity = new Entity((Index)1, 0, false);

            // Act
            UpdateMultiCompEntity(entity, 3, null, null, 0b000);
            UpdateMultiCompEntity(entity, null, null, 5f, 0b000);

            // Assert
            var metas = changeBuffer.GetEntityMeta();
            var entityState = metas[entity];
            Assert.That(entityState.Components.Updates.Store[MultiComponent.ComponentID].FieldsMask, Is.EqualTo((uint)0b101));
        }

        [Test]
        [Description("Verifies that a dropped change FieldsMask is correctly merged with an existing change.")]
        public void Test_FieldsMaskDroppedMerge()
        {
            // Arrange
            var entity = new Entity((Index)1, 0, false);

            UpdateMultiCompEntity(entity, 3, null, null, 0b000);

            var sentChanges = SendChanges(expectedExistenceChanges: 0, expectedUpdateChanges: 1);

            UpdateMultiCompEntity(entity, null, null, 5f, 0b000);

            DropChanges(sentChanges);

            // Assert
            var metas = changeBuffer.GetEntityMeta();
            var entityState = metas[entity];
            Assert.That(entityState.Components.Updates.Store[MultiComponent.ComponentID].FieldsMask, Is.EqualTo((uint)0b101));
        }

        private void CreateEntity
        (
            Entity entity,
            int? intComponentValue = null,
            float? floatComponentValue = null,
            bool addOrderedComp = false
        )
        {
            var data = GetComponentData(intComponentValue, floatComponentValue, addOrderedComp);

            var createChange = new EntityCreateChange
            {
                ID = entity,
                Data = ComponentUpdates.New(data),
            };

            changeBuffer.CreateEntity(createChange);
        }

        private void UpdateEntity
        (
            Entity entity,
            int? intComponentValue = null,
            float? floatComponentValue = null,
            bool addOrderedComp = false
        )
        {
            var data = GetComponentData(intComponentValue, floatComponentValue, addOrderedComp);

            var updateChange = new EntityUpdateChange
            {
                ID = entity,
                Data = ComponentUpdates.New(data),
            };

            changeBuffer.UpdateEntity(updateChange);
        }

        private void UpdateMultiCompEntity
        (
            Entity entity,
            int? intField,
            float? float0Field,
            float? float1Field,
            uint stoppedMask
        )
        {
            var data = GetMultiCompData(intField, float0Field, float1Field);
            data.StoppedMask = stoppedMask;

            var comps = new ComponentData[] { data };

            var updateChange = new EntityUpdateChange
            {
                ID = entity,
                Data = ComponentUpdates.New(comps),
            };

            changeBuffer.UpdateEntity(updateChange);
        }

        private void DestroyEntity(Entity entity)
        {
            changeBuffer.DestroyEntity(entity, ackedEntities);
        }

        private OutgoingEntityUpdate GetEntityState(
            long priority = 0,
            EntityOperation operation = EntityOperation.Update,
            int? intComponentValue = null,
            float? floatComponentValue = null,
            bool addOrderedComp = false,
            ComponentDestroyMap destroys = null)
        {
            var data = GetComponentData(intComponentValue, floatComponentValue, addOrderedComp);
            var store = new Dictionary<uint, ComponentData>();

            foreach (var datum in data)
            {
                store.Add(datum.ComponentID, datum);
            }

            var outgoingUpdate = OutgoingEntityUpdate.New();
            outgoingUpdate.Priority = priority;
            outgoingUpdate.Operation = operation;
            outgoingUpdate.Components.Updates = ComponentUpdates.New(store);
            outgoingUpdate.Components.Destroys = destroys ?? new ComponentDestroyMap();

            return outgoingUpdate;
        }

        private ComponentData[] GetComponentData
        (
            int? intComponentValue = null,
            float? floatComponentValue = null,
            bool addOrderedComp = false
        )
        {
            var data = new List<ComponentData>();

            if (intComponentValue != null)
            {
                data.Add(IntComponent.Create(schemaDataFactory, intComponentValue.Value));
            }

            if (floatComponentValue != null)
            {
                data.Add(FloatComponent.Create(schemaDataFactory, floatComponentValue.Value));
            }

            if (addOrderedComp)
            {
                data.Add(OrderedComponent.Create(schemaDataFactory));
            }

            return data.ToArray();
        }

        private ComponentData GetMultiCompData
        (
            int? intField,
            float? float0Field,
            float? float1Field
        )
        {
            var compData = MultiComponent.Create(schemaDataFactory,
                intField ?? 0,
                float0Field ?? 0f,
                float1Field ?? 0f);

            var component = MultiComponent.From(compData);
            component.IsIntValueSet = intField != null;
            component.IsFloatValue0Set = float0Field != null;
            component.IsFloatValue1Set = float1Field != null;

            return component.Data;
        }

        private Dictionary<Entity, OutgoingEntityUpdate> SendChanges(int expectedExistenceChanges, int expectedUpdateChanges)
        {
            List<EntityChange> existenceChangeBuffer = new List<EntityChange>(32);
            List<EntityChange> updateChangeBuffer = new List<EntityChange>(32);

            var numChanges =
                changeBuffer.GetPrioritizedChanges(existenceChangeBuffer, updateChangeBuffer, ackedEntities);
            Assert.AreEqual(expectedExistenceChanges + expectedUpdateChanges, numChanges,
                "Number of changes is expected existance + expected update changes");
            Assert.AreEqual(expectedExistenceChanges, existenceChangeBuffer.Count,
                "Number of existence changes is correct");
            Assert.AreEqual(expectedUpdateChanges, updateChangeBuffer.Count, "Number of update changes is correct");

            var sentEntities = new List<Entity>();
            foreach (var change in existenceChangeBuffer)
            {
                sentEntities.Add(change.ID);
            }

            foreach (var change in updateChangeBuffer)
            {
                sentEntities.Add(change.ID);
            }

            Dictionary<Entity, OutgoingEntityUpdate> updatesSent = null;
            changeBuffer.AppendSentUpdates(ref updatesSent, sentEntities);

            return updatesSent;
        }

        private void DropChanges(Dictionary<Entity, OutgoingEntityUpdate> changesSent,
            params Dictionary<Entity, OutgoingEntityUpdate>[] unackedSent)
        {
            var unackedSentChanges = new LinkedList<ChangeBuffer>();
            foreach (var unacked in unackedSent)
            {
                _ = unackedSentChanges.AddFirst(new ChangeBuffer(unacked, new Queue<SerializedEntityMessage>(),
                    new Queue<SerializedEntityMessage>(), schemaDataFactory, logger));
            }

            var droppedChanges = new ChangeBuffer(changesSent, new Queue<SerializedEntityMessage>(),
                new Queue<SerializedEntityMessage>(), schemaDataFactory, logger);

            changeBuffer.ResetWithLostChanges(droppedChanges, unackedSentChanges, ackedEntities);
        }

        private void Assert_EntityStateSame(Entity entityID, OutgoingEntityUpdate expectedUpdate)
        {
            Assert.IsTrue(changeBuffer.Buffer.ContainsKey(entityID), "Have Entity in Buffer");

            var bufferedEntityState = changeBuffer.Buffer[entityID];

            Assert.AreEqual(expectedUpdate.Operation, bufferedEntityState.Operation, "Operation");
            Assert.AreEqual(expectedUpdate.IsDestroy, bufferedEntityState.IsDestroy, "Is Destroy");
            Assert.AreEqual(expectedUpdate.Priority, bufferedEntityState.Priority, "Priority");

            Assert.AreEqual(expectedUpdate.Components.Updates.Store.Count,
                bufferedEntityState.Components.Updates.Store.Count, "Same number of component changes");

            foreach (var (compID, expectedComp) in expectedUpdate.Components.Updates.Store)
            {
                if (!bufferedEntityState.Components.Updates.Store.TryGetValue(compID, out var comp))
                    Assert.Fail($"Missing component {compID}");

                Assert.That(comp.FieldCount, Is.EqualTo(expectedComp.FieldCount), "ComponentIDs");
                for (var i = 0; i < comp.FieldCount; i++)
                {
                    var expectedField = expectedComp.GetField(i);
                    var field = comp.GetField(i);
                    Assert.That(field.Equals(expectedField), Is.True, $"Field {i} value");

                    Assert.That(field.HasSimFrames, Is.EqualTo(expectedField.HasSimFrames), $"Field {i} HasSimFrames");

                    if (field.HasSimFrames)
                    {
                        Assert.That(field.GetSimFrame(), Is.EqualTo(expectedField.GetSimFrame()), $"Field {i} simFrame");
                    }
                }

                Assert.That(comp.FieldsMask, Is.EqualTo(expectedComp.FieldsMask), "Fields Mask");
                Assert.That(comp.StoppedMask, Is.EqualTo(expectedComp.StoppedMask), "Stopped Mask");
            }

            foreach (var compType in expectedUpdate.Components.Destroys)
            {
                Assert.IsTrue(bufferedEntityState.Components.Destroys.Contains(compType),
                    "Component Destroys Are Equal");
            }
        }
    }
}
