using System;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

/// Identitas pemain untuk chat yang TETAP SAMA walau keluar-masuk room (clientID Normcore berubah tiap join).
/// Dipakai supaya pesan lama milik sendiri tetap tampil di kanan setelah rejoin, dan warna nama tidak berubah.
///
/// Yang dikirim ke pemain lain hanya hash pendek dari PlayerId Unity Authentication, bukan ID aslinya.
/// Kalau belum login (mis. tes di Editor tanpa akun), dipakai ID acak per perangkat yang disimpan di PlayerPrefs.
public static class ChatIdentity {
    private const string DeviceKeyPref = "Chat.DeviceKey";
    private const int KeyBytes = 12;

    private static string _cachedSource;
    private static string _cachedKey;

    /// Kunci akun lokal (hex 24 karakter). Dihitung ulang kalau pemain ganti akun.
    public static string LocalKey {
        get {
            string source = AuthService.PlayerId;
            if (string.IsNullOrEmpty(source))
                source = "device:" + DeviceKey();
            if (source != _cachedSource) {
                _cachedSource = source;
                _cachedKey = Hash(source);
            }
            return _cachedKey;
        }
    }

    public static bool IsLocalKey(string key) {
        return !string.IsNullOrEmpty(key) && key == LocalKey;
    }

    private static string DeviceKey() {
        string key = PlayerPrefs.GetString(DeviceKeyPref, "");
        if (string.IsNullOrEmpty(key)) {
            key = Guid.NewGuid().ToString("N");
            PlayerPrefs.SetString(DeviceKeyPref, key);
            PlayerPrefs.Save();
        }
        return key;
    }

    private static string Hash(string source) {
        using (SHA256 sha = SHA256.Create()) {
            byte[] digest = sha.ComputeHash(Encoding.UTF8.GetBytes("govista-chat:" + source));
            var sb = new StringBuilder(KeyBytes * 2);
            for (int i = 0; i < KeyBytes; i++)
                sb.Append(digest[i].ToString("x2"));
            return sb.ToString();
        }
    }
}
