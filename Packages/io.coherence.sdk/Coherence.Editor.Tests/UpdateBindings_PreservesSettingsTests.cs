namespace Coherence.Editor.Tests
{
    using System.Linq;
    using System.Reflection;
    using Coherence.Interpolation;
    using Coherence.Tests;
    using Coherence.Toolkit;
    using Coherence.Toolkit.Bindings;
    using NUnit.Framework;
    using UnityEngine;
    using Object = UnityEngine.Object;

    // Regression coverage for #9817: changing a synced member's definition must not reset other
    // binding settings (interpolation, sync mode, ...) that the user already configured.
    public class UpdateBindings_PreservesSettingsTests : CoherenceTest
    {
        private class ComponentWithSyncedCallback : MonoBehaviour
        {
            [Sync]
            [OnValueSynced(nameof(OnFloatValueSynced))]
            public float floatValue;

            public void OnFloatValueSynced(float oldValue, float newValue) { }
        }

        // Live component: the member `value` is an int.
        private class IntComponent : MonoBehaviour
        {
            [Sync] public int value;
        }

        // Used only to source a float-typed descriptor for `value`, simulating the serialized
        // state from before the member's type was changed from float to int.
        private class FloatComponent : MonoBehaviour
        {
            [Sync] public float value;
        }

        private GameObject gameObject;
        private CoherenceSync sync;
        private ComponentWithSyncedCallback component;
        private InterpolationSettings interpolation;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();
            gameObject = new GameObject();
            sync = gameObject.AddComponent<CoherenceSync>();
            component = gameObject.AddComponent<ComponentWithSyncedCallback>();
            interpolation = ScriptableObject.CreateInstance<InterpolationSettings>();
        }

        [TearDown]
        public override void TearDown()
        {
            new DescriptorProvider().ClearDescriptorCache();
            Object.DestroyImmediate(interpolation);
            Object.DestroyImmediate(gameObject);
            base.TearDown();
        }

        [Test, Description("Adding [OnValueSynced] (which changes the descriptor's callback name) must update the descriptor in place and keep user-configured interpolation and sync mode instead of resetting them.")]
        public void UpdateBindings_PreservesUserSettings_WhenValueSyncCallbackAdded()
        {
            // Simulate the serialized state from BEFORE [OnValueSynced] was added in code:
            // a binding whose descriptor has no value-sync callback, but which the user has
            // configured with a non-default interpolation and sync mode.
            var fieldInfo = typeof(ComponentWithSyncedCallback).GetField(nameof(ComponentWithSyncedCallback.floatValue));
            var staleDescriptor = new Descriptor(typeof(ComponentWithSyncedCallback), fieldInfo);
            staleDescriptor.ValueSyncCallbackName = string.Empty;

            var binding = staleDescriptor.InstantiateBinding(component);
            binding.interpolationSettings = interpolation;
            binding.SyncMode = SyncMode.Manual;
            sync.Bindings.Add(binding);

            // The live descriptor (reflected from the component) now carries the callback name,
            // which makes UpdateBindings consider the descriptor changed.
            var changed = CoherenceSyncUtils.UpdateBindings(sync, forceUpdate: true);

            Assert.That(changed, Is.True, "UpdateBindings should report a change when the callback was added.");
            Assert.That(sync.Bindings.Contains(binding), Is.True, "The binding should be updated in place, not replaced.");
            Assert.That(binding.Descriptor.ValueSyncCallbackName, Is.EqualTo(nameof(ComponentWithSyncedCallback.OnFloatValueSynced)), "The descriptor should have been updated with the new callback name.");
            Assert.That(binding.interpolationSettings, Is.SameAs(interpolation), "User-configured interpolation must be preserved.");
            Assert.That(binding.SyncMode, Is.EqualTo(SyncMode.Manual), "User-configured sync mode must be preserved.");
        }

        [Test, Description("Changing a synced member's type (e.g. float -> int) re-instantiates the binding for the new type, but must carry over user-configured settings (interpolation, sync mode, guid) instead of resetting them.")]
        public void UpdateBindings_PreservesUserSettings_WhenMemberTypeChanged()
        {
            var intComponent = gameObject.AddComponent<IntComponent>();

            // Build the serialized state from BEFORE the type change: a FloatBinding for the
            // member `value`. The float descriptor is sourced from FloatComponent and re-pointed
            // at IntComponent so it matches the live (int) descriptor's owner and member name.
            var floatField = typeof(FloatComponent).GetField(nameof(FloatComponent.value));
            var staleDescriptor = new Descriptor(typeof(FloatComponent), floatField);
            typeof(Descriptor)
                .GetField("ownerType", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(staleDescriptor, new SerializableType(typeof(IntComponent)));

            var staleBinding = staleDescriptor.InstantiateBinding(intComponent);
            staleBinding.interpolationSettings = interpolation;
            staleBinding.SyncMode = SyncMode.Manual;
            var originalGuid = staleBinding.guid;
            sync.Bindings.Add(staleBinding);

            // The live descriptor reflects `value` as an int, so the binding type must change
            // from FloatBinding to IntBinding.
            var changed = CoherenceSyncUtils.UpdateBindings(sync, forceUpdate: true);

            var newBinding = sync.Bindings.FirstOrDefault(b => b.UnityComponent == (Component)intComponent);

            Assert.That(changed, Is.True, "UpdateBindings should report a change when the member's type changed.");
            Assert.That(newBinding, Is.Not.Null, "A binding for the int member should exist after the update.");
            Assert.That(newBinding.GetType(), Is.Not.EqualTo(staleBinding.GetType()), "The binding should have been re-instantiated for the new (int) type.");
            Assert.That(newBinding.Descriptor.MonoAssemblyType, Does.Contain("Int32"), "The binding's descriptor should now describe an int member.");
            Assert.That(newBinding.interpolationSettings, Is.SameAs(interpolation), "User-configured interpolation must be preserved across the type change.");
            Assert.That(newBinding.SyncMode, Is.EqualTo(SyncMode.Manual), "User-configured sync mode must be preserved across the type change.");
            Assert.That(newBinding.guid, Is.EqualTo(originalGuid), "The binding's guid must be preserved so archetype/override references stay valid.");
        }
    }
}
