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

    [Header("Collision (anti tembus tembok)")]
    [Tooltip("Layer yang dianggap penghalang kamera (bangunan, tembok, dll)")]
    [SerializeField] private LayerMask collisionMask = ~0;
    [Tooltip("Radius bola untuk cek tabrakan. Otomatis diperbesar supaya near plane kamera tidak masuk tembok.")]
    [SerializeField] private float collisionRadius = 0.2f;
    [Tooltip("Jarak sisa antara kamera dan tembok")]
    [SerializeField] private float collisionBuffer = 0.1f;
    [Tooltip("Jarak minimum kamera dari pivot (kepala player)")]
    [SerializeField] private float minDistance = 0.3f;
    [Tooltip("Kecepatan kamera mundur kembali setelah penghalang hilang (meter/detik)")]
    [SerializeField] private float returnSpeed = 6f;

    private readonly RaycastHit[] hits = new RaycastHit[32];
    private float currentDistance = -1f;   // jarak kamera aktual dari pivot (setelah collision)
    private Camera cam;

    private float yaw;
    private float pitch = 15f;

    private bool focusing;
    private Transform focusPoint;
    private float fDistance, fHeight, fSide;
    private float blend;   // 0 = kamera normal, 1 = fokus penuh

    [Header("Menyetir")]
    [Tooltip("Jeda tanpa geser layar sebelum kamera kembali ke belakang mobil (detik)")]
    [SerializeField] private float vehicleRecenterDelay = 1.2f;
    [Tooltip("Kecepatan kamera kembali ke belakang mobil")]
    [SerializeField] private float vehicleRecenterSpeed = 2.5f;
    [Tooltip("Sudut pitch kamera saat kembali ke belakang mobil")]
    [SerializeField] private float vehiclePitch = 12f;

    private Transform vehicle;
    private Rigidbody vehicleBody;
    private float vehicleDistance;
    private float vehicleHeight;
    private float lastLookTime;

    /// Kamera mengikuti mobil (dipakai VehicleOccupant selama menyetir). Target player tetap disimpan.
    public void SetVehicleView(Transform car, float followDistance, float pivotHeight)
    {
        vehicle = car;
        vehicleBody = car ? car.GetComponent<Rigidbody>() : null;
        vehicleDistance = followDistance;
        vehicleHeight = pivotHeight;
        currentDistance = -1f;
        lastLookTime = Time.time;
    }

    public void ClearVehicleView()
    {
        if (!vehicle) return;
        vehicle = null;
        vehicleBody = null;
        currentDistance = -1f;
    }

    /// Pengali jarak kamera. Di bawah 1 = zoom mendekat (dipakai saat layar mati).
    public float DistanceScale { get; set; } = 1f;

    /// Geseran kamera di sumbu layarnya, dalam meter. Dipakai untuk goyang saat benturan.
    public Vector2 ShakeOffset { get; set; }

    public void SetTarget(Transform value)
    {
        target = value;
        currentDistance = -1f;
        if (target) yaw = transform.eulerAngles.y;
    }

    void Start()
    {
        yaw = transform.eulerAngles.y;
        cam = GetComponent<Camera>();
    }

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
            if (look.sqrMagnitude > 0f) lastLookTime = Time.time;
        }
        if (vehicle) RecenterBehindVehicle();

        // Pose kamera normal (selalu dihitung, jadi saat kembali posisinya persis seperti sebelum fokus)
        Quaternion orbitRot = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 pivot;
        Vector3 orbitPos;
        if (vehicle)
        {
            pivot = vehicle.position + Vector3.up * vehicleHeight;
            orbitPos = pivot - orbitRot * Vector3.forward * (vehicleDistance * DistanceScale);
        }
        else
        {
            // Ikuti skala avatar (mis. dikecilkan di venue) supaya framing kamera sama seperti di World.
            float avatarScale = Mathf.Max(0.05f, target.localScale.y);
            pivot = target.position + pivotOffset * avatarScale;
            orbitPos = pivot - orbitRot * Vector3.forward * (distance * DistanceScale * avatarScale);
        }

        blend = Mathf.MoveTowards(blend, focusing ? 1f : 0f, Time.deltaTime / Mathf.Max(0.01f, focusBlendTime));

        if (blend <= 0f || !focusPoint)
        {
            ApplyPose(ResolveCollision(pivot, orbitPos), orbitRot);
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
        ApplyPose(
            ResolveCollision(pivot, Vector3.Lerp(orbitPos, focusPos, t)),
            Quaternion.Slerp(orbitRot, focusRot, t));
    }

    /// Setelah pemain berhenti menggeser layar, kamera pelan-pelan kembali ke belakang mobil (searah laju,
    /// termasuk saat mundur kamera tetap di belakang badan mobil).
    private void RecenterBehindVehicle()
    {
        if (Time.time - lastLookTime < vehicleRecenterDelay) return;
        float speed = vehicleBody ? vehicleBody.linearVelocity.magnitude : 0f;
        if (speed < 1f) return;   // mobil diam: biarkan pemain melihat sekeliling
        float t = 1f - Mathf.Exp(-vehicleRecenterSpeed * Time.deltaTime);
        yaw = Mathf.LerpAngle(yaw, vehicle.eulerAngles.y, t);
        pitch = Mathf.LerpAngle(pitch, vehiclePitch, t);
    }

    private void ApplyPose(Vector3 position, Quaternion rotation)
    {
        transform.SetPositionAndRotation(position + rotation * (Vector3)ShakeOffset, rotation);
    }

    /// Tarik kamera maju kalau ada penghalang antara pivot (kepala player) dan posisi kamera yang diinginkan.
    /// Maju langsung (supaya tidak pernah tembus), mundur kembali secara halus.
    private Vector3 ResolveCollision(Vector3 pivot, Vector3 desiredPos)
    {
        Vector3 offset = desiredPos - pivot;
        float maxDist = offset.magnitude;
        if (maxDist < 0.0001f) return desiredPos;
        Vector3 dir = offset / maxDist;

        float radius = Mathf.Max(collisionRadius, NearPlaneRadius());
        float allowed = maxDist;

        // Sphere cast: penghalang di sepanjang jalur kamera.
        int count = Physics.SphereCastNonAlloc(pivot, radius, dir, hits, maxDist, collisionMask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            RaycastHit h = hits[i];
            if (h.distance <= 0f || IsIgnored(h.collider)) continue;   // distance 0 = sudah overlap di titik awal, ditangani raycast di bawah
            allowed = Mathf.Min(allowed, h.distance - collisionBuffer);
        }

        // Raycast tipis: menangkap tembok yang sudah bersentuhan dengan bola di titik awal (player mepet tembok).
        count = Physics.RaycastNonAlloc(pivot, dir, hits, maxDist, collisionMask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            RaycastHit h = hits[i];
            if (IsIgnored(h.collider)) continue;
            allowed = Mathf.Min(allowed, h.distance - radius - collisionBuffer);
        }

        allowed = Mathf.Clamp(allowed, Mathf.Min(minDistance, maxDist), maxDist);

        if (currentDistance < 0f || allowed < currentDistance)
            currentDistance = allowed;   // ada penghalang: langsung maju
        else
            currentDistance = Mathf.MoveTowards(currentDistance, allowed, returnSpeed * Time.deltaTime);

        return pivot + dir * currentDistance;
    }

    /// Radius minimum supaya sudut-sudut near clip plane tidak masuk ke dalam tembok.
    private float NearPlaneRadius()
    {
        if (!cam) return 0f;
        float near = cam.nearClipPlane;
        float halfH = near * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float halfW = halfH * cam.aspect;
        return Mathf.Sqrt(near * near + halfH * halfH + halfW * halfW);
    }

    /// Player sendiri, avatar pemain lain, dan NPC tidak dianggap penghalang (supaya kamera tidak loncat-loncat).
    private bool IsIgnored(Collider c)
    {
        if (!c) return true;
        if (target && c.transform.IsChildOf(target)) return true;
        if (vehicle && c.transform.IsChildOf(vehicle)) return true;
        if (c is CharacterController) return true;
        if (c.GetComponentInParent<PlayerController>()) return true;
        if (c.GetComponentInParent<NpcController>()) return true;
        return false;
    }
}