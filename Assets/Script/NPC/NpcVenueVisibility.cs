using UnityEngine;

/// Attach to NpcAgent.prefab. Decides, on THIS client only, whether an NPC is drawn and animated:
///  • Venue: NPCs are World-only, so this client sees/collides with them only while it is in World.
///  • Distance: NPCs farther than hideDistance from this client's own avatar are not rendered and their
///    Animators are switched off (skinning 120 crowd characters every frame was the main FPS cost).
///    Uses hysteresis (showDistance &lt; hideDistance) so an NPC on the boundary doesn't flicker.
/// Works the same on host and clients because each client measures from its own avatar. The NavMeshAgent
/// and network sync are untouched, so hidden NPCs keep walking and stay in the same place for everyone.
/// Colliders follow the venue rule only (a hidden NPC far away can't be bumped into anyway).
[DisallowMultipleComponent]
public class NpcVenueVisibility : MonoBehaviour
{
    [Tooltip("NPC disembunyikan kalau lebih jauh dari ini dari avatar pemain di client ini (meter).")]
    [SerializeField] private float hideDistance = 50f;
    [Tooltip("NPC yang tersembunyi muncul lagi kalau lebih dekat dari ini (meter). Lebih kecil dari Hide Distance supaya tidak berkedip.")]
    [SerializeField] private float showDistance = 45f;
    [Tooltip("Jeda antar pengecekan jarak (detik). Tiap NPC mulai di waktu acak supaya bebannya tersebar.")]
    [SerializeField] private float checkInterval = 0.25f;

    private Renderer[] renderers;
    private Collider[] colliders;
    private Animator[] animators;
    private bool applied;
    private bool lastVisible;
    private bool lastCollidable;
    private bool near;
    private float nextCheck;
    // Animator baru boleh dimatikan setelah Start: NpcAgent/NpcSync membaca animator.parameters di Awake,
    // dan urutan Awake/OnEnable antar komponen tidak dijamin.
    private bool started;

    void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>(true);
        colliders = GetComponentsInChildren<Collider>(true);
        animators = GetComponentsInChildren<Animator>(true);
        // Supaya NPC yang berjalan tidak kembali ke Idle setiap kali muncul lagi.
        for (int i = 0; i < animators.Length; i++)
            if (animators[i]) animators[i].keepAnimatorStateOnDisable = true;
    }

    void OnEnable()
    {
        VenueTravelManager.VenueChanged += OnVenueChanged;
        applied = false;
        near = IsNear(currentlyNear: false);
        nextCheck = Time.time + Random.value * checkInterval;
        Reevaluate();   // renderer langsung disembunyikan sejak frame pertama kalau NPC jauh
    }

    void Start()
    {
        started = true;
        applied = false;
        Reevaluate();
    }

    void OnDisable()
    {
        VenueTravelManager.VenueChanged -= OnVenueChanged;
    }

    /// Avatar baru saja dipindah (travel): hitung ulang jarak sekarang, jangan pakai nilai lama.
    private void OnVenueChanged()
    {
        near = IsNear(near);
        Reevaluate();
    }

    void Update()
    {
        if (Time.time < nextCheck) return;
        nextCheck = Time.time + checkInterval;
        near = IsNear(near);
        Reevaluate();
    }

    /// Jarak dihitung di bidang datar dari avatar lokal. Belum ada avatar (lobby / sebelum spawn) = sembunyi.
    private bool IsNear(bool currentlyNear)
    {
        PlayerController local = PlayerController.Local;
        if (local == null) return false;
        Vector3 offset = transform.position - local.transform.position;
        offset.y = 0f;
        float limit = currentlyNear ? hideDistance : showDistance;
        return offset.sqrMagnitude <= limit * limit;
    }

    private void Reevaluate()
    {
        bool inWorld = VenueTravelManager.LocalVenue == VenueId.World;
        Apply(visible: inWorld && near, collidable: inWorld);
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
        if (started)
            for (int i = 0; i < animators.Length; i++)
                if (animators[i]) animators[i].enabled = visible;
        for (int i = 0; i < colliders.Length; i++)
            if (colliders[i]) colliders[i].enabled = collidable;
    }
}
