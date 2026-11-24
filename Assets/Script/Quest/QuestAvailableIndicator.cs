using UnityEngine;

/// Tanda "!" di atas NPC. Mode quest: tampil selama salah satu quest-nya ditawarkan (belum diterima, belum selesai,
/// dan syaratnya sudah selesai; lihat QuestService.IsAvailable), mis. Tur Kota lalu Tampil Beda di Vio & Vira.
/// Mode dialog (quest kosong, Dialogue Id diisi): tampil selama pemain lokal belum menyelesaikan dialog itu,
/// mis. perkenalan kasir Mira. Di kedua mode tanda juga tampil selama tujuan quest 'objectiveIds' bisa dikerjakan
/// (mis. tunjukkan gaya baru ke Mira). Progres per akun, jadi tanda ini hanya berlaku di layar pemain ini.
public sealed class QuestAvailableIndicator : MonoBehaviour {
    private const float DialoguePollSeconds = 0.5f;

    [SerializeField] private QuestData quest;
    [Tooltip("Quest lain dari NPC yang sama (mis. quest utama berikutnya). Tanda tampil kalau salah satunya ditawarkan.")]
    [SerializeField] private QuestData[] moreQuests = new QuestData[0];
    [Tooltip("Dipakai kalau Quest kosong: tanda tampil sampai dialog dengan ID ini selesai.")]
    [SerializeField] private string dialogueId = "";
    [Tooltip("Opsional: tanda juga tampil selama salah satu tujuan quest ini bisa dikerjakan di NPC ini " +
             "(mis. Tampil Beda > tunjukkan-mira).")]
    [SerializeField] private QuestData objectiveQuest;
    [SerializeField] private string[] objectiveIds = new string[0];
    [Tooltip("Objek tanda (mis. teks '!' melayang). Disembunyikan saat quest sudah diterima/selesai.")]
    [SerializeField] private GameObject indicator;
    [SerializeField] private float bobHeight = 0.12f;
    [SerializeField] private float bobSpeed = 2.5f;

    private Vector3 _basePosition;
    private float _nextPoll;

    private void Awake() {
        if (indicator != null) _basePosition = indicator.transform.localPosition;
    }

    private void OnEnable() {
        QuestService.Changed += Refresh;
        Refresh();
    }

    private void OnDisable() => QuestService.Changed -= Refresh;

    private void Refresh() {
        if (indicator == null) return;
        bool objective = AnyObjectiveAvailable();
        if (quest != null) {
            indicator.SetActive(AnyQuestAvailable() || objective);
            return;
        }
        if (string.IsNullOrEmpty(dialogueId)) {
            if (objectiveQuest != null) indicator.SetActive(objective);
            return;
        }
        // Sebelum progres dimuat, status dialog belum diketahui: jangan tampilkan dulu.
        indicator.SetActive(ProgressService.IsLoaded && (!ProgressService.IsDialogueCompleted(dialogueId) || objective));
    }

    private bool AnyObjectiveAvailable() {
        if (objectiveQuest == null || ProgressService.Current == null) return false;
        foreach (string id in objectiveIds ?? System.Array.Empty<string>())
            if (QuestService.IsObjectiveAvailable(objectiveQuest.Id, id)) return true;
        return false;
    }

    private bool AnyQuestAvailable() {
        if (QuestService.IsAvailable(quest)) return true;
        foreach (QuestData other in moreQuests ?? System.Array.Empty<QuestData>())
            if (QuestService.IsAvailable(other)) return true;
        return false;
    }

    private void Update() {
        // ProgressService tidak punya event "dialog selesai", jadi mode dialog dicek berkala (ringan).
        if (quest == null && !string.IsNullOrEmpty(dialogueId) && Time.unscaledTime >= _nextPoll) {
            _nextPoll = Time.unscaledTime + DialoguePollSeconds;
            Refresh();
        }
        if (indicator == null || !indicator.activeSelf) return;
        indicator.transform.localPosition = _basePosition + Vector3.up * (Mathf.Sin(Time.time * bobSpeed) * bobHeight);
    }
}
