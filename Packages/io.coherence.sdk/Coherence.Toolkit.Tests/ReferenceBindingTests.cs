namespace Coherence.Toolkit.Tests
{
    using Bindings.ValueBindings;
    using NUnit.Framework;
    using Coherence.Tests;
    using Entities;
    using Interpolation;
    using Schema;

    public class ReferenceBindingTests : CoherenceTest
    {
        private static readonly Entity EntityA = new((Index)1, 0, Entity.Relative);
        private static readonly Entity EntityB = new((Index)2, 0, Entity.Relative);
        private static readonly Entity EntityC = new((Index)3, 0, Entity.Relative);
        private static readonly Entity EntityD = new((Index)4, 0, Entity.Relative);

        private TestableReferenceBinding binding;
        private InterpolationSettings noneSettings;
        private InterpolationSettings linearSettings;

        public override void OneTimeSetUp()
        {
            noneSettings = InterpolationSettings.CreateEmpty();
            linearSettings = InterpolationSettings.CreateDefault();
        }

        public override void OneTimeTearDown()
        {
            UnityEngine.Object.DestroyImmediate(noneSettings);
            UnityEngine.Object.DestroyImmediate(linearSettings);
        }

        private TestableReferenceBinding CreateBinding(InterpolationSettings settings)
        {
            var b = new TestableReferenceBinding();
            b.interpolationSettings = settings;
            b.CreateArchetypeData(SchemaType.Entity, 0);
            b.Activate();
            return b;
        }

        [Test]
        [Description("Non-interpolated: IsStopped with no pending sample skips all work.")]
        public void Interpolate_None_Stopped_NoPendingSample_SkipsWork()
        {
            binding = CreateBinding(noneSettings);
            binding.Value = EntityA;

            // Receive and consume one sample
            binding.Interpolator.AppendSample(EntityB, false, true, 0.0, 0.0);
            binding.Interpolate(0.0);
            Assert.AreEqual(EntityB, binding.Value);

            // Set a local value — Interpolate should not touch it
            binding.Value = EntityA;
            binding.Interpolate(1.0);

            Assert.AreEqual(EntityA, binding.Value, "Should not overwrite value when stopped with no pending sample");
        }

        [Test]
        [Description("Non-interpolated: new sample triggers resolution and applies value.")]
        public void Interpolate_None_NewSample_AppliesValue()
        {
            binding = CreateBinding(noneSettings);
            binding.Value = EntityA;

            binding.Interpolator.AppendSample(EntityB, false, true, 0.0, 0.0);
            binding.Interpolate(0.0);

            Assert.AreEqual(EntityB, binding.Value);
        }

        [Test]
        [Description("Non-interpolated: new sample prepares value sync callback with correct old and new values.")]
        public void Interpolate_None_NewSample_PreparesCallback()
        {
            binding = CreateBinding(noneSettings);
            binding.Value = EntityA;

            binding.Interpolator.AppendSample(EntityB, false, true, 0.0, 0.0);
            binding.Interpolate(0.0);

            Assert.IsTrue(binding.IsValueSyncPrepared, "Should prepare value sync callback on value change");
            Assert.AreEqual(EntityA, binding.SyncOldValue, "Old value should be the previous resolved entity");
            Assert.AreEqual(EntityB, binding.SyncNewValue, "New value should be the newly resolved entity");
        }

        [Test]
        [Description("Non-interpolated: when resolution fails, value is not changed and keeps retrying until resolved.")]
        public void Interpolate_None_ResolutionFails_RetriesNextFrame()
        {
            binding = CreateBinding(noneSettings);
            binding.Value = EntityA;

            binding.Interpolator.AppendSample(EntityB, false, true, 0.0, 0.0);
            binding.FailResolution = true;

            binding.Interpolate(0.0);
            Assert.AreEqual(EntityA, binding.Value, "Value must not change when resolution fails");
            Assert.IsFalse(binding.IsValueSyncPrepared, "Should not fire callback when resolution fails");

            // Simulate next frame — still failing
            binding.Interpolate(0.1);
            Assert.AreEqual(EntityA, binding.Value, "Value must remain unchanged while resolution keeps failing");
            Assert.IsFalse(binding.IsValueSyncPrepared, "Should still not fire callback");

            // Resolution succeeds
            binding.FailResolution = false;
            binding.Interpolate(0.2);

            Assert.AreEqual(EntityB, binding.Value);
            Assert.IsTrue(binding.IsValueSyncPrepared, "Should fire callback once resolution succeeds");
            Assert.AreEqual(EntityA, binding.SyncOldValue, "Old value should be the original entity before resolution");
            Assert.AreEqual(EntityB, binding.SyncNewValue, "New value should be the resolved entity");
        }

        [Test]
        [Description("Non-interpolated: sample with same entity as current value clears needsResolving without callback.")]
        public void Interpolate_None_SameEntity_NoCallback()
        {
            binding = CreateBinding(noneSettings);
            binding.Value = EntityA;

            // Consume initial — get to clean state
            binding.Interpolator.AppendSample(EntityA, false, true, 0.0, 0.0);
            binding.Interpolate(0.0);
            binding.ResetValueSyncPrepared();

            // New sample with same entity
            binding.Interpolator.AppendSample(EntityA, false, true, 1.0, 1.0);
            binding.Interpolate(1.0);

            Assert.IsFalse(binding.IsValueSyncPrepared, "Should not prepare callback when value hasn't changed");
        }

        [Test]
        [Description("Non-interpolated: IsStopped early-return is bypassed when needsResolving is true, and value is unchanged until resolved.")]
        public void Interpolate_None_Stopped_ButNeedsResolving_DoesNotSkip()
        {
            binding = CreateBinding(noneSettings);
            binding.Value = EntityA;

            // Receive sample and fail resolution — consumes the sample (IsStopped becomes true)
            // but needsResolving stays true
            binding.Interpolator.AppendSample(EntityB, false, true, 0.0, 0.0);
            binding.FailResolution = true;
            binding.Interpolate(0.0);

            Assert.IsTrue(binding.Interpolator.IsStopped, "Should be stopped after consuming the sample");
            Assert.AreEqual(EntityA, binding.Value, "Value must not change when resolution fails");
            Assert.IsFalse(binding.IsValueSyncPrepared, "Should not fire callback with failed resolution");

            // Fix resolution — should still process despite IsStopped
            binding.FailResolution = false;
            binding.Interpolate(1.0);

            Assert.IsTrue(binding.IsValueSyncPrepared, "Should process and fire callback because needsResolving overrides IsStopped");
            Assert.AreEqual(EntityB, binding.Value);
        }

        [Test]
        [Description("Interpolated: IsStopped with no needsResolving skips all work.")]
        public void Interpolate_Lerp_Stopped_SkipsWork()
        {
            binding = CreateBinding(linearSettings);
            binding.Value = EntityA;

            Assert.IsTrue(binding.Interpolator.IsStopped, "Should start stopped");

            binding.Interpolate(0.0);
            Assert.AreEqual(EntityA, binding.Value, "Should not touch value when stopped");
        }

        [Test]
        [Description("Interpolated: when resolution fails, value is not changed and keeps retrying even after IsStopped.")]
        public void Interpolate_Lerp_ResolutionFails_RetriesNextFrame()
        {
            binding = CreateBinding(linearSettings);
            binding.Value = EntityA;

            // Feed samples to start interpolation
            binding.Interpolator.AppendSample(EntityA, true, true, 0.0, 0.0);
            binding.Interpolator.AppendSample(EntityB, true, true, 1.0, 1.0);

            // Simulate resolution failure
            binding.FailResolution = true;

            // Advance time far enough for interpolation to reach EntityB
            binding.Interpolate(10.0);
            Assert.AreEqual(EntityA, binding.Value, "Value must not change when resolution fails");
            Assert.IsFalse(binding.IsValueSyncPrepared, "Should not fire callback when resolution fails");

            // Interpolator should be stopped now, but needsResolving keeps us going
            Assert.IsTrue(binding.Interpolator.IsStopped, "Interpolator should be stopped after reaching final sample");

            // Still retrying
            binding.Interpolate(11.0);
            Assert.AreEqual(EntityA, binding.Value, "Value must remain unchanged while resolution keeps failing");
            Assert.IsFalse(binding.IsValueSyncPrepared, "Should still not fire callback");

            // Resolution succeeds
            binding.FailResolution = false;
            binding.Interpolate(12.0);

            Assert.AreEqual(EntityB, binding.Value);
            Assert.IsTrue(binding.IsValueSyncPrepared, "Should fire callback once resolution succeeds");
            Assert.AreEqual(EntityA, binding.SyncOldValue, "Old value should be the original entity before resolution");
            Assert.AreEqual(EntityB, binding.SyncNewValue, "New value should be the resolved entity");
        }

        [Test]
        [Description("Interpolated: new interpolated value applies correctly.")]
        public void Interpolate_Lerp_NewValue_AppliesValue()
        {
            binding = CreateBinding(linearSettings);
            binding.Value = EntityA;

            binding.Interpolator.AppendSample(EntityA, true, true, 0.0, 0.0);
            binding.Interpolator.AppendSample(EntityB, true, true, 1.0, 1.0);

            binding.Interpolate(10.0);

            Assert.AreEqual(EntityB, binding.Value);
        }

        [Test]
        [Description("Interpolated: no callback when interpolated value equals current value.")]
        public void Interpolate_Lerp_SameEntity_NoCallback()
        {
            binding = CreateBinding(linearSettings);
            binding.Value = EntityA;

            // Both samples carry the same entity — interpolation produces EntityA
            binding.Interpolator.AppendSample(EntityA, true, true, 0.0, 0.0);
            binding.Interpolator.AppendSample(EntityA, true, true, 1.0, 1.0);

            binding.Interpolate(10.0);

            Assert.IsFalse(binding.IsValueSyncPrepared, "Should not prepare callback when value hasn't changed");
        }

        [Test]
        [Description("Interpolated: when value changes and resolves, prepares callback with correct values.")]
        public void Interpolate_Lerp_ValueChanges_PreparesCallback()
        {
            binding = CreateBinding(linearSettings);
            binding.Value = EntityA;

            binding.Interpolator.AppendSample(EntityA, true, true, 0.0, 0.0);
            binding.Interpolator.AppendSample(EntityB, true, true, 1.0, 1.0);

            // Advance time far enough for interpolation to produce EntityB
            binding.Interpolate(10.0);

            Assert.AreEqual(EntityB, binding.Value);
            Assert.IsTrue(binding.IsValueSyncPrepared, "Should prepare callback when value changes and resolves");
            Assert.AreEqual(EntityA, binding.SyncOldValue, "Old value should be the previous entity");
            Assert.AreEqual(EntityB, binding.SyncNewValue, "New value should be the interpolated entity");
        }

        [Test]
        [Description("Interpolated: when resolution fails and new far-future samples arrive, retry should use " +
                     "the interpolated value (next sample after current time) rather than jumping to the last sample in the buffer.")]
        public void Interpolate_Lerp_ResolutionRetry_UsesInterpolatedValue_NotLastSample()
        {
            binding = CreateBinding(linearSettings);
            binding.Value = EntityA;

            // Set up interpolation: EntityA → EntityB
            binding.Interpolator.AppendSample(EntityA, true, true, 0.0, 0.0);
            binding.Interpolator.AppendSample(EntityB, true, true, 1.0, 1.0);

            // At time 10, interpolation overshoots past EntityB. Fail resolution.
            binding.FailResolution = true;
            binding.Interpolate(10.0);

            Assert.AreEqual(EntityA, binding.Value, "Value must not change when resolution fails");
            Assert.AreEqual(EntityB, binding.LastAttemptedResolve, "First attempt should try EntityB (interpolated value)");

            // New samples arrive: EntityC at t=50 and EntityD at t=100.
            // After overshoot cleanup, buffer is [EntityB@1(stopped)].
            // Adding these gives [EntityB@1(stopped), EntityC@50, EntityD@100].
            binding.Interpolator.AppendSample(EntityC, true, true, 50.0, 50.0);
            binding.Interpolator.AppendSample(EntityD, true, true, 100.0, 100.0);

            // Resolution now works
            binding.FailResolution = false;

            // At time 10.1: interpolation should pick the pair (EntityB@1, EntityC@50)
            // and snap to EntityC (value2). GetLastSample() would incorrectly return EntityD.
            binding.Interpolate(10.1);

            Assert.AreEqual(EntityC, binding.LastAttemptedResolve,
                "Retry should resolve the interpolated value (EntityC), not jump to last sample (EntityD)");
            Assert.AreEqual(EntityC, binding.Value,
                "Value should be EntityC from interpolation, not EntityD from last sample");
            Assert.IsTrue(binding.IsValueSyncPrepared, "Should fire callback after successful resolution");
            Assert.AreEqual(EntityA, binding.SyncOldValue, "Old value should be the original entity before resolution");
            Assert.AreEqual(EntityC, binding.SyncNewValue, "New value should be EntityC from interpolation");
        }

        [Test]
        [Description("Interpolated: IsStopped early-return is bypassed when needsResolving is true, and value is unchanged until resolved.")]
        public void Interpolate_Lerp_Stopped_ButNeedsResolving_DoesNotSkip()
        {
            binding = CreateBinding(linearSettings);
            binding.Value = EntityA;

            binding.Interpolator.AppendSample(EntityA, true, true, 0.0, 0.0);
            binding.Interpolator.AppendSample(EntityB, true, true, 1.0, 1.0);

            // First call with failed resolution — sets needsResolving
            binding.FailResolution = true;
            binding.Interpolate(10.0);

            Assert.IsTrue(binding.Interpolator.IsStopped, "Should be stopped after reaching final sample");
            Assert.AreEqual(EntityA, binding.Value, "Value must not change when resolution fails");
            Assert.IsFalse(binding.IsValueSyncPrepared, "Should not fire callback with failed resolution");

            // Fix resolution — should still process despite IsStopped
            binding.FailResolution = false;
            binding.Interpolate(11.0);

            Assert.IsTrue(binding.IsValueSyncPrepared, "Should process and fire callback because needsResolving overrides IsStopped");
            Assert.AreEqual(EntityB, binding.Value);
        }

        private class TestableReferenceBinding : ReferenceBinding
        {
            public bool FailResolution;
            private Entity backingValue;

            /// <summary>
            /// The last entity passed to TryResolveAndSetValue, for test assertions.
            /// </summary>
            public Entity? LastAttemptedResolve;

            public override Entity Value
            {
                get => backingValue;
                set => backingValue = value;
            }

            // Simulates MapToUnityObject round-trip: when FailResolution is true,
            // the bridge can't find the Unity object for the entity, so resolution fails.
            protected override bool TryResolveAndSetValue(Entity entity)
            {
                LastAttemptedResolve = entity;

                if (FailResolution)
                {
                    return false;
                }

                backingValue = entity;
                return true;
            }

            public bool IsValueSyncPrepared => valueSyncPrepared;
            public Entity SyncOldValue => valueSyncOld;
            public Entity SyncNewValue => valueSyncNew;
            public void ResetValueSyncPrepared() => valueSyncPrepared = false;
        }
    }
}
