using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Menyalakan/mematikan post-processing kamera sesuai GameSettings.GraphicsEffects (PENGATURAN > Efek grafik).
/// Dipasang di Main Camera MainMultiplayerScene. Tampilan kota (tonemapping, warna, bloom, vignette) ada di
/// Global Volume WorldScene; tanpa post-processing kamera, Volume itu tidak terlihat dan render lebih ringan.
/// </summary>
[RequireComponent(typeof(Camera))]
public sealed class CameraGraphicsEffects : MonoBehaviour {
    private UniversalAdditionalCameraData _cameraData;

    private void Awake() {
        _cameraData = GetComponent<Camera>().GetUniversalAdditionalCameraData();
    }

    private void OnEnable() {
        GameSettings.GraphicsEffectsChanged += Apply;
        Apply();
    }

    private void OnDisable() {
        GameSettings.GraphicsEffectsChanged -= Apply;
    }

    private void Apply() {
        if (_cameraData != null)
            _cameraData.renderPostProcessing = GameSettings.GraphicsEffects;
    }
}
