namespace Coherence.Toolkit.Bindings.ValueBindings
{
    using Log;
    using System;
    using Coherence.Debugging;
    using System.Linq;
    using System.Reflection;
    using Entities;
    using UnityEngine;
    using Coherence.Toolkit.Debugging;

    [Serializable]
    public class ReferenceBinding : ValueBinding<Entity>
    {
        private bool needsResolving;

        // Caches the owning CoherenceSync of the referenced object so the hierarchy walk in
        // MapToEntityId only runs when the referenced object changes, not on every sample.
        private object cachedTarget;
        private ICoherenceSync cachedTargetSync;

        // Stored separately from valueChangedCallback to avoid runtime generic type construction
        // (MakeGenericType + DynamicInvoke are not reliable on AOT platforms like IL2CPP).
        private MethodInfo componentCallbackMethodInfo;

        protected ReferenceBinding() { }
        public ReferenceBinding(Descriptor descriptor, Component unityComponent) : base(descriptor, unityComponent)
        {
        }

        public override Entity Value
        {
            get => MapToEntityId(GetValueUsingReflection());
            set => SetValueUsingReflection(MapToUnityObject(value));
        }

        public override void InvokeValueSyncCallback()
        {
            if (!valueSyncPrepared)
            {
                return;
            }

            try
            {
                InvokeValueChangedCallback(valueSyncOld, valueSyncNew);
            }
            catch (Exception handlerException)
            {
                Logger.Error(Error.ToolkitBindingOnValueSyncedException,
                    ("exception", handlerException));
            }

            valueSyncPrepared = false;
        }

        private Entity MapToEntityId(object target)
        {
            if (MonoAssemblyRuntimeType == typeof(CoherenceSync)) return coherenceSync.CoherenceBridge.UnityObjectToEntityId((ICoherenceSync)target);

            if (!ReferenceEquals(target, cachedTarget))
            {
                cachedTarget = target;
                cachedTargetSync = target switch
                {
                    GameObject go => EntitiesManager.GetOwningCoherenceSync(go),
                    Component component => EntitiesManager.GetOwningCoherenceSync(component),
                    _ => null,
                };
            }

            return EntitiesManager.UnityObjectToEntityId(cachedTargetSync);
        }

        private object MapToUnityObject(Entity entityId)
        {
            if (MonoAssemblyRuntimeType == typeof(GameObject)) return coherenceSync.CoherenceBridge.EntityIdToGameObject(entityId);
            if (MonoAssemblyRuntimeType == typeof(RectTransform)) return coherenceSync.CoherenceBridge.EntityIdToRectTransform(entityId);
            if (MonoAssemblyRuntimeType == typeof(Transform)) return coherenceSync.CoherenceBridge.EntityIdToTransform(entityId);
            if (MonoAssemblyRuntimeType == typeof(CoherenceSync)) return coherenceSync.CoherenceBridge.EntityIdToCoherenceSync(entityId);
            if (typeof(Component).IsAssignableFrom(MonoAssemblyRuntimeType)) return coherenceSync.CoherenceBridge.EntityIdToGameObject(entityId)?.GetComponentInChildren(MonoAssemblyRuntimeType, true);
            throw new Exception("unexpected type: " + MonoAssemblyRuntimeType);
        }

        protected override Type GetValueChangedCallbackType()
        {
            // Only GameObject (a non-Component type) reaches this path via InitializeValueChangedCallback.
            // Component subtypes (CoherenceSync, Transform, RectTransform, etc.) use componentCallbackMethodInfo instead.
            if (MonoAssemblyRuntimeType == typeof(GameObject)) return typeof(Action<GameObject, GameObject>);
            throw new Exception("unexpected type: " + MonoAssemblyRuntimeType);
        }

        public override bool HasValueChangedCallback()
        {
            if (!typeof(Component).IsAssignableFrom(MonoAssemblyRuntimeType))
            {
                return base.HasValueChangedCallback();
            }

            // For arbitrary Component subtypes, avoid runtime generic type construction
            // (MakeGenericType + CreateDelegate are not reliable on AOT platforms).
            // Instead, find and store the MethodInfo for direct invocation.
            if (componentCallbackMethodInfo != null)
            {
                return true;
            }

            var callbackName = descriptor?.ValueSyncCallbackName;
            if (string.IsNullOrEmpty(callbackName))
            {
                return false;
            }

            componentCallbackMethodInfo = GetUnityComponentType()
                .GetMethods(Descriptor.OnValueSyncedCallbackBindingFlags)
                .FirstOrDefault(m => m.Name == callbackName);

            return componentCallbackMethodInfo != null;
        }

        /// <summary>
        /// Attempts to resolve the Entity reference to a Unity object and write it to the binding.
        /// Returns false if the referenced entity hasn't been created on this client yet,
        /// leaving the binding value unchanged.
        /// </summary>
        protected virtual bool TryResolveAndSetValue(Entity entity)
        {
            var unityObject = MapToUnityObject(entity);
            var roundTrip = MapToEntityId(unityObject);
            if (DiffersFrom(roundTrip, entity))
            {
                return false;
            }

            Value = entity;
            return true;
        }

        // Entity references can't use the simple consume-once optimization from ValueBinding
        // because resolution from Entity ID to Unity object can fail when the referenced entity
        // hasn't been created yet on this client. We consume the sample immediately but keep
        // re-evaluating every frame until the reference resolves successfully.
        public override void Interpolate(double time)
        {
            if (Interpolator.IsStopped && !needsResolving)
            {
                return;
            }

            var oldValue = Value;
            Entity newValue;

            if (Interpolator.IsInterpolationNone)
            {
                _ = Interpolator.TryConsumePendingSample(out _);

                var lastSample = Interpolator.GetLastSample();
                if (!lastSample.HasValue)
                {
                    DbgAssert.That(false, "needsResolving was true but no sample in buffer");
                    return;
                }

                newValue = lastSample.Value.Value;
            }
            else
            {
                newValue = Interpolate(time, oldValue);
            }

            if (!DiffersFrom(oldValue, newValue))
            {
                // Interpolation hasn't produced a new value or there was a sample
                // carrying the same value. If we were waiting to resolve a previous
                // value that is no longer what interpolation returns, clear the
                // flag — the problematic sample has been superseded.
                needsResolving = false;
                return;
            }

            if (!TryResolveAndSetValue(newValue))
            {
                needsResolving = true;
                return;
            }

            needsResolving = false;
            PrepareValueSyncCallback(oldValue, newValue);
        }


        private void InvokeValueChangedCallback(Entity oldEntityId, Entity newEntityId)
        {
            if (MonoAssemblyRuntimeType == typeof(GameObject))
            {
                ((Action<GameObject, GameObject>)valueChangedCallback).Invoke(
                    coherenceSync.CoherenceBridge.EntityIdToGameObject(oldEntityId),
                    coherenceSync.CoherenceBridge.EntityIdToGameObject(newEntityId));
                return;
            }

            if (typeof(Component).IsAssignableFrom(MonoAssemblyRuntimeType))
            {
                var before = coherenceSync.CoherenceBridge.EntityIdToGameObject(oldEntityId)?.GetComponentInChildren(MonoAssemblyRuntimeType, true);
                var after = coherenceSync.CoherenceBridge.EntityIdToGameObject(newEntityId)?.GetComponentInChildren(MonoAssemblyRuntimeType, true);
                componentCallbackMethodInfo?.Invoke(UnityComponent, new object[] { before, after });
                return;
            }

            throw new Exception("unexpected type: " + MonoAssemblyRuntimeType);
        }

        protected override bool DiffersFrom(Entity first, Entity second)
        {
            return first != second;
        }

        [InterpolationDebugContextItem]
        public void ToggleInterpolationDebugGrapher() => ToggleInterpolationDebugGrapher(v => (uint)v.Index);
    }
}
