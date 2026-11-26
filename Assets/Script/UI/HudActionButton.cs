using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Tombol HUD tambahan di samping tombol Interact (mis. "Gaya Rambut" saat duduk di kursi barber, di sebelah
/// tombol Berdiri). InteractButtonUI hanya punya satu target, jadi aksi kedua butuh tombol sendiri.
///
/// Dibuat saat runtime dari salinan tampilan tombol Interact (sprite, ukuran, skala), tanpa InteractButtonUI dan
/// CustomizableControl. Tiap frame mengikuti posisi tombol Interact (layout bisa diatur pemain di ATUR KONTROL):
/// di kirinya, atau di kanannya kalau tidak muat. Ikut tersembunyi / pudar bersama tombol Interact (dialog, menu,
/// layar mati, tertabrak), jadi tidak perlu mendaftar ke semua sistem yang menyembunyikan HUD.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public sealed class HudActionButton : MonoBehaviour {
    private const float Gap = 28f;
    private const float EdgeMargin = 16f;

    private RectTransform rect;
    private CanvasGroup group;
    private RectTransform interactRect;
    private CanvasGroup interactGroup;
    private Action onClick;
    private bool shown;

    /// <summary>Buat tombol di canvas yang sama dengan tombol Interact. null kalau HUD belum ada.</summary>
    public static HudActionButton Create(string name, Sprite icon, string label, Action onClick) {
        InteractButtonUI interact = InteractButtonUI.Instance;
        if (!interact) return null;
        // Disalin di bawah parent nonaktif: Awake InteractButtonUI salinan (yang mengambil alih Instance) dan
        // CustomizableControl (yang memuat posisi tersimpan tombol Interact) tidak pernah berjalan.
        var holder = new GameObject("__HudButtonHolder");
        holder.SetActive(false);
        GameObject clone = Instantiate(interact.gameObject, holder.transform, false);
        clone.name = name;
        foreach (InteractButtonUI part in clone.GetComponents<InteractButtonUI>()) DestroyImmediate(part);
        foreach (CustomizableControl part in clone.GetComponents<CustomizableControl>()) DestroyImmediate(part);

        Button button = clone.GetComponent<Button>();
        if (button) button.onClick = new Button.ButtonClickedEvent(); // OnClick() bawaan prefab = InteractButtonUI
        foreach (Image image in clone.GetComponentsInChildren<Image>(true)) {
            if (image.gameObject == clone) continue;
            image.sprite = icon;
            image.preserveAspect = true;
            image.gameObject.SetActive(true);
        }

        var self = clone.AddComponent<HudActionButton>();
        self.interactRect = (RectTransform)interact.transform;
        self.interactGroup = interact.GetComponent<CanvasGroup>();
        self.onClick = onClick;
        if (button) button.onClick.AddListener(self.Click);
        if (!string.IsNullOrEmpty(label)) AddLabel(clone.transform, label);

        clone.transform.SetParent(interact.transform.parent, false);
        clone.transform.SetSiblingIndex(interact.transform.GetSiblingIndex());
        Destroy(holder);
        self.Follow();
        self.ApplyVisibility();
        return self;
    }

    /// Nama aksi di bawah tombol (tombol Interact tidak punya label; prompt dunia kursi dihapus karena terpotong kamera).
    private static void AddLabel(Transform button, string label) {
        var assets = new ChatUiAssets();
        assets.ResolveFallbacks();
        RectTransform pill = ChatUiKit.CreateRect("Label", button);
        pill.anchorMin = pill.anchorMax = new Vector2(0.5f, 0f);
        pill.pivot = new Vector2(0.5f, 1f);
        pill.anchoredPosition = new Vector2(0f, -6f);
        pill.sizeDelta = new Vector2(230f, 46f);
        Image background = ChatUiKit.AddShape(pill.gameObject, assets.rounded, new Color32(0x0D, 0x12, 0x22, 0xCC), 2f);
        background.raycastTarget = false;
        TextMeshProUGUI text = ChatUiKit.CreateText("Text", pill, assets.font, label, 28, Color.white, FontStyles.Bold,
                                                    TextAlignmentOptions.Center);
        ChatUiKit.Stretch(text.rectTransform, 0f, 0f, 0f, 0f);
        text.raycastTarget = false;
    }

    void Awake() {
        rect = (RectTransform)transform;
        group = GetComponent<CanvasGroup>();
    }

    /// <summary>Tampilkan / sembunyikan (tetap ikut sembunyi kalau tombol Interact tersembunyi).</summary>
    public bool Shown {
        get => shown;
        set {
            shown = value;
            ApplyVisibility();
        }
    }

    void LateUpdate() {
        if (!interactRect) {
            Destroy(gameObject); // HUD lama hilang (pindah scene)
            return;
        }
        Follow();
        ApplyVisibility();
    }

    private void Click() {
        if (shown && group.interactable) onClick?.Invoke();
    }

    private void ApplyVisibility() {
        if (!group) return;
        bool interactShown = !interactGroup || interactGroup.alpha > 0f;
        bool visible = shown && interactShown;
        group.alpha = visible ? (interactGroup ? interactGroup.alpha : 1f) : 0f;
        bool usable = visible && (!interactGroup || interactGroup.interactable);
        group.interactable = usable;
        group.blocksRaycasts = usable;
    }

    /// Sama ukuran/skala dengan tombol Interact, di kirinya (atau kanannya kalau keluar layar).
    private void Follow() {
        if (!rect || !interactRect) return;
        rect.anchorMin = interactRect.anchorMin;
        rect.anchorMax = interactRect.anchorMax;
        rect.pivot = interactRect.pivot;
        rect.sizeDelta = interactRect.sizeDelta;
        rect.localScale = interactRect.localScale;
        float width = interactRect.rect.width * interactRect.localScale.x;
        Vector3 local = interactRect.localPosition;
        Vector3 left = local + Vector3.left * (width + Gap);
        var parent = rect.parent as RectTransform;
        bool fits = !parent || left.x - width * interactRect.pivot.x >= parent.rect.xMin + EdgeMargin;
        rect.localPosition = fits ? left : local + Vector3.right * (width + Gap);
    }
}
