using UnityEngine;

/// Pasang di Player (butuh CharacterController, otomatis ditambah).
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [SerializeField] private float walkSpeed = 3f;
    [SerializeField] private float sprintSpeed = 6f;
    [SerializeField] private float turnSpeed = 720f;
    [SerializeField] private float gravity = -20f;
    [SerializeField] private float jumpHeight = 1.5f;
    [SerializeField] private float jumpCooldown = 0.5f;

    [Tooltip("ON = joystick atas = maju searah kamera. OFF = joystick atas = selalu +Z dunia, kamera tidak ngaruh.")]
    [SerializeField] private bool cameraRelative = true;
    [SerializeField] private Transform cameraTransform;   // kosong = pakai Camera.main

    public bool IsSprinting { get; private set; }
    public float Speed01 { get; private set; }            // 0..1 buat Animator nanti

    private CharacterController cc;
    private float verticalVel;
    private float nextJumpTime;

    void Awake()
    {
        cc = GetComponent<CharacterController>();
        if (!cameraTransform && Camera.main) cameraTransform = Camera.main.transform;
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

        if (cc.isGrounded && verticalVel <= 0f)
            verticalVel = -2f;
        else
            verticalVel += gravity * Time.deltaTime;
        cc.Move((dir * speed + Vector3.up * verticalVel) * Time.deltaTime);
    }

    public void TryJump()
    {
        var input = PlayerInputHub.Instance;
        if ((input && input.InputLocked) || !cc.isGrounded || Time.time < nextJumpTime)
            return;

        verticalVel = Mathf.Sqrt(2f * Mathf.Abs(gravity) * Mathf.Max(0f, jumpHeight));
        nextJumpTime = Time.time + Mathf.Max(0f, jumpCooldown);
    }
}
