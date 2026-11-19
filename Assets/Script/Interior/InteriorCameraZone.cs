using UnityEngine;

/// Kamera interior bangunan (gaya Persona / diorama Synty). Pasang satu di root scene interior.
///
/// Selama komponen ini aktif (scene interior ter-load), CameraOrbit berhenti mengorbit dan memakai
/// pose ini: rotasi TETAP (pitch + yaw), posisi mengikuti pemain, titik fokus dijepit ke kotak
/// followArea supaya kamera tidak keluar dari ruangan. Input geser layar diabaikan.
/// Latar = warna polos gelap ("void"), jadi ruangan terlihat seperti diorama mengambang.
///
/// Karena kontrol PlayerController relatif terhadap yaw kamera, yaw tetap = arah stik selalu konsisten.
/// Yaw diukur relatif terhadap rotasi Y objek ini, jadi memutar root interior ikut memutar kamera.
[DisallowMultipleComponent]
public class InteriorCameraZone : MonoBehaviour
{
    [Header("Sudut kamera")]
    [Tooltip("Kemiringan ke bawah (derajat). 90 = tegak lurus dari atas.")]
    [Range(20f, 89f)]
    [SerializeField] private float pitch = 48f;
    [Tooltip("Arah hadap kamera relatif terhadap rotasi Y objek ini (derajat).")]
    [SerializeField] private float yaw = 120f;
    [Tooltip("Field of view kamera selama di interior. Kecil = lebih 'diorama', perspektif lebih datar.")]
    [Range(10f, 60f)]
    [SerializeField] private float fieldOfView = 28f;
    [Tooltip("Jarak kamera dari titik fokus (meter).")]
    [SerializeField] private float distance = 13f;
    [Tooltip("Tinggi titik fokus di atas kaki pemain (meter).")]
    [SerializeField] private float focusHeight = 0.9f;

    [Header("Mengikuti pemain")]
    [Tooltip("Kotak (lokal, XZ) tempat titik fokus boleh bergerak. Pemain di luar kotak = kamera berhenti di tepi. " +
             "0 di sebuah sumbu = kamera terkunci di tengah sumbu itu.")]
    [SerializeField] private Vector3 followAreaCenter = Vector3.zero;
    [SerializeField] private Vector2 followAreaSize = new Vector2(3f, 2f);
    [Tooltip("Kecepatan kamera menyusul pemain. Besar = lebih kaku.")]
    [SerializeField] private float followSharpness = 6f;

    [Header("Latar")]
    [SerializeField] private Color voidColor = new Color(0.07f, 0.07f, 0.08f);

    /// Zona interior yang sedang aktif (null = di luar ruangan, kamera orbit biasa).
    public static InteriorCameraZone Active { get; private set; }

    public Quaternion Rotation => Quaternion.Euler(pitch, transform.eulerAngles.y + yaw, 0f);
    public float FieldOfView => fieldOfView;
    public float Distance => distance;
    public float FollowSharpness => followSharpness;
    public Color VoidColor => voidColor;

    void OnEnable() => Active = this;

    void OnDisable()
    {
        if (Active == this) Active = null;
    }

    /// Titik fokus kamera untuk posisi kaki pemain: naikkan setinggi focusHeight, lalu jepit ke followArea.
    public Vector3 FocusFor(Vector3 feet, float avatarScale)
    {
        Vector3 local = transform.InverseTransformPoint(feet);
        Vector3 c = followAreaCenter;
        float hx = Mathf.Max(0f, followAreaSize.x) * 0.5f;
        float hz = Mathf.Max(0f, followAreaSize.y) * 0.5f;
        local.x = Mathf.Clamp(local.x, c.x - hx, c.x + hx);
        local.z = Mathf.Clamp(local.z, c.z - hz, c.z + hz);
        Vector3 world = transform.TransformPoint(local);
        world.y = feet.y + focusHeight * avatarScale;
        return world;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Active = null;

    void OnDrawGizmosSelected()
    {
        // Kotak followArea (kuning) + arah pandang kamera dari tengah kotak (cyan).
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(followAreaCenter + Vector3.up * focusHeight,
            new Vector3(followAreaSize.x, 0.05f, followAreaSize.y));
        Gizmos.matrix = Matrix4x4.identity;

        Vector3 focus = transform.TransformPoint(followAreaCenter) + Vector3.up * focusHeight;
        Vector3 camPos = focus - Rotation * Vector3.forward * distance;
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(camPos, focus);
        Gizmos.DrawWireSphere(camPos, 0.3f);
    }
}
