using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// Pasang di root prefab "InteractPrompt" (Canvas World Space).
/// Melayang di atas objek, selalu menghadap kamera, tampil hanya saat jadi target terdekat.
/// Muncul/hilang dengan fade + sedikit membesar, dan selalu tergambar di atas bangunan
/// (lihat WorldUiOnTop) supaya tidak tertutup dinding di sekitar pintu.
[RequireComponent(typeof(CanvasGroup))]
public class InteractPromptWorld : MonoBehaviour
{
    private const float HiddenScale = 0.85f;

    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text nameLabel;
    [SerializeField] private TMP_Text verbLabel;

    [Header("Animasi")]
    [Tooltip("Kecepatan fade (1 = satu detik penuh).")]
    [SerializeField] private float fadeSpeed = 8f;
    [Tooltip("Gambar di atas semua geometri (tidak tertutup dinding).")]
    [SerializeField] private bool alwaysOnTop = true;

    private CanvasGroup group;
    private Transform cam;
    private Transform follow;
    private Vector3 offset;
    private Vector3 baseScale;
    private float visibility;
    private float targetVisibility;

    void Awake()
    {
        group = GetComponent<CanvasGroup>();
        baseScale = transform.localScale;
        if (alwaysOnTop) WorldUiOnTop.Apply(gameObject);
        visibility = targetVisibility = 0f;
        ApplyVisibility();
    }

    public void Setup(Transform followTarget, Vector3 worldOffset, string objectName, string verb, Sprite icon)
    {
        follow = followTarget;
        offset = worldOffset;
        if (nameLabel) nameLabel.text = objectName;
        if (verbLabel) verbLabel.text = verb;
        if (iconImage)
        {
            iconImage.sprite = icon;
            iconImage.enabled = icon != null;
        }
    }

    public void Show() => targetVisibility = 1f;
    public void Hide() => targetVisibility = 0f;

    void LateUpdate()
    {
        if (follow) transform.position = follow.position + offset;

        if (!cam && Camera.main) cam = Camera.main.transform;
        if (cam) transform.rotation = cam.rotation;   // billboard: sejajar layar kamera

        if (!Mathf.Approximately(visibility, targetVisibility))
        {
            visibility = Mathf.MoveTowards(visibility, targetVisibility, Time.unscaledDeltaTime * fadeSpeed);
            ApplyVisibility();
        }
    }

    private void ApplyVisibility()
    {
        if (group) group.alpha = visibility;
        float eased = 1f - (1f - visibility) * (1f - visibility);   // ease-out
        transform.localScale = baseScale * Mathf.Lerp(HiddenScale, 1f, eased);
    }
}
