using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Tombol − / + di samping slider: tiap ketukan menggeser nilai slider sebesar Step
/// (negatif untuk −). Nilai diubah lewat slider.value, jadi listener slider yang sudah ada
/// (simpan sensitivitas kamera, ukuran kontrol) tetap terpanggil seperti saat slider digeser.
/// </summary>
[RequireComponent(typeof(Button))]
public sealed class SliderStepButton : MonoBehaviour {
    [SerializeField] private Slider slider;
    [Tooltip("Besar perubahan per ketukan. Negatif = mengurangi.")]
    [SerializeField] private float step = 0.1f;

    public void Init(Slider target, float amount) {
        slider = target;
        step = amount;
    }

    private void Awake() {
        GetComponent<Button>().onClick.AddListener(Step);
    }

    private void Step() {
        if (slider == null || !slider.IsInteractable())
            return;
        // Dibulatkan ke kelipatan step supaya angkanya rapi (1.0x, 1.1x, ...).
        float size = Mathf.Abs(step);
        float next = Mathf.Round((slider.value + step) / size) * size;
        slider.value = Mathf.Clamp(next, slider.minValue, slider.maxValue);
    }
}
