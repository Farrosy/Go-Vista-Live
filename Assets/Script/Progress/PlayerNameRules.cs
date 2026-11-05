using System.Text;

/// <summary>
/// Aturan nama pemain (diisi di menu KARAKTER, disimpan di PlayerProgress.playerName, lalu tampil
/// di daftar pemain lewat LobbyPresence). Nama ini bukan username login dan tidak harus unik.
/// Tanpa spasi dan tanpa '#', karena juga dipakai sebagai Player Name friendlist (Unity menambahkan #1234).
/// </summary>
public static class PlayerNameRules {
    public const int MaxLength = 16;

    /// <summary>
    /// Rapikan input: hapus SEMUA spasi/whitespace (nama dipakai juga sebagai Player Name Unity
    /// Authentication untuk friendlist, yang tidak boleh berisi spasi), karakter kontrol, dan &lt; &gt;
    /// (supaya tidak bisa menyisipkan rich text seperti &lt;size=200&gt; ke daftar pemain orang lain),
    /// lalu potong ke MaxLength. Hasil kosong = pemain belum mengisi nama.
    /// </summary>
    public static string Sanitize(string raw) {
        if (string.IsNullOrEmpty(raw))
            return "";

        var sb = new StringBuilder(raw.Length);
        foreach (char c in raw) {
            if (!IsAllowedChar(c))
                continue;
            sb.Append(c);
        }

        string result = sb.ToString();
        if (result.Length > MaxLength)
            result = result.Substring(0, MaxLength);
        return result;
    }

    /// <summary>Karakter yang boleh ada di nama. Dipakai juga untuk menolak ketikan langsung di kolom nama.</summary>
    public static bool IsAllowedChar(char c) {
        return !char.IsWhiteSpace(c) && !char.IsControl(c) && c != '<' && c != '>' && c != '#';
    }

    /// <summary>Nama yang ditampilkan: nama yang diisi pemain, atau "Player N" kalau kosong.</summary>
    public static string DisplayName(string playerName, int clientId) {
        string clean = Sanitize(playerName);
        return clean.Length > 0 ? clean : "Player " + (clientId + 1);
    }
}
