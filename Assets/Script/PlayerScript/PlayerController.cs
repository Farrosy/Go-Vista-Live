using System;
using Normal.Realtime;
using UnityEngine;

/// Pasang di Player (root, butuh CharacterController, otomatis ditambah).
/// Model karakter (yang punya Animator) jadi CHILD dari object ini.
///
/// Multiplayer: kalau ada RealtimeView, hanya avatar milik client ini yang membaca input.
/// Avatar pemain lain cuma ikut posisi dari RealtimeTransform, animasinya dari PlayerAnimSync.
/// Tanpa RealtimeView (mis. DevScene/characterControl), selalu dianggap pemain lokal.
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Gerak")]
    [SerializeField] private float walkSpeed = 3f;
    [SerializeField] private float sprintSpeed = 6f;
    [SerializeField] private float turnSpeed = 720f;
    [SerializeField] private float gravity = -20f;

    [Header("Air")]
    [Tooltip("Kecepatan berjalan di air (meter/detik). Tidak bisa lari atau lompat.")]
    [SerializeField] private float wadingSpeed = 1f;
    [Tooltip("Nilai Speed animator saat berjalan di air. 0.5 = animasi jalan penuh.")]
    [SerializeField] private float wadingAnimSpeed = 0.25f;

    [Header("Lompat")]
    [SerializeField] private float jumpHeight = 1.5f;
    [SerializeField] private float jumpCooldown = 0.5f;
    [Tooltip("Jeda dari animasi JumpStart mulai sampai karakter benar-benar terangkat (detik). " +
             "Naikkan kalau JumpStart punya gerakan menekuk lutut yang panjang.")]
    [SerializeField] private float jumpLaunchDelay = 0.05f;

    [Header("Kamera")]
    [Tooltip("ON = joystick atas = maju searah kamera. OFF = joystick atas = selalu +Z dunia.")]
    [SerializeField] private bool cameraRelative = true;
    [SerializeField] private Transform cameraTransform;   // kosong = pakai Camera.main

    [Header("Animasi")]
    [Tooltip("Kosong = otomatis cari di child")]
    [SerializeField] private Animator animator;
    [Tooltip("Kehalusan perpindahan idle/walk/sprint. Makin besar makin lambat berubah.")]
    [SerializeField] private float speedDampTime = 0.1f;
    [Tooltip("Toleransi 'masih dianggap menapak tanah' supaya animasi tidak berkedip di tanjakan/anak tangga.")]
    [SerializeField] private float groundedGrace = 0.1f;

    [Header("Tertabrak mobil")]
    [Tooltip("Kalau Animator belum punya state ber-tag \"Knockdown\", kontrol dibuka lagi setelah ini (detik).")]
    [SerializeField] private float knockdownStartTimeout = 0.5f;
    [Tooltip("Batas aman: kontrol pasti dibuka lagi setelah ini walau animasi bangkit belum selesai (detik).")]
    [SerializeField] private float knockdownMaxSeconds = 8f;
    [Tooltip("Tertabrak sampai mati: jeda dari tertabrak sampai mulai masuk ke tanah. Samakan dengan panjang klip Hit Knockback.")]
    [SerializeField] private float deathSinkDelay = 1.5f;
    [Tooltip("Seberapa dalam model masuk ke tanah (meter).")]
    [SerializeField] private float deathSinkDepth = 1.5f;
    [Tooltip("Lama model masuk ke tanah (detik).")]
    [SerializeField] private float deathSinkSeconds = 2f;

    /// Avatar yang dikendalikan client ini. Null sebelum spawn / setelah keluar world.
    public static PlayerController Local { get; private set; }

    public bool IsLocal { get; private set; }
    public bool IsFrozen => frozen;
    /// True selama masuk, menyetir, dan keluar mobil (VehicleOccupant). Gerak, lompat, dan tertabrak mati.
    public bool InVehicle { get; private set; }
    public bool IsSprinting { get; private set; }
    public float Speed01 { get; private set; }   // 0 = diam, 0.5 = jalan, 1 = sprint
    public bool IsGroundedForAnim => Time.time - lastGroundedTime <= groundedGrace;
    /// True dari tertabrak mobil sampai berdiri idle lagi. Selama ini gerak, lompat, lari, dan interaksi mati.
    public bool IsKnockedDown { get; private set; }
    /// True kalau tabrakannya mematikan: tetap rebahan lalu masuk ke tanah sampai bangkit di titik spawn.
    public bool IsKnockedDownDead => IsKnockedDown && knockdownFatal;
    /// Sisa detik sampai model selesai masuk ke tanah. 0 kalau tidak sedang mati tertabrak.
    public float DeathSinkRemaining =>
        IsKnockedDownDead ? Mathf.Max(0f, deathSinkDelay + deathSinkSeconds - knockdownTimer) : 0f;
    /// Dipanggil saat tertabrak mobil (argumen: mematikan atau tidak). Dipakai PlayerAnimSync.
    public event Action<bool> KnockedDown;
    /// Dipanggil saat lompat dimulai (dipakai PlayerAnimSync supaya pemain lain ikut melihat).
    public event Action Jumped;

    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int IsGroundedHash = Animator.StringToHash("IsGrounded");
    private static readonly int JumpHash = Animator.StringToHash("Jump");
    private static readonly int KnockdownHash = Animator.StringToHash("Knockdown");
    private static readonly int IsDeadHash = Animator.StringToHash("IsDead");
    /// Tag di state Hit Knockback dan LayToIdle. Selama Animator ada di state ber-tag ini, pemain belum berdiri.
    public const string KnockdownTag = "Knockdown";

    // Snap ke tanah saat spawn/teleport: cari tanah dari 1 m di atas sampai 3 m di bawah titik tujuan.
    private const float GroundSnapProbeUp = 1f;
    private const float GroundSnapProbeDown = 3f;
    // Hasil RaycastNonAlloc tidak berurutan dan kelebihan hit dibuang, jadi buffer dibuat lega untuk area ramai.
    private static readonly RaycastHit[] GroundSnapHits = new RaycastHit[32];

    private CharacterController cc;
    private float verticalVel;
    private float nextJumpTime;
    private bool launchPending;
    private float launchTimer;
    private float lastGroundedTime;
    private bool wading;
    private bool frozen;
    private Vector3 shoveVelocity;
    private float shoveTimer;
    private bool knockdownFatal;
    private bool knockdownStateSeen;
    private float knockdownTimer;
    private Vector3 modelBaseLocalPos;
    private float appliedSink;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Local = null;
    }

    void Awake()
    {
        cc = GetComponent<CharacterController>();
        if (!animator) animator = GetComponentInChildren<Animator>();
        if (animator) animator.applyRootMotion = false;   // gerak dikendalikan script, bukan animasi
        if (animator) modelBaseLocalPos = animator.transform.localPosition;
        lastGroundedTime = Time.time;

        // Mode offline (mis. prefab Player dipakai di DevScene tanpa Realtime): matikan komponen
        // Normcore supaya tidak error "none exist in the scene", dan avatar dianggap pemain lokal.
        if (IsOffline)
        {
            var rt = GetComponent<RealtimeTransform>();
            if (rt) rt.enabled = false;
            var view = GetComponent<RealtimeView>();
            if (view) view.enabled = false;
        }
    }

    private bool IsOffline => FindAnyObjectByType<Realtime>() == null;

    void Start()
    {
        var view = GetComponent<RealtimeView>();
        IsLocal = view == null || !view.enabled || IsOffline || view.isOwnedLocallyInHierarchy;

        if (!IsLocal)
        {
            // Avatar pemain lain: tidak membaca input. CharacterController tetap aktif sebagai
            // collider supaya pemain tidak saling tembus.
            enabled = false;
            return;
        }

        var realtimeTransform = GetComponent<RealtimeTransform>();
        if (realtimeTransform && realtimeTransform.enabled) realtimeTransform.RequestOwnership();

        Local = this;
        SnapToGround();
    }

    void OnDestroy()
    {
        if (Local != this) return;
        Local = null;
        if (IsKnockedDown) SetControlsStunned(false);
    }

    /// Kamera yang dipakai untuk arah gerak. Dipanggil PlayerManager saat spawn.
    public void SetCamera(Transform cam) => cameraTransform = cam;

    /// True selama kaki cukup dalam di air: berjalan lambat, tidak bisa lari atau lompat.
    public void SetWading(bool value) => wading = value;

    /// Hentikan semua gerakan avatar (dipakai selama layar mati). Dorongan tabrakan mobil yang sedang
    /// berjalan tetap diselesaikan, supaya tabrakan terakhir tetap terpental dulu baru rebahan.
    public void SetFrozen(bool value)
    {
        frozen = value;
        launchPending = false;
        verticalVel = 0f;
    }

    /// Mendorong avatar sejauh offset selama beberapa detik, tetap tertahan tembok.
    public void Shove(Vector3 offset, float seconds)
    {
        shoveVelocity = offset / Mathf.Max(0.01f, seconds);
        shoveTimer = seconds;
    }

    /// Dipanggil VehicleOccupant. Posisi avatar selama di mobil diatur VehicleOccupant, bukan script ini.
    public void SetInVehicle(bool value)
    {
        InVehicle = value;
        launchPending = false;
        verticalVel = 0f;
        shoveTimer = 0f;
        IsSprinting = false;
        Speed01 = 0f;
        lastGroundedTime = Time.time;
    }

    /// Pindahkan avatar tanpa terhalang CharacterController. Kalau sedang di mobil, keluar dulu tanpa animasi.
    public void TeleportTo(Vector3 position, Quaternion rotation)
    {
        if (InVehicle && TryGetComponent(out VehicleOccupant occupant)) occupant.ExitImmediate();
        shoveTimer = 0f;
        cc.enabled = false;
        transform.SetPositionAndRotation(position, rotation);
        Physics.SyncTransforms();
        cc.enabled = true;
        SnapToGround();
    }

    /// Tempelkan kapsul ke tanah di bawahnya (dipakai saat spawn/teleport). CharacterController baru
    /// mendorong dirinya ke atas tanah setelah bergerak, jadi tanpa ini avatar yang ditaruh tepat di
    /// permukaan terlihat tenggelam sebesar skin width sampai pemain mulai berjalan.
    public void SnapToGround()
    {
        if (!cc) return;
        Vector3 origin = transform.position + Vector3.up * GroundSnapProbeUp;
        int count = Physics.RaycastNonAlloc(origin, Vector3.down, GroundSnapHits, GroundSnapProbeUp + GroundSnapProbeDown,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

        float bestDistance = float.MaxValue;
        float groundY = 0f;
        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = GroundSnapHits[i];
            if (hit.distance >= bestDistance || !IsGroundForSnap(hit.collider)) continue;
            bestDistance = hit.distance;
            groundY = hit.point.y;
        }
        if (bestDistance == float.MaxValue) return;   // tidak ada tanah di dekat titik ini: biarkan

        float scale = transform.lossyScale.y;
        float capsuleBottom = (cc.center.y - cc.height * 0.5f) * scale;
        float targetY = groundY + cc.skinWidth * scale - capsuleBottom;

        bool wasEnabled = cc.enabled;
        cc.enabled = false;
        transform.position = new Vector3(transform.position.x, targetY, transform.position.z);
        Physics.SyncTransforms();
        cc.enabled = wasEnabled;
    }

    /// Tanah = bukan avatar ini, bukan avatar/NPC lain (kapsulnya bisa berdiri di titik spawn),
    /// dan bukan benda yang bergerak dengan fisika (mobil).
    private bool IsGroundForSnap(Collider c)
    {
        if (c.transform.IsChildOf(transform)) return false;
        if (c is CharacterController || c.GetComponentInParent<PlayerController>() || c.GetComponentInParent<NpcAgent>()) return false;
        Rigidbody body = c.attachedRigidbody;
        return body == null || body.isKinematic;
    }

    /// Tertabrak mobil: putar Hit Knockback. Kalau tidak mati, lanjut LayToIdle lalu kontrol dibuka lagi.
    /// Kalau mati, tetap rebahan lalu perlahan masuk ke tanah sampai ClearKnockdown dipanggil saat bangkit.
    /// pushDirection: arah avatar terdorong. Avatar diputar menghadap sumber tabrakan (berlawanan arah dorong),
    /// supaya animasi Hit Knockback terlihat terpental menjauhi mobil.
    public void KnockDown(bool fatal, Vector3 pushDirection)
    {
        if (!IsLocal || InVehicle) return;
        Vector3 face = Vector3.ProjectOnPlane(-pushDirection, Vector3.up);
        if (face.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(face.normalized, Vector3.up);
        IsKnockedDown = true;
        knockdownFatal = fatal;
        knockdownStateSeen = false;
        knockdownTimer = 0f;
        launchPending = false;
        IsSprinting = false;
        TriggerKnockdown(animator, fatal);
        SetControlsStunned(true);
        KnockedDown?.Invoke(fatal);
    }

    /// Dipanggil saat bangkit setelah mati: model kembali ke permukaan dan Animator kembali ke idle.
    public void ClearKnockdown()
    {
        bool wasDown = IsKnockedDown;
        IsKnockedDown = false;
        knockdownFatal = false;
        ApplyModelSink(0f);
        ResetKnockdown(animator);
        if (wasDown) SetControlsStunned(false);
    }

    void Update()
    {
        UpdateKnockdown();

        if (InVehicle)
        {
            // Diam di kursi. Speed 0 + menapak supaya kembali ke Locomotion idle setelah animasi keluar.
            IsSprinting = false;
            Speed01 = 0f;
            lastGroundedTime = Time.time;
            UpdateAnimator();
            return;
        }

        if (frozen)
        {
            IsSprinting = false;
            Speed01 = 0f;
            lastGroundedTime = Time.time;
            ApplyShoveWhileFrozen();
            UpdateAnimator();
            return;
        }

        if (!cameraTransform && Camera.main) cameraTransform = Camera.main.transform;

        var input = PlayerInputHub.Instance;
        Vector2 m = input && !IsKnockedDown ? input.Move : Vector2.zero;
        Vector3 dir = new Vector3(m.x, 0f, m.y);

        if (cameraRelative && cameraTransform)
            dir = Quaternion.Euler(0f, cameraTransform.eulerAngles.y, 0f) * dir;

        dir = Vector3.ClampMagnitude(dir, 1f);

        bool moving = dir.sqrMagnitude > 0.0001f;
        IsSprinting = input && input.Sprint && moving && !wading && !IsKnockedDown;
        float speed = wading ? wadingSpeed : IsSprinting ? sprintSpeed : walkSpeed;
        Speed01 = dir.magnitude * (wading ? wadingAnimSpeed : IsSprinting ? 1f : 0.5f);

        if (moving)
        {
            Quaternion target = Quaternion.LookRotation(dir);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, turnSpeed * Time.deltaTime);
        }

        // Lompat: tunggu sebentar supaya selaras dengan animasi JumpStart, baru terangkat.
        if (launchPending)
        {
            launchTimer -= Time.deltaTime;
            if (launchTimer <= 0f)
            {
                launchPending = false;
                verticalVel = Mathf.Sqrt(2f * Mathf.Abs(gravity) * Mathf.Max(0f, jumpHeight));
            }
        }

        if (cc.isGrounded && verticalVel <= 0f)
            verticalVel = -2f;
        else
            verticalVel += gravity * Time.deltaTime;

        // Dibatasi sisa waktu dorongan supaya jaraknya tetap sama saat FPS rendah.
        Vector3 shoveStep = shoveVelocity * Mathf.Min(Mathf.Max(0f, shoveTimer), Time.deltaTime);
        shoveTimer -= Time.deltaTime;
        cc.Move((dir * speed + Vector3.up * verticalVel) * Time.deltaTime + shoveStep);

        if (cc.isGrounded) lastGroundedTime = Time.time;
        UpdateAnimator();
    }

    /// Dipanggil JumpButton.
    public void TryJump()
    {
        var input = PlayerInputHub.Instance;
        // Pakai toleransi menapak tanah: cc.isGrounded bisa false sesaat di tanjakan/anak tangga.
        if (!IsLocal || frozen || wading || IsKnockedDown || InVehicle || (input && input.MovementLocked) || !IsGroundedForAnim ||
            launchPending ||
            Time.time < nextJumpTime)
            return;

        nextJumpTime = Time.time + Mathf.Max(0f, jumpCooldown);
        launchPending = true;
        launchTimer = Mathf.Max(0f, jumpLaunchDelay);
        if (animator) animator.SetTrigger(JumpHash);
        Jumped?.Invoke();
    }

    // Hanya sisa dorongan tabrakan; sedikit tekanan ke bawah supaya tetap menempel di tanah/tanjakan.
    private void ApplyShoveWhileFrozen()
    {
        if (shoveTimer <= 0f || !cc.enabled) return;
        Vector3 shoveStep = shoveVelocity * Mathf.Min(shoveTimer, Time.deltaTime);
        shoveTimer -= Time.deltaTime;
        cc.Move(shoveStep + Vector3.down * (2f * Time.deltaTime));
    }

    void LateUpdate()
    {
        // LateUpdate supaya posisi model tidak tertimpa Animator.
        ApplyModelSink(IsKnockedDownDead ? SinkDepthAt(knockdownTimer) : 0f);
    }

    private void UpdateKnockdown()
    {
        if (!IsKnockedDown) return;
        knockdownTimer += Time.deltaTime;
        if (knockdownFatal) return;   // tetap rebahan sampai bangkit di titik spawn

        bool inState = IsInKnockdownState(animator);
        if (inState) knockdownStateSeen = true;
        bool finished = knockdownStateSeen ? !inState : knockdownTimer >= knockdownStartTimeout;
        if (finished || knockdownTimer >= knockdownMaxSeconds)
        {
            IsKnockedDown = false;
            SetControlsStunned(false);
        }
    }

    /// True selama Hit Knockback / LayToIdle masih jalan, termasuk saat transisi masuk ke sana.
    private static bool IsInKnockdownState(Animator anim)
    {
        if (!anim || !anim.isActiveAndEnabled || !anim.runtimeAnimatorController) return false;
        if (anim.GetCurrentAnimatorStateInfo(0).IsTag(KnockdownTag)) return true;
        return anim.IsInTransition(0) && anim.GetNextAnimatorStateInfo(0).IsTag(KnockdownTag);
    }

    private static void SetControlsStunned(bool stunned)
    {
        if (PlayerInputHub.Instance) PlayerInputHub.Instance.SetStunLocked(stunned);
        MobileControls.SetDimmed(stunned);
    }

    /// Kedalaman model di dalam tanah, detik ke-t sejak tertabrak sampai mati.
    public float SinkDepthAt(float secondsSinceKnockdown)
    {
        float t = (secondsSinceKnockdown - deathSinkDelay) / Mathf.Max(0.01f, deathSinkSeconds);
        return deathSinkDepth * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
    }

    /// Turunkan model (child yang punya Animator), bukan root, supaya kamera dan collider tetap di tempat.
    /// Dipakai juga oleh PlayerAnimSync untuk avatar pemain lain.
    public void ApplyModelSink(float depth)
    {
        if (!animator || animator.transform == transform || Mathf.Approximately(depth, appliedSink)) return;
        appliedSink = depth;
        animator.transform.localPosition = modelBaseLocalPos + Vector3.down * depth;
    }

    private void UpdateAnimator()
    {
        ApplyAnimator(animator, Speed01, IsGroundedForAnim, speedDampTime);
    }

    /// Dipakai juga oleh PlayerAnimSync untuk avatar pemain lain, supaya parameternya sama persis.
    public static void ApplyAnimator(Animator anim, float speed01, bool grounded, float dampTime)
    {
        if (!anim) return;
        anim.SetBool(IsGroundedHash, grounded);
        anim.SetFloat(SpeedHash, speed01, dampTime, Time.deltaTime);
    }

    public static void TriggerJump(Animator anim)
    {
        if (anim) anim.SetTrigger(JumpHash);
    }

    /// Dipakai juga oleh PlayerAnimSync untuk avatar pemain lain.
    public static void TriggerKnockdown(Animator anim, bool fatal)
    {
        if (!anim) return;
        anim.SetBool(IsDeadHash, fatal);
        anim.SetTrigger(KnockdownHash);
    }

    public static void SetKnockdownDead(Animator anim, bool dead)
    {
        if (anim) anim.SetBool(IsDeadHash, dead);
    }

    /// Kembalikan Animator ke state awal (idle). Tanpa ini, avatar yang bangkit akan memutar LayToIdle di titik spawn.
    public static void ResetKnockdown(Animator anim)
    {
        if (!anim || !anim.isActiveAndEnabled) return;
        anim.Rebind();
        anim.Update(0f);
    }

    public Animator Animator => animator;
    public float SpeedDampTime => speedDampTime;
}