// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Editor.Toolkit
{
    using Coherence.Toolkit;
    using UnityEditor;

    [CanEditMultipleObjects]
    [CustomEditor(typeof(CoherenceTagQuery))]
    internal class CoherenceTagQueryEditor : CoherenceQueryEditor
    {
        protected override string HelpTitle => "What is a Tag Query";
        protected override string HelpBody => "Makes all network entities with a specific Tag visible.\n\nAn entity's Tag can be set using their CoherenceSync component.\n\nIf multiple query components are active simultaneously, they work additively.";

        protected override void OnGUI()
        {
            base.OnGUI();
            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, "m_Script");
            _ = serializedObject.ApplyModifiedProperties();
        }
    }
}
