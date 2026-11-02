using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Lingkaran putus-putus di sekitar setiap tombol mobile selama mode ATUR KONTROL, supaya pemain tahu
/// tombol mana saja yang bisa digeser. Tombol yang sedang dipilih tidak diberi lingkaran (sudah ada
/// bingkai pilihan).
///
/// Pasang di objek "Control Layout Editor" (dilakukan Go Vista > Desain Baru > Atur Kontrol).
/// Lingkaran dibuat saat runtime di dalam "Container", jadi tidak menyentuh tombolnya sendiri.
/// </summary>
public sealed class ControlEditRings : MonoBehaviour {
    [Tooltip("Tempat lingkaran dibuat (child full-screen, di bawah toolbar).")]
    [SerializeField] private RectTransform container;
    [SerializeField] private RectTransform selectionFrame;
    [SerializeField] private Sprite ringSprite;
    [SerializeField] private Color ringColor = new Color(1f, 1f, 1f, 0.35f);
    [Tooltip("Jarak lingkaran dari tepi tombol (unit canvas).")]
    [SerializeField] private float padding = 14f;

    private readonly List<CustomizableControl> _controls = new List<CustomizableControl>();
    private readonly List<Image> _rings = new List<Image>();
    private readonly Vector3[] _corners = new Vector3[4];

    public void Init(RectTransform ringContainer, RectTransform frame, Sprite sprite) {
        container = ringContainer;
        selectionFrame = frame;
        ringSprite = sprite;
    }

    private void Awake() {
        if (ringSprite == null)
            ringSprite = Resources.Load<Sprite>("UI/ui-dashed-ring");
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null)
            canvas.rootCanvas.GetComponentsInChildren(true, _controls);
    }

    private void LateUpdate() {
        if (container == null)
            return;
        while (_rings.Count < _controls.Count)
            _rings.Add(CreateRing(_rings.Count));

        Vector2 frameCenter = Vector2.zero;
        bool frameShown = selectionFrame != null && selectionFrame.gameObject.activeInHierarchy;
        if (frameShown)
            frameCenter = container.InverseTransformPoint(selectionFrame.position);

        for (int i = 0; i < _controls.Count; i++) {
            CustomizableControl control = _controls[i];
            Image ring = _rings[i];
            bool show = control != null && control.gameObject.activeInHierarchy;
            if (show) {
                control.Rect.GetWorldCorners(_corners);
                Vector2 min = container.InverseTransformPoint(_corners[0]);
                Vector2 max = container.InverseTransformPoint(_corners[2]);
                Vector2 center = (min + max) * 0.5f;
                show = !frameShown || (center - frameCenter).sqrMagnitude > 4f;
                float size = Mathf.Max(max.x - min.x, max.y - min.y) + padding * 2f;
                ring.rectTransform.localPosition = center;
                ring.rectTransform.sizeDelta = new Vector2(size, size);
            }
            if (ring.gameObject.activeSelf != show)
                ring.gameObject.SetActive(show);
        }
    }

    private Image CreateRing(int index) {
        var go = new GameObject("Edit Ring " + index, typeof(RectTransform), typeof(Image));
        go.layer = container.gameObject.layer;
        var rect = (RectTransform)go.transform;
        rect.SetParent(container, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        Image image = go.GetComponent<Image>();
        image.sprite = ringSprite;
        image.color = ringColor;
        image.preserveAspect = true;
        image.raycastTarget = false;
        return image;
    }
}
