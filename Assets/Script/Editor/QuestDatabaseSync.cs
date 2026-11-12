using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// Menjaga Assets/Resources/QuestDatabase.asset selalu berisi semua QuestData di project.
public sealed class QuestDatabaseSync : AssetPostprocessor {
    private const string DatabasePath = "Assets/Resources/" + QuestDatabase.ResourcePath + ".asset";

    private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom) {
        if (Touches(imported) || Touches(deleted) || Touches(moved)) EditorApplication.delayCall += Sync;
    }

    private static bool Touches(string[] paths) {
        foreach (string path in paths)
            if (path.EndsWith(".asset") && path != DatabasePath) return true;
        return false;
    }

    [MenuItem("Go Vista/Quest/Perbarui QuestDatabase")]
    public static void Sync() {
        var quests = new List<QuestData>();
        var ids = new HashSet<string>();
        foreach (string guid in AssetDatabase.FindAssets("t:QuestData")) {
            var quest = AssetDatabase.LoadAssetAtPath<QuestData>(AssetDatabase.GUIDToAssetPath(guid));
            if (quest == null) continue;
            if (!ids.Add(quest.Id)) Debug.LogError($"[Quest] questId '{quest.Id}' dipakai lebih dari satu QuestData.", quest);
            quests.Add(quest);
        }
        quests.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));

        var database = AssetDatabase.LoadAssetAtPath<QuestDatabase>(DatabasePath);
        if (database == null) {
            if (quests.Count == 0) return;
            Directory.CreateDirectory(Path.GetDirectoryName(DatabasePath));
            database = ScriptableObject.CreateInstance<QuestDatabase>();
            AssetDatabase.CreateAsset(database, DatabasePath);
        }

        var serialized = new SerializedObject(database);
        SerializedProperty list = serialized.FindProperty("quests");
        bool changed = list.arraySize != quests.Count;
        for (int i = 0; !changed && i < quests.Count; i++)
            changed = list.GetArrayElementAtIndex(i).objectReferenceValue != quests[i];
        if (!changed) return;

        list.arraySize = quests.Count;
        for (int i = 0; i < quests.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = quests[i];
        serialized.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssets();
    }
}
