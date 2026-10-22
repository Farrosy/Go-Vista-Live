namespace Coherence.Editor
{
    using Coherence.Toolkit;
    using UnityEditor;
    using UnityEngine;

    [InitializeOnLoad]
    internal class SyncPrefabInstanceUniqueId
    {
        static SyncPrefabInstanceUniqueId() => ObjectChangeEvents.changesPublished += ObjectChangeEventsOnChangesPublished;

        private static void ObjectChangeEventsOnChangesPublished(ref ObjectChangeEventStream stream)
        {
            if (CloneMode.Enabled && !CloneMode.AllowEdits)
            {
                return;
            }

            for (int i = 0; i < stream.length; i++)
            {
                var changeEvent = stream.GetEventType(i);

                switch (changeEvent)
                {
                    case ObjectChangeKind.CreateGameObjectHierarchy:
                        {
                            stream.GetCreateGameObjectHierarchyEvent(i, out var data);

                            var go = EditorUtility.
#if UNITY_6000_4_OR_NEWER
                                EntityIdToObject(data.entityId) as GameObject;
#elif UNITY_6000_3_OR_NEWER
                                EntityIdToObject(data.instanceId) as GameObject;
#else
                                InstanceIDToObject(data.instanceId) as GameObject;
#endif
                            PrefabHierarchyUpdated(go, true);
                            break;
                        }
                    case ObjectChangeKind.UpdatePrefabInstances:
                        {
                            stream.GetUpdatePrefabInstancesEvent(i, out var data);

#if UNITY_6000_4_OR_NEWER
                            for (int j = 0; j < data.entityIds.Length; j++)
                            {
                                var entityId = data.entityIds[j];
                                var go = EditorUtility.EntityIdToObject(entityId);
                                PrefabHierarchyUpdated(go as GameObject, false);
                            }
#elif UNITY_6000_3_OR_NEWER
                            for (int j = 0; j < data.instanceIds.Length; j++)
                            {
                                var instanceId = data.instanceIds[j];
                                var go = EditorUtility.EntityIdToObject(instanceId);
                                PrefabHierarchyUpdated(go as GameObject, false);
                            }
#else
                            for (int j = 0; j < data.instanceIds.Length; j++)
                            {
                                var instanceId = data.instanceIds[j];
                                var go = EditorUtility.InstanceIDToObject(instanceId);
                                PrefabHierarchyUpdated(go as GameObject, false);
                            }
#endif
                            break;
                        }
                    case ObjectChangeKind.ChangeGameObjectStructureHierarchy:
                        {
                            stream.GetChangeGameObjectStructureHierarchyEvent(i, out var data);

                            var go = EditorUtility.
#if UNITY_6000_4_OR_NEWER
                                EntityIdToObject(data.entityId) as GameObject;
#elif UNITY_6000_3_OR_NEWER
                                EntityIdToObject(data.instanceId) as GameObject;
#else
                                InstanceIDToObject(data.instanceId) as GameObject;
#endif
                            PrefabHierarchyUpdated(go, false);
                            break;
                        }
                }
            }
        }

        internal static void SyncInstanceUpdated(CoherenceSync instance, string id, bool forceUpdate = false)
        {
            if (instance == null || !instance.IsUnique || instance.ManualUniqueId == id)
            {
                return;
            }

            using var so = new SerializedObject(instance);

            var property = so.FindProperty("scenePrefabInstanceUUID");

            if (string.IsNullOrEmpty(property.stringValue) || forceUpdate)
            {
                property.stringValue = id;
                so.ApplyModifiedProperties();
            }
        }

        private static void PrefabHierarchyUpdated(GameObject go, bool forceUpdate)
        {
            if (go == null)
            {
                return;
            }

            var syncs = go.GetComponentsInChildren<CoherenceSync>(true);

            foreach (var sync in syncs)
            {
                var id =
#if UNITY_6000_4_OR_NEWER
                    EntityId.ToULong(sync.GetEntityId()).ToString();
#else
                    sync.GetInstanceID().ToString();
#endif
                SyncInstanceUpdated(sync, id, forceUpdate);
            }
        }
    }
}
