using System;
using UnityEngine;

/// <summary>
/// Fisika menyetir mobil pemain, dengan rasa arcade CarMotor bawaan AllStarCharacterLibrary:
///  - gas/mundur: torsi roda penggerak + dorongan langsung ke badan mobil (cepat & responsif), saat roda menapak,
///  - gravitasi ekstra KHUSUS mobil (AllStar mengubah Physics.gravity global jadi -20.81; di sini player dan
///    fisika lain tidak ikut berubah),
///  - saat belok badan didorong ke arah belokan dan sedikit miring, makin kencang makin kuat,
///  - rem tangan (DriveInput.Handbrake): roda belakang direm dan grip sampingnya dilonggarkan -> drift,
///  - bekas ban (CarSkidmarks) saat roda selip, juga untuk mobil yang dikendarai client lain.
/// Bedanya dengan CarMotor: ada batas kecepatan, input dari DriveInput (UI mobile + keyboard), tidak mencari
/// object "Skidmarks"/"ramp" di scene, dan grip belakang kembali normal setelah rem tangan dilepas.
///
/// Tanpa pengemudi mobil direm penuh (parkir).
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class CarDriveController : MonoBehaviour {
    [Serializable]
    public class DriveWheel {
        public WheelCollider collider;
        public Transform model;
        public bool steer;
        public bool drive;
    }

    // Nilai asli CarMotor AllStar (untuk mobil 13500 kg): dorongan belok 2000 N dan miring 2000 N·m per mph.
    private const float AllStarReferenceMass = 13500f;
    private const float MetresPerSecondToMph = 2.23693629f;
    // Bagian percepatan dari torsi roda; sisanya dorongan badan. CarMotor: torsi 2 x 20000 N·m (±40%) + 200000 N.
    private const float WheelPowerShare = 0.4f;

    [SerializeField] private DriveWheel[] wheels = Array.Empty<DriveWheel>();

    [Header("Kecepatan")]
    [Tooltip("Batas kecepatan maju (km/jam).")]
    [SerializeField] private float maxSpeedKmh = 60f;
    [Tooltip("Batas kecepatan mundur (km/jam).")]
    [SerializeField] private float maxReverseKmh = 25f;
    [Tooltip("Percepatan saat gas penuh (m/s²). Badan mobil didorong langsung seperti CarMotor AllStar.")]
    [SerializeField] private float acceleration = 10f;
    [Tooltip("Perlambatan saat rem penuh (m/s²).")]
    [SerializeField] private float brakeDeceleration = 14f;
    [Tooltip("Perlambatan saat gas dan rem dilepas (m/s²), supaya mobil berhenti sendiri pelan-pelan.")]
    [SerializeField] private float coastDeceleration = 1.5f;

    [Header("Setir")]
    [Tooltip("Sudut belok roda maksimal saat pelan (derajat).")]
    [SerializeField] private float lowSpeedSteerAngle = 32f;
    [Tooltip("Sudut belok roda maksimal di batas kecepatan (derajat).")]
    [SerializeField] private float highSpeedSteerAngle = 12f;
    [Tooltip("Kecepatan roda berbelok mengikuti input (derajat/detik).")]
    [SerializeField] private float steerSpeed = 120f;
    [Tooltip("Dorongan badan ke arah belokan, pengali nilai AllStar (1 = sama dengan CarMotor). 0 = mati.")]
    [SerializeField] private float turnPush = 1f;
    [Tooltip("Badan miring saat belok, pengali nilai AllStar (1 = sama dengan CarMotor). 0 = mati.")]
    [SerializeField] private float bodyLean = 1f;
    [Tooltip("Batas kecepatan (mph) untuk dorongan & miring saat belok, supaya tidak terguling di kecepatan tinggi.")]
    [SerializeField] private float turnAssistMaxMph = 60f;

    [Header("Rem tangan (drift)")]
    [Tooltip("Pengali grip samping roda belakang selama rem tangan ditahan (AllStar: 0.5).")]
    [SerializeField, Range(0.05f, 1f)] private float driftRearGrip = 0.5f;
    [Tooltip("Perlambatan dari roda belakang yang dikunci rem tangan (m/s²).")]
    [SerializeField] private float handbrakeDeceleration = 6f;
    [Tooltip("Lama grip belakang kembali normal setelah rem tangan dilepas (detik).")]
    [SerializeField] private float gripRecoverySeconds = 0.6f;

    [Header("Kestabilan")]
    [Tooltip("Titik berat relatif ke root mobil. Rendah = tidak mudah terguling.")]
    [SerializeField] private Vector3 centerOfMass = Vector3.zero;
    [Tooltip("Pengali gravitasi khusus mobil ini (AllStar: 20.81 / 9.81 ≈ 2.12). Mobil menapak lebih mantap.")]
    [SerializeField] private float gravityMultiplier = 2.12f;
    [Tooltip("Gaya tekan ke bawah per m/s (N), supaya roda tetap menapak di kecepatan tinggi.")]
    [SerializeField] private float downforce = 600f;

    [Header("Bekas ban")]
    [Tooltip("Roda dianggap selip kalau bergeser ke samping lebih cepat dari ini (m/s).")]
    [SerializeField] private float skidSlipSpeed = 2.5f;

    private Rigidbody body;
    private float currentSteer;
    private int driveWheelCount;
    private int rearWheelCount;
    private float[] baseSideStiffness;
    private float rearGrip = 1f;   // 1 = normal, driftRearGrip = rem tangan ditahan

    // Visual roda saat mobil digerakkan dari jaringan (Rigidbody kinematic, WheelCollider tidak jalan).
    private bool simulating = true;
    private float remoteSteer;
    private float wheelSpin;
    private Vector3 lastPosition;
    private Vector3[] wheelRestPosition;
    private Quaternion[] wheelRestRotation;

    // Bekas ban (dihitung dari gerak mobil, jadi sama untuk sopir dan client lain).
    private int[] lastSkid;
    private Vector3[] lastSkidPoint;
    private Vector3 skidLastPosition;
    private Quaternion skidLastRotation;
    private static readonly RaycastHit[] GroundHits = new RaycastHit[8];

    /// <summary>Sudut belok roda depan sekarang (derajat). Dikirim CarSync ke pemain lain.</summary>
    public float SteerAngle => currentSteer;

    /// <summary>
    /// False = client lain yang menjalankan fisika mobil ini (CarSync): tidak ada torsi/rem, roda digerakkan visual saja.
    /// </summary>
    public void SetSimulating(bool value) {
        if (simulating == value) return;
        simulating = value;
        lastPosition = transform.position;
        if (!value) ResetWheelModels();
    }

    /// <summary>Sudut belok roda depan dari sopir di client lain (derajat).</summary>
    public void SetRemoteSteer(float angle) => remoteSteer = angle;

    /// <summary>True selama ada pemain di kursi sopir. Tanpa pengemudi mobil direm penuh.</summary>
    public bool HasDriver { get; private set; }

    /// <summary>Kecepatan searah depan mobil (m/s), negatif saat mundur.</summary>
    public float ForwardSpeed { get; private set; }

    public float SpeedKmh => Mathf.Abs(DriveRules.ToKmh(ForwardSpeed));
    public Rigidbody Body => body;

    /// <summary>Dipakai editor setup untuk mengisi roda dari CarMotor bawaan asset.</summary>
    public void SetWheels(DriveWheel[] value, Vector3 com) {
        wheels = value ?? Array.Empty<DriveWheel>();
        centerOfMass = com;
    }

    public void SetDriver(bool value) {
        HasDriver = value;
        if (!value) currentSteer = 0f;
    }

    void Awake() {
        body = GetComponent<Rigidbody>();
        body.centerOfMass = centerOfMass;
        driveWheelCount = 0;
        rearWheelCount = 0;
        baseSideStiffness = new float[wheels.Length];
        wheelRestPosition = new Vector3[wheels.Length];
        wheelRestRotation = new Quaternion[wheels.Length];
        lastSkid = new int[wheels.Length];
        lastSkidPoint = new Vector3[wheels.Length];
        for (int i = 0; i < wheels.Length; i++) {
            DriveWheel w = wheels[i];
            lastSkid[i] = -1;
            if (w.collider) {
                if (w.drive) driveWheelCount++;
                if (!w.steer) rearWheelCount++;
                baseSideStiffness[i] = w.collider.sidewaysFriction.stiffness;
            }
            if (!w.model) continue;
            wheelRestPosition[i] = w.model.localPosition;
            wheelRestRotation[i] = w.model.localRotation;
        }
        lastPosition = transform.position;
        skidLastPosition = transform.position;
        skidLastRotation = transform.rotation;
    }

    void FixedUpdate() {
        if (!simulating) return;
        ForwardSpeed = Vector3.Dot(body.linearVelocity, transform.forward);

        float steerInput = 0f, drive = 0f, brake = 0f;
        bool handbrake = false;
        if (HasDriver) {
            steerInput = DriveInput.Steer;
            handbrake = DriveInput.Handbrake;
            DriveRules.ResolvePedals(DriveInput.Gas, DriveInput.Brake, ForwardSpeed, out drive, out brake);
        }

        float maxForward = maxSpeedKmh / DriveRules.KmhPerMetrePerSecond;
        float maxReverse = maxReverseKmh / DriveRules.KmhPerMetrePerSecond;
        float limit = drive >= 0f ? DriveRules.LimiterFactor(ForwardSpeed, maxForward)
                                  : DriveRules.LimiterFactor(ForwardSpeed, maxReverse);

        float targetSteer = DriveRules.SteerAngle(steerInput, ForwardSpeed, maxForward, lowSpeedSteerAngle, highSpeedSteerAngle);
        currentSteer = Mathf.MoveTowards(currentSteer, targetSteer, steerSpeed * Time.fixedDeltaTime);

        // Grip belakang: langsung longgar saat rem tangan ditahan, kembali normal perlahan setelah dilepas.
        float gripTarget = handbrake ? driftRearGrip : 1f;
        rearGrip = handbrake ? gripTarget
                             : Mathf.MoveTowards(rearGrip, 1f, Time.fixedDeltaTime / Mathf.Max(0.01f, gripRecoverySeconds) * (1f - driftRearGrip));

        float mass = body.mass;
        int allWheels = Mathf.Max(1, wheels.Length);
        bool grounded = false;
        for (int i = 0; i < wheels.Length; i++) {
            DriveWheel w = wheels[i];
            if (!w.collider) continue;
            if (w.collider.isGrounded) grounded = true;
            float radius = w.collider.radius;
            float brakeTorque;

            if (!HasDriver) {
                // Parkir: rem penuh supaya tidak bisa didorong/menggelinding di tanjakan.
                brakeTorque = DriveRules.WheelTorque(mass, brakeDeceleration * 2f, radius, allWheels);
            } else if (brake > 0f) {
                brakeTorque = DriveRules.WheelTorque(mass, brakeDeceleration * brake, radius, allWheels);
            } else if (Mathf.Approximately(drive, 0f)) {
                // Tidak ada pedal: hampir diam = tahan di tempat, kalau masih jalan = melambat pelan.
                bool nearlyStopped = Mathf.Abs(ForwardSpeed) < DriveRules.StoppedSpeed;
                float decel = nearlyStopped ? brakeDeceleration : coastDeceleration;
                brakeTorque = DriveRules.WheelTorque(mass, decel, radius, allWheels);
            } else {
                brakeTorque = 0f;
            }
            if (handbrake && !w.steer)
                brakeTorque = Mathf.Max(brakeTorque, DriveRules.WheelTorque(mass, handbrakeDeceleration, radius, Mathf.Max(1, rearWheelCount)));

            // Sebagian tenaga lewat roda penggerak: tanpa torsi roda, PhysX menahan roda di kecepatan rendah
            // ("sticky tire") dan dorongan badan saja tidak bisa menggerakkan mobil dari diam.
            float motor = 0f;
            if (w.drive && brakeTorque <= 0f && !Mathf.Approximately(drive, 0f))
                motor = Mathf.Sign(drive) * DriveRules.WheelTorque(mass, acceleration * WheelPowerShare * Mathf.Abs(drive) * limit,
                                                                  radius, Mathf.Max(1, driveWheelCount));
            w.collider.motorTorque = motor;
            w.collider.brakeTorque = brakeTorque;
            if (w.steer) w.collider.steerAngle = currentSteer;

            WheelFrictionCurve side = w.collider.sidewaysFriction;
            float stiffness = baseSideStiffness[i] * (w.steer ? 1f : rearGrip);
            if (!Mathf.Approximately(side.stiffness, stiffness)) {
                side.stiffness = stiffness;
                w.collider.sidewaysFriction = side;
            }
        }

        // Gravitasi ekstra hanya untuk mobil ini (AllStar: Physics.gravity global -20.81).
        body.AddForce(Physics.gravity * (gravityMultiplier - 1f), ForceMode.Acceleration);
        body.AddForce(-transform.up * (downforce * Mathf.Abs(ForwardSpeed)));

        if (!grounded) return;

        // Gas/mundur: sisanya dorong badan langsung (CarMotor: AddForce 200000 N), dibatasi limiter kecepatan.
        if (!Mathf.Approximately(drive, 0f) && driveWheelCount > 0)
            body.AddForce(transform.forward * (drive * acceleration * (1f - WheelPowerShare) * limit), ForceMode.Acceleration);

        // Belok: dorong badan ke arah belokan dan miringkan sedikit (CarMotor: 2000 * (mph + 1)).
        if (Mathf.Abs(steerInput) > 0.01f && ForwardSpeed > DriveRules.StoppedSpeed) {
            float mph = Mathf.Min(ForwardSpeed * MetresPerSecondToMph, turnAssistMaxMph);
            float perMph = 2000f * (mass / AllStarReferenceMass) * (mph + 1f) * steerInput;
            body.AddForce(transform.right * (perMph * turnPush));
            body.AddRelativeTorque(0f, 0f, -perMph * bodyLean);   // negatif = miring ke sisi penumpang (kanan)
        }
    }

    void LateUpdate() {
        if (!simulating) UpdateRemoteWheels();
        else {
            // Visual roda mengikuti WheelCollider (putaran, belok, suspensi).
            foreach (DriveWheel w in wheels) {
                if (!w.collider || !w.model) continue;
                w.collider.GetWorldPose(out Vector3 position, out Quaternion rotation);
                w.model.SetPositionAndRotation(position, rotation);
            }
        }
        UpdateSkidmarks();
    }

    // ---------------------------------------------------------------- Bekas ban

    /// Selip dihitung dari gerak mobil antar frame (bukan WheelCollider), jadi mobil yang disetir client lain
    /// (kinematic, digerakkan CarSync) juga meninggalkan bekas ban yang sama.
    private void UpdateSkidmarks() {
        float dt = Time.deltaTime;
        Vector3 position = transform.position;
        Quaternion rotation = transform.rotation;
        Vector3 velocity = dt > 0f ? (position - skidLastPosition) / dt : Vector3.zero;
        (rotation * Quaternion.Inverse(skidLastRotation)).ToAngleAxis(out float angle, out Vector3 axis);
        if (angle > 180f) angle -= 360f;
        Vector3 angular = dt > 0f && axis.sqrMagnitude > 0f ? axis * (angle * Mathf.Deg2Rad / dt) : Vector3.zero;
        skidLastPosition = position;
        skidLastRotation = rotation;

        bool moving = velocity.sqrMagnitude > 1f && velocity.sqrMagnitude < 10000f;   // > 1 m/s, abaikan teleport
        for (int i = 0; i < wheels.Length; i++) {
            DriveWheel w = wheels[i];
            if (!w.collider) continue;
            Vector3 center = w.collider.transform.position;
            float lateral = 0f;
            if (moving) {
                Vector3 pointVelocity = velocity + Vector3.Cross(angular, center - body.worldCenterOfMass);
                Vector3 right = Quaternion.AngleAxis(w.steer ? (simulating ? currentSteer : remoteSteer) : 0f, transform.up) * transform.right;
                lateral = Mathf.Abs(Vector3.Dot(pointVelocity, right));
            }
            if (lateral < skidSlipSpeed || !TryGetGround(w.collider, center, out RaycastHit hit)) {
                lastSkid[i] = -1;
                continue;
            }
            if (lastSkid[i] != -1 && (hit.point - lastSkidPoint[i]).sqrMagnitude < 0.04f) continue;   // jarak min 20 cm
            float intensity = Mathf.Clamp01(0.35f + (lateral - skidSlipSpeed) / 6f);
            lastSkid[i] = CarSkidmarks.Add(hit.point, hit.normal, intensity, lastSkid[i], gameObject.scene);
            lastSkidPoint[i] = hit.point;
        }
    }

    private bool TryGetGround(WheelCollider wheel, Vector3 center, out RaycastHit ground) {
        float reach = wheel.radius + wheel.suspensionDistance + 0.3f;
        int count = Physics.RaycastNonAlloc(center + transform.up * 0.1f, -transform.up, GroundHits, reach + 0.1f,
                                            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        ground = default;
        float best = float.MaxValue;
        for (int i = 0; i < count; i++) {
            RaycastHit h = GroundHits[i];
            if (h.rigidbody == body || h.collider.transform.IsChildOf(transform)) continue;
            if (h.distance < best) { best = h.distance; ground = h; }
        }
        return best < float.MaxValue;
    }

    // ---------------------------------------------------------------- Jaringan (visual roda)

    /// Roda berputar sesuai jarak tempuh dan roda depan berbelok sesuai sudut dari sopir.
    private void UpdateRemoteWheels() {
        Vector3 moved = transform.position - lastPosition;
        lastPosition = transform.position;
        float dt = Mathf.Max(Time.deltaTime, 0.0001f);
        ForwardSpeed = Vector3.Dot(moved, transform.forward) / dt;
        float radius = wheels.Length > 0 && wheels[0].collider ? wheels[0].collider.radius : 0.35f;
        wheelSpin = (wheelSpin + Vector3.Dot(moved, transform.forward) / radius * Mathf.Rad2Deg) % 360f;

        for (int i = 0; i < wheels.Length; i++) {
            DriveWheel w = wheels[i];
            if (!w.model) continue;
            Quaternion steer = Quaternion.Euler(0f, w.steer ? remoteSteer : 0f, 0f);
            w.model.localPosition = wheelRestPosition[i];
            // Putar di sumbu kanan MOBIL (bukan sumbu model), karena roda kiri dan kanan bisa model yang dicerminkan.
            w.model.localRotation = steer * Quaternion.AngleAxis(wheelSpin, Vector3.right) * wheelRestRotation[i];
        }
    }

    private void ResetWheelModels() {
        if (wheelRestPosition == null) return;
        for (int i = 0; i < wheels.Length; i++) {
            if (!wheels[i].model) continue;
            wheels[i].model.localPosition = wheelRestPosition[i];
            wheels[i].model.localRotation = wheelRestRotation[i];
        }
    }
}
