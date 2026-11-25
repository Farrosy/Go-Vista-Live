using System;
using UnityEngine;

/// <summary>
/// Kamera sinematik kursi (kursi barber). Hanya untuk avatar lokal, tidak ada yang disinkronkan.
///
///  • Tekan "Duduk": kamera orbit berayun dalam busur mengelilingi kursi ke bidikan pertama (A, depan pemain dari
///    arah cermin). Kalau kamera mulai dari belakang kursi, busur lewat sisi <see cref="swingVia"/> (C, sisi
///    jendela), bukan sisi tempat avatar berjalan masuk. Kamera sampai lebih dulu, lalu avatar masuk ke bidikan.
///  • Duduk diam: kamera terkunci (geser layar diabaikan). Tiap bidikan di <see cref="shots"/> bergerak pelan
///    (<see cref="moveSeconds"/>) dari From ke To, diam sebentar (<see cref="stopSeconds"/>), lalu dipotong ke bidikan
///    berikutnya: A → C → D → A ...
///  • Editor rambut / jenggot memanggil <see cref="SetEditorShot"/>(true): kamera meluncur ke versi A yang di-zoom
///    dan diam di sana sampai editor ditutup; setelah itu siklus mulai lagi dari A.
///  • Tekan "Berdiri": kamera berayun kembali lewat busur yang sama ke pose orbit normal.
///
/// Busur = koordinat silinder di sekitar <see cref="Pivot"/> (yaw, jarak datar, tinggi). Jarak menyusut lebih dulu
/// (masuk, lalu memutar) dan dijepit oleh tembok di sepanjang jalan; collider kursi + meja cermin diabaikan, jadi
/// bidikan A boleh berada di dalam meja cermin (bagian belakangnya tidak dirender).
/// Dipasang oleh Go Vista > Interior > Pasang Kursi Barber (BarberChairBuilder).
/// </summary>
[RequireComponent(typeof(ChairSeat))]
public sealed class ChairSeatCamera : MonoBehaviour, CameraOrbit.ICameraShot {
    private const float AmbiguousYaw = 135f;   // selisih yaw di atas ini = "di belakang kursi": lewat sisi swingVia
    private const float FrontalYaw = 30f;      // tujuan sedekat ini ke bidikan pertama = "bidikan depan"
    private const float RadiusLead = 0.7f;     // jarak busur sudah sampai di bidikan pada 70% ayunan
    private const float ClampRampIn = 0.15f;   // jepit tembok mulai bekerja penuh setelah 15% ayunan
    private const float ClampRadius = 0.12f;
    private const float ClampBuffer = 0.08f;
    private const float MinRadius = 0.4f;

    /// Satu bidikan diam: kamera bergerak pelan dari From ke To (sumbu biru = arah pandang).
    [Serializable]
    public struct Shot {
        [Tooltip("Awal gerak. Bidikan pertama: ayunan masuk mendarat di sini.")]
        public Transform from;
        [Tooltip("Akhir gerak (diam di sini sebelum dipotong). Kosong = bidikan diam di From.")]
        public Transform to;
        public float fieldOfView;
    }

    [Tooltip("Urutan bidikan saat duduk diam (dipotong berurutan, lalu kembali ke awal). [0] = A (depan).")]
    [SerializeField] private Shot[] shots = new Shot[0];
    [Tooltip("Bidikan di sisi yang dilewati ayunan kalau kamera mulai dari belakang kursi (C, sisi jendela).")]
    [SerializeField] private Transform swingVia;
    [Tooltip("Bidikan editor rambut (versi A yang di-zoom). Kosong = pakai awal bidikan pertama dengan FOV editor.")]
    [SerializeField] private Transform editorShot;
    [SerializeField] private float editorFov = 38f;
    [Tooltip("Pusat busur: tinggi di atas Seat (meter), kira-kira dada/leher pemain yang duduk.")]
    [SerializeField] private float pivotHeight = 1.0f;
    [Tooltip("Lama ayunan orbit ↔ kursi (detik).")]
    [SerializeField] private float swingSeconds = 1.5f;
    [Tooltip("Lama luncuran ke/dari bidikan editor (detik).")]
    [SerializeField] private float editorSeconds = 0.6f;
    [Tooltip("Lama gerak pelan tiap bidikan saat duduk diam (detik).")]
    [SerializeField] private float moveSeconds = 5f;
    [Tooltip("Lama diam di akhir gerak sebelum dipotong ke bidikan berikutnya (detik).")]
    [SerializeField] private float stopSeconds = 1.5f;

    private ChairSeat chair;
    private CameraOrbit orbit;
    private bool active;          // ingin di bidikan kursi (dari tekan Duduk sampai tekan Berdiri)
    private float swing;          // 0 = orbit normal, 1 = bidikan kursi
    private bool seated;          // duduk diam: potongan antar bidikan berjalan
    private int shotIndex;        // bidikan diam sekarang
    private float shotTime;       // detik sejak bidikan sekarang dimulai (gerak, lalu diam)
    private bool editor;
    private float editorBlend;    // 0 = bidikan diam, 1 = bidikan editor
    private bool travelLocked;
    private float yawTravel;      // derajat dari yaw orbit ke yaw bidikan (arah ayunan dikunci di awal ayunan)

    /// True selama kamera dipegang kursi ini.
    public bool IsActive => orbit != null;

    private Vector3 Pivot => chair && chair.Seat ? chair.Seat.position + Vector3.up * pivotHeight : transform.position;

    void Awake() => chair = GetComponent<ChairSeat>();

    void OnEnable() {
        chair.SitStarted += OnSitStarted;
        chair.Seated += OnSeated;
        chair.StandingUp += OnLeaving;
        chair.Left += OnLeaving;
    }

    void OnDisable() {
        chair.SitStarted -= OnSitStarted;
        chair.Seated -= OnSeated;
        chair.StandingUp -= OnLeaving;
        chair.Left -= OnLeaving;
        ReleaseImmediate();
    }

    /// Editor rambut / jenggot dibuka (true) atau ditutup (false). Hanya berlaku selama kamera dipegang kursi.
    public void SetEditorShot(bool on) {
        if (!IsActive) return;
        editor = on && active;
    }

    /// Kamera sudah diam penuh di bidikan editor (luncuran selesai).
    public bool EditorShotSettled => IsActive && editor && editorBlend >= 1f;

    /// Kamera sudah kembali dari bidikan editor ke bidikan diam (atau tidak dipegang kursi lagi).
    public bool EditorShotReleased => !IsActive || (!editor && editorBlend <= 0f);

    /// Pose + FOV bidikan editor (sama dengan pose kamera utama saat luncuran selesai). Dipakai editor rambut
    /// supaya kamera preview melihat persis dari tempat yang sama.
    public bool TryGetEditorPose(out Pose pose, out float fieldOfView) {
        Transform zoom = editorShot ? editorShot : shots.Length > 0 ? shots[0].from : null;
        pose = zoom ? new Pose(zoom.position, zoom.rotation) : default;
        fieldOfView = editorFov;
        return zoom;
    }

    // ---------------------------------------------------------------- Event kursi

    private void OnSitStarted() {
        if (shots.Length == 0 || !shots[0].from) {
            Debug.LogWarning($"[ChairSeatCamera] '{name}' belum punya bidikan, kamera tidak dipindah.");
            return;
        }
        Camera main = Camera.main;
        CameraOrbit found = main ? main.GetComponent<CameraOrbit>() : null;
        if (!found) return;
        orbit = found;
        orbit.SetShot(this);
        active = true;
        seated = false;
        shotIndex = 0;
        shotTime = 0f;
        editor = false;
        editorBlend = 0f;
        if (swing <= 0f) travelLocked = false;
    }

    private void OnSeated() {
        seated = true;
        // Gerak A pertama mungkin sudah selesai sebelum avatar duduk: beri jeda diam penuh sebelum potong pertama.
        shotTime = Mathf.Min(shotTime, moveSeconds);
    }

    private void OnLeaving() {
        if (!active) return;
        active = false;
        seated = false;
        editor = false;
        if (swing >= 1f) travelLocked = false;   // arah ayunan pulang dihitung dari bidikan sekarang
    }

    private void ReleaseImmediate() {
        if (orbit) orbit.ClearShot(this);
        orbit = null;
        active = seated = editor = false;
        swing = editorBlend = 0f;
        travelLocked = false;
    }

    // ---------------------------------------------------------------- Frame

    void Update() {
        if (!orbit) return;
        float dt = Time.deltaTime;
        swing = Mathf.MoveTowards(swing, active ? 1f : 0f, dt / Mathf.Max(0.01f, swingSeconds));
        editorBlend = Mathf.MoveTowards(editorBlend, editor ? 1f : 0f, dt / Mathf.Max(0.01f, editorSeconds));

        if (swing >= 1f) travelLocked = false;
        if (editor && editorBlend >= 1f) {
            // Sudah penuh di bidikan editor: setelah editor ditutup meluncur kembali ke awal A, lalu siklus berlanjut.
            shotIndex = 0;
            shotTime = 0f;
        }
        // Siklus diam: gerak pelan → diam → potong ke bidikan berikutnya → gerak ... Mulai begitu ayunan sampai
        // (avatar masih berjalan masuk / duduk), tapi potong pertama menunggu avatar duduk. Berhenti selama editor.
        if (active && swing >= 1f && !editor && editorBlend <= 0f) {
            shotTime += dt;
            if (seated && shots.Length > 1 && shotTime >= moveSeconds + stopSeconds) {
                shotTime = 0f;
                shotIndex = NextUsable(shotIndex);   // potong langsung (tanpa luncuran)
            }
        }

        if (!active && swing <= 0f) {
            orbit.ClearShot(this);
            orbit = null;
            travelLocked = false;
        }
    }

    /// Dipanggil CameraOrbit tiap LateUpdate dengan pose orbit normal (sudah termasuk collision).
    void CameraOrbit.ICameraShot.Resolve(ref Vector3 position, ref Quaternion rotation, ref float fieldOfView,
                                         Vector3 orbitPivot) {
        ShotPose(out Vector3 shotPos, out Quaternion shotRot, out float shotFov);
        if (swing >= 1f) {
            position = shotPos;
            rotation = shotRot;
            fieldOfView = shotFov;
            return;
        }

        float t = Mathf.SmoothStep(0f, 1f, swing);
        float tRadius = Mathf.SmoothStep(0f, 1f, swing / RadiusLead);
        Vector3 pivot = Pivot;
        Vector3 from = position - pivot;
        Vector3 to = shotPos - pivot;
        float fromYaw = Yaw(from), toYaw = Yaw(to);
        LockTravel(fromYaw, toYaw);

        float yaw = fromYaw + yawTravel * t;
        Vector3 dir = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
        float radius = Mathf.Lerp(FlatLength(from), FlatLength(to), tRadius);
        float height = Mathf.Lerp(from.y, to.y, t);
        radius = Mathf.Lerp(radius, ClampToWalls(pivot, dir, radius, height), Mathf.SmoothStep(0f, 1f, swing / ClampRampIn));
        Vector3 arcPos = pivot + dir * radius + Vector3.up * height;

        // Orbit melihat ke kepala pemain, bidikan melihat sepanjang sumbu birunya: titik lihat meluncur di antaranya.
        Vector3 lookTo = shotPos + shotRot * Vector3.forward * Mathf.Max(0.5f, to.magnitude);
        Vector3 look = Vector3.Lerp(orbitPivot, lookTo, t) - arcPos;
        if (look.sqrMagnitude > 0.0001f) rotation = Quaternion.LookRotation(look, Vector3.up);
        position = arcPos;
        fieldOfView = Mathf.Lerp(fieldOfView, shotFov, t);
    }

    /// Pose kamera di kursi sekarang: bidikan diam (di titik geraknya) dicampur dengan bidikan editor.
    private void ShotPose(out Vector3 position, out Quaternion rotation, out float fov) {
        int index = Mathf.Clamp(shotIndex, 0, shots.Length - 1);
        Shot shot = shots[index].from ? shots[index] : shots[0];
        Transform to = shot.to ? shot.to : shot.from;
        float move = Mathf.SmoothStep(0f, 1f, shotTime / Mathf.Max(0.01f, moveSeconds));
        position = Vector3.Lerp(shot.from.position, to.position, move);
        rotation = Quaternion.Slerp(shot.from.rotation, to.rotation, move);
        fov = shot.fieldOfView > 1f ? shot.fieldOfView : 60f;
        if (editorBlend <= 0f) return;
        Transform zoom = editorShot ? editorShot : shots[0].from;
        float t = Mathf.SmoothStep(0f, 1f, editorBlend);
        position = Vector3.Lerp(position, zoom.position, t);
        rotation = Quaternion.Slerp(rotation, zoom.rotation, t);
        fov = Mathf.Lerp(fov, editorFov, t);
    }

    private int NextUsable(int index) {
        for (int i = 1; i <= shots.Length; i++) {
            int next = (index + i) % shots.Length;
            if (shots[next].from) return next;
        }
        return 0;
    }

    /// Arah ayunan dipilih sekali di awal ayunan (supaya ayunan pulang menelusuri busur yang sama), lalu diikuti
    /// tanpa lompatan 360° walau pose orbit bergeser sedikit (avatar masih berjalan).
    private void LockTravel(float fromYaw, float toYaw) {
        float raw = Mathf.DeltaAngle(fromYaw, toYaw);
        if (travelLocked) {
            yawTravel = raw + 360f * Mathf.Round((yawTravel - raw) / 360f);
            return;
        }
        travelLocked = true;
        yawTravel = raw;
        if (Mathf.Abs(raw) < AmbiguousYaw || !swingVia) return;
        // Hanya untuk tujuan di depan (A / editor): bidikan samping didatangi lewat sisinya sendiri (rute terpendek).
        float frontYaw = Yaw(shots[0].from.position - Pivot);
        if (Mathf.Abs(Mathf.DeltaAngle(frontYaw, toYaw)) > FrontalYaw) return;
        // Kamera di belakang kursi: datang ke depan dari sisi swingVia (sisi tempat avatar berjalan masuk tetap bersih).
        float viaSide = Mathf.DeltaAngle(Yaw(swingVia.position - Pivot), toYaw);
        if (Mathf.Abs(viaSide) > 1f && Mathf.Sign(viaSide) != Mathf.Sign(raw)) yawTravel = raw - 360f * Mathf.Sign(raw);
    }

    /// Jarak datar maksimum sebelum menabrak tembok/rak, dari pusat busur ke arah <paramref name="dir"/>.
    private float ClampToWalls(Vector3 pivot, Vector3 dir, float radius, float height) {
        Vector3 offset = dir * radius + Vector3.up * height;
        float length = offset.magnitude;
        if (length < 0.001f) return radius;
        RaycastHit[] hits = Physics.SphereCastAll(pivot, ClampRadius, offset / length, length, ~0, QueryTriggerInteraction.Ignore);
        float allowed = length;
        foreach (RaycastHit hit in hits) {
            if (hit.distance <= 0f || IsIgnored(hit.collider)) continue;
            allowed = Mathf.Min(allowed, hit.distance - ClampBuffer);
        }
        if (allowed >= length) return radius;
        return Mathf.Max(MinRadius, radius * Mathf.Max(0f, allowed) / length);
    }

    private bool IsIgnored(Collider c) {
        if (!c || c.transform.IsChildOf(transform)) return true;
        foreach (Collider own in chair.ChairColliders)
            if (own == c) return true;
        if (c is CharacterController) return true;
        if (c.GetComponentInParent<PlayerController>()) return true;
        if (c.GetComponentInParent<NpcController>()) return true;
        return false;
    }

    private static float Yaw(Vector3 v) => Mathf.Atan2(v.x, v.z) * Mathf.Rad2Deg;

    private static float FlatLength(Vector3 v) => new Vector2(v.x, v.z).magnitude;
}
