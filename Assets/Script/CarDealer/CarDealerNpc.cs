using UnityEngine;

/// <summary>
/// NPC dealer mobil (Johnny). Pasang di root NPC bersama DialogueTrigger + Interactable (On Interact =
/// DialogueTrigger.StartDialogue). Dipasang otomatis oleh Go Vista > Dealer Mobil > 3. Pasang Johnny di Flamingo.
///
/// DialogueTrigger: stage = dialog intro (sekali saja), Repeat Dialogue = sapaan singkat. Begitu dialog selesai,
/// panel dealer terbuka. Kalau tutorial belum pernah dilihat, panel dibuka dengan tutorial (sekali saja).
/// </summary>
[RequireComponent(typeof(DialogueTrigger))]
public sealed class CarDealerNpc : MonoBehaviour {
    [Tooltip("Kosong = Resources/CarDealer/CarCatalog.")]
    [SerializeField] private CarCatalog catalog;

    private DialogueTrigger _trigger;

    private void Awake() => _trigger = GetComponent<DialogueTrigger>();
    private void OnEnable() => _trigger.DialogueEnded += OpenDealer;
    private void OnDisable() => _trigger.DialogueEnded -= OpenDealer;

    private void OpenDealer() {
        CarCatalog source = catalog != null ? catalog : CarCatalog.Load();
        if (source == null) {
            Debug.LogWarning("[CarDealer] CarCatalog tidak ditemukan. Jalankan Go Vista > Dealer Mobil > 1.", this);
            return;
        }
        CarDealerPanel.Show(source, withTutorial: !CarDealerService.TutorialDone);
    }
}
