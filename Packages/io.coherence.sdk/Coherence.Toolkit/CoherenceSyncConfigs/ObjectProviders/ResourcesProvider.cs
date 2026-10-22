// Copyright (c) coherence ApS.
// See the license file in the project root for more information.

namespace Coherence.Toolkit
{
    using System;
    using UnityEngine;
    using Object = UnityEngine.Object;

    [Serializable, DisplayName("Resources", "Load this prefab using the Resources system. The prefab must be inside a Resources folder.")]
    public sealed class ResourcesProvider : INetworkObjectProvider
    {
        [SerializeField, ReadOnly(false)]
        private string resourcesPath;

        internal ICoherenceSync syncObject;
        private int references;

        public int References => references;

        public void LoadAsset(string networkAssetId, Action<ICoherenceSync> onLoaded)
        {
            GetSyncObject(networkAssetId);

            onLoaded.Invoke(syncObject);
        }

        public ICoherenceSync LoadAsset(string networkAssetId)
        {
            GetSyncObject(networkAssetId);

            return syncObject;
        }

        private void GetSyncObject(string networkAssetId)
        {
            references++;
            syncObject ??= LoadCoherenceSync();

            if (syncObject == null)
            {
                Debug.LogError($"Asset with ID {networkAssetId} failed to load from Resources with path {resourcesPath}. " +
                               "Please double check the Asset configuration.");
            }
        }

        public void Release(ICoherenceSync obj)
        {
            references--;

            if (references == 0)
            {
                syncObject = null;
            }
        }

        public void OnApplicationQuit()
        {
            references = 0;
            syncObject = null;
        }

        private ICoherenceSync LoadCoherenceSync()
        {
            var obj = Resources.Load<Object>(resourcesPath);

            if (obj is GameObject go)
            {
                return go.GetComponent<ICoherenceSync>();
            }

            return obj as ICoherenceSync;
        }

        public void Initialize(CoherenceSyncConfig entry)
        {
#if UNITY_EDITOR
           resourcesPath = GetResourcesPath(entry);
           UnityEditor.EditorUtility.SetDirty(entry);
#endif
        }

        public bool Validate(CoherenceSyncConfig entry)
        {
#if UNITY_EDITOR
            var newResourcesPath = GetResourcesPath(entry);

            return resourcesPath.Equals(newResourcesPath);
#else
            return true;
#endif
        }

#if UNITY_EDITOR
        internal static string GetResourcesPath(CoherenceSyncConfig entry)
        {
            var path = UnityEditor.AssetDatabase.GetAssetPath(entry.EditorTarget);
            return GetResourcesPath(path);
        }

        internal static string GetResourcesPath(string path)
        {
            const string resourcesFolder = "/Resources/";
            var resourcesFolderLength = resourcesFolder.Length;

            var index = path.IndexOf(resourcesFolder, StringComparison.InvariantCulture);

            if (index == -1)
            {
                return string.Empty;
            }

            // Example: "Assets/Prefabs/Resources/A/B/Resources/C/Foo.Bar.prefab" -> "A/B/Resources/C/Foo.Bar"
            // Note that it'll be also accessible via "C/Foo.Bar",
            // but we prefer the longest path to avoid potential name clashes.
            var startIndex = index + resourcesFolderLength;
            var length = path.LastIndexOf('.') - startIndex;
            return path.Substring(startIndex, length);
        }
#endif
    }
}

