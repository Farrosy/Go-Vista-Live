using UnityEngine;

/// Pasang di root avatar (butuh PlayerVenueSync). Dalam satu Normcore room, avatar hanya terlihat &
/// bisa ditabrak kalau venue-nya sama dengan venue pemain lokal (VenueTravelManager.LocalVenue).
/// Avatar lokal SELALU terlihat oleh dirinya sendiri, kecuali sedang disembunyikan di layar mati.
/// Collider gerak avatar lokal tidak pernah dimatikan.
///
/// Re-evaluasi saat: (a) venue avatar ini berubah, (b) venue pemain lokal berubah (VenueChanged),
/// (c) avatar ini disembunyikan atau ditampilkan lagi (HiddenChanged).
[RequireComponent(typeof(PlayerVenueSync))]
public class VenueVisibilityFilter : MonoBehaviour
{
    private PlayerVenueSync venueSync;
    private Renderer[] renderers;
    private Collider[] colliders;
    private bool applied;
    private bool lastVisible;
    private bool lastCollidable;

    void Awake()
    {
        venueSync = GetComponent<PlayerVenueSync>();
        renderers = GetComponentsInChildren<Renderer>(true);
        colliders = GetComponentsInChildren<Collider>(true);
    }

    void OnEnable()
    {
        venueSync.VenueChanged += OnThisAvatarVenueChanged;
        venueSync.HiddenChanged += Reevaluate;
        VenueTravelManager.VenueChanged += Reevaluate;
        applied = false;
        Reevaluate();
    }

    void OnDisable()
    {
        venueSync.VenueChanged -= OnThisAvatarVenueChanged;
        venueSync.HiddenChanged -= Reevaluate;
        VenueTravelManager.VenueChanged -= Reevaluate;
    }

    private void OnThisAvatarVenueChanged(PlayerVenueSync sync, VenueId venue) => Reevaluate();

    private void Reevaluate()
    {
        bool isLocal = venueSync.IsLocalAvatar;
        bool inSameVenue = isLocal || venueSync.Venue == VenueTravelManager.LocalVenue;
        bool visible = inSameVenue && !venueSync.IsHidden;
        // Jangan pernah matikan collider gerak avatar lokal.
        bool collidable = isLocal || visible;
        Apply(visible, collidable);
    }

    private void Apply(bool visible, bool collidable)
    {
        if (applied && visible == lastVisible && collidable == lastCollidable)
            return;
        applied = true;
        lastVisible = visible;
        lastCollidable = collidable;

        for (int i = 0; i < renderers.Length; i++)
            if (renderers[i]) renderers[i].enabled = visible;
        for (int i = 0; i < colliders.Length; i++)
            if (colliders[i]) colliders[i].enabled = collidable;
    }
}
