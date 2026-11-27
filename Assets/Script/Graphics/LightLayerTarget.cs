using UnityEngine;

/// <summary>
/// Menambahkan rendering layer (light layer) ke semua renderer di objek ini dan anak-anaknya, supaya lampu
/// khusus (mis. lampu wajah Johnny) hanya menerangi objek ini dan tidak menerangi pemain yang lewat di depannya.
/// Dijalankan lagi beberapa frame setelah aktif, karena mesh avatar BSMC baru digabung saat runtime.
///
/// Pasangannya: Light dengan Rendering Layers = layer yang sama saja (bukan Default).
/// </summary>
public sealed class LightLayerTarget : MonoBehaviour {
    [Tooltip("Index rendering layer (0 = Default). 1 = \"Light Layer 1\" dipakai lampu wajah NPC.")]
    [SerializeField] private int renderingLayer = 1;

    private const int RetryFrames = 30;
    private int _framesLeft;

    private void OnEnable() {
        Apply();
        _framesLeft = RetryFrames;
    }

    private void LateUpdate() {
        if (_framesLeft <= 0) {
            enabled = false;
            return;
        }
        _framesLeft--;
        if (_framesLeft % 10 == 0) Apply();
    }

    private void Apply() {
        uint bit = 1u << renderingLayer;
        foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
            r.renderingLayerMask |= bit;
    }
}
