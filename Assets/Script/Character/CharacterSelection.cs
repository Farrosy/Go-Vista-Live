/// <summary>
/// Karakter pilihan pemain. Disimpan di Cloud Save akun (PlayerProgress.characterId), jadi ikut
/// pindah saat login di HP lain. Dipakai PlayerManager saat spawn avatar.
/// </summary>
public static class CharacterSelection {
    public static string CurrentId => ProgressService.Current.characterId;

    /// Karakter yang dipakai sekarang (default: karakter pertama di CharacterDatabase).
    public static CharacterDatabase.Entry Current {
        get {
            CharacterDatabase db = CharacterDatabase.Instance;
            return db ? db.Get(CurrentId) : null;
        }
    }

    public static void Select(string id) => ProgressService.SetCharacter(id);
}
