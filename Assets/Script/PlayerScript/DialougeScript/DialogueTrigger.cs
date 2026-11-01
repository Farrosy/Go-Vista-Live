using UnityEngine;

/// Pasang di root NPC. Hubungkan ke Interactable > On Interact: drag NPC ini, pilih DialogueTrigger > StartDialogue.
public class DialogueTrigger : MonoBehaviour
{
    [SerializeField] private DialogueData dialogue;
    [Tooltip("Titik yang difokuskan kamera (kepala NPC). Kosong = dibuat otomatis di atas pivot")]
    [SerializeField] private Transform focusPoint;
    [SerializeField] private Vector3 defaultFocusOffset = new Vector3(0f, 1.6f, 0f);

    /// Dipakai NpcController untuk animasi dan arah hadap.
    public event System.Action DialogueStarted;
    public event System.Action DialogueEnded;

    void Awake()
    {
        if (focusPoint) return;
        var go = new GameObject("FocusPoint");
        go.transform.SetParent(transform);
        go.transform.localPosition = defaultFocusOffset;
        focusPoint = go.transform;
    }

    public void StartDialogue()
    {
        if (!DialogueUI.Instance) return;

        bool started = DialogueUI.Instance.StartDialogue(dialogue, focusPoint, () => DialogueEnded?.Invoke());
        if (started) DialogueStarted?.Invoke();
    }
}