using UnityEngine;

/// Lingkaran di tanah dan beacon cahaya untuk tujuan quest. Lingkaran digambar dengan LineRenderer
/// (radius sama dengan area trigger), beacon berdenyut pelan supaya mudah terlihat dari jauh.
/// Objek Ring diputar 90° di sumbu X dan memakai alignment TransformZ, jadi garisnya rebah di tanah:
/// lingkaran digambar di bidang XY lokal, dan sumbu Z lokal mengarah ke bawah.
public sealed class TourMarkerVisual : MonoBehaviour {
    private const int Segments = 64;
    private const float GroundOffset = 0.08f;

    [SerializeField] private LineRenderer ring;
    [SerializeField] private float radius = 4f;
    [SerializeField] private Renderer beacon;
    [SerializeField] private float pulseSpeed = 1.6f;
    [SerializeField, Range(0f, 1f)] private float pulseMin = 0.45f;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private MaterialPropertyBlock _block;
    private Color _beaconColor;
    private Color _ringColor;

    private void Awake() {
        _block = new MaterialPropertyBlock();
        if (beacon != null) _beaconColor = beacon.sharedMaterial.GetColor(BaseColorId);
        if (ring != null) _ringColor = ring.startColor;
        DrawRing();
    }

    private void OnValidate() => DrawRing();

    private void DrawRing() {
        if (ring == null) return;
        ring.useWorldSpace = false;
        ring.alignment = LineAlignment.TransformZ;
        ring.loop = true;
        ring.positionCount = Segments;
        for (int i = 0; i < Segments; i++) {
            float angle = i * Mathf.PI * 2f / Segments;
            ring.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, -GroundOffset));
        }
    }

    private void Update() {
        float pulse = Mathf.Lerp(pulseMin, 1f, 0.5f + 0.5f * Mathf.Sin(Time.time * pulseSpeed * Mathf.PI));
        if (beacon != null) {
            Color color = _beaconColor;
            color.a *= pulse;
            beacon.GetPropertyBlock(_block);
            _block.SetColor(BaseColorId, color);
            beacon.SetPropertyBlock(_block);
        }
        if (ring != null) {
            Color color = _ringColor;
            color.a *= Mathf.Lerp(0.7f, 1f, pulse);
            ring.startColor = ring.endColor = color;
        }
    }
}
