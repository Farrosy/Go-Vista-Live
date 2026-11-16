/// <summary>Aturan slot karakter custom: maksimal 2, bernama "Karakter N", tanpa input nama.</summary>
public static class CustomSlotRules {
    public const int MaxCustomSlots = 2;
    /// <summary>Tidak ada slot custom yang aktif: pemain memakai template (PlayerProgress.characterId).</summary>
    public const int NoActiveSlot = -1;

    public static bool CanCreate(int existingSlots) => existingSlots < MaxCustomSlots;

    public static string SlotName(int index) => $"Karakter {index + 1}";

    /// <summary>
    /// Indeks slot aktif setelah slot <paramref name="deletedSlot"/> dihapus. Kalau yang dihapus
    /// adalah slot aktif, pemain kembali ke template asalnya (NoActiveSlot).
    /// </summary>
    public static int ActiveAfterDelete(int activeSlot, int deletedSlot) {
        if (activeSlot == NoActiveSlot || activeSlot == deletedSlot) return NoActiveSlot;
        return deletedSlot < activeSlot ? activeSlot - 1 : activeSlot;
    }
}
