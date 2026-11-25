using UnityEngine;

/// Penanda dunia (mis. lingkaran cahaya + beacon Tur Kota, prefab Tour_Marker) yang hanya tampil selama satu tujuan
/// quest bisa dikerjakan, mis. "Tunjukkan gaya barumu ke Mira" di depan meja kasir. Hanya penanda: tujuannya tetap
/// diselesaikan dengan caranya sendiri (bicara dengan NPC, dll).
/// Disembunyikan selama dialog berjalan, karena kamera dialog bisa berada di dalam beacon dan membuat layar menguning.
public sealed class QuestObjectiveMarker : MonoBehaviour {
    [SerializeField] private QuestData quest;
    [Tooltip("ID tujuan di quest (QuestData > Objectives > Objective Id).")]
    [SerializeField] private string objectiveId;
    [Tooltip("Objek yang dinyalakan/dimatikan (mis. instance Tour_Marker). Jangan objek ini sendiri.")]
    [SerializeField] private GameObject marker;

    private bool _objectiveAvailable;

    private void OnEnable() {
        QuestService.Changed += Refresh;
        Refresh();
    }

    private void OnDisable() => QuestService.Changed -= Refresh;

    private void Refresh() {
        _objectiveAvailable = quest != null && ProgressService.Current != null &&
                              QuestService.IsObjectiveAvailable(quest.Id, objectiveId);
        Apply();
    }

    // DialogueUI tidak punya event mulai/selesai untuk semua dialog, jadi status dialog dicek tiap frame (ringan).
    private void Update() => Apply();

    private void Apply() {
        if (marker == null) return;
        bool talking = DialogueUI.Instance != null && DialogueUI.Instance.IsActive;
        bool show = _objectiveAvailable && !talking;
        if (marker.activeSelf != show) marker.SetActive(show);
    }
}
