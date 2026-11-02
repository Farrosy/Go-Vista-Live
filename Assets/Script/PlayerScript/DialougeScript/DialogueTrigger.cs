using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// Pasang di root NPC. Hubungkan ke Interactable > On Interact: drag NPC ini, pilih DialogueTrigger > StartDialogue.
///
/// Tahap dialog: dalam SATU interaksi, NPC memutar semua stage yang BELUM selesai secara berurutan
/// (mis. pertama kali: 1 -> 2). Kalau semua sudah selesai, yang diputar Repeat Dialogue
/// (kosong = stage terakhir diulang). Menambah stage baru di akhir (update NPC) = pemain otomatis
/// dapat stage baru itu saat bicara lagi, lalu kembali ke Repeat Dialogue.
public class DialogueTrigger : MonoBehaviour
{
    [Tooltip("Cerita utama NPC ini. Semua stage yang belum selesai diputar berurutan dalam satu interaksi. " +
             "Tiap asset harus punya ID unik (lihat DialogueData > Progres).")]
    [SerializeField] private DialogueData[] stages;

    [Tooltip("Diputar setiap kali pemain bicara lagi setelah semua stage selesai (mis. sapaan 'Ketemu lagi!'). " +
             "Kosong = stage terakhir diulang.")]
    [SerializeField] private DialogueData repeatDialogue;

    // Field lama (satu dialog). Dipindah otomatis ke 'stages' supaya scene/prefab lama tidak rusak.
    [SerializeField, HideInInspector, FormerlySerializedAs("dialogue")] private DialogueData legacyDialogue;

    [Tooltip("Titik yang difokuskan kamera (kepala NPC). Kosong = dibuat otomatis di atas pivot")]
    [SerializeField] private Transform focusPoint;
    [SerializeField] private Vector3 defaultFocusOffset = new Vector3(0f, 1.6f, 0f);

    /// Dipakai NpcController untuk animasi dan arah hadap.
    public event System.Action DialogueStarted;
    public event System.Action DialogueEnded;

    void OnValidate()
    {
        MigrateLegacy();
        WarnDuplicateIds();
    }

    // ID sama = selesai satu tahap dianggap menyelesaikan tahap lain juga (tahap terlewat).
    private void WarnDuplicateIds()
    {
        var seen = new HashSet<string>();
        var all = new List<DialogueData>(stages ?? System.Array.Empty<DialogueData>()) { repeatDialogue };
        foreach (var s in all)
        {
            if (s && !seen.Add(s.Id))
                Debug.LogWarning($"[Dialogue] '{name}': beberapa stage/Repeat Dialogue memakai Dialogue Id '{s.Id}'. " +
                                 "Tiap stage harus punya ID unik, kalau tidak tahapnya akan terlewat.", this);
        }
    }

    void Awake()
    {
        MigrateLegacy();
        if (focusPoint) return;
        var go = new GameObject("FocusPoint");
        go.transform.SetParent(transform);
        go.transform.localPosition = defaultFocusOffset;
        focusPoint = go.transform;
    }

    private void MigrateLegacy()
    {
        if (!legacyDialogue) return;
        if (stages == null || stages.Length == 0) stages = new[] { legacyDialogue };
        legacyDialogue = null;
    }

    /// Urutan dialog untuk interaksi ini: semua stage yang belum selesai. Kalau semua selesai:
    /// Repeat Dialogue, atau stage terakhir kalau Repeat Dialogue kosong.
    public List<DialogueData> GetSequence()
    {
        var result = new List<DialogueData>();

        DialogueData last = null;
        foreach (var s in stages ?? System.Array.Empty<DialogueData>())
        {
            if (!s) continue;
            last = s;
            if (!ProgressService.IsDialogueCompleted(s.Id)) result.Add(s);
        }
        if (result.Count == 0)
        {
            if (repeatDialogue) result.Add(repeatDialogue);
            else if (last) result.Add(last);
        }
        return result;
    }

    public void StartDialogue()
    {
        if (!DialogueUI.Instance) return;

        bool started = DialogueUI.Instance.StartDialogue(GetSequence(), focusPoint, () => DialogueEnded?.Invoke());
        if (started) DialogueStarted?.Invoke();
    }
}
