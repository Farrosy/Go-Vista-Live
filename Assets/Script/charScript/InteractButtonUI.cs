using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// Pasang di InteractButton. Disembunyikan lewat CanvasGroup (bukan SetActive)
/// supaya Update tetap jalan dan bisa milih target terdekat.
[RequireComponent(typeof(CanvasGroup))]
public class InteractButtonUI : MonoBehaviour
{
    public static InteractButtonUI Instance { get; private set; }

    [Header("Isi dari child InteractButton")]
    [SerializeField] private Image iconImage;      // icon di dalam tombol
    [SerializeField] private TMP_Text nameLabel;   // "Joseph Mavry"
    [SerializeField] private TMP_Text verbLabel;   // "Talk" / "Sit"

    private readonly List<Interactable> inRange = new List<Interactable>();
    private Interactable current;
    private Transform player;
    private CanvasGroup group;

    void Awake()
    {
        Instance = this;
        group = GetComponent<CanvasGroup>();
        SetVisible(false);
    }

    void Start()
    {
        var pc = FindFirstObjectByType<PlayerController>();
        if (pc) player = pc.transform;
    }

    public void Register(Interactable i)
    {
        if (!inRange.Contains(i)) inRange.Add(i);
    }

    public void Unregister(Interactable i) => inRange.Remove(i);

    void Update()
    {
        inRange.RemoveAll(i => i == null);

        Interactable best = null;
        float bestDist = float.MaxValue;
        if (player)
        {
            foreach (var i in inRange)
            {
                float d = (i.transform.position - player.position).sqrMagnitude;
                if (d < bestDist) { bestDist = d; best = i; }
            }
        }

        if (best != current) SetCurrent(best);
    }

    private void SetCurrent(Interactable target)
    {
        current = target;
        SetVisible(target != null);
        if (target == null) return;

        if (nameLabel) nameLabel.text = target.displayName;
        if (verbLabel) verbLabel.text = target.action ? target.action.verb : "Interact";
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