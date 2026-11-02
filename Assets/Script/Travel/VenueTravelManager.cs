using System;
using UnityEngine;

/// Pengelola perpindahan venue (teleport antar scene).
///
/// FASE 2 (sekarang): baru menyimpan state venue LOKAL + event, dipakai PlayerVenueSync &
/// VenueVisibilityFilter untuk visibilitas "satu room, hanya lihat yang se-venue".
///
/// FASE 3 (nanti): kelas ini menjadi singleton pada object persisten di MainMultiplayerScene
/// (sebelah PlayerManager), menyimpan CurrentVenue, dan punya coroutine TravelTo(target, slot)
/// untuk fade → load/unload scene additive → set active → reposisi avatar → set venue.
public class VenueTravelManager : MonoBehaviour
{
    /// Venue yang sedang ditempati pemain lokal. Cerminan dari PlayerVenueSync avatar lokal.
    public static VenueId LocalVenue { get; private set; } = VenueId.World;

    /// Dipicu saat LocalVenue berubah. VenueVisibilityFilter mendengarkan ini untuk re-evaluasi.
    public static event Action VenueChanged;

    /// Dipanggil PlayerVenueSync avatar lokal saat venue-nya berubah.
    public static void SetLocalVenue(VenueId venue)
    {
        if (LocalVenue == venue)
            return;
        LocalVenue = venue;
        VenueChanged?.Invoke();
    }

    // Reset state statik saat masuk play mode / domain reload supaya tidak ada subscriber basi.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        LocalVenue = VenueId.World;
        VenueChanged = null;
    }
}
