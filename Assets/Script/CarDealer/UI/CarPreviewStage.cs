using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Preview 3D mobil yang berputar di panel dealer. Model tampilan (CarCatalog.Entry.previewModel, tanpa fisika dan
/// jaringan) ditaruh jauh di bawah kota, difoto kamera khusus ke RenderTexture, lalu ditampilkan di RawImage.
/// Kamera dan model hanya ada selama panel terbuka.
/// </summary>
public sealed class CarPreviewStage : MonoBehaviour {
    // Jauh di bawah map supaya kamera preview tidak melihat kota dan kamera utama tidak melihat model ini.
    private static readonly Vector3 StagePosition = new Vector3(0f, -2000f, 0f);

    [SerializeField] private RawImage target;
    [SerializeField] private Vector2Int resolution = new Vector2Int(960, 520);
    [Tooltip("Derajat per detik.")]
    [SerializeField] private float spinSpeed = 35f;
    [SerializeField] private float fieldOfView = 26f;
    [Tooltip("Sudut kamera dari atas (derajat).")]
    [SerializeField] private float pitch = 14f;
    [Tooltip("Jarak tambahan supaya mobil tidak terpotong saat berputar.")]
    [SerializeField] private float padding = 1.08f;

    private RenderTexture _texture;
    private Camera _camera;
    private Light _light;
    private Transform _pivot;
    private GameObject _model;
    private GameObject _shownSource;

    public void Show(GameObject previewModel) {
        if (previewModel == _shownSource && _model != null) return;
        Clear();
        _shownSource = previewModel;
        if (previewModel == null || target == null) {
            if (target != null) target.enabled = false;
            return;
        }
        EnsureRig();
        _model = Instantiate(previewModel, _pivot);
        _model.transform.localPosition = Vector3.zero;
        _model.transform.localRotation = Quaternion.identity;
        Frame();
        target.texture = _texture;
        target.enabled = true;
        _camera.enabled = true;
        _light.enabled = true;
    }

    public void Hide() {
        Clear();
        if (_camera != null) _camera.enabled = false;
        if (_light != null) _light.enabled = false;
        if (target != null) target.enabled = false;
    }

    private void Clear() {
        if (_model != null) Destroy(_model);
        _model = null;
        _shownSource = null;
    }

    private void EnsureRig() {
        if (_texture == null) {
            _texture = new RenderTexture(resolution.x, resolution.y, 24, RenderTextureFormat.ARGB32) {
                name = "CarPreviewRT", antiAliasing = 4,
            };
        }
        if (_pivot == null) {
            _pivot = new GameObject("CarPreviewPivot").transform;
            _pivot.position = StagePosition;
            DontDestroyOnLoad(_pivot.gameObject);
        }
        if (_camera == null) {
            var go = new GameObject("CarPreviewCamera");
            DontDestroyOnLoad(go);
            _camera = go.AddComponent<Camera>();
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            _camera.fieldOfView = fieldOfView;
            _camera.nearClipPlane = 0.05f;
            _camera.farClipPlane = 60f;
            _camera.targetTexture = _texture;
            _camera.depth = -50;
            _camera.allowHDR = false;
            _camera.allowMSAA = true;
        }
        if (_light == null) {
            // Cahaya sendiri supaya mobil tetap terang walau ruangan/scene tidak punya directional light.
            var go = new GameObject("CarPreviewLight");
            DontDestroyOnLoad(go);
            _light = go.AddComponent<Light>();
            _light.type = LightType.Point;
            _light.range = 40f;
            _light.intensity = 3f;
            _light.shadows = LightShadows.None;
        }
    }

    /// <summary>Atur jarak kamera supaya seluruh mobil (dari sudut mana pun saat berputar) masuk frame.</summary>
    private void Frame() {
        _pivot.rotation = Quaternion.Euler(0f, 210f, 0f);
        Bounds bounds = new Bounds(_pivot.position, Vector3.one);
        bool any = false;
        foreach (Renderer r in _model.GetComponentsInChildren<Renderer>()) {
            if (!any) { bounds = r.bounds; any = true; } else bounds.Encapsulate(r.bounds);
        }
        // Pusatkan model di pivot supaya berputar di tempat.
        _model.transform.position += _pivot.position - new Vector3(bounds.center.x, bounds.center.y, bounds.center.z);
        float radius = Mathf.Max(0.5f, bounds.extents.magnitude) * padding;

        float verticalHalf = fieldOfView * 0.5f * Mathf.Deg2Rad;
        float horizontalHalf = Mathf.Atan(Mathf.Tan(verticalHalf) * resolution.x / resolution.y);
        float distance = radius / Mathf.Sin(Mathf.Min(verticalHalf, horizontalHalf));
        // Mobil lebih lebar dari tinggi: ruang vertikal tidak perlu sepenuh radius.
        distance *= 0.42f;

        Quaternion look = Quaternion.Euler(pitch, 0f, 0f);
        Vector3 offset = look * new Vector3(0f, 0f, -distance);
        _camera.transform.position = _pivot.position + offset;
        _camera.transform.rotation = look;
        _light.transform.position = _pivot.position + new Vector3(-radius * 1.5f, radius * 2f, -radius * 2f);
        _light.range = distance * 4f;
    }

    private void Update() {
        if (_model != null && _pivot != null) _pivot.Rotate(0f, spinSpeed * Time.unscaledDeltaTime, 0f, Space.World);
    }

    private void OnDestroy() {
        if (_pivot != null) Destroy(_pivot.gameObject);
        if (_camera != null) Destroy(_camera.gameObject);
        if (_light != null) Destroy(_light.gameObject);
        if (_texture != null) _texture.Release();
        if (_texture != null) Destroy(_texture);
    }
}
