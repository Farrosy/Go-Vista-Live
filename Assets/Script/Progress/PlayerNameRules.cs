using System.Text;

/// <summary>
/// Aturan nama pemain (diisi di menu KARAKTER, disimpan di PlayerProgress.playerName, lalu tampil
/// di daftar pemain lewat LobbyPresence). Nama ini bukan username login, jadi boleh spasi dan
/// tidak harus unik.
/// </summary>
public static class PlayerNameRules {
    public const int MaxLength = 16;

    /// <summary>
    /// Rapikan input: hapus karakter kontrol dan &lt; &gt; (supaya tidak bisa menyisipkan rich text
    /// seperti &lt;size=200&gt; ke daftar pemain orang lain), gabungkan spasi berturut-turut,
    /// trim, lalu potong ke MaxLength. Hasil kosong = pemain belum mengisi nama.
    /// </summary>
    public static string Sanitize(string raw) {
        if (string.IsNullOrEmpty(raw))
            return "";

        var sb = new StringBuilder(raw.Length);
        bool lastWasSpace = true; // true supaya spasi di awal ikut terbuang
        foreach (char c in raw) {
            if (char.IsWhiteSpace(c)) {
                if (!lastWasSpace) sb.Append(' ');
                lastWasSpace = true;
                continue;
            }
            if (char.IsControl(c) || c == '<' || c == '>')
                continue;
            sb.Append(c);
            lastWasSpace = false;
        }

        string result = sb.ToString().TrimEnd();
        if (result.Length > MaxLength)
            result = result.Substring(0, MaxLength).TrimEnd();
        return result;
    }

    /// <summary>Nama yang ditampilkan: nama yang diisi pemain, atau "Player N" kalau kosong.</summary>
    public static string DisplayName(string playerName, int clientId) {
        string clean = Sanitize(playerName);
        return clean.Length > 0 ? clean : "Player " + (clientId + 1);
    }
}
