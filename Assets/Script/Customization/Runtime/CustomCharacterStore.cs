using System.Collections.Generic;

/// <summary>
/// Slot karakter custom milik akun yang sedang login, disimpan di PlayerProgress (Cloud Save per akun,
/// termasuk tamu). Semua data yang dibaca sudah lewat CustomCharacterValidator. Setiap perubahan
/// membuat list baru (tidak mengubah list lama) lalu disimpan lewat ProgressService.
///
/// Sampai Tahap 3, avatar di world tetap memakai template: slot aktif menyetel characterId ke id
/// template asalnya (CharacterSelection), jadi PlayerManager tidak perlu diubah.
/// </summary>
public static class CustomCharacterStore {
    public static int Count => ProgressService.Current.customCharacters?.Count ?? 0;
    public static int ActiveSlot => ProgressService.Current.activeCustomSlot;
    public static bool CanCreate => CustomSlotRules.CanCreate(Count);

    /// <summary>Nama karakter yang sedang dipakai: "Karakter N" (slot custom) atau label template.</summary>
    public static string ActiveDisplayName {
        get {
            if (Get(ActiveSlot) != null) return CustomSlotRules.SlotName(ActiveSlot);
            string id = CharacterSelection.Current?.id ?? "";
            return CharacterTemplates.TryGet(id, out CharacterTemplate template)
                ? template.Label
                : CharacterSelection.Current?.displayName ?? "-";
        }
    }

    /// <returns>Record bersih, atau null kalau slot tidak ada / templatenya tidak dikenal.</returns>
    public static CustomCharacterRecord Get(int slot) {
        List<CustomCharacterRecord> all = ProgressService.Current.customCharacters;
        if (all == null || slot < 0 || slot >= all.Count) return null;
        return CustomCharacterValidator.Sanitize(all[slot], OutfitCatalog.Exists);
    }

    /// <summary>Simpan ke slot yang ada, atau buat slot baru kalau <paramref name="slot"/> &lt; 0.</summary>
    /// <returns>Indeks slot yang disimpan, atau -1 kalau gagal (data tidak valid / slot penuh).</returns>
    public static int Save(int slot, CustomCharacterRecord record) {
        CustomCharacterRecord clean = CustomCharacterValidator.Sanitize(record, OutfitCatalog.Exists);
        if (clean == null) return -1;

        var all = new List<CustomCharacterRecord>(ProgressService.Current.customCharacters ?? new List<CustomCharacterRecord>());
        if (slot < 0) {
            if (!CustomSlotRules.CanCreate(all.Count)) return -1;
            all.Add(clean);
            slot = all.Count - 1;
        } else if (slot < all.Count) {
            all[slot] = clean;
        } else {
            return -1;
        }
        ProgressService.SetCustomCharacters(all, ActiveSlot);
        return slot;
    }

    public static void Delete(int slot) {
        var all = new List<CustomCharacterRecord>(ProgressService.Current.customCharacters ?? new List<CustomCharacterRecord>());
        if (slot < 0 || slot >= all.Count) return;
        all.RemoveAt(slot);
        ProgressService.SetCustomCharacters(all, CustomSlotRules.ActiveAfterDelete(ActiveSlot, slot));
    }

    /// <summary>Pakai slot custom. Avatar di world memakai template asalnya sampai Tahap 3.</summary>
    public static void Activate(int slot) {
        CustomCharacterRecord record = Get(slot);
        if (record == null) return;
        CharacterSelection.Select(record.templateId);
        ProgressService.SetCustomCharacters(ProgressService.Current.customCharacters, slot);
    }

    /// <summary>Pakai template biasa (tidak ada slot custom aktif).</summary>
    public static void ActivateTemplate(string templateId) {
        CharacterSelection.Select(templateId);
        if (ActiveSlot != CustomSlotRules.NoActiveSlot)
            ProgressService.SetCustomCharacters(ProgressService.Current.customCharacters, CustomSlotRules.NoActiveSlot);
    }
}
