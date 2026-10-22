// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Editor.Toolkit
{
    using UnityEditor;

    [CanEditMultipleObjects]
    [CustomEditor(typeof(Coherence.Toolkit.CoherenceGlobalQuery))]
    internal class CoherenceGlobalQueryEditor : CoherenceQueryEditor
    {
        protected override string HelpTitle => "What is a Global Query";
        protected override string HelpBody => "Makes all network entities that have been marked as Is Global visible.\n\nAn entity can marked as global using their CoherenceSync component.\n\nIf multiple query components are active simultaneously, they work additively.";

        protected override void OnGUI()
        {
            base.OnGUI();
            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, "m_Script");
            _ = serializedObject.ApplyModifiedProperties();
        }
    }
}
