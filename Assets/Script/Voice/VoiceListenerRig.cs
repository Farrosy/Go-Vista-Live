using UnityEngine;

/// Satu-satunya AudioListener di MainMultiplayerScene. Posisinya di avatar lokal supaya jarak suara dihitung
/// dari karakter, bukan dari kamera orbit (±4 m di belakangnya). Rotasinya mengikuti kamera supaya kiri-kanan
/// suara cocok dengan layar. Tanpa avatar (lobby), posisinya ikut kamera.
/// Berjalan setelah CameraOrbit supaya memakai posisi kamera frame ini.
[DefaultExecutionOrder(1000)]
[RequireComponent(typeof(AudioListener))]
public sealed class VoiceListenerRig : MonoBehaviour {
    [SerializeField] private Transform cameraTransform;

    private void LateUpdate() {
        if (cameraTransform == null) return;
        PlayerController localPlayer = PlayerController.Local;
        Vector3 position = localPlayer != null ? localPlayer.transform.position : cameraTransform.position;
        transform.SetPositionAndRotation(position, cameraTransform.rotation);
    }
}
