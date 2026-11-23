using UnityEngine;

/// Tanda "!" di atas NPC. Mode quest: tampil selama quest belum diterima dan belum selesai.
/// Mode dialog (quest kosong, Dialogue Id diisi): tampil selama pemain lokal belum menyelesaikan dialog itu,
/// mis. perkenalan kasir Mira. Progres per akun, jadi tanda ini hanya berlaku di layar pemain ini.
public sealed class QuestAvailableIndicator : MonoBehaviour {
    private const float DialoguePollSeconds = 0.5f;

    [SerializeField] private QuestData quest;
    [Tooltip("Dipakai kalau Quest kosong: tanda tampil sampai dialog dengan ID ini selesai.")]
    [SerializeField] private string dialogueId = "";
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
        if (quest != null) {
            indicator.SetActive(!QuestService.IsActive(quest.Id) && !QuestService.IsCompleted(quest.Id));
            return;
        }
        if (string.IsNullOrEmpty(dialogueId)) return;
        // Sebelum progres dimuat, status dialog belum diketahui: jangan tampilkan dulu.
        indicator.SetActive(ProgressService.IsLoaded && !ProgressService.IsDialogueCompleted(dialogueId));
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
