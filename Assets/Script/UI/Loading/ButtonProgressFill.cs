using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Loading bar di dalam tombol: latar tombol terisi dari kiri ke kanan, dengan label "MEMUAT… 45%" di atasnya.
/// Dipakai tombol MULAI di lobby tunggu selama karakter di-bake dan kerumunan NPC disiapkan.
///
/// Bar tidak pernah mundur dan bergerak halus menuju target (jadi tidak terlihat loncat-loncat).
/// Fill memakai sprite & tipe Image tombol yang sama, lebarnya lewat anchorMax.x, sehingga sudut
/// membulatnya tetap. Fill tidak ikut color tint Button (bukan targetGraphic), jadi tetap terang
/// walau tombol sedang non-interaktif dan latarnya meredup.
/// </summary>
[DisallowMultipleComponent]
public class ButtonProgressFill : MonoBehaviour {
    public const string FillName = "Progress Fill";

    [SerializeField] private RectTransform fill;
    [SerializeField] private Text label;
    [Tooltip("Teks sebelum persen, mis. \"MEMUAT…\" → \"MEMUAT… 45%\".")]
    [SerializeField] private string labelPrefix = "MEMUAT…";
    [Tooltip("Kecepatan maksimal bar mengejar target (bagian bar per detik).")]
    [SerializeField] private float fillSpeed = 1.5f;

    private float target;
    private float shown;
    private bool active;

    /// Bar sudah menyusul target (dipakai untuk menunggu bar penuh sebelum masuk world).
    public bool Settled => !active || shown >= target - 0.001f;

    /// Tampilkan bar dan arahkan ke progress (0..1). Nilai lebih kecil dari target sebelumnya diabaikan.
    public void SetProgress(float progress) {
        if (!active) {
            active = true;
            shown = 0f;
            target = 0f;
            if (fill != null) fill.gameObject.SetActive(true);
        }
        target = Mathf.Max(target, Mathf.Clamp01(progress));
        Apply();
    }

    /// Sembunyikan bar dan reset ke 0. Label dibiarkan; pemanggil yang mengisi teks berikutnya.
    public void Hide() {
        active = false;
        shown = 0f;
        target = 0f;
        if (fill != null) {
            SetFillWidth(0f);
            fill.gameObject.SetActive(false);
        }
    }

    private void Awake() {
        if (!active && fill != null) fill.gameObject.SetActive(false);
    }

    private void Update() {
        if (!active || shown >= target) return;
        shown = Mathf.MoveTowards(shown, target, fillSpeed * Time.unscaledDeltaTime);
        Apply();
    }

    private void Apply() {
        SetFillWidth(shown);
        if (label != null) label.text = $"{labelPrefix} {JoinLoadProgress.Percent(shown)}%";
    }

    private void SetFillWidth(float t) {
        if (fill == null) return;
        fill.anchorMin = Vector2.zero;
        fill.anchorMax = new Vector2(t, 1f);
        fill.offsetMin = Vector2.zero;
        fill.offsetMax = Vector2.zero;
    }

    /// Pasang fill + komponen ini di tombol (layout default & instalasi prefab). Aman dipanggil ulang.
    public static ButtonProgressFill Install(Button button, Text label, Color fillColor) {
        if (button == null) return null;
        var host = button.GetComponent<ButtonProgressFill>();
        if (host == null) host = button.gameObject.AddComponent<ButtonProgressFill>();

        Transform existing = button.transform.Find(FillName);
        RectTransform rect = existing as RectTransform;
        if (rect == null) {
            var go = new GameObject(FillName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.layer = button.gameObject.layer;
            rect = go.GetComponent<RectTransform>();
            rect.SetParent(button.transform, false);
        }
        rect.SetAsFirstSibling(); // di bawah label

        var image = rect.GetComponent<Image>();
        var source = button.targetGraphic as Image;
        if (source != null) {
            image.sprite = source.sprite;
            image.type = source.type;
            image.pixelsPerUnitMultiplier = source.pixelsPerUnitMultiplier;
        }
        image.color = fillColor;
        image.raycastTarget = false;

        host.fill = rect;
        host.label = label;
        host.SetFillWidth(0f);
        rect.gameObject.SetActive(false);
        return host;
    }
}
