using UnityEngine;

/// Pasang di root avatar (butuh PlayerVenueSync). Dalam satu Normcore room, avatar hanya terlihat &
/// bisa ditabrak kalau venue-nya sama dengan venue pemain lokal (VenueTravelManager.LocalVenue).
/// Avatar lokal SELALU terlihat oleh dirinya sendiri (renderer & collider-nya tidak pernah dimatikan).
///
/// Re-evaluasi saat: (a) venue avatar ini berubah, (b) venue pemain lokal berubah (VenueChanged).
[RequireComponent(typeof(PlayerVenueSync))]
public class VenueVisibilityFilter : MonoBehaviour
{
    private PlayerVenueSync venueSync;
    private Renderer[] renderers;
    private Collider[] colliders;
    private bool applied;
    private bool lastVisible;

    void Awake()
    {
        venueSync = GetComponent<PlayerVenueSync>();
        renderers = GetComponentsInChildren<Renderer>(true);
        colliders = GetComponentsInChildren<Collider>(true);
    }

    void OnEnable()
    {
        venueSync.VenueChanged += OnThisAvatarVenueChanged;
        VenueTravelManager.VenueChanged += Reevaluate;
        applied = false;
        Reevaluate();
    }

    void OnDisable()
    {
        venueSync.VenueChanged -= OnThisAvatarVenueChanged;
        VenueTravelManager.VenueChanged -= Reevaluate;
    }

    private void OnThisAvatarVenueChanged(PlayerVenueSync sync, VenueId venue) => Reevaluate();

    private void Reevaluate()
    {
        // Avatar lokal selalu terlihat oleh dirinya sendiri (jangan pernah matikan collider geraknya).
        bool visible = venueSync.IsLocalAvatar || venueSync.Venue == VenueTravelManager.LocalVenue;
        Apply(visible);
    }

    private void Apply(bool visible)
    {
        if (applied && visible == lastVisible)
            return;
        applied = true;
        lastVisible = visible;

        for (int i = 0; i < renderers.Length; i++)
            if (renderers[i]) renderers[i].enabled = visible;
        for (int i = 0; i < colliders.Length; i++)
            if (colliders[i]) colliders[i].enabled = visible;
    }
}
