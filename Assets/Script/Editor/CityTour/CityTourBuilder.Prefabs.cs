using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// Prefab dan aset visual Tur Kota: maskot Vio & Vira (model chibi lama), marker lingkaran + beacon, efek poof.
public static partial class CityTourBuilder {
    private const string PrefabFolder = "Assets/Prefab/CityTour";
    private const string ArtFolder = "Assets/Art/CityTour";
    // Model yang sama dengan pemain chibi lama (Player.prefab / Player Female.prefab). Tulangnya "root/pelvis/...",
    // cocok dengan klip MaleChibiAnimation. FBX di Models/*Chibi memakai "Armature/..." sehingga animasinya T-pose.
    private const string VioModelPath = "Assets/Characters/Animation/MaleChibiAnimation/idle_loop_unity_fit.fbx";
    private const string ViraModelPath = "Assets/Characters/Animation/FemaleChibiAnimation/idle_loop_unity_fit (1).fbx";
    // Material URP Lit bertekstur. Material bawaan FBX bukan URP, jadi modelnya tampil putih polos.
    private const string VioMaterialPath = "Assets/Characters/Models/MaleChibi/Materials/Meshy_AI_stylized_chibi_boy_b_biped_texture_0.mat";
    private const string ViraMaterialPath = "Assets/Characters/Models/FemaleChibi/Materials/Meshy_AI_G_Cap_Girl_in_Pink_Ho_biped_texture_0.mat";
    // Controller chibi lama (dulu dipakai Player.prefab / Player Female.prefab): state default Locomotion = idle.
    // MascotDuo menyetel IsGrounded = true supaya tidak pindah ke pose InAir.
    private const string VioControllerPath = "Assets/Characters/Animation/AnimationController/MaleAnimationController.controller";
    private const string ViraControllerPath = "Assets/Characters/Animation/AnimationController/FemaleAnimationController.controller";
    private const string IdleClipPath = "Assets/Characters/Animation/MaleChibiAnimation/idle_loop_fit.anim";
    private const float IdleSampleTime = 0.5f;
    private const string ParticleMaterialPath = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/ParticlesUnlit.mat";

    // Sama dengan tinggi pemain dan NPC (1,80 m, termasuk rambut/topi).
    private const float MascotHeight = 1.8f;
    private const float MascotSpacing = 1.5f;
    // Lingkaran tur di depan pintu; trotoar depan sebagian gedung sempit, jadi jangan terlalu besar.
    private const float RingRadius = 3f;
    private const float BeaconHeight = 60f;
    private const float BeaconRadius = 1.4f;

    private static readonly Color Gold = new Color32(255, 204, 64, 255);

    private sealed class TourPrefabs {
        public GameObject Duo, Marker, Poof;
    }

    private static TourPrefabs BuildPrefabs() {
        Directory.CreateDirectory(PrefabFolder);
        Directory.CreateDirectory(ArtFolder);
        var vio = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(VioControllerPath);
        var vira = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ViraControllerPath);
        if (vio == null || vira == null) Debug.LogWarning("[Tour] Controller animasi chibi lama tidak ditemukan.");

        var prefabs = new TourPrefabs {
            Duo = BuildDuoPrefab(vio, vira),
            Marker = BuildMarkerPrefab(),
            Poof = BuildPoofPrefab(),
        };
        AssetDatabase.SaveAssets();
        return prefabs;
    }

    // ------------------------------------------------------------ Maskot

    private static GameObject BuildDuoPrefab(RuntimeAnimatorController vioController, RuntimeAnimatorController viraController) {
        var root = new GameObject("Mascot_VioVira");
        try {
            Transform vio = BuildMascot(root.transform, "Vio", VioModelPath, VioMaterialPath, vioController, -MascotSpacing * 0.5f);
            Transform vira = BuildMascot(root.transform, "Vira", ViraModelPath, ViraMaterialPath, viraController, MascotSpacing * 0.5f);
            var focus = new GameObject("FocusPoint").transform;
            focus.SetParent(root.transform, false);
            focus.localPosition = new Vector3(0f, MascotHeight * 0.8f, 0f);

            var duo = root.AddComponent<MascotDuo>();
            var serialized = new SerializedObject(duo);
            SerializedProperty mascots = serialized.FindProperty("mascots");
            mascots.arraySize = 2;
            SetMascot(mascots.GetArrayElementAtIndex(0), CityTourContent.Vio, vio);
            SetMascot(mascots.GetArrayElementAtIndex(1), CityTourContent.Vira, vira);
            serialized.FindProperty("focusPoint").objectReferenceValue = focus;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabFolder}/Mascot_VioVira.prefab");
        } finally {
            Object.DestroyImmediate(root);
        }
    }

    private static Transform BuildMascot(Transform parent, string name, string modelPath, string materialPath,
        RuntimeAnimatorController controller, float x) {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        var body = (GameObject)PrefabUtility.InstantiatePrefab(model);
        PrefabUtility.UnpackPrefabInstance(body, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
        body.name = name;
        body.transform.SetParent(parent, false);
        body.transform.localPosition = new Vector3(x, 0f, 0f);
        var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material != null)
            foreach (Renderer renderer in body.GetComponentsInChildren<Renderer>())
                renderer.sharedMaterials = System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Repeat(material, renderer.sharedMaterials.Length));
        else
            Debug.LogWarning($"[Tour] Material maskot tidak ditemukan: {materialPath}");

        // Ukur dan tempelkan ke tanah dalam pose IDLE (pose yang dipakai Animator saat main), bukan pose bind:
        // pose idle mengangkat badan sekitar 0,2 m, sehingga maskot yang diratakan dari pose bind tampak melayang.
        // Pose idle juga ikut tersimpan di prefab, jadi di Editor maskot tidak tampil T-pose.
        var idle = AssetDatabase.LoadAssetAtPath<AnimationClip>(IdleClipPath);
        if (idle != null) idle.SampleAnimation(body, IdleSampleTime);
        else Debug.LogWarning($"[Tour] Klip idle tidak ditemukan: {IdleClipPath}");
        Bounds posed = PosedBounds(body);
        float scale = posed.size.y > 0.01f ? MascotHeight / posed.size.y : 1f;
        body.transform.localScale = Vector3.one * scale;
        posed = PosedBounds(body);
        body.transform.localPosition += Vector3.up * (parent.position.y - posed.min.y);
        float feetLocalY = (parent.position.y - body.transform.position.y) / scale;

        if (!body.TryGetComponent(out Animator animator)) animator = body.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

        var collider = body.AddComponent<CapsuleCollider>();
        collider.height = MascotHeight / scale;
        collider.radius = 0.3f / scale;
        collider.center = new Vector3(0f, feetLocalY + collider.height * 0.5f, 0f);
        return body.transform;
    }

    private static void SetMascot(SerializedProperty mascot, string speaker, Transform body) {
        mascot.FindPropertyRelative("speakerName").stringValue = speaker;
        mascot.FindPropertyRelative("body").objectReferenceValue = body;
        mascot.FindPropertyRelative("animator").objectReferenceValue = body.GetComponent<Animator>();
    }

    /// Batas mesh dalam pose sekarang (hasil skinning), bukan batas pose bind seperti Renderer.bounds di Editor.
    private static Bounds PosedBounds(GameObject root) {
        var mesh = new Mesh();
        try {
            bool any = false;
            var bounds = new Bounds();
            foreach (SkinnedMeshRenderer skinned in root.GetComponentsInChildren<SkinnedMeshRenderer>()) {
                skinned.BakeMesh(mesh, true);
                Transform t = skinned.transform;
                foreach (Vector3 vertex in mesh.vertices) {
                    Vector3 world = t.position + t.rotation * vertex;
                    if (any) bounds.Encapsulate(world);
                    else { bounds = new Bounds(world, Vector3.zero); any = true; }
                }
            }
            return any ? bounds : RendererBounds(root);
        } finally {
            Object.DestroyImmediate(mesh);
        }
    }

    private static Bounds RendererBounds(GameObject root) {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return new Bounds(root.transform.position, Vector3.zero);
        Bounds bounds = renderers[0].bounds;
        foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
        return bounds;
    }

    // ------------------------------------------------------------ Marker (lingkaran + beacon)

    private static GameObject BuildMarkerPrefab() {
        var root = new GameObject("Tour_Marker");
        try {
            var ringObject = new GameObject("Ring");
            ringObject.transform.SetParent(root.transform, false);
            var ring = ringObject.AddComponent<LineRenderer>();
            ring.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(ParticleMaterialPath);
            ring.widthMultiplier = 0.35f;
            ring.startColor = ring.endColor = new Color(Gold.r, Gold.g, Gold.b, 0.9f);
            ring.shadowCastingMode = ShadowCastingMode.Off;
            ring.receiveShadows = false;
            ring.alignment = LineAlignment.TransformZ;
            ringObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            var beacon = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            beacon.name = "Beacon";
            Object.DestroyImmediate(beacon.GetComponent<Collider>());
            beacon.transform.SetParent(root.transform, false);
            beacon.transform.localPosition = new Vector3(0f, BeaconHeight * 0.5f, 0f);
            beacon.transform.localScale = new Vector3(BeaconRadius * 2f, BeaconHeight * 0.5f, BeaconRadius * 2f);
            var beaconRenderer = beacon.GetComponent<MeshRenderer>();
            beaconRenderer.sharedMaterial = BuildBeaconMaterial();
            beaconRenderer.shadowCastingMode = ShadowCastingMode.Off;
            beaconRenderer.receiveShadows = false;

            var visual = root.AddComponent<TourMarkerVisual>();
            var serialized = new SerializedObject(visual);
            serialized.FindProperty("ring").objectReferenceValue = ring;
            serialized.FindProperty("radius").floatValue = RingRadius;
            serialized.FindProperty("beacon").objectReferenceValue = beaconRenderer;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabFolder}/Tour_Marker.prefab");
        } finally {
            Object.DestroyImmediate(root);
        }
    }

    /// Unlit transparan aditif dengan gradasi memudar ke atas, tanpa tutup atas yang terlihat.
    private static Material BuildBeaconMaterial() {
        string path = $"{ArtFolder}/TourBeacon.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null) {
            material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            AssetDatabase.CreateAsset(material, path);
        }
        material.SetTexture("_BaseMap", BuildGradientTexture());
        material.SetColor("_BaseColor", new Color(Gold.r, Gold.g, Gold.b, 0.55f));
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", 2f); // aditif
        material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)BlendMode.One);
        material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
        material.SetFloat("_DstBlendAlpha", (float)BlendMode.One);
        material.SetFloat("_ZWrite", 0f);
        material.SetFloat("_Cull", (float)CullMode.Off);
        material.SetOverrideTag("RenderType", "Transparent");
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)RenderQueue.Transparent;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Texture2D BuildGradientTexture() {
        string path = $"{ArtFolder}/beacon_gradient.png";
        if (!File.Exists(path)) {
            const int height = 128;
            var texture = new Texture2D(4, height, TextureFormat.RGBA32, false);
            for (int y = 0; y < height; y++) {
                float v = y / (height - 1f);
                // Terang di bawah, memudar ke atas, dan benar-benar hilang di ujung (tutup silinder tak terlihat).
                float alpha = Mathf.Pow(1f - v, 1.6f) * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1f, 0.92f, v));
                for (int x = 0; x < 4; x++) texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // ------------------------------------------------------------ Poof

    private static GameObject BuildPoofPrefab() {
        var root = new GameObject("Mascot_Poof");
        try {
            var particles = root.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = particles.main;
            main.duration = 0.6f;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.75f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 3f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
            main.startColor = new Color(1f, 1f, 1f, 0.85f);
            main.gravityModifier = -0.15f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 40;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 28) });

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.6f;
            shape.position = new Vector3(0f, 0.7f, 0f);

            ParticleSystem.SizeOverLifetimeModule size = particles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0f));

            ParticleSystem.ColorOverLifetimeModule color = particles.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) });
            color.color = gradient;

            var renderer = root.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(ParticleMaterialPath);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            return PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabFolder}/Mascot_Poof.prefab");
        } finally {
            Object.DestroyImmediate(root);
        }
    }
}
