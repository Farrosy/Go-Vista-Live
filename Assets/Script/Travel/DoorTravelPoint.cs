using UnityEngine;

/// Pintu bangunan: tekan Interact → fade ke hitam → pindah ke venue tujuan (lewat VenueTravelManager).
/// Dipakai dua arah: di WorldScene (masuk ke interior) dan di scene interior (keluar ke WorldScene).
/// Pasang bareng Interactable + collider trigger, seperti DockTravelPoint, tapi tanpa menu pilihan.
[RequireComponent(typeof(Interactable))]
public class DoorTravelPoint : MonoBehaviour
{
    [SerializeField] private VenueId targetVenue = VenueId.Barber;
    [SerializeField] private SpawnSlot targetSlot = SpawnSlot.InteriorEntrance;

    private Interactable interactable;

    void Awake()
    {
        interactable = GetComponent<Interactable>();
    }

    void OnEnable()
    {
        if (interactable != null) interactable.onInteract.AddListener(Go);
    }

    void OnDisable()
    {
        if (interactable != null) interactable.onInteract.RemoveListener(Go);
    }

    private void Go()
    {
        VenueTravelManager manager = VenueTravelManager.Instance;
        if (manager == null)
        {
            Debug.LogWarning("[DoorTravelPoint] VenueTravelManager belum ada.", this);
            return;
        }
        if (manager.IsTraveling) return;
        manager.TravelTo(targetVenue, targetSlot);
    }
}
