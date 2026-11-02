using UnityEngine;

/// <summary>
/// "Panggung" preview karakter di menu KARAKTER: kamera + lampu + meja putar, diletakkan jauh dari
/// dunia (prefab CharacterSelectCanvas, objek Preview Stage). Kamera merender ke RenderTexture yang
/// ditampilkan RawImage di canvas. Atur posisi kamera/lampu langsung di prefab kalau framing kurang pas.
/// </summary>
public class CharacterPreviewStage : MonoBehaviour {
    [SerializeField] private Camera previewCamera;
    [Tooltip("Model karakter dipasang sebagai child objek ini, lalu diputar.")]
    [SerializeField] private Transform turntable;
    [Tooltip("Putar otomatis (derajat/detik). 0 = tidak berputar sendiri.")]
    [SerializeField] private float autoRotateSpeed = 15f;
    [Tooltip("Jeda sebelum putar otomatis lagi setelah pemain menggeser (detik).")]
    [SerializeField] private float resumeAutoRotateDelay = 2f;
    [SerializeField] private Vector2Int textureSize = new Vector2Int(1024, 1024);

    private RenderTexture texture;
    private GameObject model;
    private float lastDragTime = -999f;

    public RenderTexture Texture {
        get {
            if (!texture) {
                texture = new RenderTexture(textureSize.x, textureSize.y, 24, RenderTextureFormat.ARGB32) {
                    name = "Character Preview",
                    antiAliasing = 2,
                };
                texture.Create();
            }
            if (previewCamera) previewCamera.targetTexture = texture;
            return texture;
        }
    }

    public void Show(CharacterDatabase.Entry character) {
        Clear();
        GameObject source = character?.PreviewSource;
        if (!source) return;

        // Posisi/scale lokal model ikut seperti di prefab avatar, jadi tinggi kaki sama dengan saat main.
        model = Instantiate(source, turntable, false);
        model.name = "Preview " + character.displayName;
        Animator animator = model.GetComponentInChildren<Animator>();
        if (animator) {
            animator.applyRootMotion = false;
            // Di preview tidak ada PlayerController. Default IsGrounded di controller = false,
            // jadi tanpa ini karakter terus memutar animasi melayang (InAir).
            PlayerController.ApplyAnimator(animator, 0f, true, 0f);
        }
        turntable.localRotation = Quaternion.identity;
    }

    public void Clear() {
        if (model) Destroy(model);
        model = null;
    }

    /// Dipanggil CharacterPreviewDrag saat layar preview digeser.
    public void Rotate(float degrees) {
        if (turntable) turntable.Rotate(0f, degrees, 0f, Space.Self);
        lastDragTime = Time.unscaledTime;
    }

    void Update() {
        if (model && autoRotateSpeed != 0f && Time.unscaledTime - lastDragTime > resumeAutoRotateDelay)
            turntable.Rotate(0f, autoRotateSpeed * Time.unscaledDeltaTime, 0f, Space.Self);
    }

    void OnDestroy() {
        if (texture) texture.Release();
    }
}
