// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Editor.Toolkit
{
    using Coherence.Toolkit;
    using UnityEngine;
    using UnityEditor;

    [CanEditMultipleObjects]
    [CustomEditor(typeof(CoherenceLiveQuery))]
    internal class CoherenceLiveQueryEditor : CoherenceQueryEditor
    {
        private static class GUIContents
        {
            public static readonly GUIContent advanced = new("Advanced");
            public static readonly GUIContent inifinite = new("Infinite", "All entities within the scene are considered, independently of their position.");
            public static readonly GUIContent limited = new("Constrained", "Only consider entities contained within a cube volume.");
            public static readonly GUIContent extent = new("Extent", "Extent of the cube. It's always half the length of cube's edges.");
            public static readonly GUIContent buffer = new("Buffer", "Buffered addition to the extent to prevent entity create / destroy hysteresis. The query only adds entities inside the extent and only deletes entities outside of the extent + buffer.");
        }

        private SerializedProperty extentProperty;
        private SerializedProperty bufferProperty;
        private SerializedProperty extentUpdateThreshold;
        private SerializedProperty distanceUpdateThreshold;

        protected override string HelpTitle => "What is a Live Query";
        protected override string HelpBody => "Makes all network entities within an area of interest visible.\n\nIf multiple query components are active simultaneously, they work additively.";

        private bool AdvancedExpanded
        {
            get => extentProperty.isExpanded;
            set => extentProperty.isExpanded = value;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            extentProperty = serializedObject.FindProperty(CoherenceLiveQuery.Properties.Extent);
            bufferProperty = serializedObject.FindProperty(CoherenceLiveQuery.Properties.Buffer);
            extentUpdateThreshold = serializedObject.FindProperty(CoherenceLiveQuery.Properties.ExtentUpdateThreshold);
            distanceUpdateThreshold = serializedObject.FindProperty(CoherenceLiveQuery.Properties.DistanceUpdateThreshold);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            extentProperty.Dispose();
            bufferProperty.Dispose();
            extentUpdateThreshold.Dispose();
            distanceUpdateThreshold.Dispose();
        }

        protected override void OnGUI()
        {
            base.OnGUI();
            serializedObject.Update();
            EditorGUILayout.Space();
            DrawAreaOfInterest();
            _ = serializedObject.ApplyModifiedProperties();
        }

        private void OnSceneGUI()
        {
            var liveQuery = target as CoherenceLiveQuery;
            if (!liveQuery || liveQuery.Extent <= 0)
            {
                return;
            }

            Handles.DrawWireCube(liveQuery.transform.position, 2f * liveQuery.Extent * Vector3.one);

            if (liveQuery.Buffer > 0)
            {
                using (new Handles.DrawingScope(Color.grey))
                {
                    Handles.DrawWireCube(liveQuery.transform.position, 2f * (liveQuery.Extent + liveQuery.Buffer) * Vector3.one);
                }
            }
        }

        private void DrawAreaOfInterest()
        {
            EditorGUI.showMixedValue = extentProperty.hasMultipleDifferentValues;
            EditorGUI.BeginChangeCheck();
            var infinite = EditorGUILayout.Toggle(GUIContents.inifinite, extentProperty.floatValue <= 0, EditorStyles.radioButton);
            if (EditorGUI.EndChangeCheck() && infinite)
            {
                extentProperty.floatValue = 0f;
            }
            EditorGUI.BeginChangeCheck();
            var constrained = EditorGUILayout.Toggle(GUIContents.limited, extentProperty.floatValue > 0, EditorStyles.radioButton);
            if (EditorGUI.EndChangeCheck() && constrained)
            {
                extentProperty.floatValue = 10f;
            }
            EditorGUI.showMixedValue = false;

            if (constrained)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(extentProperty, GUIContents.extent);
                EditorGUILayout.PropertyField(bufferProperty, GUIContents.buffer);
                AdvancedExpanded = EditorGUILayout.Foldout(AdvancedExpanded, GUIContents.advanced, true);
                if (AdvancedExpanded)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(distanceUpdateThreshold);
                    EditorGUILayout.PropertyField(extentUpdateThreshold);
                    EditorGUI.indentLevel--;
                }
                EditorGUI.indentLevel--;
            }
        }
    }
}
