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
/// lalu lintas), lalu mengisinya ke "Minimap Routes" milik Easy Minimap System. Garis GPS mengikuti GARIS TENGAH
/// jalan (seperti GTA), bukan lajur, supaya tidak zig-zag pindah lajur:
///  1. Spline lajur dipasangkan dengan lajur lawan arahnya (kedua ujung berjarak ± LaneWidth). Garis tengah
///     ruas jalan = rata-rata keduanya.
///  2. Lajur tanpa pasangan di luar persimpangan digeser supaya kedua ujungnya jatuh di titik tengah jalan terdekat.
///  3. Di persimpangan (RoadCross) lajur hanya dipakai untuk menemukan ujung-ujung jalan dan arahnya. Setiap dua
///     ujung disambung lengkung mulus (lurus kalau berhadapan), jadi semua belokan tersedia untuk GPS walau di data
///     lalu lintas sebagian belokan tidak ada, dan garis tidak menekuk tajam di mulut persimpangan.
///  4. Garis tengah disederhanakan (bagian lurus jadi sedikit titik, lengkung tetap halus) lalu dipecah supaya
///     jarak antar titik maksimal MaxNodeSpacing. Hanya UJUNG garis tengah yang digabung (PortWeldRadius), jadi
///     garis yang bersilangan di tengah persimpangan tidak saling tersambung.
///  5. Pulau kecil yang terpisah disambung ke jaringan utama kalau dekat (BridgeRadius), selain itu dibuang,
///     supaya A* tidak pernah gagal menemukan rute.
/// Hasilnya prefab Assets/Prefab/GpsRoadNetwork.prefab yang dipasang di WorldScene. Jalankan lagi setiap
/// jalan kota berubah. Titik bisa dirapikan manual lewat editor waypoint di komponen Minimap Routes (prefab).
public static class GpsRoadNetworkBuilder {
    private const string WorldScenePath = "Assets/Scenes/WorldScene.unity";
    private const string PrefabPath = "Assets/Prefab/GpsRoadNetwork.prefab";
    private const string RootName = "GpsRoadNetwork"; // = nama prefab (SaveAsPrefabAsset memberi nama root sesuai file)
    private const string WaypointSpritePath = "Assets/Art/Minimap/radar_waypoint.png";

    private const float LaneWidth = 3f;            // jarak dua lajur berlawanan di kota ini (diukur: selalu 3.0 m)
    private const float PairTolerance = 0.8f;      // total selisih (m) kedua ujung terhadap LaneWidth
    private const float PortSnapRadius = 2.5f;     // ujung lajur tanpa pasangan → titik tengah jalan terdekat
    private const float PortWeldRadius = 1f;       // ujung garis tengah yang sedekat ini dianggap satu titik
    private const float CurveSampleStep = 0.5f;    // cuplikan rapat sebelum disederhanakan
    private const float SimplifyTolerance = 0.1f;  // simpangan maksimal (m) saat menyederhanakan garis
    private const float MaxNodeSpacing = 6f;       // titik sejauh ini paling jauh; menentukan pilihan titik awal/akhir
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
            CentrelineStats stats = BuildCentrelines(scene, graph);
            (int bridged, int dropped) = graph.KeepOneNetwork(BridgeRadius);
            if (graph.Count == 0) {
                Debug.LogError("[GPS] Tidak ada spline jalan (RoadSection) di WorldScene.");
                return;
            }
            if (stats.Skipped > 0)
                Debug.LogWarning($"[GPS] {stats.Skipped} lajur tanpa pasangan dilewati karena tidak ada titik tengah jalan " +
                                 $"dalam {PortSnapRadius} m dari ujungnya.");

            GameObject prefab = BuildPrefab(graph);
            InstallInScene(scene, prefab);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[GPS] Jaringan jalan GPS (garis tengah): {graph.Count} titik, {graph.EdgeCount} sambungan dari " +
                      $"{stats.Lanes} lajur ({stats.Pairs} ruas jalan, {stats.Junctions} persimpangan dengan " +
                      $"{stats.JunctionCurves} lengkung, {stats.Shifted} lajur tunggal digeser ke tengah; " +
                      $"{bridged} pulau disambung, {dropped} titik terpencil dibuang). Prefab: {PrefabPath}");
        } finally {
            if (opened) EditorSceneManager.CloseScene(scene, true);
        }
    }

    // ------------------------------------------------------------ Garis tengah jalan

    private struct CentrelineStats {
        public int Lanes, Pairs, Shifted, Skipped, Junctions, JunctionCurves;
    }

    private sealed class Lane {
        public SplineContainer Container;
        public int Index;
        public RoadCross Cross; // null = ruas jalan biasa; isi = belokan / lurus di dalam persimpangan
        public Vector3 Start, End;
        public float Length;
        public Vector3 At(float t) => (Vector3)Container.EvaluatePosition(Index, Mathf.Clamp01(t));
    }

    private static CentrelineStats BuildCentrelines(Scene scene, RoadGraph graph) {
        var lanes = new List<Lane>();
        foreach (GameObject root in scene.GetRootGameObjects())
        foreach (RoadSection section in root.GetComponentsInChildren<RoadSection>(true)) {
            if (!section.gameObject.activeInHierarchy) continue;
            SplineContainer container = section.GetComponent<SplineContainer>();
            if (container == null) continue;
            section.GetSection(out RoadCross cross, out _, out _);
            for (int s = 0; s < container.Splines.Count; s++)
                lanes.Add(new Lane {
                    Container = container, Index = s, Cross = cross,
                    Start = (Vector3)container.EvaluatePosition(s, 0f),
                    End = (Vector3)container.EvaluatePosition(s, 1f),
                    Length = container.CalculateLength(s),
                });
        }

        int[] partner = PairOppositeLanes(lanes);
        var stats = new CentrelineStats { Lanes = lanes.Count };
        var centrelines = new List<List<Vector3>>();
        var ports = new List<Vector3>();

        var junctions = new Dictionary<RoadCross, Junction>();
        Junction JunctionOf(RoadCross cross) {
            if (!junctions.TryGetValue(cross, out Junction j)) junctions[cross] = j = new Junction();
            return j;
        }

        // 1. Pasangan lajur berlawanan → rata-ratanya. Lajur b berjalan terbalik, jadi dibaca dari 1 ke 0.
        //    Di dalam persimpangan hanya ujungnya yang dipakai (lihat langkah 3).
        for (int a = 0; a < lanes.Count; a++) {
            int b = partner[a];
            if (b < a) continue;
            Lane la = lanes[a], lb = lanes[b];
            int steps = Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(la.Length, lb.Length) / CurveSampleStep));
            var line = new List<Vector3>(steps + 1);
            for (int k = 0; k <= steps; k++) {
                float t = k / (float)steps;
                line.Add((la.At(t) + lb.At(1f - t)) * 0.5f);
            }
            Vector3 first = line[0], last = line[line.Count - 1];
            ports.Add(first);
            ports.Add(last);
            if (la.Cross != null) {
                Junction junction = JunctionOf(la.Cross);
                junction.AddEnd(first, line[1] - first);
                junction.AddEnd(last, line[line.Count - 2] - last);
            } else {
                centrelines.Add(line);
                stats.Pairs++;
            }
        }

        // 2. Lajur tanpa pasangan (di data ini semuanya belok kanan di persimpangan, belokan baliknya tidak ada).
        //    Di persimpangan: cukup catat ujungnya. Di luar persimpangan: geser supaya kedua ujungnya di titik tengah jalan.
        for (int a = 0; a < lanes.Count; a++) {
            if (partner[a] >= 0) continue;
            Lane lane = lanes[a];
            if (!TryNearest(ports, lane.Start, PortSnapRadius, out Vector3 startPort) ||
                !TryNearest(ports, lane.End, PortSnapRadius, out Vector3 endPort)) {
                stats.Skipped++;
                continue;
            }
            if (lane.Cross != null) {
                Junction junction = JunctionOf(lane.Cross);
                junction.AddEnd(startPort, lane.At(0.05f) - lane.Start);
                junction.AddEnd(endPort, lane.At(0.95f) - lane.End);
                continue;
            }
            Vector3 startShift = startPort - lane.Start, endShift = endPort - lane.End;
            int steps = Mathf.Max(1, Mathf.CeilToInt(lane.Length / CurveSampleStep));
            var line = new List<Vector3>(steps + 1);
            for (int k = 0; k <= steps; k++) {
                float t = k / (float)steps;
                line.Add(lane.At(t) + Vector3.Lerp(startShift, endShift, t));
            }
            centrelines.Add(line);
            stats.Shifted++;
        }

        // 3. Persimpangan: setiap dua ujung jalan disambung lengkung yang keluar searah jalan di kedua ujungnya
        //    (lurus kalau berhadapan). Bentuk belokan lajur lalu lintas tidak dipakai: belokan kanan yang digeser ke
        //    tengah berbentuk bintang dan menekuk tajam di ujung, dan sebagian belokan memang tidak ada di data.
        foreach (Junction junction in junctions.Values) {
            stats.Junctions++;
            for (int a = 0; a < junction.Ports.Count; a++)
            for (int b = a + 1; b < junction.Ports.Count; b++) {
                centrelines.Add(Bezier(junction.Ports[a], junction.Inward[a].normalized,
                    junction.Ports[b], junction.Inward[b].normalized));
                stats.JunctionCurves++;
            }
        }

        // 4. Masukkan ke graf: hanya ujung yang digabung, titik di tengah garis selalu titik baru.
        foreach (List<Vector3> centreline in centrelines) {
            List<Vector3> nodes = Subdivide(Simplify(centreline, SimplifyTolerance), MaxNodeSpacing);
            int previous = graph.Weld(nodes[0], PortWeldRadius);
            for (int k = 1; k < nodes.Count; k++) {
                int node = k == nodes.Count - 1 ? graph.Weld(nodes[k], PortWeldRadius) : graph.Add(nodes[k]);
                graph.Connect(previous, node);
                previous = node;
            }
        }
        return stats;
    }

    /// Ujung-ujung jalan di satu persimpangan (RoadCross), beserta arah masuk ke persimpangan dari tiap ujung.
    private sealed class Junction {
        public readonly List<Vector3> Ports = new List<Vector3>();
        public readonly List<Vector3> Inward = new List<Vector3>();

        public void AddEnd(Vector3 position, Vector3 inward) {
            inward.y = 0f;
            inward = inward.sqrMagnitude > 1e-6f ? inward.normalized : Vector3.zero;
            for (int i = 0; i < Ports.Count; i++) {
                if ((Ports[i] - position).sqrMagnitude >= PortWeldRadius * PortWeldRadius) continue;
                Inward[i] += inward; // dirata-rata (dinormalkan saat dipakai)
                return;
            }
            Ports.Add(position);
            Inward.Add(inward);
        }
    }

    /// Bezier kubik dari `from` ke `to` yang berangkat searah fromInward dan tiba berlawanan arah toInward.
    /// Panjang pegangan 0.39 x tali busur ≈ seperempat lingkaran untuk belokan 90°; lurus kalau kedua ujung berhadapan.
    private static List<Vector3> Bezier(Vector3 from, Vector3 fromInward, Vector3 to, Vector3 toInward) {
        float handle = Vector3.Distance(from, to) * 0.39f;
        Vector3 c1 = from + fromInward * handle, c2 = to + toInward * handle;
        int steps = Mathf.Max(2, Mathf.CeilToInt(Vector3.Distance(from, to) / CurveSampleStep));
        var line = new List<Vector3>(steps + 1);
        for (int k = 0; k <= steps; k++) {
            float t = k / (float)steps, u = 1f - t;
            line.Add(u * u * u * from + 3f * u * u * t * c1 + 3f * u * t * t * c2 + t * t * t * to);
        }
        return line;
    }

    /// Lajur a berpasangan dengan lajur b kalau b berlawanan arah di sebelahnya: awal a dekat akhir b dan
    /// akhir a dekat awal b, masing-masing ± LaneWidth. Dipilih pasangan dengan selisih terkecil.
    private static int[] PairOppositeLanes(List<Lane> lanes) {
        var partner = new int[lanes.Count];
        for (int i = 0; i < partner.Length; i++) partner[i] = -1;
        for (int a = 0; a < lanes.Count; a++) {
            if (partner[a] >= 0) continue;
            int best = -1;
            float bestError = PairTolerance;
            for (int b = 0; b < lanes.Count; b++) {
                if (b == a || partner[b] >= 0) continue;
                float error = Mathf.Abs(Vector3.Distance(lanes[a].Start, lanes[b].End) - LaneWidth) +
                              Mathf.Abs(Vector3.Distance(lanes[a].End, lanes[b].Start) - LaneWidth);
                if (error < bestError) { bestError = error; best = b; }
            }
            if (best < 0) continue;
            partner[a] = best;
            partner[best] = a;
        }
        return partner;
    }

    private static bool TryNearest(List<Vector3> points, Vector3 from, float radius, out Vector3 nearest) {
        nearest = default;
        float best = radius * radius;
        bool found = false;
        foreach (Vector3 p in points) {
            float d = (p - from).sqrMagnitude;
            if (d >= best) continue;
            best = d;
            nearest = p;
            found = true;
        }
        return found;
    }

    /// Douglas–Peucker: buang titik yang menyimpang kurang dari tolerance dari garis lurus antar tetangganya.
    private static List<Vector3> Simplify(List<Vector3> line, float tolerance) {
        var keep = new bool[line.Count];
        keep[0] = keep[line.Count - 1] = true;
        var stack = new Stack<(int from, int to)>();
        stack.Push((0, line.Count - 1));
        while (stack.Count > 0) {
            (int from, int to) = stack.Pop();
            float worst = tolerance;
            int index = -1;
            for (int i = from + 1; i < to; i++) {
                float d = DistanceToSegment(line[i], line[from], line[to]);
                if (d > worst) { worst = d; index = i; }
            }
            if (index < 0) continue;
            keep[index] = true;
            stack.Push((from, index));
            stack.Push((index, to));
        }
        var result = new List<Vector3>();
        for (int i = 0; i < line.Count; i++)
            if (keep[i]) result.Add(line[i]);
        return result;
    }

    private static float DistanceToSegment(Vector3 p, Vector3 a, Vector3 b) {
        Vector3 ab = b - a;
        float t = ab.sqrMagnitude > 1e-6f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
        return Vector3.Distance(p, a + ab * t);
    }

    /// Pecah ruas yang lebih panjang dari maxSpacing menjadi ruas-ruas sama panjang.
    private static List<Vector3> Subdivide(List<Vector3> line, float maxSpacing) {
        var result = new List<Vector3> { line[0] };
        for (int i = 1; i < line.Count; i++) {
            int parts = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(line[i - 1], line[i]) / maxSpacing));
            for (int k = 1; k <= parts; k++) result.Add(Vector3.Lerp(line[i - 1], line[i], k / (float)parts));
        }
        return result;
    }

    // ------------------------------------------------------------ Graf jalan

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

        public int Add(Vector3 position) {
            _nodes.Add(position);
            _links.Add(new HashSet<int>());
            return _nodes.Count - 1;
        }

        public void Connect(int a, int b) {
            if (a == b) return;
            _links[a].Add(b);
            _links[b].Add(a);
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
                    Bridge(bestA, bestB);
                    bridged++;
                    linked = true;
                }
                if (!linked) return (bridged, DropAllBut(component, largest));
            }
            return (bridged, 0);
        }

        /// Sambung dua titik. Kalau keduanya ujung buntu (mis. tikungan jalan tanpa lajur lalu lintas), sambungannya
        /// lengkung yang meneruskan arah kedua jalan, bukan garis lurus yang memotong sudut.
        private void Bridge(int a, int b) {
            if (_links[a].Count != 1 || _links[b].Count != 1) {
                Connect(a, b);
                return;
            }
            Vector3 outA = Outward(a), outB = Outward(b);
            List<Vector3> curve = Subdivide(Simplify(Bezier(_nodes[a], outA, _nodes[b], outB), SimplifyTolerance),
                MaxNodeSpacing);
            int previous = a;
            for (int k = 1; k < curve.Count - 1; k++) {
                int node = Add(curve[k]);
                Connect(previous, node);
                previous = node;
            }
            Connect(previous, b);
        }

        private Vector3 Outward(int deadEnd) {
            foreach (int neighbour in _links[deadEnd]) {
                Vector3 d = _nodes[deadEnd] - _nodes[neighbour];
                d.y = 0f;
                return d.normalized;
            }
            return Vector3.zero;
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
