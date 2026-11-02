using UnityEngine;

/// Pasang di zona interaksi dermaga (bareng Interactable + collider trigger) di WorldScene.
/// Saat pemain menekan tombol Interact, buka TravelChoiceUI untuk memilih venue tujuan,
/// lalu VenueTravelManager.TravelTo memindahkan pemain ke cube venue itu.
[RequireComponent(typeof(Interactable))]
public class DockTravelPoint : MonoBehaviour
{
    [SerializeField] private string title = "Berlayar ke pulau seberang";
    [SerializeField] private string danceClubLabel = "Dance Club";
    [SerializeField] private string rooftopBarLabel = "Rooftop Bar";

    private Interactable interactable;

    void Awake()
    {
        interactable = GetComponent<Interactable>();
    }

    void OnEnable()
    {
        if (interactable != null) interactable.onInteract.AddListener(OpenChoice);
    }

    void OnDisable()
    {
        if (interactable != null) interactable.onInteract.RemoveListener(OpenChoice);
    }

    private void OpenChoice()
    {
        if (TravelChoiceUI.Instance == null)
        {
            Debug.LogWarning("[DockTravelPoint] TravelChoiceUI belum ada di scene persisten.");
            return;
        }
        if (VenueTravelManager.Instance == null)
        {
            Debug.LogWarning("[DockTravelPoint] VenueTravelManager belum ada.");
            return;
        }

        TravelChoiceUI.Instance.Open(title, new (string, System.Action)[]
        {
            (danceClubLabel,  () => VenueTravelManager.Instance.TravelTo(VenueId.DanceClub,  SpawnSlot.VenueCube)),
            (rooftopBarLabel, () => VenueTravelManager.Instance.TravelTo(VenueId.RooftopBar, SpawnSlot.VenueCube)),
        });
    }
}
