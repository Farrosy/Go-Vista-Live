using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Go Vista > Interior > Pasang Kursi Barber:
///  1. Animator HumanoidPlayerController: parameter ChairSeated + state ChairSitDown / ChairSitIdle / ChairStandUp
///     (klip MoCapCentral MC_Seated: duduk mundur F01b, idle duduk, berdiri F01).
///     IK Pass base layer dinyalakan (SeatIK: kaki di pijakan, tangan menjauhi meja).
///  2. Scene Interior_Barber: ChairSeat di kursi barber PALING KIRI (x lokal terkecil): lingkaran cahaya teal
///     di samping kursi (sisi ruangan), titik masuk di lantai samping pijakan kaki, titik berdiri di atas pijakan
///     (celah antara dudukan dan meja cermin), titik duduk, garis pijakan kaki, dan Interactable "Berdiri".
///  3. ChairSeatCamera + "Camera Shots" (bidikan A depan, C 3/4 depan, editor rambut) di bawah station.
///     BarberEditorStation (tombol HUD "Gaya Rambut" → editor rambut + jenggot selama duduk).
///  4. Kursi terkunci sampai perkenalan Bang Dodi (dialog dodi_intro) selesai + FittingRoomHint (panah di atas
///     lingkaran setelah perkenalan, sampai pertama kali diinjak).
/// Ukuran kursi (SM_Prop_Barber_Chair_01, meter, sumbu maju = ke cermin): dudukan atas 0,69, depan dudukan 0,35,
/// sandaran -0,32, sandaran tangan sampai 0,30; pijakan kaki 0,40..0,80 maju, ±0,30 samping, atas 0,215;
/// meja cermin mulai 0,84.
/// Aman dijalankan ulang (state animator dan station lama diganti). Scene ditandai berubah tapi TIDAK disimpan.
/// </summary>
public static class BarberChairBuilder {
    private const string MenuPath = "Go Vista/Interior/Pasang Kursi Barber";
    private const string ControllerPath = "Assets/Characters/Animation/AnimationController/HumanoidPlayerController.controller";
    private const string ClipFolder = "Assets/MoCapCentral/MC_Seated/Animations/SitChair/";
    private const string SitDownClip = "MCU_am_Stand_Trans_SitChair_F01b";
    private const string SitIdleClip = "MCU_am_SitChair_Idle_01";
    private const string StandUpClip = "MCU_am_SitChair_Trans_Stand_F01";

    private const string StationName = "Barber Chair Seat";
    private const string ChairPrefix = "SM_Prop_Barber_Chair_01";
    private const string SitActionPath = "Assets/NPCAction/SitChair.asset";
    private const string StandActionPath = "Assets/NPCAction/StandUp.asset";
    private const string SitIconSource = "Assets/NPCAction/Sit.asset";
    private const string StandIconPath = "Assets/Resources/UI/Icons/icon-logout.png";
    private const string IntroDialogueId = "dodi_intro";
    private const string HintTutorialId = "barber-chair-hint";
    private const string HintArrowMaterialPath = "Assets/Art/Entrance/FittingHintArrow.mat";

    // Tata letak relatif kursi (meter). forward = arah hadap kursi (ke cermin), side = sisi kursi yang menghadap ruangan.
    private const float SeatBack = -0.06f;      // posisi duduk akhir (setelah bergeser mundur dari depan dudukan)
    private const float SeatLift = 0.28f;       // dudukan barber ±0,69 m; paha pose duduk mocap ±0,40 m di atas root
    private const float FootrestTop = 0.215f;
    private const float FootrestHalfWidth = 0.3f;
    // Celah dudukan–meja diukur dari scene (raycast ke laci meja cermin), karena kursi boleh digeser mundur supaya
    // tangan muat. Kursi sit digeser 0,15 m mundur (2026-11-25): laci ±0,90 dari ±0,75.
    private const float SeatFront = 0.36f;      // depan dudukan
    private const float DefaultCounterFront = 0.75f;
    private const float StandMin = 0.50f;       // di atas pijakan (0,40..0,80) dengan sisa tepi
    private const float StandMax = 0.65f;
    private const float FootLine = 0.50f;       // garis telapak kaki di pijakan saat duduk (Footrest)
    private const float FootMin = -0.05f;       // telapak 0,45..0,65: tumit di depan dudukan, jari di belakang bibir pijakan
    private const float FootMax = 0.15f;
    private const float HandMin = -0.14f;       // tangan dari 0,36: di depan sandaran tangan (0,30)
    private const float HandCounterMargin = 0.09f;   // tangan berhenti 9 cm di belakang laci meja
    private const float EntrySide = 0.85f;      // lantai di samping pijakan, sejajar StandSpot
    private const float RingSide = 1.15f;
    private const float RingForward = 0.1f;
    private const float RingSize = 1.0f;
    private const float IgnoreRadius = 1.2f;    // collider dalam jarak ini dari celah pijakan diabaikan avatar
    private const float PromptHeight = 2.05f;
    private const float StandPromptHeight = 1.65f;
    private static readonly Vector3 TriggerSize = new Vector3(1.2f, 2.1f, 1.2f);

    [MenuItem(MenuPath)]
    private static void Run() {
        var log = new List<string>();
        PatchAnimator(log);
        PlaceStation(log);
        Debug.Log("[BarberChair]\n" + string.Join("\n", log));
    }

    // ---------------------------------------------------------------- Animator

    private static void PatchAnimator(List<string> log) {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (!controller) {
            log.Add("Animator: HumanoidPlayerController tidak ditemukan");
            return;
        }
        AnimationClip sitDown = LoadClip(SitDownClip), sitIdle = LoadClip(SitIdleClip), standUp = LoadClip(StandUpClip);
        if (!sitDown || !sitIdle || !standUp) {
            log.Add("Animator: klip MC_Seated tidak lengkap, dilewati");
            return;
        }
        AnimatorControllerLayer[] layers = controller.layers;
        layers[0].iKPass = true;   // SeatIK
        controller.layers = layers;
        AnimatorStateMachine sm = controller.layers[0].stateMachine;
        AnimatorState locomotion = sm.states.FirstOrDefault(s => s.state.name == "Locomotion").state;
        if (!locomotion) {
            log.Add("Animator: state Locomotion tidak ditemukan, dilewati");
            return;
        }

        foreach (string name in new[] { SeatOccupant.SitDownState, SeatOccupant.SitIdleState, SeatOccupant.StandUpState }) {
            ChildAnimatorState old = sm.states.FirstOrDefault(s => s.state.name == name);
            if (old.state) sm.RemoveState(old.state);
        }
        foreach (AnimatorStateTransition t in locomotion.transitions.Where(t => !t.destinationState).ToArray())
            locomotion.RemoveTransition(t);   // sisa transisi ke state yang baru dihapus
        if (controller.parameters.All(p => p.name != SeatOccupant.SeatedParam))
            controller.AddParameter(SeatOccupant.SeatedParam, AnimatorControllerParameterType.Bool);

        AnimatorState down = AddState(sm, SeatOccupant.SitDownState, sitDown, SeatOccupant.TransitionTag, new Vector3(600f, 680f));
        AnimatorState idle = AddState(sm, SeatOccupant.SitIdleState, sitIdle, SeatOccupant.SeatedTag, new Vector3(850f, 680f));
        AnimatorState up = AddState(sm, SeatOccupant.StandUpState, standUp, SeatOccupant.TransitionTag, new Vector3(1100f, 680f));

        OnSeated(locomotion.AddTransition(down), true, 0.25f);
        AfterClip(down, idle, 0.94f, 0.25f);
        OnSeated(down.AddTransition(up), false, 0.3f);   // batal di tengah duduk
        OnSeated(idle.AddTransition(up), false, 0.25f);
        AfterClip(up, locomotion, 0.88f, 0.3f);

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        log.Add("Animator: parameter + state duduk kursi ditambahkan ke HumanoidPlayerController");
    }

    private static AnimatorState AddState(AnimatorStateMachine sm, string name, AnimationClip clip, string tag, Vector3 position) {
        AnimatorState state = sm.AddState(name, position);
        state.motion = clip;
        state.tag = tag;
        state.writeDefaultValues = true;
        return state;
    }

    private static void OnSeated(AnimatorStateTransition t, bool seated, float duration) {
        t.AddCondition(seated ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, SeatOccupant.SeatedParam);
        t.hasExitTime = false;
        t.hasFixedDuration = true;
        t.duration = duration;
    }

    private static void AfterClip(AnimatorState from, AnimatorState to, float exitTime, float duration) {
        AnimatorStateTransition t = from.AddTransition(to);
        t.hasExitTime = true;
        t.exitTime = exitTime;
        t.hasFixedDuration = true;
        t.duration = duration;
    }

    private static AnimationClip LoadClip(string file) =>
        AssetDatabase.LoadAllAssetsAtPath(ClipFolder + file + ".FBX").OfType<AnimationClip>()
                     .FirstOrDefault(c => !c.name.StartsWith("__preview"));

    // ---------------------------------------------------------------- Scene

    private static void PlaceStation(List<string> log) {
        Scene scene = SceneManager.GetSceneByName(VenueMap.BarberSceneName);
        if (!scene.IsValid() || !scene.isLoaded) {
            log.Add($"Scene: buka {VenueMap.BarberSceneName} dulu, station tidak dipasang");
            return;
        }
        GameObject rootObject = scene.GetRootGameObjects().FirstOrDefault(g => g.name == VenueMap.BarberSceneName);
        if (!rootObject) {
            log.Add($"Scene: root {VenueMap.BarberSceneName} tidak ditemukan");
            return;
        }
        Transform root = rootObject.transform;
        Transform chair = root.GetComponentsInChildren<Transform>(true)
                              .Where(t => t.name.StartsWith(ChairPrefix) && !t.name.StartsWith(ChairPrefix + "_"))
                              .OrderBy(t => root.InverseTransformPoint(t.position).x)
                              .FirstOrDefault();
        if (!chair) {
            log.Add($"Scene: kursi {ChairPrefix} tidak ditemukan");
            return;
        }

        foreach (Transform old in root.Cast<Transform>().Where(t => t.name == StationName).ToArray())
            Object.DestroyImmediate(old.gameObject);

        Vector3 forward = Flat(chair.forward);
        Vector3 side = Flat(chair.right);
        if (Vector3.Dot(root.position - chair.position, side) < 0f) side = -side;   // sisi yang menghadap ruangan
        Vector3 floor = new Vector3(chair.position.x, root.position.y, chair.position.z);

        var station = new GameObject(StationName);
        station.transform.SetParent(root, false);
        station.transform.SetPositionAndRotation(floor + side * RingSide + forward * RingForward, Quaternion.LookRotation(forward));

        // Berdiri di tengah jendela tangan (depan sandaran tangan .. belakang laci): saat melangkah menyamping kedua
        // tangan ±0,22 m dari badan, jadi sama-sama muat. Lutut/betis tetap di depan dudukan.
        float counter = MeasureCounterFront(chair, floor, forward, log);
        float handMax = counter - HandCounterMargin - FootLine;
        float standForward = Mathf.Clamp(FootLine + (HandMin + handMax) * 0.5f, StandMin, StandMax);

        Quaternion facing = Quaternion.LookRotation(forward);
        Transform seat = Point(station.transform, "Seat", floor + forward * SeatBack + Vector3.up * SeatLift, facing);
        Transform standSpot = Point(station.transform, "Stand Spot", floor + forward * standForward + Vector3.up * FootrestTop, facing);
        Transform entry = Point(station.transform, "Entry", floor + forward * standForward + side * EntrySide, facing);
        Transform footrest = Point(station.transform, "Footrest", floor + forward * FootLine + Vector3.up * FootrestTop, facing);

        MeshRenderer[] glows = FittingRoomBuilder.AddFloorRing(station, RingSize);
        var ringRenderers = new List<Renderer>(glows);
        Transform ringPanel = station.transform.Find("Ring");
        if (ringPanel && ringPanel.GetComponent<Renderer>()) ringRenderers.Add(ringPanel.GetComponent<Renderer>());

        var trigger = station.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.center = new Vector3(0f, TriggerSize.y * 0.5f - 0.05f, 0f);
        trigger.size = TriggerSize;

        var prompt = AssetDatabase.LoadAssetAtPath<InteractPromptWorld>(WorldPromptPrefabs.PromptPath);
        var sitAction = EnsureAction(SitActionPath, "Duduk", SitIcons());
        var standAction = EnsureAction(StandActionPath, "Berdiri", StandIcons());
        SetupInteractable(station.AddComponent<Interactable>(), "Kursi Barber", sitAction, prompt, PromptHeight);

        var stand = new GameObject("Stand Up");
        stand.transform.SetParent(seat, false);
        var standZone = stand.AddComponent<BoxCollider>();
        standZone.isTrigger = true;
        standZone.size = Vector3.one * 0.2f;
        var standInteractable = stand.AddComponent<Interactable>();
        // Tanpa label melayang: kamera kursi dekat sekali dan label di atas kepala terpotong tepi layar.
        // Tombol Interact HUD (ikon Berdiri) tetap ada.
        SetupInteractable(standInteractable, "Kursi Barber", standAction, null, StandPromptHeight);
        stand.SetActive(false);

        var chairSeat = station.AddComponent<ChairSeat>();
        var so = new SerializedObject(chairSeat);
        so.FindProperty("seat").objectReferenceValue = seat;
        so.FindProperty("standSpot").objectReferenceValue = standSpot;
        so.FindProperty("entry").objectReferenceValue = entry;
        so.FindProperty("footrest").objectReferenceValue = footrest;
        so.FindProperty("footrestHalfWidth").floatValue = FootrestHalfWidth;
        so.FindProperty("footMin").floatValue = FootMin;
        so.FindProperty("footMax").floatValue = FootMax;
        so.FindProperty("handMin").floatValue = HandMin;
        so.FindProperty("handMax").floatValue = handMax;
        SerializedProperty hideList = so.FindProperty("hideWhileBusy");
        hideList.arraySize = ringRenderers.Count;
        for (int i = 0; i < ringRenderers.Count; i++) hideList.GetArrayElementAtIndex(i).objectReferenceValue = ringRenderers[i];
        so.FindProperty("path").arraySize = 0;
        so.FindProperty("standUp").objectReferenceValue = standInteractable;
        // Terkunci sampai perkenalan Bang Dodi (NPC_Dodi di meja kasir) selesai, seperti ruang ganti dan Mira.
        so.FindProperty("requiredDialogueId").stringValue = IntroDialogueId;
        so.FindProperty("lockedTitle").stringValue = "Kursi cukur belum bisa dipakai";
        so.FindProperty("lockedDetail").stringValue = "Ngobrol dulu dengan Bang Dodi di kasir, ya!";
        // Kursi + meja cermin di depannya: avatar masuk ke celah di antaranya. Lantai tidak (avatar tetap menapak).
        Vector3 gap = standSpot.position + Vector3.up * 0.6f;
        Collider[] colliders = chair.GetComponentsInChildren<Collider>(true)
                                    .Concat(root.GetComponentsInChildren<Collider>(true)
                                                .Where(c => c.bounds.SqrDistance(gap) < IgnoreRadius * IgnoreRadius &&
                                                            c.bounds.max.y > root.position.y + 0.3f))
                                    .Where(c => !c.isTrigger && !c.transform.IsChildOf(station.transform))
                                    .Distinct().ToArray();
        SerializedProperty colliderList = so.FindProperty("chairColliders");
        colliderList.arraySize = colliders.Length;
        for (int i = 0; i < colliders.Length; i++) colliderList.GetArrayElementAtIndex(i).objectReferenceValue = colliders[i];
        so.ApplyModifiedPropertiesWithoutUndo();
        AddSeatCamera(chairSeat, forward, side);
        // Tombol HUD "Gaya Rambut" + editor rambut/jenggot selama duduk.
        if (!station.GetComponent<BarberEditorStation>()) station.AddComponent<BarberEditorStation>();
        // Petunjuk tutorial setelah perkenalan Bang Dodi: sorot lingkaran + panah sampai pertama kali diinjak.
        var hint = station.AddComponent<FittingRoomHint>();
        var ho = new SerializedObject(hint);
        ho.FindProperty("tutorialId").stringValue = HintTutorialId;
        ho.FindProperty("afterDialogueId").stringValue = IntroDialogueId;
        ho.FindProperty("arrowMaterial").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>(HintArrowMaterialPath);
        ho.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();
        log.Add($"Scene: laci meja {counter:F2} m di depan kursi → berdiri {standForward:F2}, tangan maks {handMax:F2}");
        log.Add($"Scene: {StationName} dipasang di '{chair.name}', collider diabaikan: " +
                string.Join(", ", colliders.Select(c => c.name)) + ". Simpan scene (Ctrl+S).");
    }

    /// Jarak (meter, sumbu maju kursi) dari tengah kursi ke laci / meja cermin di depannya, setinggi paha–tangan.
    /// Diukur dari bounds renderer (collider meja cermin tidak mencakup laci; mesh-nya tidak readable).
    private static float MeasureCounterFront(Transform chair, Vector3 floor, Vector3 forward, List<string> log) {
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        float best = float.MaxValue;
        foreach (Renderer r in chair.root.GetComponentsInChildren<Renderer>()) {
            if (!r.enabled || r.transform.IsChildOf(chair) || r.name.StartsWith(ChairPrefix)) continue;
            Bounds b = r.bounds;
            if (b.max.y < floor.y + 0.4f || b.min.y > floor.y + 1.1f) continue;   // setinggi tangan di atas pijakan
            float front = float.MaxValue, sideMin = float.MaxValue, sideMax = float.MinValue;
            for (int i = 0; i < 8; i++) {
                Vector3 corner = new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y,
                                             (i & 4) == 0 ? b.min.z : b.max.z) - floor;
                front = Mathf.Min(front, Vector3.Dot(corner, forward));
                float s = Vector3.Dot(corner, right);
                sideMin = Mathf.Min(sideMin, s);
                sideMax = Mathf.Max(sideMax, s);
            }
            if (sideMax < -0.45f || sideMin > 0.45f || front < SeatFront || front > 1.6f) continue;
            best = Mathf.Min(best, front);
        }
        if (best < float.MaxValue) return best;
        log.Add($"Scene: laci meja tidak terukur, pakai {DefaultCounterFront:F2}");
        return DefaultCounterFront;
    }

    // Bidikan kamera relatif Seat (meter; maju = ke cermin, samping = menjauhi ruangan / sisi jendela).
    // A (depan) berada di dalam meja cermin: collider-nya diabaikan dan bagian belakangnya tidak dirender.
    private static readonly Vector3 FrontShot = new Vector3(0f, 1.22f, 1.49f);       // (samping, atas, maju)
    private static readonly Vector3 FrontLook = new Vector3(0f, 0.97f, -0.09f);
    private static readonly Vector3 SideShot = new Vector3(0.44f, 1.47f, 1.04f);
    private static readonly Vector3 SideLook = new Vector3(0f, 1.02f, -0.09f);
    private static readonly Vector3 EditorLook = new Vector3(0f, 1.12f, -0.05f);     // kepala (editor rambut)
    // Gerak pelan saat duduk diam: A maju sedikit + geser ke sisi ruangan; C mengayun ±15° masuk ke arah wajah
    // (mulai lebih ke samping, berakhir di C) karena di depan C sudah meja cermin.
    private static readonly Vector3 FrontShotEnd = new Vector3(-0.20f, 1.17f, 1.37f);
    private static readonly Vector3 SideShotStart = new Vector3(0.69f, 1.52f, 0.89f);
    // D: 3/4 depan dari sisi ruangan (cermin dari C), juga mengayun ±15° masuk ke arah wajah.
    private static readonly Vector3 RoomShot = new Vector3(-1.06f, 1.37f, 1.24f);
    private static readonly Vector3 RoomShotStart = new Vector3(-1.34f, 1.42f, 0.92f);
    private const float ShotFov = 60f;

    /// ChairSeatCamera + titik bidikan A (depan), C (3/4 depan), dan editor (A di-zoom) di bawah station.
    /// <paramref name="side"/> = sisi kursi yang menghadap ruangan; bidikan C ada di sisi sebaliknya supaya busur
    /// kamera tidak lewat jalur avatar berjalan masuk. Aman dipanggil ulang.
    public static void AddSeatCamera(ChairSeat chairSeat, Vector3 forward, Vector3 side) {
        Transform station = chairSeat.transform;
        Transform old = station.Find("Camera Shots");
        if (old) Object.DestroyImmediate(old.gameObject);
        var shots = new GameObject("Camera Shots").transform;
        shots.SetParent(station, false);

        Vector3 seat = chairSeat.Seat.position;
        Vector3 away = -side;
        Vector3 At(Vector3 local) => seat + away * local.x + Vector3.up * local.y + forward * local.z;
        Transform Shot(string name, Vector3 local, Vector3 look) {
            Vector3 position = At(local);
            return Point(shots, name, position, Quaternion.LookRotation(At(look) - position, Vector3.up));
        }
        Transform front = Shot("Cam Front (A)", FrontShot, FrontLook);
        Transform frontEnd = Shot("Cam Front (A) End", FrontShotEnd, FrontLook);
        Transform sideStart = Shot("Cam Side (C) Start", SideShotStart, SideLook);
        Transform sideShot = Shot("Cam Side (C)", SideShot, SideLook);
        Transform roomStart = Shot("Cam Room (D) Start", RoomShotStart, FrontLook);
        Transform roomShot = Shot("Cam Room (D)", RoomShot, FrontLook);
        Transform editor = Shot("Cam Editor", FrontShot, EditorLook);

        var seatCamera = chairSeat.GetComponent<ChairSeatCamera>();
        if (!seatCamera) seatCamera = chairSeat.gameObject.AddComponent<ChairSeatCamera>();
        var so = new SerializedObject(seatCamera);
        // Urutan potong A → C → D → A ...
        var shotList = new[] { (front, frontEnd), (sideStart, sideShot), (roomStart, roomShot) };
        SerializedProperty list = so.FindProperty("shots");
        list.arraySize = shotList.Length;
        for (int i = 0; i < shotList.Length; i++) {
            SerializedProperty entry = list.GetArrayElementAtIndex(i);
            entry.FindPropertyRelative("from").objectReferenceValue = shotList[i].Item1;
            entry.FindPropertyRelative("to").objectReferenceValue = shotList[i].Item2;
            entry.FindPropertyRelative("fieldOfView").floatValue = ShotFov;
        }
        so.FindProperty("swingVia").objectReferenceValue = sideShot;
        so.FindProperty("editorShot").objectReferenceValue = editor;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Transform Point(Transform parent, string name, Vector3 position, Quaternion rotation) {
        var t = new GameObject(name).transform;
        t.SetParent(parent, false);
        t.SetPositionAndRotation(position, rotation);
        return t;
    }

    private static void SetupInteractable(Interactable interactable, string displayName, InteractionAction action,
                                          InteractPromptWorld prompt, float promptHeight) {
        var io = new SerializedObject(interactable);
        io.FindProperty("displayName").stringValue = displayName;
        io.FindProperty("action").objectReferenceValue = action;
        io.FindProperty("promptPrefab").objectReferenceValue = prompt;
        io.FindProperty("promptOffset").vector3Value = new Vector3(0f, promptHeight, 0f);
        io.ApplyModifiedPropertiesWithoutUndo();
    }

    private static (Sprite icon, Sprite hud) SitIcons() {
        var source = AssetDatabase.LoadAssetAtPath<InteractionAction>(SitIconSource);
        return source ? (source.icon, source.hudIcon) : (null, null);
    }

    private static (Sprite icon, Sprite hud) StandIcons() {
        var icon = AssetDatabase.LoadAssetAtPath<Sprite>(StandIconPath);
        return (icon, icon);
    }

    private static InteractionAction EnsureAction(string path, string verb, (Sprite icon, Sprite hud) icons) {
        var action = AssetDatabase.LoadAssetAtPath<InteractionAction>(path);
        if (!action) {
            action = ScriptableObject.CreateInstance<InteractionAction>();
            AssetDatabase.CreateAsset(action, path);
        }
        action.verb = verb;
        action.icon = icons.icon;
        action.hudIcon = icons.hud;
        EditorUtility.SetDirty(action);
        return action;
    }

    private static Vector3 Flat(Vector3 v) {
        v.y = 0f;
        return v.sqrMagnitude > 0.0001f ? v.normalized : Vector3.forward;
    }
}
