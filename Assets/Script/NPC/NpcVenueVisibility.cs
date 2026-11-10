using UnityEngine;

/// Attach to NpcAgent.prefab. NPCs are World-only, so this client sees/collides with them only while
/// it is in the World venue. Mirrors VenueVisibilityFilter but with a constant venue (World) instead
/// of a synced per-avatar venue. Independent of authority gating — this is about THIS client's venue,
/// not who owns the NPC.
[DisallowMultipleComponent]
public class NpcVenueVisibility : MonoBehaviour
{
    private Renderer[] renderers;
    private Collider[] colliders;
    private bool applied;
    private bool lastVisible;

    void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>(true);
        colliders = GetComponentsInChildren<Collider>(true);
    }

    void OnEnable()
    {
        VenueTravelManager.VenueChanged += Reevaluate;
        applied = false;
        Reevaluate();
    }

    void OnDisable()
    {
        VenueTravelManager.VenueChanged -= Reevaluate;
    }

    private void Reevaluate()
    {
        Apply(VenueTravelManager.LocalVenue == VenueId.World);
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
