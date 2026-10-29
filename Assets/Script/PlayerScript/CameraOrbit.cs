using UnityEngine;

/// Pasang di Main Camera. Free look + bisa fokus ke titik tertentu (NPC) lalu kembali halus.
public class CameraOrbit : MonoBehaviour
{
    [SerializeField] private Transform target;            // drag Player
    [SerializeField] private Vector3 pivotOffset = new Vector3(0, 1.6f, 0);
    [SerializeField] private float distance = 4f;
    [SerializeField] private float sensitivity = 0.12f;   // derajat per pixel
    [SerializeField] private float minPitch = -25f;
    [SerializeField] private float maxPitch = 70f;

    [Header("Fokus (dialog)")]
    [Tooltip("Lama transisi ke/dari fokus, detik")]
    [SerializeField] private float focusBlendTime = 0.6f;

    private float yaw;
    private float pitch = 15f;

    private bool focusing;
    private Transform focusPoint;
    private float fDistance, fHeight, fSide;
    private float blend;   // 0 = kamera normal, 1 = fokus penuh

    public void SetTarget(Transform value)
    {
        target = value;
        if (target) yaw = transform.eulerAngles.y;
    }

    void Start() => yaw = transform.eulerAngles.y;

    public void FocusOn(Transform point, float dist, float height, float side)
    {
        focusPoint = point;
        fDistance = dist;
        fHeight = height;
        fSide = side;
        focusing = true;
    }

    public void ReleaseFocus() => focusing = false;

    void LateUpdate()
    {
        if (!target) return;

        Vector2 look = PlayerInputHub.Instance ? PlayerInputHub.Instance.ConsumeLook() : Vector2.zero;
        if (!focusing)
        {
            float lookSpeed = sensitivity * GameSettings.CameraSensitivity; // pengali dari menu Pengaturan
            yaw += look.x * lookSpeed;
            pitch = Mathf.Clamp(pitch - look.y * lookSpeed, minPitch, maxPitch);
        }

        // Pose kamera normal (selalu dihitung, jadi saat kembali posisinya persis seperti sebelum fokus)
        Quaternion orbitRot = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 pivot = target.position + pivotOffset;
        Vector3 orbitPos = pivot - orbitRot * Vector3.forward * distance;

        blend = Mathf.MoveTowards(blend, focusing ? 1f : 0f, Time.deltaTime / Mathf.Max(0.01f, focusBlendTime));

        if (blend <= 0f || !focusPoint)
        {
            transform.SetPositionAndRotation(orbitPos, orbitRot);
            return;
        }

        // Pose fokus: kamera di sisi player, menghadap titik fokus NPC
        Vector3 fp = focusPoint.position;
        Vector3 away = target.position - fp;
        away.y = 0f;
        away = away.sqrMagnitude < 0.001f ? -focusPoint.forward : away.normalized;

        Vector3 focusPos = fp + away * fDistance
                              + Vector3.Cross(Vector3.up, away) * fSide
                              + Vector3.up * fHeight;
        Quaternion focusRot = Quaternion.LookRotation(fp - focusPos);

        float t = Mathf.SmoothStep(0f, 1f, blend);
        transform.SetPositionAndRotation(
            Vector3.Lerp(orbitPos, focusPos, t),
            Quaternion.Slerp(orbitRot, focusRot, t));
    }
}