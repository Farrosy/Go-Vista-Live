using UnityEngine;

/// Cara satu tujuan quest diselesaikan.
public enum QuestObjectiveType {
    /// Selesai saat pemain lokal masuk QuestLocation dengan locationId yang sama.
    GoToLocation,
    /// Selesai saat DialogueData yang dipilih dibaca sampai baris terakhir.
    FinishDialogue,
    /// Diselesaikan dari kode lewat QuestService.CompleteObjective.
    Manual,
}

[System.Serializable]
public class QuestObjective {
    [Tooltip("ID unik tujuan di dalam quest ini (mis. 'ke-pasar'). Dipakai untuk simpan progres. " +
             "JANGAN diganti setelah rilis. Kosong = 'o<nomor urut>' (berubah kalau urutan digeser).")]
    public string objectiveId;
    [Tooltip("Teks tujuan yang tampil di detail quest, mis. 'Pergi ke Pasar Baru'.")]
    public string description;
    public QuestObjectiveType type;

    [Tooltip("Untuk GoToLocation: samakan dengan locationId di komponen QuestLocation.")]
    public string locationId;
    [Tooltip("Untuk FinishDialogue: dialog yang harus dibaca sampai selesai.")]
    public DialogueData dialogue;
}

/// Data satu quest. Dibuat sebagai asset: Create > Quest > Quest Data.
/// Setiap QuestData otomatis didaftarkan ke Resources/QuestDatabase supaya progres bisa dibaca lagi setelah login.
[CreateAssetMenu(menuName = "Quest/Quest Data")]
public class QuestData : ScriptableObject {
    [Tooltip("ID unik untuk simpan progres. Kosong = pakai nama asset. JANGAN diganti setelah rilis, " +
             "nanti quest pemain yang sedang berjalan hilang.")]
    public string questId;
    public string title = "Quest baru";
    [TextArea(2, 5)] public string description;

    [Tooltip("ON = tujuan harus diselesaikan sesuai urutan. OFF = boleh dalam urutan apa saja.")]
    public bool sequentialObjectives;
    public QuestObjective[] objectives = new QuestObjective[1];

    [Header("Hadiah")]
    [Tooltip("Go Vista Coin yang diberikan saat quest selesai. 0 = tanpa hadiah koin.")]
    [Min(0)] public int rewardCoins;

    public string Id => string.IsNullOrEmpty(questId) ? name : questId;

    public string ObjectiveId(int index) {
        string id = objectives[index].objectiveId;
        return string.IsNullOrEmpty(id) ? "o" + index : id;
    }
}
