using UnityEngine;

/// <summary>
/// Petunjuk tutorial ruang ganti: setelah pemain mendengar perkenalan kasir (dialog <see cref="afterDialogueId"/>),
/// lingkaran ruang ganti disorot lebih terang dan panah melayang di atasnya, sampai pemain lokal pertama kali
/// berdiri di lingkaran itu. Selesainya disimpan di progres (<see cref="ProgressService.MarkTutorialDone"/>),
/// jadi tidak muncul lagi. Hanya visual lokal. Pasang di objek station (bersama BuildingEntranceMarker).
/// </summary>
[RequireComponent(typeof(BuildingEntranceMarker))]
public sealed class FittingRoomHint : MonoBehaviour {
    private const float CheckInterval = 0.3f;

    [Tooltip("ID tutorial yang disimpan saat pemain sudah menginjak lingkaran.")]
    [SerializeField] private string tutorialId = "fitting-room-hint";
    [Tooltip("Petunjuk baru muncul setelah dialog ini selesai (Dialogue Id perkenalan kasir).")]
    [SerializeField] private string afterDialogueId = "mira_intro";
    [Tooltip("Collider area lingkaran (trigger station). Kosong = collider di objek ini.")]
    [SerializeField] private Collider zone;

    [Header("Panah")]
    [SerializeField] private Material arrowMaterial;
    [SerializeField] private float arrowHeight = 2.3f;
    [SerializeField] private float arrowSize = 0.45f;
    [SerializeField] private float bobAmplitude = 0.12f;
    [SerializeField] private float bobSpeed = 3f;

    private BuildingEntranceMarker _marker;
    private Transform _arrow;
    private bool _showing;
    private bool _done;
    private float _nextCheck;

    void Awake() {
        _marker = GetComponent<BuildingEntranceMarker>();
        if (!zone) zone = GetComponent<Collider>();
    }

    void OnDisable() => SetShowing(false);

    void Update() {
        if (_done) return;
        if (Time.unscaledTime >= _nextCheck) {
            _nextCheck = Time.unscaledTime + CheckInterval;
            UpdateState();
        }
        if (_showing && _arrow) AnimateArrow();
    }

    private void UpdateState() {
        if (!ProgressService.IsLoaded) return;
        if (ProgressService.IsTutorialDone(tutorialId)) {
            _done = true;
            SetShowing(false);
            return;
        }
        bool introHeard = string.IsNullOrEmpty(afterDialogueId) || ProgressService.IsDialogueCompleted(afterDialogueId);
        if (introHeard && IsLocalPlayerInZone()) {
            ProgressService.MarkTutorialDone(tutorialId);
            _done = true;
            SetShowing(false);
            return;
        }
        SetShowing(introHeard);
    }

    private bool IsLocalPlayerInZone() {
        PlayerController local = PlayerController.Local;
        return local && zone && zone.bounds.Contains(local.transform.position + Vector3.up * 0.1f);
    }

    private void SetShowing(bool show) {
        if (show == _showing) return;
        _showing = show;
        if (_marker) _marker.SetHighlighted(show);
        if (show && !_arrow) _arrow = CreateArrow();
        if (_arrow) _arrow.gameObject.SetActive(show);
    }

    private void AnimateArrow() {
        float bob = Mathf.Sin(Time.time * bobSpeed) * bobAmplitude;
        _arrow.position = transform.position + Vector3.up * (arrowHeight + bob);
        Camera cam = Camera.main;
        if (!cam) return;
        Vector3 toCamera = cam.transform.position - _arrow.position;
        toCamera.y = 0f;
        if (toCamera.sqrMagnitude > 0.001f) _arrow.rotation = Quaternion.LookRotation(-toCamera);
    }

    private Transform CreateArrow() {
        var go = new GameObject("Hint Arrow", typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(transform, false);
        go.transform.localScale = Vector3.one * arrowSize;
        go.GetComponent<MeshFilter>().sharedMesh = BuildArrowMesh();
        var renderer = go.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = arrowMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return go.transform;
    }

    /// Panah datar menunjuk ke bawah (bidang XY, ujung di y=0), dua sisi supaya terlihat dari depan/belakang.
    private static Mesh BuildArrowMesh() {
        var v = new[] {
            new Vector3(0f, 0f, 0f),       // 0 ujung
            new Vector3(-0.6f, 0.6f, 0f),  // 1 sayap kiri
            new Vector3(0.6f, 0.6f, 0f),   // 2 sayap kanan
            new Vector3(-0.25f, 0.6f, 0f), // 3 batang kiri bawah
            new Vector3(0.25f, 0.6f, 0f),  // 4 batang kanan bawah
            new Vector3(-0.25f, 1.3f, 0f), // 5 batang kiri atas
            new Vector3(0.25f, 1.3f, 0f),  // 6 batang kanan atas
        };
        var front = new[] { 0, 1, 2, 3, 5, 6, 3, 6, 4 };
        var tris = new int[front.Length * 2];
        for (int i = 0; i < front.Length; i += 3) {
            tris[i] = front[i]; tris[i + 1] = front[i + 1]; tris[i + 2] = front[i + 2];
            int b = front.Length + i;
            tris[b] = front[i]; tris[b + 1] = front[i + 2]; tris[b + 2] = front[i + 1];
        }
        var mesh = new Mesh { name = "HintArrow", vertices = v, triangles = tris };
        mesh.RecalculateBounds();
        return mesh;
    }
}
