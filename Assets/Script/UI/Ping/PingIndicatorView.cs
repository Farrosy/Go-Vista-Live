using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// Chip ping di HUD: ikon WiFi (titik + 3 busur) + teks "45 ms".
/// Sprite WiFi berwarna putih (Assets/GoVista_HUD_Assets/Ping/icon_wifi_1..4); warnanya diatur dari sini.
/// Nilainya diisi PingIndicator (sekitar 1x per detik), bukan tiap frame.
public sealed class PingIndicatorView : MonoBehaviour {
    /// Batas ping untuk mobile. PingIndicator memakai BadMs juga untuk peringatan koneksi buruk.
    public const int GoodMs = 80;   // <= ini: hijau, 4 bar
    public const int OkMs = 150;    // <= ini: kuning, 3 bar
    public const int BadMs = 250;   // <= ini: kuning, 2 bar; di atasnya merah, 1 bar

    private static readonly Color Green = new Color32(80, 220, 110, 255);
    private static readonly Color Yellow = new Color32(255, 205, 60, 255);
    private static readonly Color Red = new Color32(240, 70, 70, 255);
    // Bar mati: abu kebiruan, bukan sekadar alpha rendah, supaya tetap terlihat di latar terang.
    private static readonly Color Off = new Color32(150, 160, 190, 110);
    private static readonly Color Disconnected = new Color32(170, 178, 200, 255);

    [Tooltip("icon_wifi_1..4: titik, busur kecil, tengah, besar.")]
    [SerializeField] private Image[] bars = new Image[4];
    [SerializeField] private TextMeshProUGUI label;

    private const int DisconnectedValue = int.MinValue;
    private int _lastMs = int.MaxValue;

    /// Tampilkan ping dalam milidetik.
    public void SetPing(int ms) {
        ms = Mathf.Max(0, ms);
        if (ms == _lastMs) return;   // hindari alokasi string berulang
        _lastMs = ms;

        (int lit, Color color) = Classify(ms);
        SetBars(lit, color);
        label.SetText("{0} ms", ms);
        label.color = color;
    }

    /// Belum/tidak terhubung ke room: semua bar mati dan teks "—".
    public void SetDisconnected() {
        if (_lastMs == DisconnectedValue) return;
        _lastMs = DisconnectedValue;
        SetBars(0, Off);
        label.SetText("—");
        label.color = Disconnected;
    }

    private static (int lit, Color color) Classify(int ms) {
        if (ms <= GoodMs) return (4, Green);
        if (ms <= OkMs) return (3, Yellow);
        if (ms <= BadMs) return (2, Yellow);
        return (1, Red);
    }

    private void SetBars(int lit, Color color) {
        for (int i = 0; i < bars.Length; i++)
            if (bars[i] != null) bars[i].color = i < lit ? color : Off;
    }
}
