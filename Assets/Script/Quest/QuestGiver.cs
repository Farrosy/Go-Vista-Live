using UnityEngine;

/// Pemberi quest. Untuk NPC quest pakai mode AfterDialogue: quest diterima setelah dialog tertentu dari NPC
/// (biasanya salah satu stage di DialogueTrigger) dibaca sampai habis. Quest yang sudah aktif/selesai dilewati.
/// Kalau pemain memilih jawaban dengan "Ask Again Later" (mis. 'No'), quest tidak diberikan dan NPC bertanya lagi.
public sealed class QuestGiver : MonoBehaviour {
    public enum GiveMode {
        /// Setelah DialogueData 'giveAfterDialogue' dibaca sampai baris terakhir.
        AfterDialogue,
        /// Saat pemain lokal masuk Collider trigger di objek ini.
        OnEnter,
        /// Hanya lewat Give() dari skrip lain atau UnityEvent.
        Manual,
    }

    [SerializeField] private QuestData[] quests = new QuestData[0];
    [SerializeField] private GiveMode mode = GiveMode.AfterDialogue;
    [Tooltip("Untuk mode AfterDialogue: dialog yang harus tamat dulu, mis. stage NPC yang berisi permintaan tolong.")]
    [SerializeField] private DialogueData giveAfterDialogue;

    private void OnValidate() {
        if (mode == GiveMode.AfterDialogue && giveAfterDialogue == null)
            Debug.LogWarning($"[Quest] QuestGiver '{name}': isi Give After Dialogue untuk mode AfterDialogue.", this);
    }

    private void OnEnable() => DialogueUI.DialogueFinished += OnDialogueFinished;
    private void OnDisable() => DialogueUI.DialogueFinished -= OnDialogueFinished;

    /// <summary>Berikan semua quest yang belum aktif/selesai. Mengembalikan jumlah quest baru.</summary>
    public int Give() {
        int given = 0;
        foreach (QuestData quest in quests)
            if (QuestService.Accept(quest)) given++;
        return given;
    }

    // Dialog hanya bisa dibaca pemain di client ini, jadi event ini selalu milik pemain lokal.
    private void OnDialogueFinished(DialogueData dialogue) {
        if (mode == GiveMode.AfterDialogue && dialogue != null && giveAfterDialogue != null &&
            dialogue.Id == giveAfterDialogue.Id && !ProgressService.ShouldAskAgain(dialogue))
            Give();
    }

    private void OnTriggerEnter(Collider other) {
        if (mode != GiveMode.OnEnter) return;
        var player = other.GetComponentInParent<PlayerController>();
        if (player != null && player == PlayerController.Local) Give();
    }
}
