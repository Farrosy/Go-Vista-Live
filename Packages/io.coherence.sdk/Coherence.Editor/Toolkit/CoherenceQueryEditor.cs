// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Editor.Toolkit
{
    using Coherence.Toolkit;
    using UnityEditor;
    using UnityEngine;

    [CanEditMultipleObjects]
    internal abstract class CoherenceQueryEditor : BaseEditor
    {
        private CoherenceQuery query;
        private GameObjectStatus gameObjectStatus;
        private SerializedProperty helpExpanded;

        protected virtual string HelpTitle => null;
        protected virtual string HelpBody => null;

        protected override void OnEnable()
        {
            base.OnEnable();
            EditorApplication.hierarchyChanged += OnHierarchyChanged;
            helpExpanded = serializedObject.FindProperty(nameof(CoherenceQuery.helpExpanded));
        }

        protected override void OnDisable()
        {
            EditorApplication.hierarchyChanged -= OnHierarchyChanged;
            helpExpanded.Dispose();
            base.OnDisable();
        }

        private void OnHierarchyChanged() => Setup();

        private void Setup()
        {
            query = (CoherenceQuery)target;
            gameObjectStatus = new(query.gameObject);
        }

        protected override void OnGUI()
        {
            if (!ReferenceEquals(target, query))
            {
                Setup();
            }

            if (Application.isPlaying)
            {
                CoherenceQueryValidator.DrawIssueHelpBoxes(query, gameObjectStatus);
            }

            DrawHelp();
        }

        private void DrawHelp()
        {
            if (HelpTitle is not null)
            {
                helpExpanded.isExpanded = DrawHelp(helpExpanded.isExpanded, HelpTitle, HelpBody);
            }
        }

        protected static bool DrawHelp(bool isExpanded, string title, string body)
        {
            isExpanded = EditorGUILayout.BeginFoldoutHeaderGroup(isExpanded, title);
            if (isExpanded)
            {
                EditorGUILayout.LabelField(body, ContentUtils.GUIStyles.wrappedLabel);
                EditorGUILayout.Space();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
            return isExpanded;
        }
    }
}
