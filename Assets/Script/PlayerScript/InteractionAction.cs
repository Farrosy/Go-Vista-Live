using UnityEngine;

/// Data jenis aksi (Talk, Sit, dll). Dibuat sebagai asset: klik kanan di Project >
/// Create > Interaction > Action. Satu asset dipakai banyak objek.
[CreateAssetMenu(menuName = "Interaction/Action")]
public class InteractionAction : ScriptableObject
{
    public string verb = "Talk";
    [Tooltip("Ikon di prompt dunia 3D (InteractPromptWorld).")]
    public Sprite icon;
    [Tooltip("Opsional. Ikon PUTIH untuk tombol Interact di layar (latar tombol gelap). Kosong = pakai Icon.")]
    public Sprite hudIcon;

    /// Ikon untuk tombol Interact di HUD.
    public Sprite HudIcon => hudIcon ? hudIcon : icon;
}
