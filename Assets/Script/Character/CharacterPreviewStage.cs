using Bozo.ModularCharacters;
using UnityEngine;
using UnityEngine.Rendering.Universal;

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

    [Header("Preset kamera Wajah (customizer)")]
    [Tooltip("Jarak kamera ke wajah (meter) saat mode Wajah.")]
    [SerializeField] private float faceDistance = 1.15f;
    [SerializeField] private float faceFieldOfView = 24f;
    [Tooltip("FOV preset Wajah di ruang ganti (layar penuh, bukan kotak preview), supaya wajah tidak terlalu besar.")]
    [SerializeField] private float worldFaceFieldOfView = 32f;
    [Tooltip("Titik bidik di atas tulang kepala (meter): kira-kira tengah wajah.")]
    [SerializeField] private float faceAimAbove = 0.09f;
    [Tooltip("Waktu (detik) kamera berpindah antara Seluruh Badan dan Wajah.")]
    [SerializeField] private float cameraSmoothTime = 0.18f;

    [Header("Kursi barber: kamera geser kiri/kanan")]
    [Tooltip("Batas putar kamera mengelilingi kepala dari bidikan editor (derajat ke tiap sisi).")]
    [SerializeField] private float seatOrbitLimit = 110f;
    [Tooltip("Waktu (detik) kamera menyusul posisi geser: makin besar makin halus/lambat berhenti.")]
    [SerializeField] private float seatOrbitSmoothTime = 0.28f;
    [Tooltip("Jarak terdekat kamera ke kepala (meter) saat ditarik masuk oleh tembok / kursi sebelah.")]
    [SerializeField] private float seatOrbitMinDistance = 0.5f;

    [Header("Thumbnail slot")]
    [SerializeField] private float thumbnailDistance = 1.7f;
    [SerializeField] private float thumbnailFieldOfView = 26f;
    [Tooltip("Titik bidik thumbnail di bawah titik wajah (meter): kepala + bahu.")]
    [SerializeField] private float thumbnailAimBelow = 0.12f;

    private RenderTexture texture;
    private GameObject model;
    private OutfitSystem liveAvatar;
    private Animator liveAnimator;
    // Ekspresi wajah avatar live (FaceExpressions). Dipasang ulang setelah BoZo selesai merakit/merge, karena
    // kepala/mesh gabungan yang baru mulai dari bobot blendshape 0.
    private int liveExpression;
    private bool expressionDirty;
    // State idle di HumanoidPlayerController. InAir/Land = pose melayang lalu mendarat yang harus dihindari.
    private static readonly int LocomotionState = Animator.StringToHash("Locomotion");
    // Kursi barber (SeatOccupant): bool duduk + state duduk diam di HumanoidPlayerController.
    private static readonly int SeatedParam = Animator.StringToHash(SeatOccupant.SeatedParam);
    private static readonly int SitIdleState = Animator.StringToHash(SeatOccupant.SitIdleState);
    private float lastDragTime = -999f;

    // Kamera "Seluruh Badan" = posisi kamera di scene/prefab (diambil saat pertama dipakai).
    private bool cameraBaseCaptured;
    private Vector3 baseCameraPosition;
    private Quaternion baseCameraRotation;
    private float baseFieldOfView;
    private bool faceFocus;
    private bool turnToFront;
    private float cameraBlend;
    private float cameraBlendVelocity;
    private Transform headBone;

    // Mode dunia (ruang ganti): panggung berdiri di bilik toko, kamera merender langsung ke layar.
    private bool worldMode;
    private float screenCenterX;
    private float screenCenterY;
    // Mode dunia kursi barber: karakter duduk diam (ChairSitIdle + SeatIK), tanpa zoom Wajah. Geser layar memutar
    // KAMERA mengelilingi kepala (bukan karakternya), dengan easing.
    private bool seated;
    private float seatYaw;            // derajat dari bidikan editor (+ = searah jarum jam dilihat dari atas)
    private float seatYawTarget;
    private float seatYawVelocity;
    private bool seatPivotReady;
    private Vector3 seatPivot;        // titik di sumbu bidikan editor setinggi/sedalam kepala
    private float seatDistance;       // jarak kamera ke pivot sekarang (dijepit tembok)
    private float seatDistanceVelocity;
    // Collider yang memang ditembus bidikan editor (mis. meja cermin): tidak dianggap menghalangi.
    private readonly System.Collections.Generic.HashSet<Collider> seatSeeThrough =
        new System.Collections.Generic.HashSet<Collider>();
    private const float SeatProbeRadius = 0.12f;
    private const float SeatProbeBuffer = 0.08f;
    // Layer sementara saat memotret thumbnail di mode dunia, supaya latar toko tidak ikut terfoto.
    private const int ThumbnailLayer = 31;

    /// <summary>Kamera sedang (menuju) preset Wajah.</summary>
    public bool FaceFocus => faceFocus;

    /// <summary>
    /// false = meja putar diam (karakter hanya berputar kalau digeser). Dimatikan customizer supaya karakter
    /// tidak berputar sendiri saat diedit; layar pilih karakter / profil tetap berputar otomatis.
    /// Saat dimatikan, karakter diputar balik menghadap depan sekali.
    /// </summary>
    public bool AutoRotate {
        get => autoRotate;
        set {
            if (autoRotate && !value) turnToFront = true;
            autoRotate = value;
        }
    }
    private bool autoRotate = true;

    /// <summary>Karakter live (custom) sedang dirakit/digabung BoZo: tampilkan indikator "Memuat…".</summary>
    /// Arah hadap karakter preview di dunia (ikut putaran meja putar). Mode dunia: avatar asli memakai arah ini
    /// saat editor ditutup, supaya tidak tiba-tiba berbalik.
    public Quaternion TurntableRotation => turntable ? turntable.rotation : transform.rotation;

    public bool IsBusy => liveAvatar && (liveAvatar.isloading || liveAvatar.isMerging || liveAvatar.isUpdatingTexture);

    /// <summary>Karakter live yang sedang tampil (untuk diedit di customizer), atau null.</summary>
    public OutfitSystem LiveAvatar => liveAvatar;

    /// <summary>Mode dunia: tidak ada RenderTexture (kamera merender ke layar), jadi null.</summary>
    public RenderTexture Texture {
        get {
            if (worldMode) return null;
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

    /// <summary>
    /// Ruang ganti: panggung dipindah ke bilik di toko (kaki karakter di <paramref name="subject"/>, wajah
    /// menghadap ke depan pose itu) dan kamera ke <paramref name="view"/>. Alas & lampu panggung dimatikan
    /// supaya karakter memakai lantai dan cahaya toko. Kamera merender LANGSUNG ke layar di atas
    /// <paramref name="reference"/> (kamera utama), dengan pengaturan render yang sama.
    /// </summary>
    /// <param name="cullingMask">Layer dunia yang dirender (masker kamera utama sebelum dimatikan).</param>
    /// <param name="seatedPose">Kursi barber: <paramref name="subject"/> = pose root avatar yang duduk. Karakter
    /// memakai pose duduk diam (kaki di pijakan lewat SeatIK), meja putar dan zoom Wajah dimatikan; geser layar
    /// memutar kamera mengelilingi kepala.</param>
    public void EnterWorldMode(Pose subject, Pose view, float fieldOfView, Camera reference, int cullingMask,
                               bool seatedPose = false) {
        if (!previewCamera || !turntable) return;
        worldMode = true;
        seated = seatedPose;
        ResetSeatOrbit();
        foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true)) {
            if (!renderer.transform.IsChildOf(turntable)) renderer.enabled = false;
        }
        foreach (Light stageLight in GetComponentsInChildren<Light>(true)) stageLight.enabled = false;

        transform.SetPositionAndRotation(subject.position - subject.rotation * turntable.localPosition, subject.rotation);
        turntable.localRotation = Quaternion.identity;
        previewCamera.transform.SetPositionAndRotation(view.position, view.rotation);
        previewCamera.fieldOfView = fieldOfView;
        previewCamera.targetTexture = null;
        if (reference) CopyRenderSettings(reference, previewCamera);
        previewCamera.cullingMask = cullingMask;
        cameraBaseCaptured = false;
        cameraBlend = 0f;
        faceFocus = false;
        CaptureCameraBase();
    }

    /// <summary>
    /// Mode dunia: posisi horizontal sumbu karakter di layar, -1 (kiri) .. 1 (kanan). Dipakai supaya karakter
    /// berdiri di tengah ruang kosong antara rail dan panel kanan, tanpa memutar kamera (lens shift).
    /// </summary>
    public float ScreenCenterX {
        get => screenCenterX;
        set => screenCenterX = Mathf.Clamp(value, -0.9f, 0.9f);
    }

    /// <summary>Sama dengan ScreenCenterX untuk sumbu vertikal (-1 bawah .. 1 atas), supaya karakter tidak
    /// tertutup bar atas editor.</summary>
    public float ScreenCenterY {
        get => screenCenterY;
        set => screenCenterY = Mathf.Clamp(value, -0.9f, 0.9f);
    }

    private static void CopyRenderSettings(Camera from, Camera to) {
        to.clearFlags = from.clearFlags;
        to.backgroundColor = from.backgroundColor;
        to.nearClipPlane = Mathf.Min(0.1f, from.nearClipPlane);
        to.farClipPlane = Mathf.Max(50f, from.farClipPlane);
        to.depth = from.depth + 1f;
        to.allowHDR = from.allowHDR;
        UniversalAdditionalCameraData source = from.GetUniversalAdditionalCameraData();
        UniversalAdditionalCameraData target = to.GetUniversalAdditionalCameraData();
        if (!source || !target) return;
        target.renderPostProcessing = source.renderPostProcessing;
        target.antialiasing = source.antialiasing;
        target.antialiasingQuality = source.antialiasingQuality;
        target.renderShadows = source.renderShadows;
        target.volumeLayerMask = source.volumeLayerMask;
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
    /// <param name="expression">Ekspresi wajah record (FaceExpressions); tidak ada di CharacterData BoZo.</param>
    public void ShowLive(OutfitSystem characterBase, CharacterData data, CharacterDatabase.Entry template,
                         int expression = 0) {
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
        avatar.OnRigChanged += _ => {
            HoldPose(liveAnimator);
            expressionDirty = true;
        };
        SetExpression(expression);
        UseTemplateAnimator(liveAnimator, source);
        if (seated) {
            SeatIK.Ensure(liveAnimator); // kaki di pijakan kursi terdekat, sama seperti avatar yang duduk
            HoldPose(liveAnimator);
        }
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
                 CharacterDatabase.Instance.Get(record.templateId), record.expression);
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
        headBone = null;
    }

    /// Dipanggil CharacterPreviewDrag saat layar preview digeser. Duduk di kursi: kamera yang berputar (arah putar
    /// karakter terlihat sama dengan meja putar: geser ke kanan = wajah berpaling ke kanan).
    public void Rotate(float degrees) {
        if (seated) {
            seatYawTarget = Mathf.Clamp(seatYawTarget - degrees, -seatOrbitLimit, seatOrbitLimit);
            return;
        }
        if (turntable) turntable.Rotate(0f, degrees, 0f, Space.Self);
        lastDragTime = Time.unscaledTime;
        turnToFront = false;
    }

    /// <summary>
    /// Preset kamera customizer: Wajah (zoom ke kepala, karakter diputar menghadap depan, putar otomatis
    /// berhenti) atau Seluruh Badan (posisi kamera di scene).
    /// </summary>
    public void SetFaceFocus(bool face) {
        if (seated) face = false; // kursi barber: kamera tetap di bidikan editor kursi
        CaptureCameraBase();
        if (face && !faceFocus) turnToFront = true;
        faceFocus = face;
    }

    /// <summary>Ganti ekspresi avatar live; dipasang begitu avatar tidak sibuk merakit.</summary>
    public void SetExpression(int expression) {
        liveExpression = expression;
        expressionDirty = true;
    }

    void Update() {
        KeepIdle();
        ApplyExpressionWhenIdle();
        UpdateTurntable();
        UpdateCamera();
    }

    void LateUpdate() {
        if (cameraBlend > 0f) ApplyCamera(cameraBlend);
        if (seated && worldMode) ApplySeatOrbit();
        ApplyLensShift();
    }

    // ---------------------------------------------------------------- Kursi barber: kamera mengelilingi kepala

    private void ResetSeatOrbit() {
        seatYaw = seatYawTarget = seatYawVelocity = 0f;
        seatDistance = seatDistanceVelocity = 0f;
        seatPivotReady = false;
        seatSeeThrough.Clear();
    }

    /// Kamera = bidikan editor yang diputar mengelilingi pivot (sumbu tegak), jadi di 0 derajat persis bidikan editor
    /// dan kepala tetap di tengah area panggung (pivot ada di sumbu kamera, lens shift tetap berlaku). Sudut menyusul
    /// target dengan SmoothDamp (mulai dan berhenti halus); jarak ditarik masuk seketika kalau terhalang tembok /
    /// kursi sebelah, dan kembali keluar dengan halus.
    private void ApplySeatOrbit() {
        if (!previewCamera || !cameraBaseCaptured) return;
        if (!seatPivotReady && !TryCaptureSeatPivot()) return;
        float dt = Time.unscaledDeltaTime;
        seatYaw = Mathf.SmoothDamp(seatYaw, seatYawTarget, ref seatYawVelocity, seatOrbitSmoothTime, Mathf.Infinity, dt);
        if (Mathf.Abs(seatYaw - seatYawTarget) < 0.01f && Mathf.Abs(seatYawVelocity) < 0.01f) seatYaw = seatYawTarget;

        Quaternion turn = Quaternion.AngleAxis(seatYaw, Vector3.up);
        Vector3 offset = turn * (baseCameraPosition - seatPivot);
        float full = offset.magnitude;
        if (full < 0.001f) return;
        Vector3 direction = offset / full;
        float allowed = SeatClearance(direction, full);
        if (seatDistance <= 0f || allowed < seatDistance) {
            seatDistance = allowed;      // tembok: langsung masuk, jangan sempat menembus
            seatDistanceVelocity = 0f;
        } else {
            seatDistance = Mathf.SmoothDamp(seatDistance, allowed, ref seatDistanceVelocity, seatOrbitSmoothTime,
                                            Mathf.Infinity, dt);
        }
        previewCamera.transform.SetPositionAndRotation(seatPivot + direction * seatDistance, turn * baseCameraRotation);
    }

    /// Pivot = titik di sumbu bidikan editor yang paling dekat dengan kepala (sekali, setelah avatar selesai dirakit).
    private bool TryCaptureSeatPivot() {
        if (!liveAnimator || IsBusy) return false;
        Transform head = liveAnimator.isHuman ? liveAnimator.GetBoneTransform(HumanBodyBones.Head) : null;
        Vector3 headPoint = head ? head.position : new Vector3(turntable.position.x, HeadHeight(), turntable.position.z);
        headPoint += Vector3.up * faceAimAbove;
        Vector3 forward = baseCameraRotation * Vector3.forward;
        float depth = Mathf.Max(0.3f, Vector3.Dot(headPoint - baseCameraPosition, forward));
        seatPivot = baseCameraPosition + forward * depth;
        seatPivotReady = true;

        // Semua yang ditembus bidikan editor sendiri (meja cermin tempat kamera A berada) boleh ditembus di sudut lain.
        seatSeeThrough.Clear();
        Vector3 toBase = baseCameraPosition - seatPivot;
        float length = toBase.magnitude;
        if (length > 0.001f) {
            foreach (RaycastHit hit in Physics.SphereCastAll(seatPivot, SeatProbeRadius, toBase / length, length, ~0,
                                                             QueryTriggerInteraction.Ignore))
                seatSeeThrough.Add(hit.collider);
            foreach (Collider inside in Physics.OverlapSphere(baseCameraPosition, SeatProbeRadius, ~0,
                                                              QueryTriggerInteraction.Ignore))
                seatSeeThrough.Add(inside);
        }
        ChairSeat chair = ChairSeat.FindNear(turntable.position, 2f);
        if (chair) {
            foreach (Collider part in chair.ChairColliders)
                if (part) seatSeeThrough.Add(part);
        }
        return true;
    }

    /// Jarak terjauh dari pivot ke arah <paramref name="direction"/> sebelum menabrak tembok / perabot.
    private float SeatClearance(Vector3 direction, float full) {
        float allowed = full;
        foreach (RaycastHit hit in Physics.SphereCastAll(seatPivot, SeatProbeRadius, direction, full, ~0,
                                                         QueryTriggerInteraction.Ignore)) {
            if (hit.distance <= 0f || IgnoredBySeatProbe(hit.collider)) continue;
            allowed = Mathf.Min(allowed, hit.distance - SeatProbeBuffer);
        }
        return Mathf.Clamp(allowed, Mathf.Min(seatOrbitMinDistance, full), full);
    }

    private bool IgnoredBySeatProbe(Collider c) {
        if (!c || seatSeeThrough.Contains(c)) return true;
        if (model && c.transform.IsChildOf(model.transform)) return true;
        if (c is CharacterController) return true;
        return c.GetComponentInParent<PlayerController>() || c.GetComponentInParent<NpcController>();
    }

    /// Geser proyeksi (bukan kamera) supaya sumbu karakter jatuh di ScreenCenterX. Dihitung ulang tiap frame
    /// karena FOV berubah saat zoom Wajah dan rasio layar bisa berubah.
    private void ApplyLensShift() {
        if (!worldMode || !previewCamera) return;
        previewCamera.ResetProjectionMatrix();
        if (Mathf.Approximately(screenCenterX, 0f) && Mathf.Approximately(screenCenterY, 0f)) return;
        Matrix4x4 projection = previewCamera.projectionMatrix;
        projection[0, 2] = -screenCenterX; // titik di sumbu kamera jatuh di NDC (-m02, -m12)
        projection[1, 2] = -screenCenterY;
        previewCamera.projectionMatrix = projection;
    }

    private void UpdateTurntable() {
        if (!model || !turntable) return;
        if (faceFocus || !autoRotate) {
            if (turnToFront)
                turntable.localRotation = Quaternion.RotateTowards(turntable.localRotation, Quaternion.identity,
                                                                   360f * Time.unscaledDeltaTime);
            return;
        }
        if (autoRotateSpeed != 0f && Time.unscaledTime - lastDragTime > resumeAutoRotateDelay)
            turntable.Rotate(0f, autoRotateSpeed * Time.unscaledDeltaTime, 0f, Space.Self);
    }

    private void UpdateCamera() {
        if (!cameraBaseCaptured || !previewCamera) return;
        float target = faceFocus ? 1f : 0f;
        if (Mathf.Approximately(cameraBlend, target) && cameraBlend == 0f) return;
        cameraBlend = Mathf.SmoothDamp(cameraBlend, target, ref cameraBlendVelocity, cameraSmoothTime,
                                       Mathf.Infinity, Time.unscaledDeltaTime);
        if (Mathf.Abs(cameraBlend - target) < 0.001f) cameraBlend = target;
        if (cameraBlend == 0f) ApplyCamera(0f);
    }

    /// Kamera di antara Seluruh Badan (0) dan Wajah (1). Titik bidik memakai tinggi tulang kepala di sumbu
    /// meja putar, jadi kamera tidak ikut bergoyang saat karakter berputar.
    private void ApplyCamera(float blend) {
        if (!previewCamera) return;
        Pose face = FramePose(faceDistance, faceAimAbove);
        previewCamera.transform.SetPositionAndRotation(Vector3.Lerp(baseCameraPosition, face.position, blend),
                                                       Quaternion.Slerp(baseCameraRotation, face.rotation, blend));
        previewCamera.fieldOfView = Mathf.Lerp(baseFieldOfView, worldMode ? worldFaceFieldOfView : faceFieldOfView, blend);
    }

    private Pose FramePose(float distance, float aimAbove) {
        Vector3 axis = turntable ? turntable.position : transform.position;
        Vector3 aim = new Vector3(axis.x, HeadHeight() + aimAbove, axis.z);
        Vector3 toCamera = baseCameraPosition - axis;
        toCamera.y = 0f;
        Vector3 direction = toCamera.sqrMagnitude > 0.0001f ? toCamera.normalized : Vector3.forward;
        Vector3 position = aim + direction * distance;
        return new Pose(position, Quaternion.LookRotation(aim - position, Vector3.up));
    }

    /// Tinggi dunia tulang kepala; tanpa tulang "head", perkiraan dari bagian atas mesh.
    private float HeadHeight() {
        if (model && !headBone) headBone = FindBone(model.transform, "head");
        if (headBone) return headBone.position.y;
        Vector3 axis = turntable ? turntable.position : transform.position;
        return axis.y + 1.55f;
    }

    private static Transform FindBone(Transform root, string boneName) {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true)) {
            if (string.Equals(child.name, boneName, System.StringComparison.OrdinalIgnoreCase)) return child;
        }
        return null;
    }

    private void CaptureCameraBase() {
        if (cameraBaseCaptured || !previewCamera) return;
        baseCameraPosition = previewCamera.transform.position;
        baseCameraRotation = previewCamera.transform.rotation;
        baseFieldOfView = previewCamera.fieldOfView;
        cameraBaseCaptured = true;
    }

    /// <summary>
    /// Foto kepala + bahu karakter yang tampil (menghadap depan, latar transparan) untuk kartu slot.
    /// Dirender sekali di frame ini; kamera dan meja putar dikembalikan seperti semula.
    /// </summary>
    /// <returns>Texture2D baru (pemanggil yang memiliki), atau null kalau tidak ada karakter.</returns>
    public Texture2D CaptureThumbnail(int width, int height) {
        if (!model || !previewCamera || !turntable) return null;
        CaptureCameraBase();
        Transform cam = previewCamera.transform;
        Vector3 oldPosition = cam.position;
        Quaternion oldRotation = cam.rotation;
        float oldFov = previewCamera.fieldOfView;
        Quaternion oldTurn = turntable.localRotation;
        RenderTexture oldTarget = previewCamera.targetTexture;
        CameraClearFlags oldClear = previewCamera.clearFlags;
        Color oldBackground = previewCamera.backgroundColor;
        RenderTexture oldActive = RenderTexture.active;
        int oldMask = previewCamera.cullingMask;
        // Mode dunia: hanya karakter yang dipotret (latar transparan), bukan dinding/rak toko di belakangnya.
        var oldLayers = worldMode ? new System.Collections.Generic.List<(Transform, int)>() : null;
        RenderTexture shot = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
        try {
            if (worldMode) {
                foreach (Transform part in model.GetComponentsInChildren<Transform>(true)) {
                    oldLayers.Add((part, part.gameObject.layer));
                    part.gameObject.layer = ThumbnailLayer;
                }
                previewCamera.cullingMask = 1 << ThumbnailLayer;
                previewCamera.ResetProjectionMatrix();
            }
            turntable.localRotation = Quaternion.identity;
            Pose pose = FramePose(thumbnailDistance, faceAimAbove - thumbnailAimBelow);
            cam.SetPositionAndRotation(pose.position, pose.rotation);
            previewCamera.fieldOfView = thumbnailFieldOfView;
            previewCamera.clearFlags = CameraClearFlags.SolidColor;
            previewCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            previewCamera.targetTexture = shot;
            previewCamera.Render();
            RenderTexture.active = shot;
            var result = new Texture2D(width, height, TextureFormat.RGBA32, false) { name = "Slot Thumbnail" };
            result.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            result.Apply();
            return result;
        } finally {
            RenderTexture.active = oldActive;
            if (oldLayers != null) {
                foreach ((Transform part, int layer) in oldLayers) {
                    if (part) part.gameObject.layer = layer;
                }
            }
            previewCamera.cullingMask = oldMask;
            previewCamera.targetTexture = oldTarget;
            previewCamera.clearFlags = oldClear;
            previewCamera.backgroundColor = oldBackground;
            cam.SetPositionAndRotation(oldPosition, oldRotation);
            previewCamera.fieldOfView = oldFov;
            turntable.localRotation = oldTurn;
            RenderTexture.ReleaseTemporary(shot);
        }
    }

    /// Jaring pengaman selain OnRigChanged: kalau parameter idle sempat ter-reset (Rebind di jalur lain),
    /// pasang lagi di frame berikutnya. Murah: hanya membaca satu bool.
    private void KeepIdle() {
        if (!liveAnimator || !liveAnimator.isActiveAndEnabled) return;
        bool reset = !liveAnimator.GetBool(PlayerController.IsGroundedParam);
        if (seated && !reset && SeatOccupant.HasSeatedParam(liveAnimator)) reset = !liveAnimator.GetBool(SeatedParam);
        if (reset) HoldPose(liveAnimator);
    }

    private void HoldPose(Animator animator) {
        if (seated && ForceSeated(animator)) return;
        ForceIdle(animator);
    }

    /// Duduk diam di kursi: bool duduk + langsung ke ChairSitIdle (tanpa klip duduk dari berdiri).
    /// <returns>false kalau controller tidak punya state duduk (pakai idle berdiri).</returns>
    private static bool ForceSeated(Animator animator) {
        if (!animator || !animator.runtimeAnimatorController || !animator.isActiveAndEnabled) return false;
        if (!SeatOccupant.HasSeatedParam(animator) || !animator.HasState(0, SitIdleState)) return false;
        PlayerController.ApplyAnimator(animator, 0f, true, 0f);
        animator.SetBool(SeatedParam, true);
        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
        bool inSeat = state.shortNameHash == SitIdleState;
        if (!inSeat || animator.IsInTransition(0))
            animator.Play(SitIdleState, 0, inSeat ? state.normalizedTime % 1f : 0f);
        return true;
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

    private void ApplyExpressionWhenIdle() {
        if (!expressionDirty || !liveAvatar || IsBusy) return;
        expressionDirty = false;
        LiveAvatarEdits.ApplyExpression(liveAvatar.gameObject, liveExpression);
    }

    void OnDestroy() {
        if (texture) texture.Release();
    }
}
