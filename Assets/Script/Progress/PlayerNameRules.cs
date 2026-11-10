using System.Text;

/// <summary>
/// Aturan nama pemain (diubah di menu PROFIL, disimpan di PlayerProgress.playerName, tampil di daftar pemain
/// lewat LobbyPresence). Nama juga dipakai sebagai Player Name Unity Authentication untuk friendlist, yang
/// ditambah tag angka oleh Unity (mis. "ganteng#74044"), jadi nama tidak harus unik.
///
/// Aturan (Docs/Profil/03a-profil-edit.png): 3–16 karakter, hanya huruf A–Z/a–z, angka 0–9, dan '_'.
/// Karakter lain (spasi, emoji, titik, strip, '#', '&lt;', '&gt;') diblokir saat diketik maupun di-paste.
/// Nama lama yang lebih pendek dari 3 karakter tetap dipakai sampai pemain mengubahnya sendiri.
/// </summary>
public static class PlayerNameRules {
    public const int MinLength = 3;
    public const int MaxLength = 16;
    public const string Hint = "3–16 karakter. Hanya huruf, angka, dan _ (tanpa spasi atau simbol)";

    /// <summary>Buang karakter yang tidak diizinkan, lalu potong ke MaxLength. Hasil kosong = belum mengisi nama.</summary>
    public static string Sanitize(string raw) {
        if (string.IsNullOrEmpty(raw))
            return "";

        var sb = new StringBuilder(raw.Length);
        foreach (char c in raw) {
            if (IsAllowedChar(c))
                sb.Append(c);
            if (sb.Length == MaxLength)
                break;
        }
        return sb.ToString();
    }

    /// <summary>Karakter yang boleh ada di nama (huruf/angka ASCII dan '_'). Dipakai juga untuk memblokir ketikan.</summary>
    public static bool IsAllowedChar(char c) {
        return (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '_';
    }

    /// <summary>Nama baru boleh disimpan? (sudah dirapikan, panjang MinLength–MaxLength).</summary>
    public static bool IsValid(string name) {
        return !string.IsNullOrEmpty(name) && name.Length >= MinLength && name.Length <= MaxLength &&
               Sanitize(name) == name;
    }

    /// <summary>Nama yang ditampilkan: nama yang diisi pemain, atau "Player N" kalau kosong.</summary>
    public static string DisplayName(string playerName, int clientId) {
        string clean = Sanitize(playerName);
        return clean.Length > 0 ? clean : "Player " + (clientId + 1);
    }
}
