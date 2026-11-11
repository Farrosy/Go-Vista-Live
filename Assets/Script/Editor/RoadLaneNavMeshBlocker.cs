using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Supaya NPC tidak jalan di lajur mobil: tile jalan (mesh "road_*") adalah SATU mesh berisi trotoar + aspal,
/// jadi NavMeshModifier per objek tidak bisa memisahkannya. Tool ini mencari permukaan aspal di mesh
/// (permukaan menghadap atas yang lebih rendah dari trotoar: aspal y lokal -0.1, trotoar 0) lalu memasang
/// NavMeshModifierVolume "Not Walkable" di atasnya, sebagai child "NavBlock_Lanes" di tiap tile.
///
/// Jalankan lagi setiap ada tile jalan baru, lalu bake ulang NavMesh Surface.
/// </summary>
public static class RoadLaneNavMeshBlocker {
    private const string ChildName = "NavBlock_Lanes";
    private const string RoadMeshPrefix = "road_";
    private const float CellSize = 0.25f;
    private const float AsphaltMaxLocalY = -0.05f;   // trotoar = 0, aspal = -0.1, marka/zebra = -0.09
    private const float MinUpNormalY = 0.9f;
    private const float VolumeHeight = 1f;
    private const string NotWalkableArea = "Not Walkable";

    [MenuItem("Go Vista/NavMesh/Blokir Lajur Aspal Jalan")]
    public static void BlockAllRoadLanes() {
        int area = NavMesh.GetAreaFromName(NotWalkableArea);
        if (area < 0) {
            Debug.LogError($"[RoadLaneNavMeshBlocker] Area \"{NotWalkableArea}\" tidak ditemukan.");
            return;
        }

        var cache = new Dictionary<Mesh, List<Rect>>();
        int tiles = 0, volumes = 0;
        foreach (MeshFilter filter in FindRoadTiles()) {
            if (!cache.TryGetValue(filter.sharedMesh, out List<Rect> rects)) {
                rects = FindAsphaltRects(filter.sharedMesh);
                cache[filter.sharedMesh] = rects;
            }
            RemoveBlocker(filter.transform);
            if (rects.Count == 0) continue;
            CreateBlocker(filter.transform, rects, area);
            tiles++;
            volumes += rects.Count;
        }
        EditorSceneManager.MarkAllScenesDirty();
        Debug.Log($"[RoadLaneNavMeshBlocker] {volumes} volume di {tiles} tile jalan ({cache.Count} jenis mesh). " +
                  "Bake ulang NavMesh Surface supaya berlaku.");
    }

    [MenuItem("Go Vista/NavMesh/Hapus Blokir Lajur Aspal")]
    public static void RemoveAllRoadLaneBlocks() {
        int removed = 0;
        foreach (MeshFilter filter in FindRoadTiles())
            if (RemoveBlocker(filter.transform)) removed++;
        EditorSceneManager.MarkAllScenesDirty();
        Debug.Log($"[RoadLaneNavMeshBlocker] {removed} blokir dihapus.");
    }

    private static IEnumerable<MeshFilter> FindRoadTiles() {
        foreach (MeshFilter filter in Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Exclude)) {
            Mesh mesh = filter.sharedMesh;
            if (mesh != null && mesh.name.ToLowerInvariant().StartsWith(RoadMeshPrefix))
                yield return filter;
        }
    }

    private static bool RemoveBlocker(Transform tile) {
        Transform old = tile.Find(ChildName);
        if (old == null) return false;
        Undo.DestroyObjectImmediate(old.gameObject);
        return true;
    }

    private static void CreateBlocker(Transform tile, List<Rect> rects, int area) {
        var go = new GameObject(ChildName);
        Undo.RegisterCreatedObjectUndo(go, "Blokir lajur aspal");
        go.transform.SetParent(tile, false);
        go.isStatic = true;
        foreach (Rect r in rects) {
            var volume = go.AddComponent<NavMeshModifierVolume>();
            volume.area = area;
            volume.center = new Vector3(r.center.x, 0f, r.center.y);
            volume.size = new Vector3(r.width, VolumeHeight, r.height);
        }
    }

    /// Area aspal di ruang lokal mesh (x,z), digabung jadi sesedikit mungkin persegi panjang.
    private static List<Rect> FindAsphaltRects(Mesh mesh) {
        List<Vector3[]> asphalt = FindAsphaltTriangles(mesh);
        Bounds b = mesh.bounds;
        int nx = Mathf.CeilToInt(b.size.x / CellSize), nz = Mathf.CeilToInt(b.size.z / CellSize);
        var grid = new bool[nx, nz];
        for (int ix = 0; ix < nx; ix++)
            for (int iz = 0; iz < nz; iz++) {
                var p = new Vector2(b.min.x + (ix + 0.5f) * CellSize, b.min.z + (iz + 0.5f) * CellSize);
                grid[ix, iz] = asphalt.Exists(t => InsideXZ(p, t));
            }
        return MergeCells(grid, nx, nz, new Vector2(b.min.x, b.min.z));
    }

    private static List<Vector3[]> FindAsphaltTriangles(Mesh mesh) {
        Vector3[] v = mesh.vertices;
        int[] tris = mesh.triangles;
        var result = new List<Vector3[]>();
        for (int i = 0; i < tris.Length; i += 3) {
            Vector3 a = v[tris[i]], b = v[tris[i + 1]], c = v[tris[i + 2]];
            Vector3 normal = Vector3.Cross(b - a, c - a);
            if (normal.sqrMagnitude < 1e-10f || normal.normalized.y < MinUpNormalY) continue;
            if ((a.y + b.y + c.y) / 3f > AsphaltMaxLocalY) continue;
            result.Add(new[] { a, b, c });
        }
        return result;
    }

    private static bool InsideXZ(Vector2 p, Vector3[] t) {
        float d1 = Side(p, t[0], t[1]), d2 = Side(p, t[1], t[2]), d3 = Side(p, t[2], t[0]);
        bool hasNeg = d1 < 0 || d2 < 0 || d3 < 0, hasPos = d1 > 0 || d2 > 0 || d3 > 0;
        return !(hasNeg && hasPos);
    }

    private static float Side(Vector2 p, Vector3 a, Vector3 b) =>
        (p.x - b.x) * (a.z - b.z) - (a.x - b.x) * (p.y - b.z);

    private static List<Rect> MergeCells(bool[,] grid, int nx, int nz, Vector2 origin) {
        var used = new bool[nx, nz];
        var rects = new List<Rect>();
        for (int iz = 0; iz < nz; iz++)
            for (int ix = 0; ix < nx; ix++) {
                if (!grid[ix, iz] || used[ix, iz]) continue;
                int w = 1;
                while (ix + w < nx && grid[ix + w, iz] && !used[ix + w, iz]) w++;
                int h = 1;
                while (iz + h < nz && RowFree(grid, used, ix, w, iz + h)) h++;
                for (int x = ix; x < ix + w; x++)
                    for (int z = iz; z < iz + h; z++) used[x, z] = true;
                rects.Add(new Rect(origin.x + ix * CellSize, origin.y + iz * CellSize, w * CellSize, h * CellSize));
            }
        return rects;
    }

    private static bool RowFree(bool[,] grid, bool[,] used, int ix, int w, int iz) {
        for (int x = ix; x < ix + w; x++)
            if (!grid[x, iz] || used[x, iz]) return false;
        return true;
    }
}
