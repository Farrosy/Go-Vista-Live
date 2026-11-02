using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Posisi dan ukuran tombol mobile (joystick, lompat, lari, interaksi) yang diatur pemain lewat
/// menu Pengaturan > ATUR KONTROL. Disimpan per perangkat (PlayerPrefs), karena layar tiap HP beda.
/// Kontrol yang masih di posisi default tidak disimpan, jadi kalau layout di prefab MobileUI diubah,
/// pemain yang belum pernah mengatur ikut memakai layout baru.
/// </summary>
public static class ControlLayoutSettings {
    public const float MinScale = 0.6f;
    public const float MaxScale = 1.6f;

    private const string LayoutKey = "Settings.ControlLayout";

    [Serializable]
    private class Entry {
        public string id;
        public float x, y;
        public float scale = 1f;
    }

    [Serializable]
    private class LayoutData {
        public List<Entry> items = new List<Entry>();
    }

    private static LayoutData _data;
    private static LayoutData Data => _data ??= Load();

    public static bool TryGet(string id, out Vector2 position, out float scale) {
        Entry e = Find(id);
        position = e != null ? new Vector2(e.x, e.y) : Vector2.zero;
        scale = e != null ? e.scale : 1f;
        return e != null;
    }

    public static void Set(string id, Vector2 position, float scale) {
        Entry e = Find(id);
        if (e == null) {
            e = new Entry { id = id };
            Data.items.Add(e);
        }
        e.x = position.x;
        e.y = position.y;
        e.scale = Mathf.Clamp(scale, MinScale, MaxScale);
    }

    public static void Remove(string id) {
        Data.items.RemoveAll(e => e.id == id);
    }

    public static void Save() {
        PlayerPrefs.SetString(LayoutKey, JsonUtility.ToJson(Data));
        PlayerPrefs.Save();
    }

    private static Entry Find(string id) {
        return Data.items.Find(e => e.id == id);
    }

    private static LayoutData Load() {
        string json = PlayerPrefs.GetString(LayoutKey, "");
        if (string.IsNullOrEmpty(json))
            return new LayoutData();
        try {
            return JsonUtility.FromJson<LayoutData>(json) ?? new LayoutData();
        } catch (Exception e) {
            Debug.LogWarning($"[ControlLayout] Data layout rusak, pakai default: {e.Message}");
            return new LayoutData();
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCache() {
        _data = null;
    }
}
