using UnityEngine;

/// Satu pemberhentian tur di depan gedung. Pasang pada objek dengan Collider trigger (area lingkaran).
///
/// Selama tujuan quest untuk gedung ini sedang aktif: lingkaran + beacon menyala dan minimap menunjuk ke sini.
/// Saat pemain lokal berdiri di dalam lingkaran dan sedang bebas (lihat CityTourGuide.CanPresent),
/// Vio & Vira muncul di 'mascotSpot' dan memutar 'dialogue'. Tujuan quest-nya bertipe FinishDialogue dengan
/// dialog yang sama, jadi tujuan otomatis selesai saat dialog dibaca sampai habis.
[RequireComponent(typeof(Collider))]
public sealed class CityTourStop : MonoBehaviour {
    [SerializeField] private QuestData quest;
    [Tooltip("ID tujuan di quest untuk gedung ini (QuestData > Objectives > Objective Id).")]
    [SerializeField] private string objectiveId;
    [Tooltip("Dialog penjelasan gedung. Harus sama dengan dialog di tujuan FinishDialogue-nya.")]
    [SerializeField] private DialogueData dialogue;
    [Tooltip("Tempat Vio & Vira muncul (di depan pintu gedung).")]
    [SerializeField] private Transform mascotSpot;
    [Tooltip("Lingkaran di tanah + beacon. Hanya tampil saat gedung ini tujuan aktif.")]
    [SerializeField] private GameObject marker;

    private bool _playerInside;
    private bool _active;

    public string ObjectiveId => objectiveId;

    private void Reset() => GetComponent<Collider>().isTrigger = true;

    private void Awake() {
        GetComponent<Collider>().isTrigger = true;
        if (quest == null || dialogue == null || mascotSpot == null)
            Debug.LogWarning($"[Tour] CityTourStop '{name}' belum lengkap (quest/dialogue/mascotSpot).", this);
    }

    private void OnEnable() {
        QuestService.Changed += Refresh;
        Refresh();
    }

    private void OnDisable() {
        QuestService.Changed -= Refresh;
        _playerInside = false;
        SetActive(false);
    }

    private void Refresh() => SetActive(IsCurrentObjective());

    private bool IsCurrentObjective() {
        if (quest == null || !QuestService.IsActive(quest.Id)) return false;
        foreach (QuestService.Entry entry in QuestService.GetActiveQuests()) {
            if (entry.Data != quest) continue;
            for (int i = 0; i < quest.objectives.Length; i++)
                if (quest.ObjectiveId(i) == objectiveId)
                    return QuestService.IsObjectiveAvailable(quest, entry.Progress, i);
        }
        return false;
    }

    private void SetActive(bool active) {
        _active = active;
        if (marker != null) marker.SetActive(active);
        if (active) QuestWaypoint.Set(transform);
        else QuestWaypoint.Clear(transform);
    }

    // Dicek tiap frame selama pemain di dalam, supaya maskot tetap muncul setelah pemain selesai
    // dengan hal lain (dialog NPC lain, panel, jatuh) tanpa harus keluar-masuk lingkaran.
    private void Update() {
        if (!_active || !_playerInside || CityTourGuide.Instance == null) return;
        if (CityTourGuide.Instance.IsPresenting || !CityTourGuide.CanPresent) return;
        // Beacon disembunyikan selama maskot bicara: kamera dialog bisa berada di dalam silinder beacon dan
        // membuat layar kuning. Setelah selesai, Refresh menentukan apakah marker perlu tampil lagi.
        if (CityTourGuide.Instance.Present(mascotSpot, dialogue, Refresh) && marker != null) marker.SetActive(false);
    }

    private void OnTriggerEnter(Collider other) {
        if (IsLocalPlayer(other)) _playerInside = true;
    }

    private void OnTriggerExit(Collider other) {
        if (IsLocalPlayer(other)) _playerInside = false;
    }

    private static bool IsLocalPlayer(Collider other) {
        var player = other.GetComponentInParent<PlayerController>();
        return player != null && player == PlayerController.Local;
    }
}
