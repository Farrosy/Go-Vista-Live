using System;
using UnityEngine;
using UnityEngine.UI;

/// Saklar on/off bergaya iOS (track + knob) untuk dialog PENGATURAN. Dibangun oleh SettingsPanelRedesign.
/// Mengetuk tombolnya membalik nilai lalu memanggil <see cref="Changed"/>; <see cref="SetWithoutNotify"/> untuk
/// mengisi nilai awal tanpa dianggap mengubah pengaturan.
[RequireComponent(typeof(Button))]
public sealed class SettingsSwitch : MonoBehaviour {
    [SerializeField] private Image track;
    [SerializeField] private RectTransform knob;
    [SerializeField] private Color onColor = new Color32(52, 211, 153, 255);
    [SerializeField] private Color offColor = new Color32(38, 48, 79, 255);
    [Tooltip("Posisi X knob saat mati / nyala (anchoredPosition, pivot kiri).")]
    [SerializeField] private float knobOffX = 6f;
    [SerializeField] private float knobOnX = 46f;

    public event Action<bool> Changed;
    public bool IsOn { get; private set; }

    /// Dipakai pembuat prefab (Editor) untuk mengisi referensi.
    public void Init(Image trackImage, RectTransform knobRect, float offX, float onX) {
        track = trackImage;
        knob = knobRect;
        knobOffX = offX;
        knobOnX = onX;
    }

    private void Awake() => GetComponent<Button>().onClick.AddListener(Toggle);

    private void Toggle() {
        SetWithoutNotify(!IsOn);
        Changed?.Invoke(IsOn);
    }

    public void SetWithoutNotify(bool on) {
        IsOn = on;
        if (track != null) track.color = on ? onColor : offColor;
        if (knob != null) knob.anchoredPosition = new Vector2(on ? knobOnX : knobOffX, knob.anchoredPosition.y);
    }
}
