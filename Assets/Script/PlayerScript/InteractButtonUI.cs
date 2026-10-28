using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// Pasang di InteractButton (HUD). Tombol tap-nya saja: icon berubah sesuai aksi target.
/// Nama + aksi ditampilkan oleh prompt world-space di Interactable.
/// Disembunyikan lewat CanvasGroup (bukan SetActive) supaya Update tetap jalan.
[RequireComponent(typeof(CanvasGroup))]
public class InteractButtonUI : MonoBehaviour
{
    public static InteractButtonUI Instance { get; private set; }

    [SerializeField] private Image iconImage;   // icon di dalam tombol

    private readonly List<Interactable> inRange = new List<Interactable>();
    private Interactable current;
    private Transform player;
    private CanvasGroup group;
    private bool suppressed;

    void Awake()
    {
        Instance = this;
        group = GetComponent<CanvasGroup>();
        SetVisible(false);
    }

    void Start()
    {
        var pc = FindAnyObjectByType<PlayerController>();
        if (pc) player = pc.transform;
    }

    public void Register(Interactable i)
    {
        if (!inRange.Contains(i)) inRange.Add(i);
    }

    public void Unregister(Interactable i)
    {
        inRange.Remove(i);
        if (current == i) SetCurrent(null);
    }

    void Update()
    {
        inRange.RemoveAll(i => i == null);

        Interactable best = null;
        float bestDist = float.MaxValue;
        if (player && !suppressed)
        {
            foreach (var i in inRange)
            {
                float d = (i.transform.position - player.position).sqrMagnitude;
                if (d < bestDist) { bestDist = d; best = i; }
            }
        }

        if (DialogueUI.Instance != null && DialogueUI.Instance.IsActive) return;  // sembunyikan saat dialog
        if (best != current) SetCurrent(best);
    }

    /// Dipakai dialog: sembunyikan prompt + tombol selama dialog berjalan.
    public void SetSuppressed(bool value)
    {
        suppressed = value;
        if (value) SetCurrent(null);
    }

    private void SetCurrent(Interactable target)
    {
        if (current) current.SetPromptVisible(false);
        current = target;
        SetVisible(target != null);
        if (target == null) return;

        target.SetPromptVisible(true);
        if (iconImage && target.action) iconImage.sprite = target.action.icon;
    }

    private void SetVisible(bool v)
    {
        group.alpha = v ? 1f : 0f;
        group.interactable = v;
        group.blocksRaycasts = v;
    }

    /// Hubungkan ke Button > OnClick()
    public void OnClick() => current?.Interact();
}