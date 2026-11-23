using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Tutorial dealer sekali saja, di atas CarDealerPanel. Tiap langkah menyorot satu bagian panel (sisanya digelapkan)
/// dan Johnny menjelaskan lewat kotak teks. Ketuk di mana saja untuk lanjut. Selama tutorial seluruh layar ditutup
/// penahan sentuhan, jadi tombol asli panel tidak bisa ditekan; tombol Buy/Spawn hanya diperagakan tampilannya.
/// Dibangun bersama panel oleh Go Vista > Dealer Mobil > 2. Bangun Panel Dealer.
/// </summary>
public sealed class CarDealerTutorial : MonoBehaviour, IPointerClickHandler {
    private enum Target { Card, Preview, Speed, Price, Action }

    private struct Step {
        public Target target;
        public DealerButtonState? button;
        public string text;

        public Step(Target target, string text, DealerButtonState? button = null) {
            this.target = target;
            this.text = text;
            this.button = button;
        }
    }

    private static readonly Step[] Steps = {
        new Step(Target.Card, "Ini daftar mobil yang dijual di dealer saya. Ketuk kartu mobil untuk memilihnya."),
        new Step(Target.Preview, "Mobil yang kamu pilih tampil di sini, lengkap dengan namanya. Lihat dari semua sisi!"),
        new Step(Target.Speed, "Bar ini menunjukkan kecepatan mobil. Makin penuh, makin ngebut!"),
        new Step(Target.Price, "Ini harganya. Kalau gold kamu kurang, tombol Buy jadi abu-abu dan belum bisa ditekan.",
                 DealerButtonState.BuyDisabled),
        new Step(Target.Action, "Kalau gold kamu cukup, tekan Buy. Tombolnya berubah jadi Bought! tandanya mobil sudah jadi milikmu.",
                 DealerButtonState.Buy),
        new Step(Target.Action, "Mobil yang sudah dibeli bisa kamu Spawn kapan saja. Mobilmu akan muncul di parkiran depan dealer ini.",
                 DealerButtonState.Spawn),
        new Step(Target.Action, "Sekarang giliran kamu. Silakan pilih mobil dan coba sendiri!"),
    };

    [SerializeField] private RectTransform overlay;
    [Tooltip("Empat potong gelap di sekitar bagian yang disorot: atas, bawah, kiri, kanan.")]
    [SerializeField] private RectTransform dimTop, dimBottom, dimLeft, dimRight;
    [SerializeField] private RectTransform highlight;
    [SerializeField] private RectTransform caption;
    [SerializeField] private TMP_Text captionText;
    [SerializeField] private TMP_Text hintText;
    [Tooltip("Jarak sorotan dari tepi bagian yang disorot.")]
    [SerializeField] private float highlightPadding = 10f;
    [SerializeField] private float captionGap = 24f;

    private CarDealerPanel _panel;
    private Action _finished;
    private int _step = -1;

    public bool IsPlaying => _step >= 0;

    public void Play(CarDealerPanel panel, Action finished) {
        _panel = panel;
        _finished = finished;
        _step = 0;
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        // Kartu hasil klon dan skala ResponsivePanel harus sudah final sebelum posisinya dibaca.
        Canvas.ForceUpdateCanvases();
        ShowStep();
    }

    /// <summary>Hentikan tanpa menandai selesai (panel ditutup di tengah tutorial = diulang lain kali).</summary>
    public void Stop() {
        _step = -1;
        _finished = null;
        gameObject.SetActive(false);
    }

    public void OnPointerClick(PointerEventData eventData) {
        if (_step < 0) return;
        _step++;
        if (_step >= Steps.Length) {
            Action done = _finished;
            Stop();
            if (_panel != null) _panel.RestoreButton();
            done?.Invoke();
            return;
        }
        ShowStep();
    }

    private void LateUpdate() {
        // Posisi diperbarui tiap frame: layar diputar / ResponsivePanel mengubah skala setelah langkah tampil.
        if (_step >= 0) Layout();
    }

    private void ShowStep() {
        Step step = Steps[_step];
        if (captionText != null) captionText.text = step.text;
        if (hintText != null) hintText.text = _step == Steps.Length - 1 ? "Ketuk untuk mulai" : "Ketuk untuk lanjut";
        if (_panel != null) {
            if (step.button.HasValue) _panel.ApplyButton(step.button.Value);
            else _panel.RestoreButton();
        }
        Layout();
    }

    private RectTransform TargetRect(Target target) {
        if (_panel == null) return null;
        return target switch {
            Target.Card => _panel.FirstCard,
            Target.Preview => _panel.PreviewArea,
            Target.Speed => _panel.SpeedArea,
            Target.Price => _panel.PriceArea,
            _ => _panel.ActionArea,
        };
    }

    private void Layout() {
        RectTransform target = TargetRect(Steps[_step].target);
        Rect area = overlay.rect;
        Rect hole;
        if (target != null) {
            var corners = new Vector3[4];
            target.GetWorldCorners(corners);
            Vector2 min = overlay.InverseTransformPoint(corners[0]);
            Vector2 max = overlay.InverseTransformPoint(corners[2]);
            hole = Rect.MinMaxRect(min.x - highlightPadding, min.y - highlightPadding,
                                   max.x + highlightPadding, max.y + highlightPadding);
        } else {
            hole = new Rect(area.center, Vector2.zero);
        }

        // Gelap di luar lubang. Koordinat lokal overlay (pivot tengah).
        Place(dimTop, new Rect(area.xMin, hole.yMax, area.width, area.yMax - hole.yMax));
        Place(dimBottom, new Rect(area.xMin, area.yMin, area.width, hole.yMin - area.yMin));
        Place(dimLeft, new Rect(area.xMin, hole.yMin, hole.xMin - area.xMin, hole.height));
        Place(dimRight, new Rect(hole.xMax, hole.yMin, area.xMax - hole.xMax, hole.height));
        if (highlight != null) {
            highlight.gameObject.SetActive(target != null);
            Place(highlight, hole);
        }
        PlaceCaption(hole, area);
    }

    /// <summary>Kotak teks di samping bagian yang disorot, di sisi yang ruangnya paling lega, tetap di dalam layar.</summary>
    private void PlaceCaption(Rect hole, Rect area) {
        if (caption == null) return;
        Vector2 size = caption.rect.size;
        float spaceLeft = hole.xMin - area.xMin;
        float spaceRight = area.xMax - hole.xMax;
        float spaceBelow = hole.yMin - area.yMin;
        float spaceAbove = area.yMax - hole.yMax;

        Vector2 center;
        if (spaceLeft >= size.x + captionGap * 2f || spaceRight >= size.x + captionGap * 2f) {
            float x = spaceLeft >= spaceRight ? hole.xMin - captionGap - size.x * 0.5f
                                              : hole.xMax + captionGap + size.x * 0.5f;
            center = new Vector2(x, hole.center.y);
        } else {
            float y = spaceBelow >= spaceAbove ? hole.yMin - captionGap - size.y * 0.5f
                                               : hole.yMax + captionGap + size.y * 0.5f;
            center = new Vector2(hole.center.x, y);
        }
        float margin = 16f;
        center.x = Mathf.Clamp(center.x, area.xMin + size.x * 0.5f + margin, area.xMax - size.x * 0.5f - margin);
        center.y = Mathf.Clamp(center.y, area.yMin + size.y * 0.5f + margin, area.yMax - size.y * 0.5f - margin);
        caption.anchorMin = caption.anchorMax = caption.pivot = new Vector2(0.5f, 0.5f);
        caption.anchoredPosition = center;
    }

    private static void Place(RectTransform rect, Rect r) {
        if (rect == null) return;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = r.center;
        rect.sizeDelta = new Vector2(Mathf.Max(0f, r.width), Mathf.Max(0f, r.height));
    }
}
