namespace Coherence.Editor
{
#if UNITY_6000_3_OR_NEWER
    using System.Linq;
#endif
    using Coherence.Toolkit;
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.SceneManagement;

    // This class exists to circumvent an issue with Unity where the selection gets lost.
    // See https://issuetracker.unity3d.com/product/unity/issues/guid/UUM-61690

    [InitializeOnLoad]
    internal static class SelectionRestorer
    {
        private static
#if UNITY_6000_3_OR_NEWER
            EntityId[]
#else
            int[]
#endif
            cachedSelection;

        private const string key = "Coherence.SelectionRestorer.Cache";

        static SelectionRestorer()
        {
            var scene = SceneManager.GetActiveScene();
            cachedSelection = SessionState.GetIntArray(key, null)
#if UNITY_6000_4_OR_NEWER
                ?.Select(x => EntityId.FromULong(EntityIdExtensions.GetFromInstanceId(x, scene))).ToArray()
#elif UNITY_6000_3_OR_NEWER
                ?.Select(x => (EntityId)x).ToArray()
#endif
                ;

            Selection.selectionChanged += OnSelectionChanged;
        }

        /// <summary>
        /// Restore to the last known selection on the next Editor frame
        /// (via <see cref="UnityEditor.EditorApplication.delayCall"/>)
        /// </summary>
        public static void RequestRestore()
        {
            EditorApplication.delayCall -= RestoreSelection;
            EditorApplication.delayCall += RestoreSelection;
        }

        private static void OnSelectionChanged()
        {
            var selection = Selection.
#if UNITY_6000_3_OR_NEWER
                entityIds
#else
                instanceIDs
#endif
                ;

            if (selection.Length <= 0)
            {
                return;
            }

            cachedSelection = selection;
            SessionState.SetIntArray(key, cachedSelection
#if UNITY_6000_4_OR_NEWER
                ?.Select(x => x.GetInstanceId()).ToArray()
#elif UNITY_6000_3_OR_NEWER
                .Select(x => (int)x).ToArray()
#endif
            );
        }

        private static void RestoreSelection()
        {
            if (cachedSelection is not null)
            {
#if UNITY_6000_3_OR_NEWER
                Selection.entityIds = cachedSelection;
#else
                Selection.instanceIDs = cachedSelection;
#endif
            }
        }
    }
}
