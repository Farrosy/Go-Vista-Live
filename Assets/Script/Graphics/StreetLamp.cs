using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Satu tiang lampu kota. DayNightCycle mengatur kecerahannya (0 siang .. 1 malam) dan mematikan
/// Light-nya kalau jauh dari kamera supaya HP tidak berat. Bohlam ditukar ke material malam yang lebih
/// terang dan hangat, jadi kena bloom (efek glow) dari Global Volume.
/// Dipasang oleh menu Go Vista > Kota > Pasang Lampu Jalan (StreetLightsBuilder).
/// </summary>
[DisallowMultipleComponent]
public sealed class StreetLamp : MonoBehaviour {
    private static readonly List<StreetLamp> ActiveLamps = new List<StreetLamp>();
    public static IReadOnlyList<StreetLamp> All => ActiveLamps;

    // Warna bohlam saat menyala (HDR, > 1 supaya melewati threshold bloom 0.95).
    private static readonly Color NightEmission = new Color(1f, 0.74f, 0.42f) * 7f;
    private static readonly Color NightBase = new Color(1f, 0.9f, 0.75f);
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    // Material bohlam malam dibagi semua lampu (per material asli), supaya batching tetap jalan.
    private static readonly Dictionary<Material, Material> NightMaterials = new Dictionary<Material, Material>();

    [SerializeField] private Light lampLight;
    [SerializeField] private Renderer bulbRenderer;
    [Tooltip("Slot material bohlam di renderer (lamppost_001 dan lamp_post_001: slot 1 = Emissive).")]
    [SerializeField] private int bulbMaterialIndex = 1;
    [SerializeField] private float onIntensity = 60f;

    private float _level = -1f;
    private bool _culled;
    private bool _bulbNight;
    private Material _dayBulb;

    public Vector3 LightPosition => lampLight != null ? lampLight.transform.position : transform.position;

    public void Configure(Light light, Renderer bulb, int materialIndex, float intensity) {
        lampLight = light;
        bulbRenderer = bulb;
        bulbMaterialIndex = materialIndex;
        onIntensity = intensity;
    }

    private void OnEnable() {
        ActiveLamps.Add(this);
        if (lampLight != null) lampLight.enabled = false; // DayNightCycle yang menyalakan
    }

    private void OnDisable() {
        ActiveLamps.Remove(this);
        SetBulbNight(false);
        _level = -1f;
    }

    /// <summary>0 = mati (siang), 1 = menyala penuh (malam).</summary>
    public void SetLevel(float level) {
        if (Mathf.Abs(level - _level) < 0.005f) return;
        _level = level;
        Apply();
    }

    /// <summary>true = terlalu jauh dari kamera; Light dimatikan tapi bohlam tetap menyala.</summary>
    public void SetCulled(bool culled) {
        if (culled == _culled) return;
        _culled = culled;
        Apply();
    }

    private void Apply() {
        float level = Mathf.Max(0f, _level);
        if (lampLight != null) {
            bool on = level > 0.01f && !_culled;
            lampLight.enabled = on;
            if (on) lampLight.intensity = onIntensity * level;
        }
        SetBulbNight(level > 0.4f);
    }

    private void SetBulbNight(bool night) {
        if (night == _bulbNight || bulbRenderer == null) return;
        Material[] mats = bulbRenderer.sharedMaterials;
        if (bulbMaterialIndex < 0 || bulbMaterialIndex >= mats.Length) return;
        if (night) {
            _dayBulb = mats[bulbMaterialIndex];
            if (_dayBulb == null) return;
            mats[bulbMaterialIndex] = NightMaterialFor(_dayBulb);
        } else {
            if (_dayBulb == null) return;
            mats[bulbMaterialIndex] = _dayBulb;
        }
        bulbRenderer.sharedMaterials = mats;
        _bulbNight = night;
    }

    private static Material NightMaterialFor(Material day) {
        if (NightMaterials.TryGetValue(day, out Material night) && night != null) return night;
        night = new Material(day) { name = day.name + " (Night)" };
        night.EnableKeyword("_EMISSION");
        night.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        if (night.HasProperty(EmissionColorId)) night.SetColor(EmissionColorId, NightEmission);
        if (night.HasProperty(BaseColorId)) night.SetColor(BaseColorId, NightBase);
        NightMaterials[day] = night;
        return night;
    }
}
