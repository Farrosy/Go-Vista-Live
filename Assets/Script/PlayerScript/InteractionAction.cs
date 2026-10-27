using UnityEngine;

/// Data jenis aksi (Talk, Sit, dll). Dibuat sebagai asset: klik kanan di Project >
/// Create > Interaction > Action. Satu asset dipakai banyak objek.
[CreateAssetMenu(menuName = "Interaction/Action")]
public class InteractionAction : ScriptableObject
{
    public string verb = "Talk";
    public Sprite icon;
}