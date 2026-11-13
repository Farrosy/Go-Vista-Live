using System.Collections.Generic;
using System.IO;
using ITHappy;
using MTAssets.EasyMinimapSystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Splines;

/// Membangun jaringan jalan untuk GPS (GpsRoute) dari spline jalan kota (RoadSection, sama dengan yang dipakai
/// lalu lintas), lalu mengisinya ke "Minimap Routes" milik Easy Minimap System:
///  1. Setiap spline lajur dicuplik tiap SampleStep meter; titik yang berdekatan (WeldRadius) digabung,
///     jadi ujung lajur yang bertemu di persimpangan otomatis tersambung.
///  2. Lajur yang bersebelahan disambung (LaneLinkRadius) supaya rute bisa menyeberang ke lajur lain.
///  3. Pulau kecil yang terpisah disambung ke jaringan utama kalau dekat (BridgeRadius), selain itu dibuang,
///     supaya A* tidak pernah gagal menemukan rute.
/// Hasilnya prefab Assets/Prefab/GpsRoadNetwork.prefab yang dipasang di WorldScene. Jalankan lagi setiap
/// jalan kota berubah. Titik bisa dirapikan manual lewat editor waypoint di komponen Minimap Routes (prefab).
public static class GpsRoadNetworkBuilder {
    private const string WorldScenePath = "Assets/Scenes/WorldScene.unity";
    private const string PrefabPath = "Assets/Prefab/GpsRoadNetwork.prefab";
    private const string RootName = "GpsRoadNetwork"; // = nama prefab (SaveAsPrefabAsset memberi nama root sesuai file)
    private const string WaypointSpritePath = "Assets/Art/Minimap/radar_waypoint.png";

    private const float SampleStep = 8f;
    private const float WeldRadius = 2.5f;
    private const float LaneLinkRadius = 6f;
    private const float BridgeRadius = 25f;

    private const float WaypointPixels = 26f;
    private const int WaypointOrder = 3; // di atas tempat penting (2), di bawah pemain lain (4)
    private static readonly Color CustomColor = new Color32(214, 64, 224, 255);

    [MenuItem("Go Vista/Minimap/Bangun jaringan jalan GPS (WorldScene)")]
    public static void Build() {
        Scene scene = SceneManager.GetSceneByPath(WorldScenePath);
        bool opened = !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(WorldScenePath, OpenSceneMode.Additive);
        try {
            var graph = new RoadGraph();
            int splines = SampleRoads(scene, graph);
            graph.LinkNeighbours(LaneLinkRadius);
            (int bridged, int dropped) = graph.KeepOneNetwork(BridgeRadius);
            if (graph.Count == 0) {
                Debug.LogError("[GPS] Tidak ada spline jalan (RoadSection) di WorldScene.");
                return;
            }

            GameObject prefab = BuildPrefab(graph);
            InstallInScene(scene, prefab);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[GPS] Jaringan jalan GPS: {graph.Count} titik, {graph.EdgeCount} sambungan dari {splines} spline " +
                      $"({bridged} pulau disambung, {dropped} titik terpencil dibuang). Prefab: {PrefabPath}");
        } finally {
            if (opened) EditorSceneManager.CloseScene(scene, true);
        }
    }

    // ------------------------------------------------------------ Graf jalan

    private static int SampleRoads(Scene scene, RoadGraph graph) {
        int splines = 0;
        foreach (GameObject root in scene.GetRootGameObjects())
        foreach (RoadSection section in root.GetComponentsInChildren<RoadSection>(true)) {
            if (!section.gameObject.activeInHierarchy) continue;
            SplineContainer container = section.GetComponent<SplineContainer>();
            if (container == null) continue;
            for (int s = 0; s < container.Splines.Count; s++) {
                splines++;
                float length = container.CalculateLength(s);
                int steps = Mathf.Max(1, Mathf.CeilToInt(length / SampleStep));
                int previous = -1;
                for (int k = 0; k <= steps; k++) {
                    int node = graph.Weld((Vector3)container.EvaluatePosition(s, k / (float)steps), WeldRadius);
                    if (previous >= 0) graph.Connect(previous, node);
                    previous = node;
                }
            }
        }
        return splines;
    }

    private sealed class RoadGraph {
        private readonly List<Vector3> _nodes = new List<Vector3>();
        private readonly List<HashSet<int>> _links = new List<HashSet<int>>();

        public int Count => _nodes.Count;
        public Vector3 this[int index] => _nodes[index];
        public IEnumerable<int> LinksOf(int index) => _links[index];

        public int EdgeCount {
            get {
                int sum = 0;
                foreach (HashSet<int> links in _links) sum += links.Count;
                return sum / 2;
            }
        }

        public int Weld(Vector3 position, float radius) {
            float sq = radius * radius;
            for (int i = 0; i < _nodes.Count; i++)
                if ((_nodes[i] - position).sqrMagnitude < sq) return i;
            _nodes.Add(position);
            _links.Add(new HashSet<int>());
            return _nodes.Count - 1;
        }

        public void Connect(int a, int b) {
            if (a == b) return;
            _links[a].Add(b);
            _links[b].Add(a);
        }

        public void LinkNeighbours(float radius) {
            float sq = radius * radius;
            for (int i = 0; i < _nodes.Count; i++)
            for (int j = i + 1; j < _nodes.Count; j++)
                if ((_nodes[i] - _nodes[j]).sqrMagnitude < sq) Connect(i, j);
        }

        /// Sambungkan pulau kecil ke jaringan terbesar kalau cukup dekat, buang sisanya. Mengembalikan
        /// (jumlah pulau yang disambung, jumlah titik yang dibuang).
        public (int bridged, int dropped) KeepOneNetwork(float bridgeRadius) {
            int bridged = 0;
            while (true) {
                int[] component = Components(out int count, out int largest);
                if (count <= 1) break;
                bool linked = false;
                for (int c = 0; c < count && !linked; c++) {
                    if (c == largest) continue;
                    int bestA = -1, bestB = -1;
                    float best = bridgeRadius * bridgeRadius;
                    for (int a = 0; a < _nodes.Count; a++) {
                        if (component[a] != c) continue;
                        for (int b = 0; b < _nodes.Count; b++) {
                            if (component[b] != largest) continue;
                            float d = (_nodes[a] - _nodes[b]).sqrMagnitude;
                            if (d < best) { best = d; bestA = a; bestB = b; }
                        }
                    }
                    if (bestA < 0) continue;
                    Connect(bestA, bestB);
                    bridged++;
                    linked = true;
                }
                if (!linked) return (bridged, DropAllBut(component, largest));
            }
            return (bridged, 0);
        }

        private int[] Components(out int count, out int largest) {
            var component = new int[_nodes.Count];
            for (int i = 0; i < component.Length; i++) component[i] = -1;
            var sizes = new List<int>();
            var queue = new Queue<int>();
            for (int i = 0; i < _nodes.Count; i++) {
                if (component[i] >= 0) continue;
                int id = sizes.Count, size = 0;
                component[i] = id;
                queue.Enqueue(i);
                while (queue.Count > 0) {
                    int n = queue.Dequeue();
                    size++;
                    foreach (int m in _links[n])
                        if (component[m] < 0) { component[m] = id; queue.Enqueue(m); }
                }
                sizes.Add(size);
            }
            count = sizes.Count;
            largest = 0;
            for (int i = 1; i < sizes.Count; i++)
                if (sizes[i] > sizes[largest]) largest = i;
            return component;
        }

        private int DropAllBut(int[] component, int keep) {
            var remap = new int[_nodes.Count];
            var nodes = new List<Vector3>();
            for (int i = 0; i < _nodes.Count; i++) {
                remap[i] = component[i] == keep ? nodes.Count : -1;
                if (remap[i] >= 0) nodes.Add(_nodes[i]);
            }
            var links = new List<HashSet<int>>();
            for (int i = 0; i < _nodes.Count; i++) {
                if (remap[i] < 0) continue;
                var set = new HashSet<int>();
                foreach (int m in _links[i]) set.Add(remap[m]);
                links.Add(set);
            }
            int dropped = _nodes.Count - nodes.Count;
            _nodes.Clear();
            _nodes.AddRange(nodes);
            _links.Clear();
            _links.AddRange(links);
            return dropped;
        }
    }

    // ------------------------------------------------------------ Prefab

    private static GameObject BuildPrefab(RoadGraph graph) {
        var root = new GameObject(RootName);
        try {
            var routesObject = new GameObject("Routes");
            routesObject.transform.SetParent(root.transform, false);
            MinimapRoutes routes = routesObject.AddComponent<MinimapRoutes>();
            routes.currentExistingWaypointsItems = new List<MinimapRoutes.WaypointItem>(graph.Count);
            for (int i = 0; i < graph.Count; i++)
                routes.currentExistingWaypointsItems.Add(new MinimapRoutes.WaypointItem {
                    thisWaypointId = i,
                    globalPosition = graph[i],
                    connectedWaypointsIds = new List<int>(graph.LinksOf(i)),
                });
            routes.lineRenderColor = new Color32(255, 204, 64, 255);
            routes.lineWidthSize = 3f;
            routes.showRoutesOnStart = false;
            routes.routesUpdatesFrequency = MinimapRoutes.RoutesUpdateFrequency.x4PerSecond;
            // Trotoar, plaza, dan jalan di kota ini hampir rata; toleransi longgar supaya titik jalan terdekat selalu terpilih.
            routes.startingHeightTolerance = 6f;
            routes.startingPointFindMethod = MinimapRoutes.StartingFindMethod.OptimizedMathCalcs;

            var destination = new GameObject("Destination");
            destination.transform.SetParent(root.transform, false);

            var blipObject = new GameObject("Waypoint Blip");
            blipObject.transform.SetParent(root.transform, false);
            MinimapBlip blip = blipObject.AddComponent<MinimapBlip>();
            var serializedBlip = new SerializedObject(blip);
            serializedBlip.FindProperty("pixelSize").floatValue = WaypointPixels;
            serializedBlip.FindProperty("keepUpright").boolValue = true;
            serializedBlip.FindProperty("clampToEdge").boolValue = true;
            serializedBlip.ApplyModifiedPropertiesWithoutUndo();
            MinimapItem item = blip.Item;
            item.itemSprite = EnsureWaypointSprite();
            item.spriteColor = CustomColor;
            item.orderInLayer = WaypointOrder;
            item.raycastTarget = false;
            item.enabled = false; // GpsRoute menyalakannya saat pemain memasang tujuan

            GpsRoute gps = root.AddComponent<GpsRoute>();
            var serializedGps = new SerializedObject(gps);
            serializedGps.FindProperty("routes").objectReferenceValue = routes;
            serializedGps.FindProperty("destination").objectReferenceValue = destination.transform;
            serializedGps.FindProperty("waypointBlip").objectReferenceValue = blip;
            serializedGps.FindProperty("customColor").colorValue = CustomColor;
            serializedGps.ApplyModifiedPropertiesWithoutUndo();

            Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
            return PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        } finally {
            Object.DestroyImmediate(root);
        }
    }

    private static void InstallInScene(Scene scene, GameObject prefab) {
        foreach (GameObject root in scene.GetRootGameObjects()) {
            if (root.name != RootName) continue;
            if (PrefabUtility.GetCorrespondingObjectFromSource(root) == prefab) return; // prefab sudah diperbarui di tempat
            Object.DestroyImmediate(root);
        }
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
    }

    // ------------------------------------------------------------ Ikon tujuan

    /// Ikon tujuan buatan pemain: lingkaran putih bergaris gelap dengan titik di tengah (diwarnai lewat spriteColor).
    private static Sprite EnsureWaypointSprite() {
        if (!File.Exists(WaypointSpritePath)) {
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            float center = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++) {
                float r = Mathf.Sqrt((x - center) * (x - center) + (y - center) * (y - center)) / (size * 0.5f);
                // Dari luar ke dalam: garis gelap, isi putih (diwarnai), titik tengah gelap.
                float outer = Mathf.Clamp01((0.96f - r) * size * 0.5f);
                float fill = Mathf.Clamp01((0.80f - r) * size * 0.5f);
                float dot = Mathf.Clamp01((0.30f - r) * size * 0.5f);
                Color dark = new Color(0.12f, 0.08f, 0.16f, 1f);
                Color color = Color.Lerp(dark, Color.white, fill);
                color = Color.Lerp(color, dark, dot);
                color.a = outer;
                pixels[y * size + x] = color;
            }
            texture.SetPixels32(pixels);
            File.WriteAllBytes(WaypointSpritePath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(WaypointSpritePath);
        }
        if (AssetImporter.GetAtPath(WaypointSpritePath) is TextureImporter importer &&
            importer.textureType != TextureImporterType.Sprite) {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(WaypointSpritePath);
    }
}
