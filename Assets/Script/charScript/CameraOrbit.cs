using UnityEngine;

/// Pasang di Main Camera. Free look: kamera muter sendiri, tidak memutar karakter.
public class CameraOrbit : MonoBehaviour
{
    [SerializeField] private Transform target;            // drag Player
    [SerializeField] private Vector3 pivotOffset = new Vector3(0, 1.6f, 0);
    [SerializeField] private float distance = 4f;
    [SerializeField] private float sensitivity = 0.12f;   // derajat per pixel
    [SerializeField] private float minPitch = -25f;
    [SerializeField] private float maxPitch = 70f;

    private float yaw;
    private float pitch = 15f;

    void Start() => yaw = transform.eulerAngles.y;

    void LateUpdate()
    {
        if (!target) return;

        Vector2 look = PlayerInputHub.Instance.ConsumeLook();
        yaw += look.x * sensitivity;
        pitch = Mathf.Clamp(pitch - look.y * sensitivity, minPitch, maxPitch);

        Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 pivot = target.position + pivotOffset;

        transform.rotation = rot;
        transform.position = pivot - rot * Vector3.forward * distance;
    }
}