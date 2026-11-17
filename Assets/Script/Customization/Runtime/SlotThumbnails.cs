using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Thumbnail kartu "Karakter kamu" di layar Pilih Karakter, hasil tangkapan panggung preview (bukan ikon
/// template). Disimpan per ISI record (hash JSON) di perangkat ini saja, jadi:
///  - thumbnail lama otomatis tidak dipakai lagi setelah karakter diubah;
///  - di perangkat lain (Cloud Save) thumbnail dibuat ulang saat slot itu pertama kali dipreview.
/// Tidak ikut Cloud Save: gambar bisa dibuat ulang kapan saja, dan kuota Cloud Save kecil.
/// </summary>
public static class SlotThumbnails {
    private const string Folder = "CustomizerThumbs";
    private const string Extension = ".png";

    private static readonly Dictionary<string, Texture2D> Cache = new Dictionary<string, Texture2D>();

    /// <summary>Thumbnail baru tersimpan (layar Pilih Karakter memperbarui kartunya).</summary>
    public static event Action Changed;

    private static string Directory => Path.Combine(Application.persistentDataPath, Folder);

    /// <returns>Thumbnail untuk isi record ini, atau null kalau belum pernah ditangkap.</returns>
    public static Texture2D Get(CustomCharacterRecord record) {
        if (record == null) return null;
        string key = KeyOf(record);
        if (Cache.TryGetValue(key, out Texture2D cached) && cached) return cached;
        string file = PathOf(key);
        if (!File.Exists(file)) return null;
        try {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false) { name = "Slot Thumbnail" };
            if (!texture.LoadImage(File.ReadAllBytes(file))) {
                UnityEngine.Object.Destroy(texture);
                return null;
            }
            Cache[key] = texture;
            return texture;
        } catch (Exception e) {
            Debug.LogWarning($"[Customizer] Thumbnail '{file}' gagal dibaca: {e.Message}");
            return null;
        }
    }

    public static bool Has(CustomCharacterRecord record) => Get(record) != null;

    /// <summary>Simpan tangkapan untuk isi record ini; thumbnail record yang sudah tidak ada dibuang.</summary>
    public static void Store(CustomCharacterRecord record, Texture2D texture, IEnumerable<CustomCharacterRecord> keep) {
        if (record == null || !texture) return;
        string key = KeyOf(record);
        if (Cache.TryGetValue(key, out Texture2D old) && old && old != texture) UnityEngine.Object.Destroy(old);
        Cache[key] = texture;
        try {
            System.IO.Directory.CreateDirectory(Directory);
            File.WriteAllBytes(PathOf(key), texture.EncodeToPNG());
            Prune(keep);
        } catch (Exception e) {
            Debug.LogWarning($"[Customizer] Thumbnail gagal disimpan: {e.Message}");
        }
        Changed?.Invoke();
    }

    private static void Prune(IEnumerable<CustomCharacterRecord> keep) {
        var wanted = new HashSet<string>();
        foreach (CustomCharacterRecord record in keep) {
            if (record != null) wanted.Add(KeyOf(record) + Extension);
        }
        foreach (string file in System.IO.Directory.GetFiles(Directory, "*" + Extension)) {
            if (wanted.Contains(Path.GetFileName(file))) continue;
            File.Delete(file);
            Cache.Remove(Path.GetFileNameWithoutExtension(file));
        }
    }

    private static string PathOf(string key) => Path.Combine(Directory, key + Extension);

    /// FNV-1a 64-bit atas JSON record: stabil antar sesi (string.GetHashCode tidak).
    private static string KeyOf(CustomCharacterRecord record) {
        string json = JsonUtility.ToJson(record);
        ulong hash = 14695981039346656037UL;
        foreach (char c in json) {
            hash ^= c;
            hash *= 1099511628211UL;
        }
        return hash.ToString("x16");
    }
}
