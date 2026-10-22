namespace Coherence.Toolkit.Tests
{
    using System;
    using System.Collections.Generic;
    using Bindings;
    using Coherence;
    using Coherence.Common.Tests;
    using Coherence.Tests;
    using Entities;
    using Log;
    using Moq;
    using NUnit.Framework;
    using Bindings.TransformBindings;
    using Common;
    using ProtocolDef;
    using SimulationFrame;
    using Toolkit;
    using UnityEngine;
    using Object = UnityEngine.Object;

    public class CoherenceSyncUpdaterTests : CoherenceTest
    {
        private class FakeBinding : Binding
        {
            private readonly bool isDirty;
            private readonly bool isReadyToSample;
            private readonly bool isCurrentlyPredicted;
            private bool isStopped;

            public bool isInterpolated;
            public bool CalledIsDirty;
            public bool ReceivedComponentData;
            public bool CalledSampleValue;
            public bool CalledWriteComponentData;
            public bool CalledInterpolation;
            public override bool IsInterpolated => isInterpolated;
            public override bool HasValueChangedCallback() => HasCallback;

            public bool HasCallback { get; set; }
            public override uint CoherenceComponentID => IntComponent.ComponentID;
            public override int FieldIndex => 0;

            public Exception ThrowOnInterpolate { private get; set; }
            public Exception ThrowOnIsDirty { private get; set; }

            public FakeBinding(bool isDirty, bool isReadyToSample, bool isCurrentlyPredicted)
            {
                this.isDirty = isDirty;
                this.isReadyToSample = isReadyToSample;
                this.isCurrentlyPredicted = isCurrentlyPredicted;
            }

            public override int GetHashCode() => throw new NotImplementedException();

            public override void IsDirty(AbsoluteSimulationFrame simulationFrame, out bool dirty, out bool justStopped)
            {
                dirty = isDirty;
                justStopped = isStopped;
                CalledIsDirty = true;
                if (ThrowOnIsDirty is not null)
                {
                    throw ThrowOnIsDirty;
                }
            }

            internal override void MarkAsReadyToSend()
            {
            }

            public override void Interpolate(double time)
            {
                CalledInterpolation = true;
                if (ThrowOnInterpolate is not null)
                {
                    throw ThrowOnInterpolate;
                }
            }

            public override void SampleValue(double time) => CalledSampleValue = true;

            public override void RemoveOutdatedSamples(double time)
            {
            }

            public override void ReceiveComponentData(ComponentData coherenceComponent,
                AbsoluteSimulationFrame clientFrame, Vector3 floatingOriginDelta) => ReceivedComponentData = true;

            internal override bool IsReadyToSample(double currentTime) => isReadyToSample;

            public override bool IsCurrentlyPredicted() => isCurrentlyPredicted;
        }

        private class DummyComponent : MonoBehaviour
        {
        }

        private class FakePositionBinding : PositionBinding
        {
            private readonly bool dirty;

            public bool IsInterpolatedOverride { get; set; }
            public bool CalledMarkAsReadyToSend { get; private set; }
            public bool CalledSampleValue { get; set; }

            public override bool IsInterpolated => IsInterpolatedOverride;
            public override int FieldIndex => 0;

            public FakePositionBinding(bool dirty = false) => this.dirty = dirty;

            public override Vector3 Value
            {
                get => Vector3.zero;
                set { }
            }

            public override void IsDirty(AbsoluteSimulationFrame simulationFrame, out bool isDirty,
                out bool justStopped)
            {
                isDirty = dirty;
                justStopped = false;
            }

            public override void SampleValue(double time) => CalledSampleValue = true;

            public override void WriteComponentData(ComponentData coherenceComponent, AbsoluteSimulationFrame simFrame)
            {
            }

            internal override void MarkAsReadyToSend() => CalledMarkAsReadyToSend = true;

            internal override bool IsReadyToSample(double currentTime) => true;
        }

        private GameObject gameObject;

        [TearDown]
        public override void TearDown()
        {
            if (gameObject)
            {
                Object.DestroyImmediate(gameObject);
            }

            base.TearDown();
        }

        [Test]
        [Description("A binding that is both ready to sample and dirty must produce a ComponentData entry in GetComponentUpdates.")]
        public void Should_GetComponentUpdate_When_ComponentIsReadyToSampleAndDirty()
        {
            var binding = new FakeBinding(true, true, false)
            {
                isInterpolated = true,
            };
            binding.unityComponent = CreateDummyComponent();
            using var syncBuilder = new MockSyncBuilder().SetBindings(binding);
            using var clientBuilder = new MockClientBuilder();
            var updater = new CoherenceSyncUpdater(syncBuilder.Sync, clientBuilder.Client);

            var updates = new List<ComponentData>();
            updater.SampleAllBindings(0);
            updater.GetComponentUpdates(updates, 0, 0);

            Assert.IsTrue(binding.CalledSampleValue);
            Assert.IsTrue(updates[0].ComponentID == binding.CoherenceComponentID);
        }

        [Test]
        [Description("A binding that is dirty but not yet ready to sample (e.g. rate-limited) must not produce a ComponentData entry.")]
        public void Should_NotGetComponentUpdate_When_ComponentIsNotReadyToSample()
        {
            var binding = new FakeBinding(true, false, false)
            {
                isInterpolated = true,
            };
            binding.unityComponent = CreateDummyComponent();
            using var  syncBuilder = new MockSyncBuilder().SetBindings(binding);
            using var clientBuilder = new MockClientBuilder();

            var updater = new CoherenceSyncUpdater(syncBuilder.Sync, clientBuilder.Client);

            var updates = new List<ComponentData>();
            updater.SampleAllBindings(0);
            updater.GetComponentUpdates(updates, 0, 0);

            Assert.IsTrue(binding.CalledSampleValue);
            Assert.IsTrue(updates.Count == 0);
        }

        [Test]
        [Description("A binding that is ready to sample but has no changed value must not produce a ComponentData entry — there is nothing new to send.")]
        public void Should_NotGetComponentUpdate_When_ComponentIsNotDirty()
        {
            var binding = new FakeBinding(false, true, false)
            {
                isInterpolated = true,
            };
            binding.unityComponent = CreateDummyComponent();
            using var syncBuilder = new MockSyncBuilder().SetBindings(binding);
            using var clientBuilder = new MockClientBuilder();

            var updater = new CoherenceSyncUpdater(syncBuilder.Sync, clientBuilder.Client);

            var updates = new List<ComponentData>();
            updater.SampleAllBindings(0);
            updater.GetComponentUpdates(updates, 0, 0);

            Assert.IsTrue(binding.CalledSampleValue);
            Assert.IsTrue(updates.Count == 0);
        }

        [Test]
        [Description("When the entity has a uniqueId that differs from the last serialized value, GetComponentUpdates must include a UniqueIDComponent in the update list.")]
        public void Should_GetUuidComponent_When_UuidExistsAndIsDifferentThanLastSerialized()
        {
            var binding = new FakeBinding(true, true, false);
            binding.unityComponent = CreateDummyComponent();
            using var syncBuilder = new MockSyncBuilder().SetBindings(binding).SetUUID("mockId");
            using var clientBuilder = new MockClientBuilder();

            var updater = new CoherenceSyncUpdater(syncBuilder.Sync, clientBuilder.Client);

            var updates = new List<ComponentData>();
            updater.SampleAllBindings(0);
            updater.GetComponentUpdates(updates, 0, 0);

            Assert.IsTrue(updates[1].ComponentID == UniqueIDComponent.ComponentID);
        }

        [Test]
        [Description("PerformInterpolationOnAllBindings must interpolate non-predicted bindings and skip predicted ones, since predicted values are driven locally.")]
        public void Should_CallInterpolation_On_NonPredictedBindings()
        {
            var binding1 = new FakeBinding(true, true, false);
            var binding2 = new FakeBinding(true, true, false);
            var binding3 = new FakeBinding(true, true, true);

            var bridge = CreateComponent<CoherenceBridge>();
            var component = CreateDummyComponent();
            binding1.unityComponent = component;
            binding2.unityComponent = component;
            binding3.unityComponent = component;
            using var syncBuilder = new MockSyncBuilder().SetBindings(binding1, binding2, binding3).SetBridge(bridge);
            using var clientBuilder = new MockClientBuilder();

            var updater = new CoherenceSyncUpdater(syncBuilder.Sync, clientBuilder.Client);

            updater.PerformInterpolationOnAllBindings();

            Assert.IsTrue(binding1.CalledInterpolation);
            Assert.IsTrue(binding2.CalledInterpolation);
            Assert.IsFalse(binding3.CalledInterpolation);
        }

        [Test]
        [Description("PerformInterpolationOnAllBindings catches exceptions thrown during interpolation - e.g. by property setters - logs a warning about it, and then continues to interpolate other bindings.")]
        public void PerformInterpolationOnAllBindings_Catches_Exceptions()
        {
            var loggedWarnings = new List<Warning>();
            UnityLogger.OnLogWarningEventExt += OnLogWarningEventExt;

            try
            {
                var bridge = CreateComponent<CoherenceBridge>();
                var component = CreateDummyComponent();
                var binding1 = new FakeBinding(true, true, false)
                {
                    unityComponent = component
                };
                var binding2 = new FakeBinding(true, true, false)
                {
                    unityComponent = component,
                    ThrowOnInterpolate = new("Test Exception")
                };
                var binding3 = new FakeBinding(true, true, true)
                {
                    unityComponent = component
                };

                using var syncBuilder = new MockSyncBuilder()
                    .SetBindings(binding1, binding2, binding3)
                    .SetBridge(bridge);
                using var clientBuilder = new MockClientBuilder();
                var updater = new CoherenceSyncUpdater(syncBuilder.Sync, clientBuilder.Client);

                updater.PerformInterpolationOnAllBindings();

                Assert.IsTrue(binding1.CalledInterpolation);
                Assert.IsTrue(binding2.CalledInterpolation);
                Assert.IsFalse(binding3.CalledInterpolation);
                Assert.That(loggedWarnings, Contains.Item(Warning.ToolkitBindingInterpolateException));
            }
            finally
            {
                UnityLogger.OnLogWarningEventExt -= OnLogWarningEventExt;
            }

            void OnLogWarningEventExt(object context, Warning warning, string message, (string key, object value)[] args)
                => loggedWarnings.Add(warning);
        }

        [Test]
        [Description("GetComponentUpdates catches exceptions thrown during IsDirty - e.g. by property getters - logs a warning about it, and then continues to check other bindings.")]
        public void GetComponentUpdates_Catches_Exceptions()
        {
            var loggedWarnings = new List<Warning>();
            UnityLogger.OnLogWarningEventExt += OnLogWarningEventExt;

            try
            {
                var component = CreateDummyComponent();
                var binding1 = new FakeBinding(true, true, false)
                {
                    unityComponent = component,
                    isInterpolated = true
                };
                var binding2 = new FakeBinding(true, true, false)
                {
                    unityComponent = component,
                    isInterpolated = true,
                    ThrowOnIsDirty = new("Test Exception")
                };
                var binding3 = new FakeBinding(true, true, false)
                {
                    unityComponent = component,
                    isInterpolated = true
                };
                using var syncBuilder = new MockSyncBuilder().SetBindings(binding1, binding2, binding3);
                using var clientBuilder = new MockClientBuilder();
                var updater = new CoherenceSyncUpdater(syncBuilder.Sync, clientBuilder.Client);
                var updates = new List<ComponentData>();
                updater.SampleAllBindings(0);

                updater.GetComponentUpdates(updates, 0, 0);

                Assert.IsTrue(binding1.CalledIsDirty);
                Assert.IsTrue(binding2.CalledIsDirty);
                Assert.IsFalse(binding3.CalledIsDirty);
                Assert.That(loggedWarnings, Contains.Item(Warning.ToolkitBindingIsDirtyException));
            }
            finally
            {
                UnityLogger.OnLogWarningEventExt -= OnLogWarningEventExt;
            }

            void OnLogWarningEventExt(object context, Warning warning, string message, (string key, object value)[] args)
                => loggedWarnings.Add(warning);
        }

        [Test]
        [Description("When the sync has state authority, ManuallySendAllChanges must call UpdateComponents on the client with the entity's correct ID.")]
        public void Should_ManuallySendChanges_When_HasAuthority()
        {
            var binding1 = new FakeBinding(true, true, false);

            var component = CreateDummyComponent();
            binding1.unityComponent = component;

            var expectedEntityId = new Entity((Entities.Index)1, Byte.MinValue, Entity.Relative);
            using var syncBuilder = new MockSyncBuilder()
                .SetBindings(binding1)
                .EntityID(expectedEntityId);

            using var clientBuilder = new MockClientBuilder();
            var sentUpdates = false;
            var receivedEntityId = Entity.InvalidRelative;
            clientBuilder.OnEntityUpdated += (entityId, _) =>
            {
                receivedEntityId = entityId;
                sentUpdates = true;
            };

            var updater = new CoherenceSyncUpdater(syncBuilder.Sync, clientBuilder.Client);
            updater.SampleAllBindings(0);
            updater.ManuallySendAllChanges(false);

            Assert.IsTrue(sentUpdates);
            Assert.That(receivedEntityId, Is.EqualTo(expectedEntityId));
        }

        [Test]
        [Description("When the sync does not have state authority (AuthorityType.None), ManuallySendAllChanges must not call UpdateComponents.")]
        public void Should_NotManuallySendChanges_When_HasNoAuthority()
        {
            var binding1 = new FakeBinding(true, true, false);

            var component = CreateDummyComponent();
            binding1.unityComponent = component;

            using var syncBuilder = new MockSyncBuilder().SetBindings(binding1);
            var expectedEntityId = new Entity((Entities.Index)1, Byte.MinValue, Entity.Relative);
            syncBuilder.Mock.Setup(c => c.EntityState)
                .Returns(new NetworkEntityState(expectedEntityId, AuthorityType.None, default, default, syncBuilder.Sync, null));

            using var clientBuilder = new MockClientBuilder();
            var sentUpdates = false;
            var receivedEntityId = Entity.InvalidRelative;
            clientBuilder.OnEntityUpdated += (entityId, _) =>
            {
                receivedEntityId = entityId;
                sentUpdates = true;
            };

            var updater = new CoherenceSyncUpdater(syncBuilder.Sync, clientBuilder.Client);
            updater.SampleAllBindings(0);
            updater.ManuallySendAllChanges(false);

            Assert.IsFalse(sentUpdates);
            Assert.IsFalse(expectedEntityId == receivedEntityId);
        }

        [Test]
        [Description("When a ComponentUpdate arrives for a binding's component, ApplyComponentUpdates must call ReceiveComponentData on that binding.")]
        public void Should_ApplyComponentUpdate_When_ComponentUpdateExist()
        {
            var binding = new FakeBinding(true, true, false);
            var component = CreateDummyComponent();
            binding.unityComponent = component;
            using var syncBuilder = new MockSyncBuilder().SetBindings(binding);
            using var clientBuilder = new MockClientBuilder();
            var updater = new CoherenceSyncUpdater(syncBuilder.Sync, clientBuilder.Client);

            var updates = new List<ComponentData>();
            updater.SampleAllBindings(0);
            updater.GetComponentUpdates(updates, 0, 0);

            var entityData = ComponentUpdates.New(updates);

            updater.ApplyComponentUpdates(entityData);

            Assert.IsTrue(binding.ReceivedComponentData);
        }

        [Test]
        [Description("When the tag changes, SendTag should call UpdateComponents with the new tag data, but it should not call RemoveComponents even if the previous tag was non-empty. This is because we changed the implementation to include the tag in the component data instead of as a separate component, so there's no longer a need to remove a separate tag component when the tag changes or is cleared.")]
        public void SendTag_CallsUpdateComponents_When_TagChanged()
        {
            var entityId = new Entity((Entities.Index)1, Byte.MinValue, Entity.Relative);
            using var syncBuilder = new MockSyncBuilder()
                .SetCoherenceTag("mytag")
                .EntityID(entityId);

            using var clientBuilder = new MockClientBuilder();

            var updater = new CoherenceSyncUpdater(syncBuilder.Sync, clientBuilder.Client);
            updater.SendTag();

            clientBuilder.Mock.Verify(client => client.UpdateComponents(entityId, It.IsAny<IReadOnlyList<ComponentData>>()), Times.Once);
            clientBuilder.Mock.Verify(client => client.RemoveComponents(It.IsAny<Entity>(), It.IsAny<uint[]>()), Times.Never);
        }

        [Test]
        [Description("When the tag is cleared after being set, SendTag should call UpdateComponents with the new (empty) tag data, but it should not call RemoveComponents. This verifies that clearing the tag doesn't cause the system to try to remove a separate tag component, which is consistent with the new implementation where the tag is included in the component data.")]
        public void SendTag_CallsUpdateComponents_Not_RemoveComponents_When_TagClearedAfterBeingSet()
        {
            var entityId = new Entity((Entities.Index)1, Byte.MinValue, Entity.Relative);
            using var syncBuilder = new MockSyncBuilder().EntityID(entityId);
            using var clientBuilder = new MockClientBuilder();

            var updater = new CoherenceSyncUpdater(syncBuilder.Sync, clientBuilder.Client);

            // First send a non-empty tag so lastSerializedTag is no longer empty.
            syncBuilder.SetCoherenceTag("mytag");
            updater.SendTag();

            // Now clear the tag — this is the path that previously called RemoveComponents.
            syncBuilder.SetCoherenceTag("");
            updater.SendTag();

            clientBuilder.Mock.Verify(client => client.UpdateComponents(entityId, It.IsAny<IReadOnlyList<ComponentData>>()), Times.Exactly(2));
            clientBuilder.Mock.Verify(client => client.RemoveComponents(It.IsAny<Entity>(), It.IsAny<uint[]>()), Times.Never);
        }

        [Test]
        [Description("If SendTag is called but the tag hasn't changed since the last time it was sent, it should not call UpdateComponents at all since there's no new data to send. This verifies that the optimization to skip sending updates when the tag is unchanged is working correctly.")]
        public void SendTag_DoesNotCallUpdateComponents_When_TagUnchanged()
        {
            var entityId = new Entity((Entities.Index)1, Byte.MinValue, Entity.Relative);
            using var syncBuilder = new MockSyncBuilder()
                .SetCoherenceTag("mytag")
                .EntityID(entityId);

            using var clientBuilder = new MockClientBuilder();

            var updater = new CoherenceSyncUpdater(syncBuilder.Sync, clientBuilder.Client);
            updater.SendTag(); // first call — sets lastSerializedTag
            updater.SendTag(); // second call — tag unchanged, should be a no-op

            clientBuilder.Mock.Verify(client => client.UpdateComponents(entityId, It.IsAny<IReadOnlyList<ComponentData>>()), Times.Once);
        }

        [Test]
        [Description("When a ConnectedEntityComponent destroy arrives and the entity has a connected entity, the ChangedConnection flag and InvalidRelative new connection must be set.")]
        public void ApplyComponentDestroys_SetsChangedConnection_When_ConnectedEntityIsDestroyed()
        {
            var connectedSync = CreateComponent<CoherenceSync>();

            using var syncBuilder = new MockSyncBuilder().SetConnectedEntity(connectedSync);
            using var clientBuilder = new MockClientBuilder();

            var updater = new CoherenceSyncUpdater(syncBuilder.Sync, clientBuilder.Client);

            var destroyMap = new ComponentDestroyMap();
            _ = destroyMap.Add(ConnectedEntityComponent.ComponentID);
            updater.ApplyComponentDestroys(destroyMap);

            Assert.IsTrue(updater.ChangedConnection);
            Assert.AreEqual(Entity.InvalidRelative, updater.NewConnection);
        }

        [Test]
        [Description("When a ConnectedEntityComponent destroy arrives but the entity has no connected entity, the ChangedConnection flag must remain false.")]
        public void ApplyComponentDestroys_DoesNotSetChangedConnection_When_ConnectedEntityIsNull()
        {
            using var syncBuilder = new MockSyncBuilder().SetConnectedEntity(null);
            using var clientBuilder = new MockClientBuilder();

            var updater = new CoherenceSyncUpdater(syncBuilder.Sync, clientBuilder.Client);

            var destroyMap = new ComponentDestroyMap();
            _ = destroyMap.Add(ConnectedEntityComponent.ComponentID);
            updater.ApplyComponentDestroys(destroyMap);

            Assert.IsFalse(updater.ChangedConnection);
        }

        [Test]
        [Description("A destroy for any component ID other than ConnectedEntityComponent must produce a ToolkitSyncUpdateDestroyNotSupported warning.")]
        public void ApplyComponentDestroys_LogsWarning_When_UnknownComponentIsDestroyed()
        {
            var loggedWarnings = new List<Warning>();
            UnityLogger.OnLogWarningEventExt += OnLogWarningEventExt;

            try
            {
                using var syncBuilder = new MockSyncBuilder();
                using var clientBuilder = new MockClientBuilder();
                var updater = new CoherenceSyncUpdater(syncBuilder.Sync, clientBuilder.Client);

                var destroyMap = new ComponentDestroyMap();
                _ = destroyMap.Add(999u);
                updater.ApplyComponentDestroys(destroyMap);

                Assert.That(loggedWarnings, Contains.Item(Warning.ToolkitSyncUpdateDestroyNotSupported));
            }
            finally
            {
                UnityLogger.OnLogWarningEventExt -= OnLogWarningEventExt;
            }

            void OnLogWarningEventExt(object context, Warning warning, string message, (string key, object value)[] args)
                => loggedWarnings.Add(warning);
        }

        [Test]
        [Description("When forceSerialize is true, GetComponentUpdates must include updates from bindings that report IsReadyToSample=false, bypassing the rate-limiting check.")]
        public void GetComponentUpdates_IncludesNotReadyToSampleBindings_When_ForceSerialize()
        {
            var binding = new FakeBinding(isDirty: true, isReadyToSample: false, false)
            {
                isInterpolated = true,
                unityComponent = CreateDummyComponent()
            };

            using var syncBuilder = new MockSyncBuilder().SetBindings(binding);
            using var clientBuilder = new MockClientBuilder();

            var updater = new CoherenceSyncUpdater(syncBuilder.Sync, clientBuilder.Client);
            updater.SampleAllBindings(0);

            var updates = new List<ComponentData>();
            updater.GetComponentUpdates(updates, 0, 0, forceSerialize: true);

            Assert.IsTrue(binding.CalledIsDirty);
            Assert.IsTrue(updates.Count > 0);
        }

        [Test]
        [Description("After receiving a ConnectedEntityComponent update (which sets the internal changed flag), ApplyConnectedEntityChanges must invoke ConnectedEntityChanged on the sync.")]
        public void ApplyConnectedEntityChanges_CallsConnectedEntityChanged_When_FlagIsSet()
        {
            var didChangeParent = false;
            using var syncBuilder = new MockSyncBuilder().SetConnectedEntityChangedReturns(didChangeParent: didChangeParent, success: true);
            using var clientBuilder = new MockClientBuilder();
            var updater = new CoherenceSyncUpdater(syncBuilder.Sync, clientBuilder.Client);

            var parentEntity = new Entity((Entities.Index)1, 0, Entity.Relative);
            var componentUpdates = ComponentUpdates.New(new List<ComponentData>
            {
                ConnectedEntityComponent.Create(TestSchema.DataFactory, parentEntity, default(Vector3d), default, default(Vector3d))
            });
            updater.ApplyComponentUpdates(componentUpdates);

            updater.ApplyConnectedEntityChanges();

            syncBuilder.Mock.Verify(s => s.ConnectedEntityChanged(It.IsAny<Entity>(), out didChangeParent), Times.Once);
        }

        [Test]
        [Description("When ConnectedEntityChanged returns true (success), ApplyConnectedEntityChanges must clear the ChangedConnection flag so the change is not re-applied on the next frame.")]
        public void ApplyConnectedEntityChanges_ClearsChangedConnectionFlag_When_ConnectedEntityChangeSucceeds()
        {
            using var syncBuilder = new MockSyncBuilder().SetConnectedEntityChangedReturns(didChangeParent: false, success: true);
            using var clientBuilder = new MockClientBuilder();

            var updater = new CoherenceSyncUpdater(syncBuilder.Sync, clientBuilder.Client);

            var componentUpdates = ComponentUpdates.New(new List<ComponentData>
            {
                ConnectedEntityComponent.Create(TestSchema.DataFactory, Entity.InvalidRelative, default(Vector3d), default, default(Vector3d))
            });
            updater.ApplyComponentUpdates(componentUpdates);

            Assert.IsTrue(updater.ChangedConnection);

            updater.ApplyConnectedEntityChanges();

            Assert.IsFalse(updater.ChangedConnection);
        }

        [Test]
        [Description("When a connected-entity change results in a parent hierarchy change (didChangeParent=true), InvokeCallbacks must call RaiseOnConnectedEntityChanged.")]
        public void InvokeCallbacks_RaisesOnConnectedEntityChanged_When_ParentChangedDuringConnectedEntityUpdate()
        {
            var binding = new FakeBinding(false, true, false)
            {
                unityComponent = CreateDummyComponent(),
                HasCallback = true
            };

            using var syncBuilder = new MockSyncBuilder()
                .SetBindings(binding)
                .SetConnectedEntityChangedReturns(didChangeParent: true, success: true);

            using var clientBuilder = new MockClientBuilder();
            var updater = new CoherenceSyncUpdater(syncBuilder.Sync, clientBuilder.Client);
            syncBuilder.SetUpdater(updater);

            var componentUpdates = ComponentUpdates.New(new List<ComponentData>
            {
                ConnectedEntityComponent.Create(TestSchema.DataFactory, Entity.InvalidRelative, default(Vector3d), default, default(Vector3d))
            });

            updater.ApplyComponentUpdates(componentUpdates);
            updater.ApplyConnectedEntityChanges();

            updater.InvokeCallbacks();

            syncBuilder.Mock.Verify(s => s.RaiseOnConnectedEntityChanged(), Times.Once);
        }

        [Test]
        [Description("When no PositionBinding is present, TryFlushPosition must return false immediately without sending anything.")]
        public void TryFlushPosition_ReturnsFalse_When_NoPositionBinding()
        {
            using var mockSyncBuilder = new MockSyncBuilder();
            using var clientBuilder = new MockClientBuilder();
            var updater = new CoherenceSyncUpdater(mockSyncBuilder.Sync, clientBuilder.Client);

            Assert.IsFalse(updater.TryFlushPosition(false));
        }

        [Test]
        [Description("When a PositionBinding is present, TryFlushPosition must return true and call MarkAsReadyToSend on the binding before attempting to send.")]
        public void TryFlushPosition_ReturnsTrue_And_MarksBindingReady_When_PositionBindingExists()
        {
            var positionBinding = new FakePositionBinding { unityComponent = CreateDummyComponent() };
            using var syncBuilder = new MockSyncBuilder().SetBindings(positionBinding);
            using var clientBuilder = new MockClientBuilder();
            var updater = new CoherenceSyncUpdater(syncBuilder.Sync, clientBuilder.Client);

            var result = updater.TryFlushPosition(false);

            Assert.IsTrue(result);
            Assert.IsTrue(positionBinding.CalledMarkAsReadyToSend);
        }

        [Test]
        [Description("When a dirty PositionBinding is present, TryFlushPosition must call UpdateComponents on the client with the position update.")]
        public void TryFlushPosition_CallsUpdateComponents_When_PositionBindingIsDirty()
        {
            var positionBinding = new FakePositionBinding(dirty: true)
            {
                unityComponent = CreateDummyComponent(),
            };

            using var syncBuilder = new MockSyncBuilder().SetBindings(positionBinding);
            using var mockClient = new MockClientBuilder();
            var sentUpdates = false;
            mockClient.OnEntityUpdated += (_, _) => sentUpdates = true;

            var updater = new CoherenceSyncUpdater(syncBuilder.Sync, mockClient.Client);

            updater.TryFlushPosition(false);

            Assert.IsTrue(sentUpdates);
        }

        [Test]
        [Description("When an interpolated PositionBinding is present, updateConnectedEntityInSamplingLoop is true, so SampleBindings must call SendConnectedEntity.")]
        public void SampleBindings_CallsSendConnectedEntity_When_PositionBindingIsInterpolated()
        {
            var positionBinding = new FakePositionBinding { IsInterpolatedOverride = true };
            positionBinding.unityComponent = CreateDummyComponent();

            using var syncBuilder = new MockSyncBuilder().SetBindings(positionBinding);

            var updater = new CoherenceSyncUpdater(syncBuilder.Sync, new MockClientBuilder().Build());

            updater.SampleBindings(0);

            syncBuilder.Mock.Verify(s => s.SendConnectedEntity(It.IsAny<double>()), Times.Once);
        }

        [Test]
        [Description("When no interpolated transform bindings are present, updateConnectedEntityInSamplingLoop is false, so SampleBindings must not call SendConnectedEntity.")]
        public void SampleBindings_DoesNotCallSendConnectedEntity_When_NoInterpolatedTransformBindings()
        {
            using var syncBuilder = new MockSyncBuilder();

            var updater = new CoherenceSyncUpdater(syncBuilder.Sync, new MockClientBuilder().Build());

            updater.SampleBindings(0);

            syncBuilder.Mock.Verify(s => s.SendConnectedEntity(It.IsAny<double>()), Times.Never);
        }

        [Test]
        [Description("When no interpolated transform bindings are present (updateConnectedEntityInSamplingLoop=false), SendState must call SendConnectedEntity to ensure the connected entity is flushed.")]
        public void SendState_CallsSendConnectedEntity_When_NoInterpolatedTransformBindings()
        {
            using var syncBuilder = new MockSyncBuilder();

            var updater = new CoherenceSyncUpdater(syncBuilder.Sync, new MockClientBuilder().Build());

            updater.SendState(0, 0);

            syncBuilder.Mock.Verify(s => s.SendConnectedEntity(It.IsAny<double>()), Times.Once);
        }

        [Test]
        [Description("On the first SendState call before SampleAllBindings has run, SendState must internally sample all bindings to capture any changes that happened during the creation frame.")]
        public void SendState_SamplesBindings_When_InitialSampleNotYetDone()
        {
            var binding = new FakeBinding(false, true, false)
            {
                isInterpolated = true,
                unityComponent = CreateDummyComponent(),
            };

            using var syncBuilder = new MockSyncBuilder().SetBindings(binding);

            var updater = new CoherenceSyncUpdater(syncBuilder.Sync, new MockClientBuilder().Build());

            updater.SendState(0, 0);

            Assert.IsTrue(binding.CalledSampleValue);
        }

        private GameObject GetOrCreateGameObject() => gameObject ? gameObject : gameObject = new();
        private T CreateComponent<T>() where T : Component => GetOrCreateGameObject().AddComponent<T>();
        private DummyComponent CreateDummyComponent() => CreateComponent<DummyComponent>();
    }
}
