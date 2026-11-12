using System.Collections.Generic;
using UnityEngine;

/// Daftar semua QuestData, supaya quest yang tersimpan di Cloud Save (hanya ID) bisa dicari datanya lagi.
/// Diisi otomatis oleh QuestDatabaseSync di Editor setiap ada QuestData dibuat/diubah/dihapus.
public sealed class QuestDatabase : ScriptableObject {
    public const string ResourcePath = "QuestDatabase";

    [SerializeField] private List<QuestData> quests = new List<QuestData>();

    private static QuestDatabase _instance;
    public static QuestDatabase Instance => _instance ? _instance : _instance = Resources.Load<QuestDatabase>(ResourcePath);

    public IReadOnlyList<QuestData> Quests => quests;

    public QuestData Find(string questId) {
        foreach (QuestData quest in quests)
            if (quest != null && quest.Id == questId) return quest;
        return null;
    }
}
