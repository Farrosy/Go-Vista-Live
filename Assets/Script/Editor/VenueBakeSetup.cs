using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// Menyiapkan scene venue untuk bake: lampu statis jadi Baked, permukaan besar memakai lightmap,
/// props kecil dan karakter memakai light probe. Aman dijalankan ulang, tapi grup probe dibuat baru,
/// jadi bake harus diulang sesudahnya lewat Window > Rendering > Lighting.
/// </summary>
public static class VenueBakeSetup {
    private const string SceneFolder = "Assets/Download_Assets/PolygonNightclubs/Scenes/";
    private const string LightRootName = "Lighting (URP)";
    private const string FloorRootName = "Scene";
    private const string ProbeGroupName = "LightProbes (Baked)";

    private const float LightmapMinSizeMetres = 3f; // lantai dan dinding; di bawah ini cukup memakai probe
    private const int TransparentQueueStart = 3000;

    private const float ProbeSpacingMetres = 3f;
    private const float ProbeClearanceMetres = 0.15f;
    private const float WalkableMinNormalY = 0.7f;
    private static readonly float[] ProbeHeightsMetres = { 0.5f, 2.2f };

    private readonly struct VenueProfile {
        public readonly string SceneName;
        public readonly string[] GiRootNames;
        public readonly string[] RealtimeLightNames;

        public VenueProfile(string sceneName, string[] giRootNames, string[] realtimeLightNames) {
            SceneName = sceneName;
            GiRootNames = giRootNames;
            RealtimeLightNames = realtimeLightNames;
        }

        public string ScenePath => SceneFolder + SceneName + ".unity";
        public string SettingsPath => SceneFolder + SceneName + "Settings.lighting";
    }

    private static readonly VenueProfile DanceClub = new VenueProfile(
        VenueMap.DanceClubSceneName,
        new[] { FloorRootName, "Roof_Layer" },
        new[] { "Spot Light" }); // berputar lewat Animation, jadi tetap realtime

    private static readonly VenueProfile RooftopBar = new VenueProfile(
        VenueMap.RooftopBarSceneName,
        new[] { FloorRootName, "Roof_Layer", "Roof_Layer_02" },
        new string[0]);

    [MenuItem("Tools/Go Vista/Siapkan Bake DanceClub")]
    public static void ApplyDanceClub() => Apply(DanceClub);

    [MenuItem("Tools/Go Vista/Siapkan Bake Rooftop")]
    public static void ApplyRooftopBar() => Apply(RooftopBar);

    private static void Apply(VenueProfile venue) {
        Scene scene = EditorSceneManager.OpenScene(venue.ScenePath, OpenSceneMode.Single);
        GameObject floorRoot = FindRoot(scene, FloorRootName);
        GameObject[] giRoots = venue.GiRootNames.Select(name => FindRoot(scene, name)).ToArray();
        ApplyLightingSettings(EnsureLightingSettings(venue.SettingsPath));

        int bakedLights = SetLightsBaked(FindRoot(scene, LightRootName), venue.RealtimeLightNames);
        List<MeshRenderer> renderers = OpaqueRenderers(giRoots);
        int reimported = GenerateLightmapUvs(renderers.Where(IsLightmapSized));
        int lightmapped = MarkForGlobalIllumination(renderers);
        int stoppedProbes = StopEveryFrameReflectionProbes(scene);
        int probes = BuildLightProbes(scene, floorRoot);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log($"[VenueBake] {venue.SceneName}: lampu baked={bakedLights}, renderer GI={renderers.Count}, " +
                  $"lightmap={lightmapped}, FBX diimpor ulang={reimported}, " +
                  $"reflection probe diubah={stoppedProbes}, light probe={probes}");
    }

    private static GameObject FindRoot(Scene scene, string name) {
        GameObject root = scene.GetRootGameObjects().FirstOrDefault(candidate => candidate.name == name);
        if (root == null)
            throw new System.InvalidOperationException($"Root '{name}' tidak ada di {scene.path}.");
        return root;
    }

    /// Scene bawaan paket bisa merujuk aset pengaturan yang tidak ikut terimpor, jadi buat kalau belum ada.
    private static LightingSettings EnsureLightingSettings(string settingsPath) {
        if (Lightmapping.TryGetLightingSettings(out LightingSettings existing) && existing != null)
            return existing;

        LightingSettings settings = AssetDatabase.LoadAssetAtPath<LightingSettings>(settingsPath);
        if (settings == null) {
            settings = new LightingSettings();
            AssetDatabase.CreateAsset(settings, settingsPath);
        }
        Lightmapping.lightingSettings = settings;
        return settings;
    }

    private static int SetLightsBaked(GameObject lightRoot, string[] realtimeLightNames) {
        int count = 0;
        foreach (Light light in lightRoot.GetComponentsInChildren<Light>(false)) {
            if (realtimeLightNames.Contains(light.name))
                continue;
            light.lightmapBakeType = LightmapBakeType.Baked;
            PrefabUtility.RecordPrefabInstancePropertyModifications(light);
            count++;
        }
        return count;
    }

    /// Kaca dan efek transparan tidak ikut GI supaya tidak menghalangi cahaya saat bake.
    private static List<MeshRenderer> OpaqueRenderers(GameObject[] roots) {
        return roots
            .SelectMany(root => root.GetComponentsInChildren<MeshRenderer>(false))
            .Where(renderer => renderer.GetComponent<MeshFilter>()?.sharedMesh != null)
            .Where(renderer => renderer.sharedMaterials.All(
                material => material != null && material.renderQueue < TransparentQueueStart))
            .ToList();
    }

    private static bool IsLightmapSized(MeshRenderer renderer) =>
        renderer.bounds.size.magnitude >= LightmapMinSizeMetres;

    private static bool HasLightmapUvs(Mesh mesh) => mesh.HasVertexAttribute(VertexAttribute.TexCoord1);

    private static int GenerateLightmapUvs(IEnumerable<MeshRenderer> renderers) {
        List<ModelImporter> importers = renderers
            .Select(renderer => renderer.GetComponent<MeshFilter>().sharedMesh)
            .Where(mesh => !HasLightmapUvs(mesh))
            .Select(mesh => AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(mesh)) as ModelImporter)
            .Where(importer => importer != null && !importer.generateSecondaryUV)
            .Distinct()
            .ToList();

        AssetDatabase.StartAssetEditing();
        try {
            foreach (ModelImporter importer in importers) {
                importer.generateSecondaryUV = true;
                importer.SaveAndReimport();
            }
        } finally {
            AssetDatabase.StopAssetEditing();
        }
        return importers.Count;
    }

    /// Mesh yang tetap tanpa UV lightmap (mis. mesh bawaan) memakai probe, supaya tidak gagal saat bake.
    private static int MarkForGlobalIllumination(IEnumerable<MeshRenderer> renderers) {
        int lightmapped = 0;
        foreach (MeshRenderer renderer in renderers) {
            GameObject target = renderer.gameObject;
            StaticEditorFlags flags = GameObjectUtility.GetStaticEditorFlags(target);
            GameObjectUtility.SetStaticEditorFlags(target, flags | StaticEditorFlags.ContributeGI);

            bool useLightmap = IsLightmapSized(renderer) && HasLightmapUvs(renderer.GetComponent<MeshFilter>().sharedMesh);
            renderer.receiveGI = useLightmap ? ReceiveGI.Lightmaps : ReceiveGI.LightProbes;
            if (useLightmap)
                lightmapped++;

            PrefabUtility.RecordPrefabInstancePropertyModifications(target);
            PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
        }
        return lightmapped;
    }

    private static void ApplyLightingSettings(LightingSettings settings) {
        settings.bakedGI = true;
        settings.realtimeGI = false;
        settings.lightmapper = LightingSettings.Lightmapper.ProgressiveCPU;
        settings.lightmapResolution = 12f;
        settings.lightmapMaxSize = 1024;
        settings.directionalityMode = LightmapsMode.NonDirectional;
        settings.directSampleCount = 32;
        settings.indirectSampleCount = 128;
        settings.environmentSampleCount = 64;
        settings.maxBounces = 2;
        EditorUtility.SetDirty(settings);
    }

    private static int StopEveryFrameReflectionProbes(Scene scene) {
        int count = 0;
        foreach (GameObject root in scene.GetRootGameObjects()) {
            foreach (ReflectionProbe probe in root.GetComponentsInChildren<ReflectionProbe>(true)) {
                if (probe.mode != ReflectionProbeMode.Realtime || probe.refreshMode != ReflectionProbeRefreshMode.EveryFrame)
                    continue;
                probe.refreshMode = ReflectionProbeRefreshMode.OnAwake;
                PrefabUtility.RecordPrefabInstancePropertyModifications(probe);
                count++;
            }
        }
        return count;
    }

    private static int BuildLightProbes(Scene scene, GameObject floorRoot) {
        GameObject old = scene.GetRootGameObjects().FirstOrDefault(root => root.name == ProbeGroupName);
        if (old != null)
            Object.DestroyImmediate(old);

        Physics.SyncTransforms();
        Vector3[] positions = ProbePositions(floorRoot).ToArray();

        var host = new GameObject(ProbeGroupName);
        SceneManager.MoveGameObjectToScene(host, scene);
        host.AddComponent<LightProbeGroup>().probePositions = positions;
        return positions.Length;
    }

    /// Grid di atas setiap lantai yang bisa diinjak, di semua tingkat, tanpa posisi di dalam objek.
    private static IEnumerable<Vector3> ProbePositions(GameObject floorRoot) {
        Bounds bounds = floorRoot.GetComponentsInChildren<MeshRenderer>(false)
            .Select(renderer => renderer.bounds)
            .Aggregate((all, next) => { all.Encapsulate(next); return all; });

        var taken = new HashSet<Vector3Int>();
        for (float x = bounds.min.x; x <= bounds.max.x; x += ProbeSpacingMetres) {
            for (float z = bounds.min.z; z <= bounds.max.z; z += ProbeSpacingMetres) {
                var origin = new Vector3(x, bounds.max.y + 1f, z);
                RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, bounds.size.y + 5f, ~0, QueryTriggerInteraction.Ignore);
                foreach (RaycastHit hit in hits) {
                    if (hit.normal.y < WalkableMinNormalY || !hit.collider.transform.IsChildOf(floorRoot.transform))
                        continue;
                    foreach (float height in ProbeHeightsMetres) {
                        Vector3 position = hit.point + Vector3.up * height;
                        if (Physics.CheckSphere(position, ProbeClearanceMetres, ~0, QueryTriggerInteraction.Ignore))
                            continue;
                        if (taken.Add(Vector3Int.RoundToInt(position)))
                            yield return position;
                    }
                }
            }
        }
    }
}
