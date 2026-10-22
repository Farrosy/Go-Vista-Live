// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Toolkit
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using Log;
    using UnityEngine;
    using Logger = Log.Logger;

    /// <summary>
    /// A component that allows networked prefabs to be nested under each other at edit time.
    /// </summary>
    /// <remarks>
    /// By adding this component, coherence will be able to track which prefab are nested under which ones,
    /// and keep their structure and lifetime synchronised once the game is running.
    /// </remarks>
    [AddComponentMenu("coherence/Prefab Sync Group")]
    [DefaultExecutionOrder(ScriptExecutionOrder.SyncGroup)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CoherenceSync))]
    public class PrefabSyncGroup : MonoBehaviour
    {
        // for components, we don't expose direct creation of instances - add as component instead
        private PrefabSyncGroup()
        {
        }

        [Sync, OnValueSynced(nameof(OnReceivedIds))]
        public byte[] ids;

        private List<CoherenceSync> childCoherenceSyncs = new();

        private int numberOfChildren;

        private CoherenceSync sync;
        internal Logger logger;
        internal Logger lastSyncLogger;

        // this is a fixed size because we know we're using a 32bit value as the string.
        private const int idByteLen = 4;

        internal bool IsReady { get; private set; }

        /// <summary>
        /// Returns if the parameter sync is a tracked child of this prefab sync group.
        /// </summary>
        /// <param name="sync"> The <see cref="CoherenceSync"/> to check if it's a child of this prefab sync group. </param>
        /// <returns>True if the sync is a tracked child of this prefab sync group.</returns>
        public bool IsChildFromSyncGroup(CoherenceSync sync)
        {
            return childCoherenceSyncs.Contains(sync);
        }

        internal List<CoherenceSync> ChildCoherenceSyncs()
        {
            if (childCoherenceSyncs.Count == 0)
            {
                GetChildCoherenceSyncs();
            }

            return childCoherenceSyncs;
        }

        // The CS logger changes depending on when it is connected, so this behaviour
        // should change with it, since they're closely tied.
        private Logger Logger()
        {
            if (!sync || sync.logger == null)
            {
                logger = Log.GetLogger<PrefabSyncGroup>();
            }
            else if (lastSyncLogger != sync.logger)
            {
                logger = sync.logger.With<PrefabSyncGroup>();
                lastSyncLogger = sync.logger;
            }

            return logger;
        }

        private void Awake() => InitializeSelf();

        internal void InitializeSelf() => sync = gameObject.GetComponent<CoherenceSync>();

        private void OnEnable()
        {
            InitializeChildCoherenceSyncs();

            if (!IsReady)
            {
                // we delay enabling coherence sync children to ensure the updates to the ids parameter have been correctly processed
                _ = StartCoroutine(EnableChildrenRoutine());
            }
        }

        internal void InitializeChildCoherenceSyncs()
        {
            GetChildCoherenceSyncs();

            numberOfChildren = childCoherenceSyncs.Count;

            ids = new byte[numberOfChildren * idByteLen];

            var allChildSyncsReady = true;
            for (var i = 0; i < numberOfChildren; i++)
            {
                var id = Guid.NewGuid().GetHashCode();
                var idBytes = BitConverter.GetBytes(id);
                idBytes.CopyTo(ids, i * idByteLen);

                var childSync = childCoherenceSyncs[i];
                if (childSync.transform.parent != transform.parent)
                {
                    allChildSyncsReady = false;
                    childSync.enabled = false;
                    childSync.UnsyncedEntityPriority = CoherenceSync.UnsyncedNetworkEntityPriority.UniqueId;
                    childSync.uniquenessType = CoherenceSync.UniquenessType.NoDuplicates;
                    childSync.ManualUniqueId = BitConverter.ToString(idBytes);
                }
            }

            IsReady = allChildSyncsReady;
        }

        private IEnumerator EnableChildrenRoutine()
        {
            yield return null;

            while (!IsReadyToEnableChildren())
            {
                yield return null;
            }

            childCoherenceSyncs.ForEach(childSync =>
            {
                if (childSync)
                {
                    childSync.enabled = true;
                }
            });
            IsReady = true;
        }

        private bool IsReadyToEnableChildren()
        {
            if (!IsRootReadyToEnableChildren())
            {
                return false;
            }

            if (sync.HasStateAuthority)
            {
                return true;
            }

            // On a remote client, wait for the child entities before enabling local child syncs
            // or else the local child syncs could create entities and take authority first.
            return childCoherenceSyncs.All(IsChildReadyToEnable);
        }

        private bool IsChildReadyToEnable(CoherenceSync childSync)
        {
            return !childSync ||
                childSync.enabled ||
                sync.CoherenceBridge.EntitiesManager.unsyncedNetworkEntitiesByUniqueId.ContainsKey(childSync.ManualUniqueId);
        }

        private bool IsRootReadyToEnableChildren()
        {
            if (!sync.IsSynchronizedWithNetwork)
            {
                return false;
            }

            if (sync.EntityState.NetworkInstantiated)
            {
                return true;
            }

            // A locally-created unique root can still be rejected as a duplicate, so wait for the create ack
            // before allowing its child entities to create.
            return !sync.IsUnique || sync.CoherenceBridge.EntitiesManager.IsEntityAcked(sync.EntityState.EntityID);
        }

        internal void OnReceivedIds(byte[] old, byte[] newIds)
        {
            GetChildCoherenceSyncs();

            var numIDs = newIds.Length / idByteLen;

            if (numIDs > childCoherenceSyncs.Count)
            {
                Logger().Error(Error.ToolkitPrefabSyncGroupIDs,
                    ("got", numIDs),
                    ("expected", childCoherenceSyncs.Count));

                return;
            }

            for (var i = 0; i < numIDs; i++)
            {
                var childSync = childCoherenceSyncs[i];
                if (childSync.transform.parent != transform.parent)
                {
                    var idBytes = new byte[idByteLen];
                    Array.Copy(newIds, i * idByteLen, idBytes, 0, idByteLen);
                    childCoherenceSyncs[i].ManualUniqueId = BitConverter.ToString(idBytes);
                }
            }
        }

        private void GetChildCoherenceSyncs()
        {
            GetComponentsInChildren(true, childCoherenceSyncs);

            for (var i = childCoherenceSyncs.Count - 1; i >= 0; i--)
            {
                if (childCoherenceSyncs[i].transform.parent == transform.parent)
                {
                    childCoherenceSyncs.RemoveAt(i);
                    break;
                }
            }
        }

        private void OnValidate()
        {
            var parent = transform.parent;
            if (parent == null || parent.name.Equals("Canvas (Environment)") || !enabled)
            {
                return;
            }

            Logger().Error(Error.ToolkitPrefabSyncGroupValidation,
                $"{nameof(PrefabSyncGroup)} Component should be at the root of the Prefab. " +
                "If you are using this Prefab as a nested instance in other Prefab Sync Group, please disable this Component in the Prefab instance.");
        }
    }
}
