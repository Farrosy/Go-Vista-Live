using UnityEngine;

/// <summary>
/// Tampilan pemain ↔ string untuk jaringan (PlayerAppearanceModel). Kosong = pemain memakai template biasa.
/// Data dari jaringan SELALU dibersihkan lewat CustomCharacterValidator (whitelist outfit, clamp nilai):
/// pemain lain bisa mengirim apa saja.
/// </summary>
public static class AppearanceCodec {
    /// <summary>Slot custom yang sedang dipakai pemain lokal, atau "" kalau memakai template.</summary>
    public static string EncodeLocal() {
        CustomCharacterRecord record = CustomCharacterStore.Get(CustomCharacterStore.ActiveSlot);
        return record == null ? "" : JsonUtility.ToJson(record);
    }

    /// <returns>Record bersih, atau null (kosong, terlalu besar, rusak, template tidak dikenal) = tampil template.</returns>
    public static CustomCharacterRecord Decode(string payload) {
        if (string.IsNullOrEmpty(payload) || payload.Length > CustomizationRules.MaxAppearancePayloadLength) return null;
        try {
            return CustomCharacterValidator.Sanitize(JsonUtility.FromJson<CustomCharacterRecord>(payload), OutfitCatalog.Exists);
        } catch (System.ArgumentException e) {
            Debug.LogWarning($"[Appearance] Data tampilan pemain rusak, pakai template: {e.Message}");
            return null;
        }
    }
}
