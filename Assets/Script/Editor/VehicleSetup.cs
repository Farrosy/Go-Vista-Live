using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Menyiapkan sistem mobil pemain dari asset AllStarCharacterLibrary. Menu: Go Vista > Mobil.
///
///  1. Siapkan Sistem Mobil (sekali per project, aman diulang):
///     - ikon HUD (setir, panah, pedal) di Assets/Resources/UI/Icons,
///     - InteractionAction "Masuk"/"Keluar" di Assets/NPCAction,
///     - state mobil (masuk/duduk/keluar, sopir & penumpang) di HumanoidPlayerController,
///     - prefab Matador_Drivable, baris MOBIL di SettingsPanel.prefab.
///  2. Buat Prefab Mobil dari Prefab AllStar Terpilih: pilih prefab mobil di Assets/AllStarCharacterLibrary/Prefabs
///     (Project window), lalu jalankan. Hasil: Assets/Prefab/Vehicles/&lt;Nama&gt;_Drivable.prefab.
///  3. Taruh Matador di SpawnPointMobil.
///
/// Didukung: mobil darat (CarModelControl.vehicleType = 0). Bus, pesawat, gyrocopter, paraglider punya animasi
/// dan cara kendali lain, jadi ditolak.
/// </summary>
public static class VehicleSetup {
    private const string AllStarPrefabFolder = "Assets/AllStarCharacterLibrary/Prefabs/";
    private const string PrefabFolder = "Assets/Prefab/Vehicles";
    public const string DrivablePrefabPath = PrefabFolder + "/Matador_Drivable.prefab";
    private const string ControllerPath = "Assets/Characters/Animation/AnimationController/HumanoidPlayerController.controller";
    private const string ClipFolder = "Assets/AllStarCharacterLibrary/Characters/Tommy/";
    private const string IconFolder = "Assets/Resources/UI/Icons/";
    private const string IconTemplatePath = IconFolder + "icon-play.png";
    private const string ActionFolder = "Assets/NPCAction/";
    private const string PromptPrefabPath = "Assets/MobilePrefabs/NewInteractPrompt.prefab";
    private const string SpawnMarkerName = "SpawnPointMobil";
    private const int GroundVehicleType = 0;

    [MenuItem("Go Vista/Mobil/1. Siapkan Sistem Mobil")]
    public static void SetupAll() {
        var log = new List<string>();
        GenerateIcons(log);
        CreateActions(log);
        PatchAnimator(log);
        log.Add(BuildPrefab(AllStarPrefabFolder + "Matador.prefab", overwrite: false));
        log.Add("SettingsPanel: " + SettingsPanelRedesign.AddDriveRow());
        AssetDatabase.SaveAssets();
        Debug.Log("[Mobil] Setup selesai:\n- " + string.Join("\n- ", log));
    }

    [MenuItem("Go Vista/Mobil/2. Buat Prefab Mobil dari Prefab AllStar Terpilih")]
    private static void BuildFromSelection() {
        var paths = Selection.objects.Select(AssetDatabase.GetAssetPath)
            .Where(p => p.StartsWith(AllStarPrefabFolder) && p.EndsWith(".prefab")).ToList();
        if (paths.Count == 0) {
            EditorUtility.DisplayDialog("Buat Prefab Mobil",
                "Pilih dulu prefab mobil di folder " + AllStarPrefabFolder + " (Project window), lalu jalankan menu ini lagi.",
                "OK");
            return;
        }
        if (!LoadActions()) {
            EditorUtility.DisplayDialog("Buat Prefab Mobil", "Jalankan dulu Go Vista > Mobil > 1. Siapkan Sistem Mobil.", "OK");
            return;
        }
        var log = paths.Select(p => BuildPrefab(p, overwrite: true)).ToList();
        AssetDatabase.SaveAssets();
        Debug.Log("[Mobil] Buat prefab:\n- " + string.Join("\n- ", log));
    }

    [MenuItem("Go Vista/Mobil/2. Buat Prefab Mobil dari Prefab AllStar Terpilih", true)]
    private static bool BuildFromSelectionValidate() =>
        Selection.objects.Any(o => AssetDatabase.GetAssetPath(o).StartsWith(AllStarPrefabFolder));

    // ------------------------------------------------------------------ Ikon

    private static void GenerateIcons(List<string> log) {
        WriteIcon("icon-steering", SteeringWheel, log);
        WriteIcon("icon-arrow-right", ArrowRight, log);
        WriteIcon("icon-pedal", Pedal, log);
    }

    private delegate float Shape(float x, float y);   // x,y: -1..1 (kanan, atas). Return 1 = terisi.

    private static void WriteIcon(string name, Shape shape, List<string> log) {
        string path = IconFolder + name + ".png";
        if (File.Exists(path)) {
            log.Add($"{name}: sudah ada");
            return;
        }
        const int size = 256, samples = 4;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var pixels = new Color32[size * size];
        for (int py = 0; py < size; py++) {
            for (int px = 0; px < size; px++) {
                float cover = 0f;
                for (int sy = 0; sy < samples; sy++)
                    for (int sx = 0; sx < samples; sx++) {
                        float x = (px + (sx + 0.5f) / samples) / size * 2f - 1f;
                        float y = (py + (sy + 0.5f) / samples) / size * 2f - 1f;
                        cover += shape(x, y);
                    }
                byte a = (byte)Mathf.RoundToInt(255f * cover / (samples * samples));
                pixels[py * size + px] = new Color32(255, 255, 255, a);
            }
        }
        tex.SetPixels32(pixels);
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);

        // Pengaturan impor sama dengan ikon lain (Sprite, alpha transparan).
        var template = (TextureImporter)AssetImporter.GetAtPath(IconTemplatePath);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        if (template && importer) {
            var settings = new TextureImporterSettings();
            template.ReadTextureSettings(settings);
            importer.SetTextureSettings(settings);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.SaveAndReimport();
        }
        log.Add($"{name}: dibuat");
    }

    private static float SteeringWheel(float x, float y) {
        float r = Mathf.Sqrt(x * x + y * y);
        if (r <= 0.94f && r >= 0.74f) return 1f;                                  // lingkar setir
        if (r <= 0.22f) return 1f;                                                // hub
        if (Mathf.Abs(y + 0.02f) <= 0.09f && Mathf.Abs(x) <= 0.8f) return 1f;     // jari kiri-kanan
        if (Mathf.Abs(x) <= 0.09f && y <= 0f && y >= -0.8f) return 1f;            // jari bawah
        return 0f;
    }

    private static float ArrowRight(float x, float y) {
        // Segitiga (-0.45,0.65) (-0.45,-0.65) (0.7,0).
        if (x < -0.45f || x > 0.7f) return 0f;
        float halfHeight = 0.65f * (0.7f - x) / 1.15f;
        return Mathf.Abs(y) <= halfHeight ? 1f : 0f;
    }

    private static float Pedal(float x, float y) {
        // Kotak membulat dengan 4 alur horizontal.
        const float hw = 0.78f, hh = 0.92f, radius = 0.25f;
        float dx = Mathf.Max(Mathf.Abs(x) - (hw - radius), 0f);
        float dy = Mathf.Max(Mathf.Abs(y) - (hh - radius), 0f);
        if (dx * dx + dy * dy > radius * radius) return 0f;
        for (int i = 0; i < 4; i++) {
            float groove = -0.54f + i * 0.36f;
            if (Mathf.Abs(y - groove) <= 0.05f && Mathf.Abs(x) <= hw - 0.18f) return 0f;
        }
        return 1f;
    }

    // ------------------------------------------------------------------ InteractionAction

    private static InteractionAction enterAction, exitAction;

    private static void CreateActions(List<string> log) {
        Sprite steering = AssetDatabase.LoadAssetAtPath<Sprite>(IconFolder + "icon-steering.png");
        Sprite logout = AssetDatabase.LoadAssetAtPath<Sprite>(IconFolder + "icon-logout.png");
        enterAction = CreateAction("EnterCar", "Masuk", steering, log);
        exitAction = CreateAction("ExitCar", "Keluar", logout, log);
    }

    private static InteractionAction CreateAction(string file, string verb, Sprite icon, List<string> log) {
        string path = ActionFolder + file + ".asset";
        var action = AssetDatabase.LoadAssetAtPath<InteractionAction>(path);
        if (action) {
            log.Add($"{file}: sudah ada");
            return action;
        }
        action = ScriptableObject.CreateInstance<InteractionAction>();
        action.verb = verb;
        action.icon = icon;
        action.hudIcon = icon;
        AssetDatabase.CreateAsset(action, path);
        log.Add($"{file}: dibuat");
        return action;
    }

    private static bool LoadActions() {
        enterAction = AssetDatabase.LoadAssetAtPath<InteractionAction>(ActionFolder + "EnterCar.asset");
        exitAction = AssetDatabase.LoadAssetAtPath<InteractionAction>(ActionFolder + "ExitCar.asset");
        return enterAction && exitAction;
    }

    // ------------------------------------------------------------------ Animator

    // Satu state per kombinasi sisi pintu + fase, isinya BlendTree SeatHeight: 0 = mobil rendah (Low*),
    // 1 = mobil tinggi (StepVan*). Sama seperti MaleDriver.controller asset AllStar.
    private static readonly string[] LegacyStates = { "CarEnter", "CarDrive", "CarExit" };   // versi 1 (sopir saja)
    private const string EnterLeftState = "CarEnterLeft";

    private static void PatchAnimator(List<string> log) {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (!controller) {
            log.Add("Animator: HumanoidPlayerController tidak ditemukan");
            return;
        }
        AnimatorStateMachine sm = controller.layers[0].stateMachine;
        if (sm.states.Any(s => s.state.name == EnterLeftState)) {
            log.Add("Animator: state mobil sudah ada");
            return;
        }
        AnimatorState locomotion = sm.states.FirstOrDefault(s => s.state.name == "Locomotion").state;
        if (locomotion == null) {
            log.Add("Animator: state Locomotion tidak ditemukan, dilewati");
            return;
        }

        RemoveLegacyStates(sm);
        AddParameter(controller, VehicleOccupant.InVehicleParam, AnimatorControllerParameterType.Bool);
        AddParameter(controller, VehicleOccupant.CarEnterParam, AnimatorControllerParameterType.Trigger);
        AddParameter(controller, VehicleOccupant.CarLeftParam, AnimatorControllerParameterType.Bool);
        AddParameter(controller, VehicleOccupant.CarDriverParam, AnimatorControllerParameterType.Bool);
        AddParameter(controller, VehicleOccupant.SeatHeightParam, AnimatorControllerParameterType.Float);

        string tag = VehicleOccupant.VehicleStateTag, seated = VehicleOccupant.SeatedStateTag;
        AnimatorState enterLeft = SeatState(controller, sm, EnterLeftState, "DriverEnter", tag, 600f, 380f);
        AnimatorState enterRight = SeatState(controller, sm, "CarEnterRight", "PassengerEnter", tag, 600f, 520f);
        AnimatorState drive = SeatState(controller, sm, "CarDrive", "DriverDrive", seated, 850f, 380f);
        AnimatorState ride = SeatState(controller, sm, "CarRide", "PassengerRide", seated, 850f, 520f);
        AnimatorState exitLeft = SeatState(controller, sm, "CarExitLeft", "DriverExit", tag, 1100f, 380f);
        AnimatorState exitRight = SeatState(controller, sm, "CarExitRight", "PassengerExit", tag, 1100f, 520f);

        // Masuk: pintu kiri = animasi sopir, pintu kanan = animasi penumpang.
        AnyTo(sm, enterLeft, true);
        AnyTo(sm, enterRight, false);
        // Duduk: kursi sopir = Drive, kursi lain = Ride.
        foreach (AnimatorState enter in new[] { enterLeft, enterRight }) {
            AfterClip(enter, drive, 1f, 0f).AddCondition(AnimatorConditionMode.If, 0f, VehicleOccupant.CarDriverParam);
            AfterClip(enter, ride, 1f, 0f).AddCondition(AnimatorConditionMode.IfNot, 0f, VehicleOccupant.CarDriverParam);
        }
        // Keluar lewat pintu yang sama.
        foreach (AnimatorState sit in new[] { drive, ride }) {
            ExitTo(sit, exitLeft, true);
            ExitTo(sit, exitRight, false);
        }
        AfterClip(exitLeft, locomotion, 0.9f, 0.1f);
        AfterClip(exitRight, locomotion, 0.9f, 0.1f);

        EditorUtility.SetDirty(controller);
        log.Add("Animator: state mobil (sopir + penumpang) ditambahkan");
    }

    private static void RemoveLegacyStates(AnimatorStateMachine sm) {
        foreach (string name in LegacyStates) {
            ChildAnimatorState child = sm.states.FirstOrDefault(s => s.state.name == name);
            if (child.state == null) continue;
            foreach (AnimatorStateTransition t in sm.anyStateTransitions.Where(t => t.destinationState == child.state).ToArray())
                sm.RemoveAnyStateTransition(t);
            sm.RemoveState(child.state);
        }
    }

    private static void AddParameter(AnimatorController controller, string name, AnimatorControllerParameterType type) {
        if (controller.parameters.All(p => p.name != name)) controller.AddParameter(name, type);
    }

    private static AnimatorState SeatState(AnimatorController controller, AnimatorStateMachine sm, string name,
                                           string clip, string tag, float x, float y) {
        AnimatorState state = controller.CreateBlendTreeInController(name, out BlendTree tree, 0);
        tree.name = name;
        tree.blendType = BlendTreeType.Simple1D;
        tree.blendParameter = VehicleOccupant.SeatHeightParam;
        tree.useAutomaticThresholds = false;
        tree.AddChild(LoadClip("Low" + clip), 0f);
        tree.AddChild(LoadClip("StepVan" + clip), 1f);
        state.tag = tag;
        // CreateBlendTreeInController menaruh state di posisi default; rapikan supaya mudah dibaca di Animator window.
        ChildAnimatorState[] states = sm.states;
        for (int i = 0; i < states.Length; i++)
            if (states[i].state == state) states[i].position = new Vector3(x, y);
        sm.states = states;
        return state;
    }

    private static void AnyTo(AnimatorStateMachine sm, AnimatorState target, bool left) {
        AnimatorStateTransition t = sm.AddAnyStateTransition(target);
        t.AddCondition(AnimatorConditionMode.If, 0f, VehicleOccupant.CarEnterParam);
        t.AddCondition(left ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, VehicleOccupant.CarLeftParam);
        t.canTransitionToSelf = false;
        t.hasExitTime = false;
        t.duration = 0.1f;
    }

    private static AnimatorStateTransition AfterClip(AnimatorState from, AnimatorState to, float exitTime, float duration) {
        AnimatorStateTransition t = from.AddTransition(to);
        t.hasExitTime = true;
        t.exitTime = exitTime;
        t.duration = duration;
        return t;
    }

    private static void ExitTo(AnimatorState from, AnimatorState to, bool left) {
        AnimatorStateTransition t = from.AddTransition(to);
        t.AddCondition(AnimatorConditionMode.IfNot, 0f, VehicleOccupant.InVehicleParam);
        t.AddCondition(left ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, VehicleOccupant.CarLeftParam);
        t.hasExitTime = false;
        t.duration = 0.1f;
    }

    private static AnimationClip LoadClip(string name) {
        AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(ClipFolder + "Tommy@" + name + ".FBX")
            .OfType<AnimationClip>()
            .FirstOrDefault(c => c.name == name);
        if (!clip) Debug.LogWarning($"[Mobil] Clip {name} tidak ditemukan di {ClipFolder}.");
        return clip;
    }

    // ------------------------------------------------------------------ Prefab

    /// <summary>
    /// Salin prefab mobil AllStar jadi Assets/Prefab/Vehicles/&lt;Nama&gt;_Drivable.prefab: CarMotor diganti
    /// CarDriveController (roda diambil dari CarMotor), suspensi disetel, satu kursi + zona Interact per pintu.
    /// </summary>
    public static string BuildPrefab(string sourcePath, bool overwrite) {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
        if (!source) return $"{sourcePath}: tidak ditemukan";
        var sourceModel = source.GetComponent<CarModelControl>();
        if (!sourceModel || !source.GetComponent<CarMotor>())
            return $"{source.name}: bukan prefab mobil AllStar (butuh CarModelControl + CarMotor), dilewati";
        if (sourceModel.vehicleType != GroundVehicleType)
            return $"{source.name}: vehicleType {sourceModel.vehicleType} (bus/pesawat/gyro/paraglider) belum didukung, dilewati";

        string path = $"{PrefabFolder}/{source.name}_Drivable.prefab";
        if (!overwrite && AssetDatabase.LoadAssetAtPath<GameObject>(path))
            return $"{source.name}: {path} sudah ada";
        if (!enterAction && !LoadActions()) return "InteractionAction EnterCar/ExitCar belum ada";
        if (!AssetDatabase.IsValidFolder(PrefabFolder)) AssetDatabase.CreateFolder("Assets/Prefab", "Vehicles");

        var root = (GameObject)PrefabUtility.InstantiatePrefab(source);
        try {
            PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            root.name = source.name + "_Drivable";

            // Roda dari CarMotor bawaan, lalu CarMotor dibuang (ubah gravity global, cari "Skidmarks"/"ramp").
            var motor = root.GetComponent<CarMotor>();
            var wheels = motor.wheels.Where(w => w.collider).Select(w => new CarDriveController.DriveWheel {
                collider = w.collider, model = w.wheelModel, steer = w.steer, drive = w.drive,
            }).ToArray();
            Vector3 com = motor.centerOfMass;
            if (motor.rampWheel) motor.rampWheel.SetActive(false);   // roda bantu lompat ramp, tidak dipakai
            Object.DestroyImmediate(motor);
            root.AddComponent<CarDriveController>().SetWheels(wheels, com);
            TuneSuspension(root);

            var model = root.GetComponent<CarModelControl>();
            int ignoreRaycast = LayerMask.NameToLayer("Ignore Raycast");
            var prompt = AssetDatabase.LoadAssetAtPath<InteractPromptWorld>(PromptPrefabPath);

            // Satu kursi per pintu. Zona trigger di titik pintu (EnterL/EnterR/EnterBL/...).
            var seats = new List<PlayerCar.Seat>();
            for (int i = 0; i < model.entryPoints.Length; i++) {
                EntryPoint entry = model.entryPoints[i];
                if (!entry.entryPoint) continue;
                string role = entry.driver ? "Sopir" : "Penumpang";
                var zone = new GameObject($"SeatZone_{entry.entryPoint.name}") { layer = ignoreRaycast };
                zone.transform.SetParent(root.transform, false);
                zone.transform.SetLocalPositionAndRotation(entry.entryPoint.localPosition, entry.entryPoint.localRotation);
                var box = zone.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.center = new Vector3(0f, 1f, 0f);
                box.size = new Vector3(1.6f, 2f, 1.6f);
                var enter = zone.AddComponent<Interactable>();
                enter.displayName = $"{source.name} ({role})";
                enter.action = enterAction;
                SetPrompt(enter, prompt, new Vector3(0f, 1.9f, 0f));
                seats.Add(new PlayerCar.Seat { entryIndex = i, enterInteractable = enter });
            }

            // Keluar: tanpa zona (collider dimatikan), dipaksa jadi target tombol Interact selama duduk di mobil.
            var exitGo = new GameObject("SeatExit") { layer = ignoreRaycast };
            exitGo.transform.SetParent(root.transform, false);
            var exitBox = exitGo.AddComponent<BoxCollider>();
            exitBox.isTrigger = true;
            exitBox.enabled = false;
            var exit = exitGo.AddComponent<Interactable>();
            exit.displayName = source.name;
            exit.action = exitAction;

            root.AddComponent<PlayerCar>().Init(model, seats.ToArray(), exit);

            // Simpan ke path yang sama = GUID tetap, instance di scene ikut ter-update.
            PrefabUtility.SaveAsPrefabAsset(root, path);
            return $"{source.name}: {path} ({seats.Count} kursi, {wheels.Length} roda)";
        } finally {
            Object.DestroyImmediate(root);
        }
    }

    // Suspensi asset (travel 3 cm, pegas 840000) disetel untuk kota uji yang rata. Dengan massanya pegas sudah
    // tertekan habis, jarak badan ke tanah ±10 cm, dan mobil tersangkut di tepi trotoar / lantai pom bensin.
    // Pegas dihitung dari massa supaya frekuensi suspensi sama (±1,6 Hz) untuk mobil seberat apa pun.
    // Nilai ini sudah dites dengan Matador (13500 kg → pegas ±340000) di WorldScene.
    private const float SuspensionDistance = 0.18f;
    private const float SuspensionFrequency = 1.6f;   // Hz
    private const float SuspensionDampingRatio = 0.35f;
    private const float SuspensionTarget = 0.5f;

    private static void TuneSuspension(GameObject root) {
        var body = root.GetComponent<Rigidbody>();
        WheelCollider[] wheels = root.GetComponentsInChildren<WheelCollider>(true)
            .Where(w => w.gameObject.activeSelf).ToArray();
        if (!body || wheels.Length == 0) return;
        float massPerWheel = body.mass / wheels.Length;
        float omega = 2f * Mathf.PI * SuspensionFrequency;
        float spring = massPerWheel * omega * omega;
        float damper = 2f * SuspensionDampingRatio * Mathf.Sqrt(spring * massPerWheel);
        foreach (WheelCollider wheel in wheels) {
            wheel.suspensionDistance = SuspensionDistance;
            JointSpring js = wheel.suspensionSpring;
            js.spring = spring;
            js.damper = damper;
            js.targetPosition = SuspensionTarget;
            wheel.suspensionSpring = js;
        }
    }

    private static void SetPrompt(Interactable interactable, InteractPromptWorld prompt, Vector3 offset) {
        var so = new SerializedObject(interactable);
        so.FindProperty("promptPrefab").objectReferenceValue = prompt;
        so.FindProperty("promptOffset").vector3Value = offset;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ------------------------------------------------------------------ Scene

    [MenuItem("Go Vista/Mobil/3. Taruh Matador di SpawnPointMobil")]
    public static void PlaceAtSpawnMarker() {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DrivablePrefabPath);
        GameObject marker = GameObject.Find(SpawnMarkerName);
        if (!prefab || !marker) {
            Debug.LogError($"[Mobil] Butuh prefab {DrivablePrefabPath} dan object '{SpawnMarkerName}' di scene.");
            return;
        }
        var existing = Object.FindObjectsByType<PlayerCar>(FindObjectsInactive.Include)
            .FirstOrDefault(c => c.gameObject.scene == marker.scene && c.name == prefab.name);
        GameObject car = existing ? existing.gameObject
                                  : (GameObject)PrefabUtility.InstantiatePrefab(prefab, marker.scene);
        Undo.RegisterCreatedObjectUndo(car, "Taruh Matador");

        // Posisi XZ = cube. Tinggi = tanah di bawah cube (cube-nya sendiri diabaikan).
        Transform t = marker.transform;
        Vector3 position = t.position;
        Collider markerCollider = marker.GetComponent<Collider>();
        float bestY = float.NaN, bestDistance = float.MaxValue;
        foreach (RaycastHit hit in Physics.RaycastAll(t.position + Vector3.up * 5f, Vector3.down, 30f,
                                                      Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) {
            if (hit.collider == markerCollider || hit.collider.transform.IsChildOf(car.transform)) continue;
            if (hit.distance < bestDistance) {
                bestDistance = hit.distance;
                bestY = hit.point.y;
            }
        }
        if (!float.IsNaN(bestY)) position.y = bestY;
        // Depan mobil = sumbu X lokal cube (sisi panjangnya), sesuai keputusan desain.
        Vector3 forward = Vector3.ProjectOnPlane(t.right, Vector3.up).normalized;
        car.transform.SetPositionAndRotation(position, Quaternion.LookRotation(forward, Vector3.up));

        // Cube hanya patokan: collider-nya dimatikan supaya tidak menabrak mobil.
        if (markerCollider && markerCollider.enabled) {
            Undo.RecordObject(markerCollider, "Matikan collider SpawnPointMobil");
            markerCollider.enabled = false;
        }
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(marker.scene);
        Selection.activeGameObject = car;
        Debug.Log($"[Mobil] Matador ditaruh di {position} (scene {marker.scene.name}).");
    }
}
