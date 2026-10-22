// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Editor
{
    using System.Collections.Generic;
    using System.Linq;
    using Coherence.Toolkit;
    using Toolkit;
    using UnityEditor;
    using UnityEngine;

    [CustomEditor(typeof(PrefabSyncGroup))]
    internal class PrefabSyncGroupEditor : BaseEditor
    {
        private List<CoherenceSync> nestedSyncs;
        private string nestedSyncsCount;
        private bool helpFoldout;

        protected override void OnEnable()
        {
            base.OnEnable();
            RefreshData();
            EditorApplication.hierarchyChanged += OnHierarchyChanged;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            EditorApplication.hierarchyChanged -= OnHierarchyChanged;
        }

        private void OnHierarchyChanged() => RefreshData();

        private void RefreshData()
        {
            if (!target)
            {
                return;
            }

            var prefabSyncGroup = (PrefabSyncGroup)target;
            nestedSyncs = prefabSyncGroup.GetComponentsInChildren<CoherenceSync>()
                .Skip(1) // self
                .Where(PrefabUtility.IsPartOfAnyPrefab)
                .ToList();
            nestedSyncsCount = nestedSyncs.Count.ToString();
        }

        protected override void OnGUI()
        {
            helpFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(helpFoldout, $"What is a {nameof(PrefabSyncGroup)}?");
            if (helpFoldout)
            {
                GUILayout.Label("Allows Networked Prefabs <b>containing other nested Networked Prefabs</b> to be <b>instantiated</b> properly.", ContentUtils.GUIStyles.richLabel);
                GUILayout.Label("With this component, nested Networked Prefabs are treated as part of a group, and will not be (re)instantiated through the network.", ContentUtils.GUIStyles.richLabel);
                GUILayout.Label("This component is not needed when the Networked Prefab (with nested Networked Prefabs) is part of the scene. For such cases, disable this component and sync the hierarchy normally using Uniqueness.", ContentUtils.GUIStyles.richLabel);
            }

            EditorGUILayout.Separator();

            if (nestedSyncs.Count == 0)
            {
                EditorGUILayout.LabelField("No Nested Networked Prefabs");
                return;
            }

            EditorGUILayout.LabelField("Nested Networked Prefabs", nestedSyncsCount);

            EditorGUI.BeginDisabledGroup(true);
            foreach (var sync in nestedSyncs)
            {
                _ = EditorGUILayout.ObjectField(sync, typeof(CoherenceSync), true);
            }
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.Space();

            GUILayout.Label("The Networked Prefabs listed above will use 'No Duplicates' uniqueness strategy, and their Manual Unique IDs will be auto-generated at runtime.", ContentUtils.GUIStyles.miniLabel);
        }
    }
}
