using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Daftar karakter yang bisa dipilih pemain (menu KARAKTER di menu utama).
/// Asset: Assets/Resources/CharacterDatabase.asset. Tambah/ubah karakter cukup lewat Inspector asset itu.
///
/// Syarat tiap karakter:
///  - Avatar Prefab ada di folder Resources (Realtime.Instantiate memanggil lewat nama), berisi
///    RealtimeView, RealtimeTransform, PlayerController, PlayerAnimSync, dan model ber-Animator sebagai child.
///  - Animator Controller-nya punya parameter Speed (float), IsGrounded (bool), Jump (trigger).
/// </summary>
[CreateAssetMenu(menuName = "Go Vista/Character Database", fileName = "CharacterDatabase")]
public class CharacterDatabase : ScriptableObject {
    public const string ResourcePath = "CharacterDatabase";

    [Serializable]
    public class Entry {
        [Tooltip("ID yang disimpan di akun pemain. Jangan diganti setelah rilis.")]
        public string id;
        public string displayName;
        [TextArea(2, 5)] public string description;
        [Tooltip("Prefab avatar multiplayer, harus di folder Resources.")]
        public GameObject avatarPrefab;
        [Tooltip("Opsional: model untuk preview di menu. Kosong = model (Animator) di dalam Avatar Prefab.")]
        public GameObject previewModel;

        public GameObject PreviewSource {
            get {
                if (previewModel) return previewModel;
                if (!avatarPrefab) return null;
                Animator animator = avatarPrefab.GetComponentInChildren<Animator>(true);
                return animator ? animator.gameObject : null;
            }
        }
    }

    [SerializeField] private List<Entry> characters = new List<Entry>();

    public int Count => characters.Count;
    public Entry this[int index] => characters[index];

    private static CharacterDatabase _instance;
    public static CharacterDatabase Instance => _instance ? _instance : _instance = Resources.Load<CharacterDatabase>(ResourcePath);

    /// Karakter dengan id ini, atau karakter pertama (default) kalau id kosong / tidak dikenal.
    public Entry Get(string id) {
        int index = IndexOf(id);
        if (index >= 0) return characters[index];
        return characters.Count > 0 ? characters[0] : null;
    }

    public int IndexOf(string id) {
        if (string.IsNullOrEmpty(id)) return -1;
        return characters.FindIndex(c => c.id == id);
    }

#if UNITY_EDITOR
    public void EditorSetCharacters(List<Entry> list) => characters = list;

    private void OnValidate() {
        foreach (Entry c in characters) {
            if (c.avatarPrefab && !UnityEditor.AssetDatabase.GetAssetPath(c.avatarPrefab).Contains("/Resources/"))
                Debug.LogWarning($"[CharacterDatabase] Avatar Prefab '{c.avatarPrefab.name}' ({c.displayName}) " +
                                 "harus di folder Resources supaya bisa di-spawn Normcore.", this);
        }
    }
#endif
}
