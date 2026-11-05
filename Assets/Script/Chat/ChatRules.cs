using System;
using System.Globalization;
using System.Text;
using UnityEngine;

/// Aturan chat murni (tanpa Normcore/UI) supaya mudah dites: batas panjang, rapikan teks, jeda kirim,
/// warna nama, inisial avatar, dan format jam.
public static class ChatRules {
    /// Panjang maksimal pesan dalam karakter tampil (emoji dihitung 1).
    public const int MaxTextLength = 120;
    /// Jeda minimal antar pesan dari satu pemain (anti spam), detik.
    public const double MinSendIntervalSeconds = 1.0;
    /// Pesan milik satu pengirim yang disimpan di room. Lebih dari ini, pesan tertua miliknya dihapus.
    public const int MaxStoredPerSender = 20;
    /// Baris yang ditampilkan panel chat.
    public const int MaxDisplayedMessages = 50;

    private static readonly Color[] NamePalette = {
        new Color32(0xF4, 0x72, 0xB6, 0xFF), // pink
        new Color32(0x22, 0xD3, 0xEE, 0xFF), // cyan
        new Color32(0x34, 0xD3, 0x99, 0xFF), // hijau
        new Color32(0xFB, 0xBF, 0x24, 0xFF), // kuning
        new Color32(0xA7, 0x8B, 0xFA, 0xFF), // ungu
        new Color32(0x60, 0xA5, 0xFA, 0xFF), // biru
        new Color32(0xFB, 0x92, 0x3C, 0xFF), // oranye
        new Color32(0xFB, 0x71, 0x85, 0xFF), // merah muda
    };

    /// Rapikan pesan: buang karakter kontrol dan pembalik arah teks, gabungkan spasi/baris baru menjadi
    /// satu spasi, trim, lalu potong ke MaxTextLength. ZWJ dan variation selector dipertahankan
    /// supaya emoji gabungan (mis. 👨‍👩‍👧) tidak pecah. Hasil kosong = tidak ada yang dikirim.
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
            if (char.IsControl(c) || IsBidiOverride(c))
                continue;
            sb.Append(c);
            lastWasSpace = false;
        }
        return Truncate(sb.ToString().TrimEnd(), MaxTextLength);
    }

    /// Potong ke maxElements karakter tampil tanpa memotong di tengah surrogate pair emoji.
    public static string Truncate(string text, int maxElements) {
        if (string.IsNullOrEmpty(text) || maxElements <= 0)
            return "";
        var info = new StringInfo(text);
        return info.LengthInTextElements <= maxElements
            ? text
            : info.SubstringByTextElements(0, maxElements).TrimEnd();
    }

    public static bool CanSend(double now, double lastSentAt) {
        return now - lastSentAt >= MinSendIntervalSeconds;
    }

    /// Warna nama tetap per pemain (dari clientID), sama di semua client.
    public static Color NameColor(int clientId) {
        int index = ((clientId % NamePalette.Length) + NamePalette.Length) % NamePalette.Length;
        return NamePalette[index];
    }

    /// Hash FNV-1a 32-bit yang hasilnya sama di semua platform (string.GetHashCode bisa beda antara Editor
    /// dan build Android IL2CPP). Dipakai supaya warna nama dari kunci akun sama di semua client.
    public static int StableHash(string value) {
        if (string.IsNullOrEmpty(value))
            return 0;
        unchecked {
            uint hash = 2166136261u;
            foreach (char c in value) {
                hash ^= c;
                hash *= 16777619u;
            }
            return (int)(hash & 0x7FFFFFFF);
        }
    }

    /// Huruf/karakter pertama nama untuk lingkaran avatar ("rina" -> "R").
    public static string Initial(string name) {
        if (string.IsNullOrEmpty(name))
            return "?";
        var info = new StringInfo(name.Trim());
        if (info.LengthInTextElements == 0)
            return "?";
        return info.SubstringByTextElements(0, 1).ToUpperInvariant();
    }

    /// Jam lokal "HH:mm" dari waktu server Normcore (detik sejak epoch Unix).
    public static string FormatTime(double unixSeconds) {
        if (double.IsNaN(unixSeconds) || unixSeconds <= 0)
            return "";
        DateTimeOffset utc = DateTimeOffset.FromUnixTimeMilliseconds((long)(unixSeconds * 1000.0));
        return utc.ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture);
    }

    // U+202A..U+202E dan U+2066..U+2069 bisa membalik arah teks dan mengacak tampilan pesan orang lain.
    private static bool IsBidiOverride(char c) {
        return (c >= '‪' && c <= '‮') || (c >= '⁦' && c <= '⁩');
    }
}
