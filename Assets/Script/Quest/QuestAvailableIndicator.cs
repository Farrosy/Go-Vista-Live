using UnityEngine;

/// Tanda "!" di atas NPC pemberi quest. Tampil selama quest belum diterima dan belum selesai.
public sealed class QuestAvailableIndicator : MonoBehaviour {
    [SerializeField] private QuestData quest;
    [Tooltip("Objek tanda (mis. teks '!' melayang). Disembunyikan saat quest sudah diterima/selesai.")]
    [SerializeField] private GameObject indicator;
    [SerializeField] private float bobHeight = 0.12f;
    [SerializeField] private float bobSpeed = 2.5f;

    private Vector3 _basePosition;

    private void Awake() {
        if (indicator != null) _basePosition = indicator.transform.localPosition;
    }

    private void OnEnable() {
        QuestService.Changed += Refresh;
        Refresh();
    }

    private void OnDisable() => QuestService.Changed -= Refresh;

    private void Refresh() {
        if (indicator == null || quest == null) return;
        indicator.SetActive(!QuestService.IsActive(quest.Id) && !QuestService.IsCompleted(quest.Id));
    }

    private void Update() {
        if (indicator == null || !indicator.activeSelf) return;
        indicator.transform.localPosition = _basePosition + Vector3.up * (Mathf.Sin(Time.time * bobSpeed) * bobHeight);
    }
}
