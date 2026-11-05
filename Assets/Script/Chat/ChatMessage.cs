/// Pesan chat yang sudah dirapikan dan siap ditampilkan (immutable). Identitas = referensi objek,
/// dipakai ChatPanel untuk mencari baris UI saat pesan dihapus.
public sealed class ChatMessage {
    public const int NoSender = -1;

    public int SenderId { get; }
    /// Dasar warna nama/avatar. Dari kunci akun (stabil walau rejoin), atau clientID kalau tidak ada.
    public int ColorSeed { get; }
    public string SenderName { get; }
    public string Text { get; }
    public double SentAt { get; }
    public bool IsLocal { get; }
    /// Pesan sistem lokal ("Budi bergabung ke room"). Tidak dikirim ke server; tiap client membuatnya sendiri.
    public bool IsSystem { get; }

    private ChatMessage(int senderId, int colorSeed, string senderName, string text, double sentAt, bool isLocal,
                        bool isSystem) {
        SenderId = senderId;
        ColorSeed = colorSeed;
        SenderName = senderName;
        Text = text;
        SentAt = sentAt;
        IsLocal = isLocal;
        IsSystem = isSystem;
    }

    public static ChatMessage FromPlayer(int senderId, int colorSeed, string senderName, string text, double sentAt,
                                         bool isLocal) {
        return new ChatMessage(senderId, colorSeed, senderName, text, sentAt, isLocal, false);
    }

    public static ChatMessage CreateSystem(string text, double sentAt) {
        return new ChatMessage(NoSender, 0, "", text, sentAt, false, true);
    }

    /// Urutan tampil: waktu server, lalu clientID supaya dua pesan di waktu yang sama tetap stabil.
    public static int CompareByTime(ChatMessage a, ChatMessage b) {
        int byTime = a.SentAt.CompareTo(b.SentAt);
        return byTime != 0 ? byTime : a.SenderId.CompareTo(b.SenderId);
    }
}
