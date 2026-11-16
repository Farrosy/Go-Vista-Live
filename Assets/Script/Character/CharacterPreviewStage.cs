using Bozo.ModularCharacters;
using UnityEngine;

/// <summary>
/// "Panggung" preview karakter: kamera + lampu + meja putar, diletakkan jauh dari dunia. Dipakai di dua
/// tempat: scene KARAKTER (CharacterScene, objek Preview Stage) dan menu PROFIL (prefab ProfilePreviewStage
/// di scene lobby). Kamera merender ke RenderTexture yang ditampilkan RawImage di canvas. Atur posisi
/// kamera/lampu langsung di scene/prefab kalau framing kurang pas.
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
    private OutfitSystem liveAvatar;
    private Animator liveAnimator;
    // State idle di HumanoidPlayerController. InAir/Land = pose melayang lalu mendarat yang harus dihindari.
    private static readonly int LocomotionState = Animator.StringToHash("Locomotion");
    private float lastDragTime = -999f;

    /// <summary>Karakter live (custom) sedang dirakit/digabung BoZo: tampilkan indikator "Memuat…".</summary>
    public bool IsBusy => liveAvatar && (liveAvatar.isloading || liveAvatar.isMerging || liveAvatar.isUpdatingTexture);

    /// <summary>Karakter live yang sedang tampil (untuk diedit di customizer), atau null.</summary>
    public OutfitSystem LiveAvatar => liveAvatar;

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

    /// Latar kamera transparan supaya gradasi kartu preview di menu terlihat di belakang karakter
    /// (prefab lama memakai latar biru gelap solid).
    public void UseTransparentBackground() {
        if (!previewCamera) return;
        previewCamera.clearFlags = CameraClearFlags.SolidColor;
        previewCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
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

    /// <summary>
    /// Merakit karakter custom dari data BoZo di meja putar (OutfitSystem + bake runtime). Animator memakai
    /// controller dan skala model <paramref name="template"/>, jadi idle dan tingginya sama dengan saat main.
    /// </summary>
    public void ShowLive(OutfitSystem characterBase, CharacterData data, CharacterDatabase.Entry template) {
        Clear();
        if (!characterBase || data == null) return;

        // Dibuat di bawah parent nonaktif supaya Awake BoZo tidak memuat karakter bawaan prefab.
        var holder = new GameObject("__PreviewHolder");
        holder.SetActive(false);
        OutfitSystem avatar = Instantiate(characterBase, holder.transform);
        avatar.name = "Preview Custom";
        avatar.characterData = null;
        avatar.loadMode = OutfitSystem.LoadMode.Manual;
        foreach (Camera cam in avatar.GetComponentsInChildren<Camera>(true)) cam.enabled = false;
        foreach (AudioListener listener in avatar.GetComponentsInChildren<AudioListener>(true)) listener.enabled = false;
        avatar.transform.SetParent(turntable, false);
        avatar.transform.localPosition = Vector3.zero;
        avatar.transform.localRotation = Quaternion.identity;
        GameObject source = template?.PreviewSource;
        if (source) avatar.transform.localScale = source.transform.localScale;
        Destroy(holder);

        model = avatar.gameObject;
        liveAvatar = avatar;
        liveAnimator = avatar.GetComponentInChildren<Animator>();
        // Setiap merge, BoZo memanggil Rebind() (parameter kembali default: IsGrounded = false) lalu
        // OnRigChanged di frame yang sama. Idle dipasang di situ, SEBELUM animator dievaluasi, supaya
        // karakter tidak sempat "loncat" (InAir) lalu mendarat (Land) setiap ganti baju/warna.
        avatar.OnRigChanged += _ => ForceIdle(liveAnimator);
        UseTemplateAnimator(liveAnimator, source);
        _ = BMAC_SaveSystem.LoadCharacter(avatar, data, false, avatar.async);
        turntable.localRotation = Quaternion.identity;
    }

    /// <summary>Tampilkan karakter yang sedang dipakai: slot custom aktif (dirakit live) atau template.</summary>
    public void ShowActive() {
        CustomCharacterRecord record = CustomCharacterStore.Get(CustomCharacterStore.ActiveSlot);
        CustomizerSettings settings = CustomizerSettings.Instance;
        if (record == null || !settings || !CharacterDatabase.Instance) {
            Show(CharacterSelection.Current);
            return;
        }
        ShowLive(settings.characterBase, CharacterRecordConverter.ToCharacterData(record),
                 CharacterDatabase.Instance.Get(record.templateId));
    }

    /// Idle bernapas dari controller pemain (HumanoidPlayerController), bukan controller creator BoZo.
    private static void UseTemplateAnimator(Animator animator, GameObject templateModel) {
        if (!animator) return;
        Animator templateAnimator = templateModel ? templateModel.GetComponentInChildren<Animator>(true) : null;
        if (templateAnimator && templateAnimator.runtimeAnimatorController)
            animator.runtimeAnimatorController = templateAnimator.runtimeAnimatorController;
        animator.applyRootMotion = false;
        PlayerController.ApplyAnimator(animator, 0f, true, 0f);
    }

    public void Clear() {
        if (model) Destroy(model);
        model = null;
        liveAvatar = null;
        liveAnimator = null;
    }

    /// Dipanggil CharacterPreviewDrag saat layar preview digeser.
    public void Rotate(float degrees) {
        if (turntable) turntable.Rotate(0f, degrees, 0f, Space.Self);
        lastDragTime = Time.unscaledTime;
    }

    void Update() {
        KeepIdle();
        if (model && autoRotateSpeed != 0f && Time.unscaledTime - lastDragTime > resumeAutoRotateDelay)
            turntable.Rotate(0f, autoRotateSpeed * Time.unscaledDeltaTime, 0f, Space.Self);
    }

    /// Jaring pengaman selain OnRigChanged: kalau parameter idle sempat ter-reset (Rebind di jalur lain),
    /// pasang lagi di frame berikutnya. Murah: hanya membaca satu bool.
    private void KeepIdle() {
        if (liveAnimator && liveAnimator.isActiveAndEnabled && !liveAnimator.GetBool(PlayerController.IsGroundedParam))
            ForceIdle(liveAnimator);
    }

    /// Diam di tanah: parameter idle + langsung ke state Locomotion (tanpa transisi InAir → Land).
    private static void ForceIdle(Animator animator) {
        if (!animator || !animator.runtimeAnimatorController) return;
        PlayerController.ApplyAnimator(animator, 0f, true, 0f);
        if (!animator.isActiveAndEnabled || !animator.HasState(0, LocomotionState)) return;
        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
        bool inIdle = state.shortNameHash == LocomotionState;
        if (!inIdle || animator.IsInTransition(0))
            animator.Play(LocomotionState, 0, inIdle ? state.normalizedTime % 1f : 0f);
    }

    void OnDestroy() {
        if (texture) texture.Release();
    }
}
