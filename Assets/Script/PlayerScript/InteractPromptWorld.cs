using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// Pasang di root prefab "InteractPrompt" (Canvas World Space).
/// Melayang di atas objek, selalu menghadap kamera, tampil hanya saat jadi target terdekat.
[RequireComponent(typeof(CanvasGroup))]
public class InteractPromptWorld : MonoBehaviour
{
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text nameLabel;
    [SerializeField] private TMP_Text verbLabel;

    private CanvasGroup group;
    private Transform cam;
    private Transform follow;
    private Vector3 offset;

    void Awake()
    {
        group = GetComponent<CanvasGroup>();
        Hide();
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

    public void Show() => group.alpha = 1f;
    public void Hide() => group.alpha = 0f;

    void LateUpdate()
    {
        if (follow) transform.position = follow.position + offset;

        if (!cam && Camera.main) cam = Camera.main.transform;
        if (cam) transform.rotation = cam.rotation;   // billboard: sejajar layar kamera
    }
}