using Normal.Realtime;
using UnityEngine;

/// <summary>
/// State mobil pemain yang dibagi ke semua client. Kode model dibuat otomatis oleh source generator Normcore
/// (partial + [RealtimeModel]).
///
/// Model ini sengaja TIDAK dimiliki siapa pun, jadi semua client boleh menulis. Tiap field hanya ditulis oleh satu
/// pihak: pose oleh client simulator (sopir), field kursi oleh pemain yang duduk di kursi itu, pose parkir oleh
/// simulator saat berhenti atau oleh client otoritas traffic saat respawn.
///
/// Client ID disimpan sebagai clientID + 1, karena clientID 0 valid dan 0 dipakai untuk "kosong".
/// </summary>
[RealtimeModel]
public partial class CarSyncModel {
    // ---- Fisika: hanya client simulator yang menjalankan fisika, client lain menginterpolasi pose.
    [RealtimeProperty(1, true, true)] private int _simulator;
    // Unreliable: hanya pose terbaru yang penting.
    [RealtimeProperty(2, false, false)] private Vector3 _position;
    [RealtimeProperty(3, false, false)] private Quaternion _rotation;
    [RealtimeProperty(4, false, false)] private Vector3 _velocity;
    // Jam room (Room.time) saat pose diambil, untuk interpolasi. Double: Room.time = detik Unix (±1,8 miliar),
    // float hanya presisi ±128 detik di angka sebesar itu.
    [RealtimeProperty(5, false, false)] private double _stamp;
    // Sudut belok roda depan (derajat), supaya roda di layar pemain lain ikut berbelok.
    [RealtimeProperty(6, false, false)] private float _steer;

    // ---- Parkir: tanpa simulator, semua client menaruh mobil tepat di sini (kinematic).
    [RealtimeProperty(7, true, false)] private Vector3 _parkedPosition;
    [RealtimeProperty(8, true, false)] private Quaternion _parkedRotation;
    // Naik tiap pose parkir ditulis (berhenti / respawn), supaya client lain tahu harus pindah.
    [RealtimeProperty(9, true, true)] private int _parkedVersion;

    // ---- Titik spawn (untuk respawn mobil yang ditinggal menghalangi jalan).
    [RealtimeProperty(10, true, false)] private Vector3 _spawnPosition;
    [RealtimeProperty(11, true, false)] private Quaternion _spawnRotation;

    // ---- Kursi 0..3: pemain (clientID + 1), fase (VehicleSeatPhase), pose duduk relatif mobil.
    [RealtimeProperty(12, true, true)] private int _seat0Client;
    [RealtimeProperty(13, true, true)] private int _seat1Client;
    [RealtimeProperty(14, true, true)] private int _seat2Client;
    [RealtimeProperty(15, true, true)] private int _seat3Client;
    [RealtimeProperty(16, true, true)] private int _seat0Phase;
    [RealtimeProperty(17, true, true)] private int _seat1Phase;
    [RealtimeProperty(18, true, true)] private int _seat2Phase;
    [RealtimeProperty(19, true, true)] private int _seat3Phase;
    [RealtimeProperty(20, true, false)] private Vector3 _seat0Pose;
    [RealtimeProperty(21, true, false)] private Vector3 _seat1Pose;
    [RealtimeProperty(22, true, false)] private Vector3 _seat2Pose;
    [RealtimeProperty(23, true, false)] private Vector3 _seat3Pose;
    [RealtimeProperty(24, true, false)] private float _seat0Yaw;
    [RealtimeProperty(25, true, false)] private float _seat1Yaw;
    [RealtimeProperty(26, true, false)] private float _seat2Yaw;
    [RealtimeProperty(27, true, false)] private float _seat3Yaw;

    // ---- Pemilik (pemain yang men-spawn lewat dealer), clientID + 1. 0 = tanpa pemilik (spawner versi lama).
    // Satu mobil per pemain: Spawn lagi menghapus mobil lama; mobil pemain yang keluar room dihapus VehicleDirector.
    [RealtimeProperty(28, true, false)] private int _ownerClient;
}

/// <summary>Fase pemain di kursi, disinkronkan supaya pemain lain memutar animasi yang sama.</summary>
public enum VehicleSeatPhase {
    Empty = 0,
    Entering = 1,
    Seated = 2,
    Exiting = 3,
}

/// <summary>Akses kursi per index (model Normcore tidak mendukung array untuk properti biasa).</summary>
public partial class CarSyncModel {
    public const int MaxSeats = 4;

    public int GetSeatClient(int seat) => seat switch {
        0 => seat0Client, 1 => seat1Client, 2 => seat2Client, 3 => seat3Client, _ => 0,
    };

    public VehicleSeatPhase GetSeatPhase(int seat) => (VehicleSeatPhase)(seat switch {
        0 => seat0Phase, 1 => seat1Phase, 2 => seat2Phase, 3 => seat3Phase, _ => 0,
    });

    public Vector3 GetSeatPose(int seat) => seat switch {
        0 => seat0Pose, 1 => seat1Pose, 2 => seat2Pose, 3 => seat3Pose, _ => Vector3.zero,
    };

    public float GetSeatYaw(int seat) => seat switch {
        0 => seat0Yaw, 1 => seat1Yaw, 2 => seat2Yaw, 3 => seat3Yaw, _ => 0f,
    };

    public void SetSeat(int seat, int clientPlusOne, VehicleSeatPhase phase) {
        switch (seat) {
            case 0: seat0Client = clientPlusOne; seat0Phase = (int)phase; break;
            case 1: seat1Client = clientPlusOne; seat1Phase = (int)phase; break;
            case 2: seat2Client = clientPlusOne; seat2Phase = (int)phase; break;
            case 3: seat3Client = clientPlusOne; seat3Phase = (int)phase; break;
        }
    }

    public void SetSeatPose(int seat, Vector3 pose, float yaw) {
        switch (seat) {
            case 0: seat0Pose = pose; seat0Yaw = yaw; break;
            case 1: seat1Pose = pose; seat1Yaw = yaw; break;
            case 2: seat2Pose = pose; seat2Yaw = yaw; break;
            case 3: seat3Pose = pose; seat3Yaw = yaw; break;
        }
    }
}
