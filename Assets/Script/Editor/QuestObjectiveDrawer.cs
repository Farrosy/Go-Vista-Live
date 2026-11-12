using UnityEditor;
using UnityEngine;

/// Inspector satu tujuan quest: field Location Id hanya tampil untuk Go To Location, dan Dialogue hanya untuk
/// Finish Dialogue, supaya jelas field mana yang dipakai oleh Type yang dipilih.
[CustomPropertyDrawer(typeof(QuestObjective))]
public sealed class QuestObjectiveDrawer : PropertyDrawer {
    private static readonly string[] Common = { "objectiveId", "description", "type" };

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
        EditorGUI.BeginProperty(position, label, property);
        Rect line = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
        property.isExpanded = EditorGUI.Foldout(line, property.isExpanded, Title(property, label), true);
        if (property.isExpanded) {
            EditorGUI.indentLevel++;
            foreach (SerializedProperty field in VisibleFields(property)) {
                line.y += line.height + EditorGUIUtility.standardVerticalSpacing;
                line.height = EditorGUI.GetPropertyHeight(field, true);
                EditorGUI.PropertyField(line, field, true);
            }
            DrawHint(ref line, property);
            EditorGUI.indentLevel--;
        }
        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {
        float spacing = EditorGUIUtility.standardVerticalSpacing;
        float height = EditorGUIUtility.singleLineHeight;
        if (!property.isExpanded) return height;
        foreach (SerializedProperty field in VisibleFields(property))
            height += spacing + EditorGUI.GetPropertyHeight(field, true);
        if (Hint(property) != null) height += spacing + HintHeight;
        return height;
    }

    private const float HintHeight = 32f;

    private static GUIContent Title(SerializedProperty property, GUIContent label) {
        string description = property.FindPropertyRelative("description").stringValue;
        return string.IsNullOrEmpty(description) ? label : new GUIContent($"{label.text}: {description}");
    }

    private static System.Collections.Generic.IEnumerable<SerializedProperty> VisibleFields(SerializedProperty property) {
        foreach (string name in Common) yield return property.FindPropertyRelative(name);
        switch ((QuestObjectiveType)property.FindPropertyRelative("type").enumValueIndex) {
            case QuestObjectiveType.GoToLocation:
                yield return property.FindPropertyRelative("locationId");
                break;
            case QuestObjectiveType.FinishDialogue:
                yield return property.FindPropertyRelative("dialogue");
                break;
        }
    }

    private static string Hint(SerializedProperty property) {
        switch ((QuestObjectiveType)property.FindPropertyRelative("type").enumValueIndex) {
            case QuestObjectiveType.GoToLocation:
                return string.IsNullOrEmpty(property.FindPropertyRelative("locationId").stringValue)
                    ? "Isi Location Id, samakan dengan komponen QuestLocation di dunia." : null;
            case QuestObjectiveType.FinishDialogue:
                return property.FindPropertyRelative("dialogue").objectReferenceValue == null
                    ? "Pilih Dialogue yang harus dibaca sampai habis." : null;
            default:
                return "Diselesaikan dari kode: QuestService.CompleteObjective(questId, objectiveId).";
        }
    }

    private static void DrawHint(ref Rect line, SerializedProperty property) {
        string hint = Hint(property);
        if (hint == null) return;
        line.y += line.height + EditorGUIUtility.standardVerticalSpacing;
        line.height = HintHeight;
        EditorGUI.HelpBox(EditorGUI.IndentedRect(line), hint, MessageType.Info);
    }
}
