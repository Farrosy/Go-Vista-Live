using UnityEngine;

/// Titik tujuan GPS untuk satu tujuan quest yang bukan "pergi ke lokasi", mis. "Bicara dengan Pak Budi"
/// (FinishDialogue) atau tujuan Manual. Pasang di objek dunia yang harus didatangi (NPC, pintu, papan).
/// Selama tujuan itu bisa dikerjakan, marker minimap dan rute GPS mengarah ke objek ini.
/// Tujuan GoToLocation tidak perlu komponen ini: QuestLocation-nya sudah otomatis jadi tujuan GPS.
public sealed class QuestTargetPoint : MonoBehaviour {
    [SerializeField] private QuestData quest;
    [Tooltip("ID tujuan di quest (QuestData > Objectives > Objective Id). Kalau di QuestData kosong, isi 'o<nomor urut>', mis. 'o0'.")]
    [SerializeField] private string objectiveId;

    private void OnEnable() {
        if (quest == null || string.IsNullOrEmpty(objectiveId)) {
            Debug.LogWarning($"[Quest] QuestTargetPoint '{name}' belum diisi quest/objectiveId.", this);
            return;
        }
        QuestWaypoint.Register(quest.Id, objectiveId, transform);
    }

    private void OnDisable() => QuestWaypoint.Unregister(transform);
}
