using UnityEngine;
using UnityEngine.Events;

/// Pasang di NPC / kursi / (nanti) player lain. Collider-nya otomatis jadi Trigger = zona aktif.
[RequireComponent(typeof(Collider))]
public class Interactable : MonoBehaviour
{
    [Tooltip("Nama yang tampil di prompt, mis. 'Joseph Mavry' atau 'Kursi'")]
    public string displayName = "Object";
    [Tooltip("Drag asset InteractionAction (Talk / Sit / ...)")]
    public InteractionAction action;

    [Header("World Space Prompt")]
    [Tooltip("Drag PREFAB InteractPrompt dari Project window")]
    [SerializeField] private InteractPromptWorld promptPrefab;
    [Tooltip("Posisi prompt relatif ke objek (dunia), mis. (0, 2, 0) = 2 meter di atas pivot")]
    [SerializeField] private Vector3 promptOffset = new Vector3(0f, 2f, 0f);

    public UnityEvent onInteract;

    private InteractPromptWorld prompt;

    void Reset() => GetComponent<Collider>().isTrigger = true;

    void Start()
    {
        if (!promptPrefab) return;

        // Sengaja TIDAK dijadikan child supaya scale objek tidak ikut mengubah ukuran UI.
        prompt = Instantiate(promptPrefab);
        prompt.Setup(
            transform,
            promptOffset,
            displayName,
            action ? action.verb : "Interact",
            action ? action.icon : null);
    }

    void OnDestroy()
    {
        if (prompt) Destroy(prompt.gameObject);
    }

    // Hanya avatar lokal: pemain lain yang mendekat tidak boleh memunculkan tombol Interact di layar kita.
    static bool IsLocalPlayer(Collider other)
    {
        var pc = other.GetComponentInParent<PlayerController>();
        return pc && pc == PlayerController.Local;
    }

    void OnTriggerEnter(Collider other)
    {
        if (IsLocalPlayer(other) && InteractButtonUI.Instance)
            InteractButtonUI.Instance.Register(this);
    }

    void OnTriggerExit(Collider other)
    {
        if (IsLocalPlayer(other) && InteractButtonUI.Instance)
            InteractButtonUI.Instance.Unregister(this);
    }

    void OnDisable()
    {
        SetPromptVisible(false);
        if (InteractButtonUI.Instance) InteractButtonUI.Instance.Unregister(this);
    }

    public void SetPromptVisible(bool visible)
    {
        if (!prompt) return;
        if (visible) prompt.Show(); else prompt.Hide();
    }

    public void Interact() => onInteract?.Invoke();
}