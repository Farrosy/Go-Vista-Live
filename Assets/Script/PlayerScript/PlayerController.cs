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

    [Header("Lompat")]
    [SerializeField] private float jumpHeight = 1.5f;
    [SerializeField] private float jumpCooldown = 0.5f;
    [Tooltip("Jeda dari animasi JumpStart mulai sampai karakter benar-benar terangkat (detik). " +
             "Naikkan kalau JumpStart punya gerakan menekuk lutut yang panjang.")]
    [SerializeField] private float jumpLaunchDelay = 0.1f;

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

    /// Avatar yang dikendalikan client ini. Null sebelum spawn / setelah keluar world.
    public static PlayerController Local { get; private set; }

    public bool IsLocal { get; private set; }
    public bool IsSprinting { get; private set; }
    public float Speed01 { get; private set; }   // 0 = diam, 0.5 = jalan, 1 = sprint
    public bool IsGroundedForAnim => Time.time - lastGroundedTime <= groundedGrace;
    /// Dipanggil saat lompat dimulai (dipakai PlayerAnimSync supaya pemain lain ikut melihat).
    public event Action Jumped;

    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int IsGroundedHash = Animator.StringToHash("IsGrounded");
    private static readonly int JumpHash = Animator.StringToHash("Jump");

    private CharacterController cc;
    private float verticalVel;
    private float nextJumpTime;
    private bool launchPending;
    private float launchTimer;
    private float lastGroundedTime;

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
        lastGroundedTime = Time.time;
    }

    void Start()
    {
        var view = GetComponent<RealtimeView>();
        IsLocal = view == null || view.isOwnedLocallyInHierarchy;

        if (!IsLocal)
        {
            // Avatar pemain lain: tidak membaca input. CharacterController tetap aktif sebagai
            // collider supaya pemain tidak saling tembus.
            enabled = false;
            return;
        }

        var realtimeTransform = GetComponent<RealtimeTransform>();
        if (realtimeTransform) realtimeTransform.RequestOwnership();

        Local = this;
    }

    void OnDestroy()
    {
        if (Local == this) Local = null;
    }

    /// Kamera yang dipakai untuk arah gerak. Dipanggil PlayerManager saat spawn.
    public void SetCamera(Transform cam) => cameraTransform = cam;

    void Update()
    {
        if (!cameraTransform && Camera.main) cameraTransform = Camera.main.transform;

        var input = PlayerInputHub.Instance;
        Vector2 m = input ? input.Move : Vector2.zero;
        Vector3 dir = new Vector3(m.x, 0f, m.y);

        if (cameraRelative && cameraTransform)
            dir = Quaternion.Euler(0f, cameraTransform.eulerAngles.y, 0f) * dir;

        dir = Vector3.ClampMagnitude(dir, 1f);

        bool moving = dir.sqrMagnitude > 0.0001f;
        IsSprinting = input && input.Sprint && moving;
        float speed = IsSprinting ? sprintSpeed : walkSpeed;
        Speed01 = dir.magnitude * (IsSprinting ? 1f : 0.5f);

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

        cc.Move((dir * speed + Vector3.up * verticalVel) * Time.deltaTime);

        if (cc.isGrounded) lastGroundedTime = Time.time;
        UpdateAnimator();
    }

    /// Dipanggil JumpButton.
    public void TryJump()
    {
        var input = PlayerInputHub.Instance;
        if (!IsLocal || (input && input.InputLocked) || !cc.isGrounded || launchPending || Time.time < nextJumpTime)
            return;

        nextJumpTime = Time.time + Mathf.Max(0f, jumpCooldown);
        launchPending = true;
        launchTimer = Mathf.Max(0f, jumpLaunchDelay);
        if (animator) animator.SetTrigger(JumpHash);
        Jumped?.Invoke();
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

    public Animator Animator => animator;
    public float SpeedDampTime => speedDampTime;
}