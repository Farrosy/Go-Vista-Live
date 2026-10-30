using UnityEngine;

/// Pasang di Player (root, butuh CharacterController, otomatis ditambah).
/// Model karakter (yang punya Animator) jadi CHILD dari object ini.
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

    public bool IsSprinting { get; private set; }
    public float Speed01 { get; private set; }   // 0 = diam, 0.5 = jalan, 1 = sprint

    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int IsGroundedHash = Animator.StringToHash("IsGrounded");
    private static readonly int JumpHash = Animator.StringToHash("Jump");

    private CharacterController cc;
    private float verticalVel;
    private float nextJumpTime;
    private bool launchPending;
    private float launchTimer;
    private float lastGroundedTime;

    void Awake()
    {
        cc = GetComponent<CharacterController>();
        if (!cameraTransform && Camera.main) cameraTransform = Camera.main.transform;
        if (!animator) animator = GetComponentInChildren<Animator>();
        if (animator) animator.applyRootMotion = false;   // gerak dikendalikan script, bukan animasi
        lastGroundedTime = Time.time;
    }

    void Update()
    {
        var input = PlayerInputHub.Instance;
        Vector2 m = input.Move;
        Vector3 dir = new Vector3(m.x, 0f, m.y);

        if (cameraRelative && cameraTransform)
            dir = Quaternion.Euler(0f, cameraTransform.eulerAngles.y, 0f) * dir;

        dir = Vector3.ClampMagnitude(dir, 1f);

        bool moving = dir.sqrMagnitude > 0.0001f;
        IsSprinting = input.Sprint && moving;
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
        if ((input && input.InputLocked) || !cc.isGrounded || launchPending || Time.time < nextJumpTime)
            return;

        nextJumpTime = Time.time + Mathf.Max(0f, jumpCooldown);
        launchPending = true;
        launchTimer = Mathf.Max(0f, jumpLaunchDelay);
        if (animator) animator.SetTrigger(JumpHash);
    }

    private void UpdateAnimator()
    {
        if (!animator) return;
        bool animGrounded = Time.time - lastGroundedTime <= groundedGrace;
        animator.SetBool(IsGroundedHash, animGrounded);
        animator.SetFloat(SpeedHash, Speed01, speedDampTime, Time.deltaTime);
    }
}