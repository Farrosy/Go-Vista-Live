using System.Collections.Generic;
using UnityEngine;

/// Pasang di objek air (butuh Renderer). Air hanya berlaku untuk pemain yang berada di venue pemilik air ini,
/// karena scene venue lain bisa dimuat berdampingan di ruang koordinat yang sama.
[RequireComponent(typeof(Renderer))]
public sealed class WaterSurface : MonoBehaviour {
    private static readonly List<WaterSurface> Active = new List<WaterSurface>();

    [Tooltip("Venue tempat air ini berada.")]
    [SerializeField] private VenueId venue = VenueId.World;

    private Renderer _renderer;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Active.Clear();

    /// Tinggi permukaan air di posisi ini untuk venue pemain lokal. False kalau tidak ada air di sana.
    public static bool TryGetLevelAt(Vector3 position, out float level) {
        foreach (WaterSurface surface in Active) {
            if (surface.venue != VenueTravelManager.LocalVenue) continue;
            Bounds bounds = surface._renderer.bounds;
            if (position.x < bounds.min.x || position.x > bounds.max.x ||
                position.z < bounds.min.z || position.z > bounds.max.z)
                continue;
            level = surface.transform.position.y;
            return true;
        }
        level = 0f;
        return false;
    }

    private void Awake() => _renderer = GetComponent<Renderer>();
    private void OnEnable() => Active.Add(this);
    private void OnDisable() => Active.Remove(this);
}
