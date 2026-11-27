using UnityEngine;

/// <summary>
/// Memutar mobil pajangan di podium tengah showroom Flamingo pelan-pelan (seperti turntable dealer).
/// Dipasang di pivot kosong di tengah podium; mobilnya anak pivot dengan Rigidbody kinematic.
/// Hanya visual lokal (tidak disinkron), jadi tiap pemain melihat putaran sendiri.
/// </summary>
public sealed class ShowroomTurntable : MonoBehaviour {
    [Tooltip("Kecepatan putar (derajat per detik).")]
    [SerializeField] private float degreesPerSecond = 10f;

    private void Update() {
        transform.Rotate(0f, degreesPerSecond * Time.deltaTime, 0f, Space.World);
    }
}
