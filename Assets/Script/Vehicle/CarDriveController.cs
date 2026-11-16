using System;
using UnityEngine;

/// <summary>
/// Fisika menyetir mobil pemain, pengganti CarMotor bawaan AllStarCharacterLibrary.
/// Tetap memakai WheelCollider + model roda dari prefab asset, tapi:
///  - tidak mengubah Physics.gravity global dan tidak mencari object "Skidmarks"/"ramp" di scene,
///  - ada batas kecepatan (maju dan mundur),
///  - input dari DriveInput (UI mobile + keyboard), bukan langsung Input.GetKey.
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

    [SerializeField] private DriveWheel[] wheels = Array.Empty<DriveWheel>();

    [Header("Kecepatan")]
    [Tooltip("Batas kecepatan maju (km/jam).")]
    [SerializeField] private float maxSpeedKmh = 60f;
    [Tooltip("Batas kecepatan mundur (km/jam).")]
    [SerializeField] private float maxReverseKmh = 15f;
    [Tooltip("Percepatan saat gas penuh (m/s²). Torsi roda dihitung dari massa Rigidbody.")]
    [SerializeField] private float acceleration = 4f;
    [Tooltip("Perlambatan saat rem penuh (m/s²).")]
    [SerializeField] private float brakeDeceleration = 9f;
    [Tooltip("Perlambatan saat gas dan rem dilepas (m/s²), supaya mobil berhenti sendiri pelan-pelan.")]
    [SerializeField] private float coastDeceleration = 1.2f;

    [Header("Setir")]
    [Tooltip("Sudut belok roda maksimal saat pelan (derajat).")]
    [SerializeField] private float lowSpeedSteerAngle = 32f;
    [Tooltip("Sudut belok roda maksimal di batas kecepatan (derajat).")]
    [SerializeField] private float highSpeedSteerAngle = 12f;
    [Tooltip("Kecepatan roda berbelok mengikuti input (derajat/detik).")]
    [SerializeField] private float steerSpeed = 120f;

    [Header("Kestabilan")]
    [Tooltip("Titik berat relatif ke root mobil. Rendah = tidak mudah terguling.")]
    [SerializeField] private Vector3 centerOfMass = Vector3.zero;
    [Tooltip("Gaya tekan ke bawah per m/s (N), supaya roda tetap menapak di kecepatan tinggi.")]
    [SerializeField] private float downforce = 600f;

    private Rigidbody body;
    private float currentSteer;
    private int driveWheelCount;

    // Visual roda saat mobil digerakkan dari jaringan (Rigidbody kinematic, WheelCollider tidak jalan).
    private bool simulating = true;
    private float remoteSteer;
    private float wheelSpin;
    private Vector3 lastPosition;
    private Vector3[] wheelRestPosition;
    private Quaternion[] wheelRestRotation;

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
        foreach (DriveWheel w in wheels)
            if (w.collider && w.drive) driveWheelCount++;
        wheelRestPosition = new Vector3[wheels.Length];
        wheelRestRotation = new Quaternion[wheels.Length];
        for (int i = 0; i < wheels.Length; i++) {
            if (!wheels[i].model) continue;
            wheelRestPosition[i] = wheels[i].model.localPosition;
            wheelRestRotation[i] = wheels[i].model.localRotation;
        }
        lastPosition = transform.position;
    }

    void FixedUpdate() {
        if (!simulating) return;
        ForwardSpeed = Vector3.Dot(body.linearVelocity, transform.forward);

        float steerInput = 0f, drive = 0f, brake = 0f;
        if (HasDriver) {
            steerInput = DriveInput.Steer;
            DriveRules.ResolvePedals(DriveInput.Gas, DriveInput.Brake, ForwardSpeed, out drive, out brake);
        }

        float maxForward = maxSpeedKmh / DriveRules.KmhPerMetrePerSecond;
        float maxReverse = maxReverseKmh / DriveRules.KmhPerMetrePerSecond;
        float limit = drive >= 0f ? DriveRules.LimiterFactor(ForwardSpeed, maxForward)
                                  : DriveRules.LimiterFactor(ForwardSpeed, maxReverse);

        float targetSteer = DriveRules.SteerAngle(steerInput, ForwardSpeed, maxForward, lowSpeedSteerAngle, highSpeedSteerAngle);
        currentSteer = Mathf.MoveTowards(currentSteer, targetSteer, steerSpeed * Time.fixedDeltaTime);

        float mass = body.mass;
        int allWheels = Mathf.Max(1, wheels.Length);
        foreach (DriveWheel w in wheels) {
            if (!w.collider) continue;
            float radius = w.collider.radius;
            float motor = 0f, brakeTorque;

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
                if (w.drive) motor = Mathf.Sign(drive) * DriveRules.WheelTorque(mass, acceleration * Mathf.Abs(drive) * limit, radius, driveWheelCount);
            }

            w.collider.motorTorque = motor;
            w.collider.brakeTorque = brakeTorque;
            if (w.steer) w.collider.steerAngle = currentSteer;
        }

        body.AddForce(-transform.up * (downforce * Mathf.Abs(ForwardSpeed)));
    }

    void LateUpdate() {
        if (!simulating) {
            UpdateRemoteWheels();
            return;
        }
        // Visual roda mengikuti WheelCollider (putaran, belok, suspensi).
        foreach (DriveWheel w in wheels) {
            if (!w.collider || !w.model) continue;
            w.collider.GetWorldPose(out Vector3 position, out Quaternion rotation);
            w.model.SetPositionAndRotation(position, rotation);
        }
    }

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
