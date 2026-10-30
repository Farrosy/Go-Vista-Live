using UnityEditor;
using UnityEngine;

/// <summary>
/// Banyak aset Megacity (jalan, trotoar, bangunan) cuma punya mesh tanpa collider, jadi pemain
/// menembus atau tenggelam sampai ke Terrain di bawahnya.
///
/// Cara pakai: pilih objek di Hierarchy (boleh banyak sekaligus / parent-nya), lalu menu
/// "Go Vista > Tambah Mesh Collider ke Selection". Semua child yang punya MeshFilter dan belum
/// punya Collider diberi MeshCollider. Bisa di-Undo (Ctrl+Z). Jangan lupa save scene.
/// </summary>
public static class ColliderTools {
    [MenuItem("Go Vista/Tambah Mesh Collider ke Selection")]
    private static void AddMeshCollidersToSelection() {
        int added = 0, skipped = 0;
        Undo.SetCurrentGroupName("Tambah Mesh Collider");
        int group = Undo.GetCurrentGroup();

        foreach (GameObject root in Selection.gameObjects) {
            foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true)) {
                if (filter.sharedMesh == null || filter.GetComponent<Collider>() != null) {
                    skipped++;
                    continue;
                }
                var meshCollider = Undo.AddComponent<MeshCollider>(filter.gameObject);
                meshCollider.sharedMesh = filter.sharedMesh;
                added++;
            }
        }

        Undo.CollapseUndoOperations(group);
        Debug.Log($"[ColliderTools] MeshCollider ditambahkan: {added}, dilewati (sudah ada collider / tanpa mesh): {skipped}.");
    }

    [MenuItem("Go Vista/Tambah Mesh Collider ke Selection", true)]
    private static bool ValidateSelection() => Selection.gameObjects.Length > 0;
}
