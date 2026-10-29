using System.Collections.Generic;
using System.Collections;
using System.Threading.Tasks;
using Normal.Realtime;
using Normal.Realtime.Examples;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

/// <summary>
/// Normcore lobby UI. Canvas objects are generated once in edit mode so they
/// appear in the scene hierarchy and can be repositioned/styled in the Inspector.
/// Normcore doesn't expose an API to enumerate arbitrary named rooms, so the
/// list here contains local room history plus manual and Quickmatch joins.
/// </summary>
[ExecuteAlways]
public sealed class LobyUISceneController : MonoBehaviour {
    [SerializeField] private Realtime realtime;

    private const string CanvasRootName = "Loby Canvas";
    private const string WaitingCanvasRootName = "Room Waiting Canvas";
    private const string WaitingCanvasPrefabRootName = "RoomWaitingCanvas";
    private const string PrefabFolderPath = "Assets/Prefab";
    private const string LobbyCanvasPrefabPath = "Assets/Prefab/LobbyCanvas.prefab";
    private const string WaitingCanvasPrefabPath = "Assets/Prefab/RoomWaitingCanvas.prefab";
    private const string RecentRoomsKey = "LobyUIScene.RecentRooms";
    private const int MaxRecentRooms = 3;
    private const int MaxRoomPlayers = 10;
    private static readonly Color PanelColor = new Color(0.055f, 0.085f, 0.14f, 0.97f);
    private static readonly Color FieldColor = new Color(0.12f, 0.16f, 0.23f, 1f);
    private static readonly Color AccentColor = new Color(0.16f, 0.55f, 0.95f, 1f);
    private static readonly Color MutedColor = new Color(0.68f, 0.76f, 0.88f);
    private static readonly Color InputColor = new Color(0.05f, 0.07f, 0.12f, 1f);
    private static readonly Color CardColor = new Color(0.13f, 0.17f, 0.27f, 1f);
    private static readonly Color ListColor = new Color(0.08f, 0.11f, 0.18f, 1f);
    private static readonly Color PrimaryColor = new Color(0.12f, 0.44f, 0.86f, 1f);
    private static readonly Color QuickmatchColor = new Color(0.43f, 0.31f, 0.84f, 1f);
    private static readonly Color SuccessColor = new Color(0.11f, 0.55f, 0.38f, 1f);
    private static readonly Color SuccessTextColor = new Color(0.4f, 0.88f, 0.62f);
    private static readonly Color DangerTextColor = new Color(1f, 0.52f, 0.52f);
    private static readonly Color WarningColor = new Color(0.96f, 0.74f, 0.25f);
    private static readonly Color LinkColor = new Color(0.5f, 0.72f, 1f);
    private static readonly Color LineColor = new Color(1f, 1f, 1f, 0.12f);

    // Ukuran UI untuk layar HP (referensi 1920x1080). Teks < 24 atau tombol < 88 terlalu kecil untuk jari.
    // Canvas lobby dan room waiting punya versi sendiri supaya perubahan satu UI tidak
    // membangun ulang prefab UI lainnya.
    private const string LobbyLayoutVersion = "Layout v8";
    private const string WaitingCanvasLayoutVersion = "Layout v12"; // v12: batang ikon pause sama besar dan tajam
    private const int LabelFontSize = 26;
    private const int BodyFontSize = 30;
    private const int SmallFontSize = 24;
    private const int ButtonFontSize = 30;
    private const float RecentRowHeight = 94f;
    private const float RecentRowSpacing = 16f;
    private const float PlayerRowHeight = 86f;
    private const float PlayerRowSpacing = 12f;
    private const int EmptySlotsShown = 2;

    private enum StatusKind { Info, Busy, Success, Error }

    private GameObject _panel;
    private GameObject _mainMenu;
    private Text _mainMenuGreeting;
    private GameObject _settingsOverlay;
    private GameObject _exitOverlay;
    private Slider _sensitivitySlider;
    private Text _sensitivityValue;
    private Text _settingsAccount;
    private Text _exitConfirmLabel;
    private bool _quitting;
    // Keluar room memuat ulang scene; setelah itu pemain kembali ke panel lobby, bukan ke menu utama.
    private static bool s_openLobbyOnNextLoad;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() {
        s_openLobbyOnNextLoad = false;
    }
    private InputField _roomInput;
    private InputField _codeInput;
    private Text _statusText;
    private Image _statusDot;
    private GameObject _accountChip;
    private Text _accountName;
    private Text _accountInitial;
    private GameObject _signOutButton;
    private Transform _recentList;
    private ScrollRect _recentScroll;
    private bool _busy;
    private GameObject _waitingCanvas;
    private GameObject _waitingPanel;
    private Text _waitingCodeText;
    private Text _waitingStatusText;
    private Text _playerCountText;
    private RectTransform _playerListContent;
    private Text _pausePlayerCountText;
    private RectTransform _pausePlayerListContent;
    private ScrollRect _pausePlayerScroll;
    private GameObject _pauseButton;
    private GameObject _pauseOverlay;
    private GameObject _pauseMainPanel;
    private GameObject _pausePlayersPanel;
    private GameObject _pauseSettingsPanel;
    private GameObject _pauseLeaveConfirmPanel;
    private Slider _pauseSensitivitySlider;
    private Text _pauseSensitivityValue;
    private InGameMenuPage _inGameMenuPage;
    private Text _copyButtonLabel;
    private Coroutine _copyFeedbackRoutine;
    private Coroutine _guestSignOutConfirmRoutine;
    private string _waitingRosterSignature;
    private string _pauseRosterSignature;
    private Button _waitingJoinButton;
    private Button _waitingRosterToggleButton;
    private Coroutine _playerListRoutine;
    private bool _roomFullDisconnectRequested;
    private bool _startingGame;
    private bool _gameplayStarted;
    private bool _leaveRoomPending;
    private bool _worldLoading;
    private bool _unloadWorldWhenReady;
    private AsyncOperation _worldUnloadOperation;
    private bool _returnToLobbyOnDisconnect;
    private const string WorldSceneName = "WorldScene";

    private enum InGameMenuPage { Closed, Main, Players, Settings, LeaveConfirm }

    private void OnEnable() {
#if UNITY_EDITOR
        if (!Application.isPlaying && IsLobbyScene(gameObject.scene)) {
            EditorApplication.delayCall += BuildCanvasInEditor;
            return;
        }
#endif
        if (Application.isPlaying)
            InitializeRuntime();
    }

    private void OnDisable() {
#if UNITY_EDITOR
        EditorApplication.delayCall -= BuildCanvasInEditor;
#endif
        if (_playerListRoutine != null)
            StopCoroutine(_playerListRoutine);
        if (!Application.isPlaying || realtime == null)
            return;

        LoginUIController.AccountChanged -= OnAccountChanged;
        realtime.didConnectToRoom -= DidConnectToRoom;
        realtime.didDisconnectFromRoom -= DidDisconnectFromRoom;
        realtime.didDisconnectFromRoomWithEvent -= DidDisconnectFromRoomWithEvent;
    }

    private void Update() {
        if (!Application.isPlaying || !_gameplayStarted || _leaveRoomPending ||
            !Input.GetKeyDown(KeyCode.Escape))
            return;

        switch (_inGameMenuPage) {
            case InGameMenuPage.Closed:
                OpenPauseMenu();
                break;
            case InGameMenuPage.Main:
                ClosePauseMenu();
                break;
            case InGameMenuPage.Players:
            case InGameMenuPage.Settings:
            case InGameMenuPage.LeaveConfirm:
                ReturnToPauseMenu();
                break;
        }
    }

    private void InitializeRuntime() {
        if (realtime == null)
            realtime = GetComponent<Realtime>();
        if (realtime == null)
            realtime = FindFirstObjectByType<Realtime>();

        EnsureCanvasExists();
        EnsureWaitingCanvasExists();
        ResolveCanvasReferences();
        if (realtime == null) {
            SetStatus("Komponen Normcore Realtime tidak ditemukan di scene.", StatusKind.Error);
            return;
        }

        realtime.didConnectToRoom -= DidConnectToRoom;
        realtime.didDisconnectFromRoom -= DidDisconnectFromRoom;
        realtime.didDisconnectFromRoomWithEvent -= DidDisconnectFromRoomWithEvent;
        realtime.didConnectToRoom += DidConnectToRoom;
        realtime.didDisconnectFromRoom += DidDisconnectFromRoom;
        realtime.didDisconnectFromRoomWithEvent += DidDisconnectFromRoomWithEvent;

        AddClickListener("Lobby Create Button", CreateRoom);
        AddClickListener("Lobby Join Button", ConnectByRoomName);
        AddClickListener("Lobby Quickmatch Button", ConnectQuickmatch);
        AddClickListener("Lobby Join Code Button", ConnectQuickmatchCode);
        AddClickListener("Lobby Close Button", ShowMainMenu);
        AddClickListener("Lobby Sign Out Button", SignOutAccount);
        AddClickListener("Main Menu Play Button", OpenLobby);
        AddClickListener("Main Menu Settings Button", OpenSettings);
        AddClickListener("Main Menu Quit Button", OpenExitConfirm);
        AddClickListener("Settings Close Button", CloseSettings);
        AddClickListener("Settings Done Button", CloseSettings);
        AddClickListener("Exit Cancel Button", CloseExitConfirm);
        AddClickListener("Exit Confirm Button", QuitGame);
        LoginUIController.AccountChanged -= OnAccountChanged;
        LoginUIController.AccountChanged += OnAccountChanged;
        RefreshAccountChip();

        SetStatus("Siap. Buat room atau bergabung.", StatusKind.Success);
        RefreshRecentRooms();
        if (s_openLobbyOnNextLoad) {
            s_openLobbyOnNextLoad = false;
            OpenLobby();
        } else {
            ShowMainMenu();
        }
        if (realtime.connected)
            DidConnectToRoom(realtime);
    }

#if UNITY_EDITOR
    private void BuildCanvasInEditor() {
        EnsureCanvasInEditor();
    }

    private static bool IsLobbyScene(UnityEngine.SceneManagement.Scene scene) {
        return scene.IsValid() && (scene.name == "LobyUIScene" || scene.name == "MainScene");
    }

    internal void EnsureCanvasInEditor() {
        if (this == null || Application.isPlaying || !IsLobbyScene(gameObject.scene))
            return;

        if (HasCompleteCanvas()) {
            GameObject sceneCanvas = FindCanvasInScene();
            bool changed = false;
            // Canvas yang di-unpack di scene ini dibangun ulang di tempat; instance prefab ikut lewat UpgradeCanvasPrefabs.
            if (!PrefabUtility.IsPartOfPrefabInstance(sceneCanvas) &&
                !IsCurrentLayout(sceneCanvas.transform, LobbyLayoutVersion)) {
                RebuildInPlace(sceneCanvas.transform, BuildCanvasContents);
                changed = true;
            }
            changed |= EnsureLobbyCanvasPrefabInEditor();
            GameObject waitingCanvas = FindWaitingCanvas();
            if (waitingCanvas != null && !PrefabUtility.IsPartOfPrefabInstance(waitingCanvas))
                changed |= EnsureWaitingCanvasControls(waitingCanvas.transform);
            changed |= EnsureWaitingCanvasInEditor();
            if (changed) {
                EditorSceneManager.MarkSceneDirty(gameObject.scene);
                if (gameObject.scene.path == "Assets/Scenes/MainScene.unity" ||
                    gameObject.scene.path == "Assets/Scenes/Multiplayer/LobyUIScene.unity")
                    EditorSceneManager.SaveScene(gameObject.scene);
            }
            return;
        }

        RemoveIncompleteCanvas();
        GameObject lobbyPrefab = LoadCanvasPrefab(LobbyCanvasPrefabPath);
        if (lobbyPrefab != null) {
            PrefabUtility.InstantiatePrefab(lobbyPrefab, gameObject.scene);
        } else {
            Canvas canvas = CreateCanvas();
            BuildCanvasContents(canvas.transform);
            EnsureLobbyCanvasPrefabInEditor();
        }
        EnsureWaitingCanvasInEditor();
        EnsureEventSystem();
        EditorSceneManager.MarkSceneDirty(gameObject.scene);
        if (gameObject.scene.path == "Assets/Scenes/MainScene.unity" ||
            gameObject.scene.path == "Assets/Scenes/Multiplayer/LobyUIScene.unity")
            EditorSceneManager.SaveScene(gameObject.scene);
    }
#endif

#if UNITY_EDITOR
    private static void EnsurePrefabFolderExists() {
        if (!AssetDatabase.IsValidFolder(PrefabFolderPath))
            AssetDatabase.CreateFolder("Assets", "Prefab");
    }

    private static GameObject LoadCanvasPrefab(string path) {
        return AssetDatabase.LoadAssetAtPath<GameObject>(path);
    }

    /// <summary>
    /// Bangun ulang isi prefab LobbyCanvas/RoomWaitingCanvas kalau layout-nya masih versi lama.
    /// Root prefab dipertahankan, jadi instance di MainMultiplayerScene/LobbyReturnScene tetap tersambung.
    /// </summary>
    internal static void UpgradeCanvasPrefabs() {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;
        UpgradePrefab(LobbyCanvasPrefabPath, BuildCanvasContents, LobbyLayoutVersion);
        UpgradePrefab(WaitingCanvasPrefabPath, BuildWaitingCanvasContents, WaitingCanvasLayoutVersion);
    }

    private static void UpgradePrefab(string path, System.Action<Transform> build, string layoutVersion) {
        if (LoadCanvasPrefab(path) == null)
            return;
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try {
            if (IsCurrentLayout(root.transform, layoutVersion))
                return;
            RebuildInPlace(root.transform, build);
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Debug.Log($"[Lobby UI] {path} diperbarui ke {layoutVersion}.");
        } finally {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private bool EnsureLobbyCanvasPrefabInEditor() {
        GameObject lobbyCanvas = FindCanvasInScene();
        if (lobbyCanvas == null) {
            GameObject prefab = LoadCanvasPrefab(LobbyCanvasPrefabPath);
            if (prefab == null)
                return false;
            return PrefabUtility.InstantiatePrefab(prefab, gameObject.scene) != null;
        }

        if (PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(lobbyCanvas) == LobbyCanvasPrefabPath)
            return false;

        EnsurePrefabFolderExists();
        PrefabUtility.SaveAsPrefabAssetAndConnect(lobbyCanvas, LobbyCanvasPrefabPath,
                                                  InteractionMode.AutomatedAction);
        return true;
    }
#endif

    private void EnsureCanvasExists() {
        if (HasCompleteCanvas()) {
            Transform lobbyCanvas = FindCanvasInScene().transform;
            if (!IsCurrentLayout(lobbyCanvas, LobbyLayoutVersion))
                RebuildInPlace(lobbyCanvas, BuildCanvasContents);
            EnsureWaitingCanvasExists();
            return;
        }

        RemoveIncompleteCanvas();
        Canvas canvas = CreateCanvas();
        BuildCanvasContents(canvas.transform);
        EnsureWaitingCanvasExists();
        EnsureEventSystem();
    }

    private void EnsureWaitingCanvasExists() {
        GameObject waitingCanvas = FindWaitingCanvas();
        if (waitingCanvas != null) {
            EnsureWaitingCanvasControls(waitingCanvas.transform);
            return;
        }
        Canvas canvas = CreateCanvas(WaitingCanvasRootName, 51);
        BuildWaitingCanvasContents(canvas.transform);
        canvas.gameObject.SetActive(false);
    }

    private bool EnsureWaitingCanvasInEditor() {
        GameObject waitingCanvas = FindWaitingCanvas();
        if (waitingCanvas == null) {
            GameObject prefab = LoadCanvasPrefab(WaitingCanvasPrefabPath);
            if (prefab != null) {
                waitingCanvas = PrefabUtility.InstantiatePrefab(prefab, gameObject.scene) as GameObject;
                if (waitingCanvas != null)
                    waitingCanvas.SetActive(false);
                return waitingCanvas != null;
            }

            Canvas createdCanvas = CreateCanvas(WaitingCanvasRootName, 51);
            BuildWaitingCanvasContents(createdCanvas.transform);
            waitingCanvas = createdCanvas.gameObject;
            waitingCanvas.SetActive(false);
        }

        string instancePath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(waitingCanvas);
        if (instancePath == WaitingCanvasPrefabPath)
            return false;

        EnsurePrefabFolderExists();
        PrefabUtility.SaveAsPrefabAssetAndConnect(waitingCanvas, WaitingCanvasPrefabPath,
                                                  InteractionMode.AutomatedAction);
        return true;
    }

    // Layout mengikuti desain "Room Lobby". Koordinat = jarak dari kiri/atas panel (lihat Place).
    private static void BuildWaitingCanvasContents(Transform canvas) {
        RectTransform safeArea = CreateSafeArea("Waiting Safe Area", canvas);
        RectTransform panel = CreateResponsivePanel("Room Waiting Panel", safeArea, new Vector2(1200f, 910f));
        AddShape(panel.gameObject, PanelColor, cornerScale: 1.8f);
        AddOutline(panel, LineColor, cornerScale: 1.8f);

        Place(CreateText("Waiting Title", panel, "ROOM LOBBY", 56, FontStyle.Bold, TextAnchor.MiddleLeft,
                         Vector2.zero, Vector2.zero, Color.white).rectTransform, 52f, 48f, 700f, 72f);
        RectTransform count = Place(CreateRect("Waiting Player Count Pill", panel, Vector2.zero), 780f, 62f, 252f, 56f);
        AddShape(count.gameObject, CardColor, cornerScale: 1.8f);
        Stretch(CreateText("Waiting Player Count", count, "0 / 10 pemain", LabelFontSize, FontStyle.Bold,
                           TextAnchor.MiddleCenter, Vector2.zero, Vector2.zero, Color.white).rectTransform, 0f);
        Place(CreateButton("Waiting Close Roster Button", panel, "×", CardColor, 48).GetComponent<RectTransform>(),
              1054f, 43f, 94f, 94f);

        // Kode/nama room yang bisa dibagikan ke teman.
        Place(CreateText("Waiting Share Label", panel, "BAGIKAN KE TEMAN", LabelFontSize, FontStyle.Bold,
                         TextAnchor.MiddleLeft, Vector2.zero, Vector2.zero, MutedColor).rectTransform, 52f, 166f, 800f, 34f);
        RectTransform codeBox = Place(CreateRect("Waiting Room Code Box", panel, Vector2.zero), 52f, 212f, 795f, 94f);
        AddShape(codeBox.gameObject, InputColor);
        AddOutline(codeBox, new Color(1f, 1f, 1f, 0.25f), "ui-dashed");
        Stretch(CreateText("Waiting Room Code", codeBox, "-", 42, FontStyle.Bold, TextAnchor.MiddleLeft,
                           Vector2.zero, Vector2.zero, SuccessTextColor).rectTransform, 28f);
        Place(CreateButton("Waiting Copy Code Button", panel, "SALIN", SuccessColor).GetComponent<RectTransform>(),
              869f, 212f, 279f, 94f);
        Place(CreateText("Waiting Status", panel, "", LabelFontSize, FontStyle.Normal, TextAnchor.MiddleLeft,
                         Vector2.zero, Vector2.zero, MutedColor).rectTransform, 52f, 318f, 1096f, 44f);

        // Daftar pemain: baris dibuat saat runtime (RefreshPlayerRows), di-scroll kalau lebih dari 3.
        RectTransform list = Place(CreateRect("Waiting Player Box", panel, Vector2.zero), 52f, 376f, 1096f, 330f);
        AddShape(list.gameObject, ListColor);
        list.gameObject.AddComponent<RectMask2D>();
        ScrollRect scroll = list.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 28f;
        scroll.viewport = list;
        RectTransform content = CreateRect("Waiting Player List Content", list, new Vector2(0f, 330f));
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        scroll.content = content;

        // MULAI = aksi utama (besar, biru). KELUAR ROOM sengaja lebih kecil dan hanya outline supaya tidak salah tap.
        Place(CreateButton("Waiting Join Game Button", panel, "MULAI", PrimaryColor, 36).GetComponent<RectTransform>(),
              52f, 740f, 791f, 116f);
        Place(CreateButton("Waiting Disconnect Button", panel, "KELUAR ROOM", DangerTextColor, LabelFontSize, true)
                  .GetComponent<RectTransform>(), 869f, 751f, 279f, 94f);

        Button rosterToggle = CreateButton("Waiting Roster Toggle Button", safeArea, "PEMAIN", PrimaryColor);
        PlaceTopRight(rosterToggle.GetComponent<RectTransform>(), 30f, 30f, 220f, 88f);
        rosterToggle.gameObject.SetActive(false);

        BuildInGamePauseContents(canvas, safeArea);
        AddLayoutMarker(canvas, WaitingCanvasLayoutVersion);
    }

    private static void BuildInGamePauseContents(Transform canvas, RectTransform safeArea) {
        Button pauseButton = CreateButton("Pause Button", safeArea, "", PrimaryColor);
        RectTransform pauseButtonRect = pauseButton.GetComponent<RectTransform>();
        PlaceTopRight(pauseButtonRect, 30f, 30f, 96f, 96f);
        RectTransform leftPauseBarRect = Place(CreateRect("Pause Icon Left Bar", pauseButtonRect, Vector2.zero),
                                                32f, 28f, 12f, 40f);
        RectTransform rightPauseBarRect = Place(CreateRect("Pause Icon Right Bar", pauseButtonRect, Vector2.zero),
                                                 52f, 28f, 12f, 40f);
        Image leftPauseBar = leftPauseBarRect.gameObject.AddComponent<Image>();
        Image rightPauseBar = rightPauseBarRect.gameObject.AddComponent<Image>();
        leftPauseBar.color = rightPauseBar.color = Color.white;
        leftPauseBar.raycastTarget = false;
        rightPauseBar.raycastTarget = false;
        pauseButton.gameObject.SetActive(false);

        RectTransform overlay = CreateModalOverlay("Pause Overlay", canvas);
        RectTransform pauseSafeArea = CreateSafeArea("Pause Safe Area", overlay);

        RectTransform main = CreateResponsivePanel("Pause Main Panel", pauseSafeArea, new Vector2(860f, 750f));
        AddShape(main.gameObject, PanelColor, cornerScale: 1.8f);
        AddOutline(main, LineColor, cornerScale: 1.8f);
        Place(CreateText("Pause Title", main, "JEDA", 52, FontStyle.Bold, TextAnchor.MiddleCenter,
                         Vector2.zero, Vector2.zero, Color.white).rectTransform, 48f, 38f, 764f, 72f);
        Place(CreateButton("Pause Resume Button", main, "LANJUTKAN", PrimaryColor, 34).GetComponent<RectTransform>(),
              64f, 142f, 732f, 96f);
        Place(CreateButton("Pause Players Button", main, "PEMAIN", CardColor, 32).GetComponent<RectTransform>(),
              64f, 258f, 732f, 96f);
        Place(CreateButton("Pause Settings Button", main, "PENGATURAN", CardColor, 32).GetComponent<RectTransform>(),
              64f, 374f, 732f, 96f);
        Place(CreateButton("Pause Leave Room Button", main, "KELUAR ROOM", DangerTextColor, 30, true)
                  .GetComponent<RectTransform>(), 64f, 490f, 732f, 96f);

        RectTransform players = CreateResponsivePanel("Pause Players Panel", pauseSafeArea, new Vector2(1200f, 910f));
        AddShape(players.gameObject, PanelColor, cornerScale: 1.8f);
        AddOutline(players, LineColor, cornerScale: 1.8f);
        Place(CreateText("Pause Players Title", players, "PEMAIN", 52, FontStyle.Bold, TextAnchor.MiddleLeft,
                         Vector2.zero, Vector2.zero, Color.white).rectTransform, 52f, 42f, 660f, 72f);
        RectTransform count = Place(CreateRect("Pause Player Count Pill", players, Vector2.zero),
                                    780f, 52f, 252f, 56f);
        AddShape(count.gameObject, CardColor, cornerScale: 1.8f);
        Stretch(CreateText("Pause Player Count", count, "0 / 10 pemain", LabelFontSize, FontStyle.Bold,
                           TextAnchor.MiddleCenter, Vector2.zero, Vector2.zero, Color.white).rectTransform, 0f);
        RectTransform list = Place(CreateRect("Pause Player Box", players, Vector2.zero),
                                   52f, 142f, 1096f, 650f);
        AddShape(list.gameObject, ListColor);
        list.gameObject.AddComponent<RectMask2D>();
        ScrollRect scroll = list.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 28f;
        scroll.viewport = list;
        RectTransform content = CreateRect("Pause Player List Content", list, new Vector2(0f, 650f));
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        scroll.content = content;
        Place(CreateButton("Pause Players Back Button", players, "KEMBALI", PrimaryColor)
                  .GetComponent<RectTransform>(), 400f, 814f, 400f, 88f);
        players.gameObject.SetActive(false);

        RectTransform settings = CreateResponsivePanel("Pause Settings Panel", pauseSafeArea, new Vector2(960f, 620f));
        AddShape(settings.gameObject, PanelColor, cornerScale: 1.8f);
        AddOutline(settings, LineColor, cornerScale: 1.8f);
        Place(CreateText("Pause Settings Title", settings, "PENGATURAN", 48, FontStyle.Bold,
                         TextAnchor.MiddleLeft, Vector2.zero, Vector2.zero, Color.white).rectTransform,
              56f, 40f, 780f, 68f);
        Place(CreateText("Pause Sensitivity Label", settings, "SENSITIVITAS KAMERA", LabelFontSize,
                         FontStyle.Bold, TextAnchor.MiddleLeft, Vector2.zero, Vector2.zero, MutedColor).rectTransform,
              56f, 164f, 500f, 40f);
        Place(CreateText("Pause Sensitivity Value", settings, "1.0x", BodyFontSize, FontStyle.Bold,
                         TextAnchor.MiddleRight, Vector2.zero, Vector2.zero, Color.white).rectTransform,
              704f, 164f, 200f, 40f);
        Place(CreateSlider("Pause Sensitivity Slider", settings, GameSettings.MinCameraSensitivity,
                           GameSettings.MaxCameraSensitivity).GetComponent<RectTransform>(), 56f, 220f, 848f, 72f);
        Place(CreateButton("Pause Settings Back Button", settings, "KEMBALI", PrimaryColor)
                  .GetComponent<RectTransform>(), 56f, 424f, 848f, 96f);
        settings.gameObject.SetActive(false);

        RectTransform confirm = CreateResponsivePanel("Pause Leave Confirm Panel", pauseSafeArea, new Vector2(820f, 440f));
        AddShape(confirm.gameObject, PanelColor, cornerScale: 1.8f);
        AddOutline(confirm, LineColor, cornerScale: 1.8f);
        Place(CreateText("Pause Leave Confirm Title", confirm, "Keluar dari room?", 42, FontStyle.Bold,
                         TextAnchor.MiddleLeft, Vector2.zero, Vector2.zero, Color.white).rectTransform,
              56f, 48f, 708f, 64f);
        Place(CreateText("Pause Leave Confirm Body", confirm,
                         "Kamu akan meninggalkan permainan dan kembali ke lobby.", BodyFontSize,
                         FontStyle.Normal, TextAnchor.UpperLeft, Vector2.zero, Vector2.zero, MutedColor).rectTransform,
              56f, 132f, 708f, 92f);
        Place(CreateButton("Pause Leave Cancel Button", confirm, "BATAL", CardColor)
                  .GetComponent<RectTransform>(), 56f, 280f, 342f, 96f);
        Place(CreateButton("Pause Leave Confirm Button", confirm, "KELUAR ROOM", DangerTextColor, 26, true)
                  .GetComponent<RectTransform>(), 422f, 280f, 342f, 96f);
        confirm.gameObject.SetActive(false);

        overlay.gameObject.SetActive(false);
    }

    /// <summary>Pastikan canvas ruang tunggu memakai layout terbaru. Mengembalikan true kalau ada perubahan.</summary>
    internal static bool EnsureWaitingCanvasControls(Transform canvas) {
        if (IsCurrentLayout(canvas, WaitingCanvasLayoutVersion))
            return false;
        RebuildInPlace(canvas, BuildWaitingCanvasContents);
        return true;
    }

    private bool HasCompleteCanvas() {
        GameObject canvas = FindCanvasInScene();
        if (canvas == null)
            return false;

        Transform root = canvas.transform;
        return FindChild(root, "Lobby Panel") != null &&
               FindChild(root, "Lobby Room Input") != null &&
               FindChild(root, "Lobby Code Input") != null &&
               FindChild(root, "Lobby Status") != null &&
               FindChild(root, "Lobby Status") != null;
    }

    private static bool IsCurrentLayout(Transform canvas, string layoutVersion) {
        return canvas.Find(layoutVersion) != null;
    }

    private static void AddLayoutMarker(Transform canvas, string layoutVersion) {
        var marker = new GameObject(layoutVersion, typeof(RectTransform));
        marker.transform.SetParent(canvas, false);
        marker.SetActive(false);
    }

    /// <summary>Hapus semua anak canvas lalu bangun ulang. Root (Canvas, scaler, komponen lain) tetap.</summary>
    private static void RebuildInPlace(Transform canvas, System.Action<Transform> build) {
        ClearChildren(canvas);
        build(canvas);
    }

    private void RemoveIncompleteCanvas() {
        GameObject incomplete = FindCanvasInScene();
        if (incomplete == null)
            return;

        if (Application.isPlaying) {
            incomplete.name = "Loby Canvas (Rebuilding)";
            Destroy(incomplete);
        } else {
            DestroyImmediate(incomplete);
        }
    }

    private GameObject FindCanvasInScene() {
        if (!gameObject.scene.IsValid())
            return GameObject.Find(CanvasRootName);
        foreach (GameObject root in gameObject.scene.GetRootGameObjects()) {
            if (root.name == CanvasRootName)
                return root;
        }
        return null;
    }

    private GameObject FindWaitingCanvas() {
        if (!gameObject.scene.IsValid())
            return GameObject.Find(WaitingCanvasRootName) ?? GameObject.Find(WaitingCanvasPrefabRootName);
        foreach (GameObject root in gameObject.scene.GetRootGameObjects()) {
            if (root.name == WaitingCanvasRootName || root.name == WaitingCanvasPrefabRootName)
                return root;
        }
        return null;
    }

    private Canvas CreateCanvas(string rootName = CanvasRootName, int order = 50) {
        GameObject canvasObject = new GameObject(rootName,
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = order;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        return canvas;
    }

    // Layout mengikuti desain "Go Vista Lobby": kiri = Quickmatch / nama room / kode, kanan = room terakhir,
    // bawah = status + keluar akun. Koordinat = jarak dari kiri/atas panel (lihat Place).
    private static void BuildCanvasContents(Transform canvas) {
        RectTransform safeArea = CreateSafeArea("Lobby Safe Area", canvas);
        RectTransform panel = CreateResponsivePanel("Lobby Panel", safeArea, new Vector2(1680f, 960f));
        AddShape(panel.gameObject, PanelColor, cornerScale: 1.8f);
        AddOutline(panel, LineColor, cornerScale: 1.8f);

        Place(CreateText("Lobby Title", panel, "GO VISTA LOBBY", 56, FontStyle.Bold, TextAnchor.MiddleLeft,
                         Vector2.zero, Vector2.zero, Color.white).rectTransform, 53f, 38f, 1000f, 72f);
        Place(CreateText("Lobby Subtitle", panel, "Buat room, masukkan nama room, atau pakai kode Quickmatch.",
                         LabelFontSize, FontStyle.Normal, TextAnchor.MiddleLeft, Vector2.zero, Vector2.zero,
                         MutedColor).rectTransform, 53f, 112f, 1000f, 40f);

        // Chip akun (siapa yang sedang login) di kiri tombol tutup panel.
        RectTransform chip = Place(CreateRect("Lobby Account Chip", panel, Vector2.zero), 1149f, 54f, 360f, 80f);
        AddShape(chip.gameObject, CardColor, cornerScale: 2.5f);
        RectTransform avatar = Place(CreateRect("Lobby Account Avatar", chip, Vector2.zero), 12f, 12f, 56f, 56f);
        AddShape(avatar.gameObject, QuickmatchColor, "ui-circle");
        Stretch(CreateText("Lobby Account Initial", avatar, "?", 28, FontStyle.Bold, TextAnchor.MiddleCenter,
                           Vector2.zero, Vector2.zero, Color.white).rectTransform, 0f);
        Place(CreateText("Lobby Account Name", chip, "-", 28, FontStyle.Normal, TextAnchor.MiddleLeft,
                         Vector2.zero, Vector2.zero, Color.white).rectTransform, 84f, 0f, 262f, 80f);
        Place(CreateButton("Lobby Close Button", panel, "×", CardColor, 48).GetComponent<RectTransform>(),
              1533f, 49f, 90f, 90f);

        // Kolom kiri: Quickmatch = aksi utama, lalu cara manual.
        Place(CreateButton("Lobby Quickmatch Button", panel, "QUICKMATCH · CARI ROOM OTOMATIS", QuickmatchColor, 34)
                  .GetComponent<RectTransform>(), 53f, 178f, 910f, 124f);
        AddLine(panel, "Lobby Divider Left", 53f, 339f, 390f);
        Place(CreateText("Lobby Divider Text", panel, "ATAU", SmallFontSize, FontStyle.Normal, TextAnchor.MiddleCenter,
                         Vector2.zero, Vector2.zero, MutedColor).rectTransform, 443f, 322f, 130f, 36f);
        AddLine(panel, "Lobby Divider Right", 573f, 339f, 390f);
        Place(CreateText("Room Name Label", panel, "NAMA ROOM", LabelFontSize, FontStyle.Bold, TextAnchor.MiddleLeft,
                         Vector2.zero, Vector2.zero, MutedColor).rectTransform, 53f, 382f, 910f, 36f);
        Place(CreateInputField("Lobby Room Input", panel, "Masukkan nama room").GetComponent<RectTransform>(),
              53f, 428f, 910f, 96f);
        Place(CreateButton("Lobby Create Button", panel, "BUAT ROOM", PrimaryColor).GetComponent<RectTransform>(),
              53f, 549f, 443f, 96f);
        Place(CreateButton("Lobby Join Button", panel, "GABUNG", PrimaryColor).GetComponent<RectTransform>(),
              520f, 549f, 443f, 96f);
        Place(CreateInputField("Lobby Code Input", panel, "Kode Quickmatch").GetComponent<RectTransform>(),
              53f, 668f, 578f, 96f);
        Place(CreateButton("Lobby Join Code Button", panel, "GABUNG DENGAN KODE", SuccessTextColor, SmallFontSize, true)
                  .GetComponent<RectTransform>(), 656f, 668f, 307f, 96f);

        // Kolom kanan: kartu room terakhir (dibuat saat runtime di RefreshRecentRooms).
        Place(CreateText("Recent Rooms Label", panel, "ROOM TERAKHIR", LabelFontSize, FontStyle.Bold,
                         TextAnchor.MiddleLeft, Vector2.zero, Vector2.zero, MutedColor).rectTransform,
              1017f, 178f, 606f, 36f);
        RectTransform recent = Place(CreateRect("Lobby Recent Rooms", panel, Vector2.zero), 1017f, 223f, 606f, 320f);
        recent.gameObject.AddComponent<RectMask2D>();
        ScrollRect recentScroll = recent.gameObject.AddComponent<ScrollRect>();
        recentScroll.horizontal = false;
        recentScroll.movementType = ScrollRect.MovementType.Clamped;
        recentScroll.scrollSensitivity = 28f;
        recentScroll.viewport = recent;
        RectTransform recentContent = CreateRect("Lobby Recent Rooms Content", recent, new Vector2(0f, 320f));
        recentContent.anchorMin = new Vector2(0f, 1f);
        recentContent.anchorMax = new Vector2(1f, 1f);
        recentScroll.content = recentContent;
        Place(CreateText("Global Rooms Note", panel, "Daftar room publik belum tersedia.", SmallFontSize,
                         FontStyle.Normal, TextAnchor.MiddleLeft, Vector2.zero, Vector2.zero, MutedColor).rectTransform,
              1017f, 556f, 606f, 40f);

        // Footer: status bar (titik berwarna sesuai kondisi) + keluar akun.
        AddLine(panel, "Lobby Footer Line", 53f, 797f, 1574f);
        RectTransform dot = Place(CreateRect("Lobby Status Dot", panel, Vector2.zero), 53f, 858f, 20f, 20f);
        AddShape(dot.gameObject, SuccessTextColor, "ui-circle");
        Place(CreateText("Lobby Status", panel, "Siap. Buat room atau bergabung.", 28, FontStyle.Normal,
                         TextAnchor.MiddleLeft, Vector2.zero, Vector2.zero, Color.white).rectTransform,
              90f, 832f, 1130f, 72f);
        Place(CreateButton("Lobby Sign Out Button", panel, "Keluar akun", DangerTextColor, LabelFontSize, true)
                  .GetComponent<RectTransform>(), 1363f, 832f, 260f, 72f);

        BuildMainMenu(safeArea);
        BuildSettingsOverlay(canvas);
        BuildExitConfirmOverlay(canvas);
        AddLayoutMarker(canvas, LobbyLayoutVersion);
    }

    /// <summary>
    /// Menu utama setelah login: kartu di kanan (kiri dibiarkan untuk logo dan karakter di background).
    /// MAIN membuka panel lobby, PENGATURAN membuka overlay pengaturan, KELUAR GAME minta konfirmasi dulu.
    /// </summary>
    private static void BuildMainMenu(RectTransform safeArea) {
        RectTransform menu = Stretch(CreateRect("Main Menu", safeArea, Vector2.zero), 0f);
        RectTransform card = CreateRect("Main Menu Card", menu, new Vector2(640f, 532f));
        card.anchorMin = card.anchorMax = card.pivot = new Vector2(1f, 0.5f);
        card.anchoredPosition = new Vector2(-100f, -40f);
        AddShape(card.gameObject, PanelColor, cornerScale: 1.8f);
        AddOutline(card, LineColor, cornerScale: 1.8f);

        Place(CreateText("Main Menu Greeting", card, "Selamat datang!", BodyFontSize, FontStyle.Normal,
                         TextAnchor.MiddleCenter, Vector2.zero, Vector2.zero, MutedColor).rectTransform,
              40f, 40f, 560f, 44f);
        Place(CreateButton("Main Menu Play Button", card, "MAIN", PrimaryColor, 44).GetComponent<RectTransform>(),
              40f, 108f, 560f, 128f);
        Place(CreateButton("Main Menu Settings Button", card, "PENGATURAN", CardColor).GetComponent<RectTransform>(),
              40f, 260f, 560f, 104f);
        // Aksi keluar dibuat outline merah supaya jelas berbeda dari PENGATURAN.
        Place(CreateButton("Main Menu Quit Button", card, "KELUAR GAME", DangerTextColor, ButtonFontSize, true)
                  .GetComponent<RectTransform>(), 40f, 388f, 560f, 104f);
    }

    private static void BuildSettingsOverlay(Transform canvas) {
        RectTransform overlay = CreateModalOverlay("Settings Overlay", canvas);
        RectTransform dialog = CreateResponsivePanel("Settings Dialog", overlay, new Vector2(960f, 524f));
        AddShape(dialog.gameObject, PanelColor, cornerScale: 1.8f);
        AddOutline(dialog, LineColor, cornerScale: 1.8f);

        Place(CreateText("Settings Title", dialog, "PENGATURAN", 52, FontStyle.Bold, TextAnchor.MiddleLeft,
                         Vector2.zero, Vector2.zero, Color.white).rectTransform, 56f, 44f, 700f, 72f);
        Place(CreateButton("Settings Close Button", dialog, "×", CardColor, 48).GetComponent<RectTransform>(),
              816f, 36f, 88f, 88f);

        // TODO (next develop): tambahkan slider VOLUME di sini setelah game punya audio.
        Place(CreateText("Settings Sensitivity Label", dialog, "SENSITIVITAS KAMERA", LabelFontSize, FontStyle.Bold,
                         TextAnchor.MiddleLeft, Vector2.zero, Vector2.zero, MutedColor).rectTransform,
              56f, 170f, 500f, 40f);
        Place(CreateText("Settings Sensitivity Value", dialog, "1.0x", BodyFontSize, FontStyle.Bold,
                         TextAnchor.MiddleRight, Vector2.zero, Vector2.zero, Color.white).rectTransform,
              704f, 170f, 200f, 40f);
        Place(CreateSlider("Settings Sensitivity Slider", dialog, GameSettings.MinCameraSensitivity,
                           GameSettings.MaxCameraSensitivity).GetComponent<RectTransform>(), 56f, 222f, 848f, 72f);

        Place(CreateText("Settings Account", dialog, "", SmallFontSize, FontStyle.Normal, TextAnchor.MiddleLeft,
                         Vector2.zero, Vector2.zero, MutedColor).rectTransform, 56f, 326f, 848f, 40f);
        Place(CreateButton("Settings Done Button", dialog, "SELESAI", PrimaryColor).GetComponent<RectTransform>(),
              56f, 396f, 848f, 96f);
        overlay.gameObject.SetActive(false);
    }

    private static void BuildExitConfirmOverlay(Transform canvas) {
        RectTransform overlay = CreateModalOverlay("Exit Confirm Overlay", canvas);
        RectTransform dialog = CreateResponsivePanel("Exit Confirm Dialog", overlay, new Vector2(820f, 420f));
        AddShape(dialog.gameObject, PanelColor, cornerScale: 1.8f);
        AddOutline(dialog, LineColor, cornerScale: 1.8f);

        Place(CreateText("Exit Confirm Title", dialog, "Keluar dari game?", 44, FontStyle.Bold, TextAnchor.MiddleLeft,
                         Vector2.zero, Vector2.zero, Color.white).rectTransform, 56f, 48f, 708f, 64f);
        Place(CreateText("Exit Confirm Body", dialog, "Progres kamu akan disimpan dulu sebelum game ditutup.",
                         BodyFontSize, FontStyle.Normal, TextAnchor.UpperLeft, Vector2.zero, Vector2.zero,
                         MutedColor).rectTransform, 56f, 132f, 708f, 90f);
        // BATAL sengaja di kiri dan netral; KELUAR merah di kanan, supaya tidak keluar karena salah tap.
        Place(CreateButton("Exit Cancel Button", dialog, "BATAL", CardColor).GetComponent<RectTransform>(),
              56f, 268f, 342f, 96f);
        Place(CreateButton("Exit Confirm Button", dialog, "KELUAR", new Color(0.78f, 0.25f, 0.25f))
                  .GetComponent<RectTransform>(), 422f, 268f, 342f, 96f);
        overlay.gameObject.SetActive(false);
    }

    /// <summary>Lapisan gelap layar penuh yang memblokir klik ke UI di belakangnya.</summary>
    private static RectTransform CreateModalOverlay(string name, Transform canvas) {
        RectTransform overlay = Stretch(CreateRect(name, canvas, Vector2.zero), 0f);
        overlay.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.65f);
        return overlay;
    }

    private static Slider CreateSlider(string name, Transform parent, float min, float max) {
        RectTransform root = CreateRect(name, parent, Vector2.zero);
        Slider slider = root.gameObject.AddComponent<Slider>();

        RectTransform track = CreateRect("Track", root, Vector2.zero);
        track.anchorMin = new Vector2(0f, 0.5f);
        track.anchorMax = new Vector2(1f, 0.5f);
        track.pivot = new Vector2(0.5f, 0.5f);
        track.anchoredPosition = Vector2.zero;
        track.sizeDelta = new Vector2(0f, 16f);
        AddShape(track.gameObject, InputColor, cornerScale: 0.5f);

        RectTransform fillArea = CreateRect("Fill Area", track, Vector2.zero);
        Stretch(fillArea, 0f);
        RectTransform fill = Stretch(CreateRect("Fill", fillArea, Vector2.zero), 0f);
        AddShape(fill.gameObject, PrimaryColor, cornerScale: 0.5f);

        // Slider meregangkan handle setinggi area gesernya (anchor Y dipaksa 0..1), jadi area geser dibuat
        // persis setinggi handle supaya handle tetap bulat. Lebarnya dikurangi satu handle supaya tidak
        // keluar dari ujung track.
        const float handleSize = 56f;
        RectTransform handleArea = CreateRect("Handle Slide Area", root, Vector2.zero);
        handleArea.anchorMin = new Vector2(0f, 0.5f);
        handleArea.anchorMax = new Vector2(1f, 0.5f);
        handleArea.pivot = new Vector2(0.5f, 0.5f);
        handleArea.anchoredPosition = Vector2.zero;
        handleArea.sizeDelta = new Vector2(-handleSize, handleSize);
        RectTransform handle = CreateRect("Handle", handleArea, Vector2.zero);
        handle.pivot = new Vector2(0.5f, 0.5f);
        handle.sizeDelta = new Vector2(handleSize, 0f);
        Image handleImage = AddShape(handle.gameObject, Color.white, "ui-circle");
        handleImage.preserveAspect = true;

        slider.fillRect = fill;
        slider.handleRect = handle;
        slider.targetGraphic = handleImage;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = min;
        slider.maxValue = max;
        slider.colors = PressTint();
        return slider;
    }

    // ---------------------------------------------------------------- Helper UI

    /// <summary>Area penuh yang mengikuti Screen.safeArea (tidak tertutup notch/kamera depan).</summary>
    private static RectTransform CreateSafeArea(string name, Transform canvas) {
        RectTransform safeArea = Stretch(CreateRect(name, canvas, Vector2.zero), 0f);
        safeArea.gameObject.AddComponent<SafeAreaFitter>();
        return safeArea;
    }

    /// <summary>
    /// Panel berukuran desain tetap di tengah safe area. ResponsivePanel mengecilkannya secara proporsional
    /// kalau layar lebih sempit/pendek, jadi tidak pernah menempel ke tepi atau terpotong di HP mana pun.
    /// </summary>
    private static RectTransform CreateResponsivePanel(string name, Transform safeArea, Vector2 designSize) {
        RectTransform panel = CreateRect(name, safeArea, designSize);
        panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 0.5f);
        panel.anchoredPosition = Vector2.zero;
        panel.gameObject.AddComponent<ResponsivePanel>();
        return panel;
    }

    private static readonly Dictionary<string, Sprite> SpriteCache = new Dictionary<string, Sprite>();

    /// <summary>Sprite 9-slice di Assets/Resources/UI (sudut membulat, outline, garis putus-putus, lingkaran).</summary>
    private static Sprite UiSprite(string name) {
        if (!SpriteCache.TryGetValue(name, out Sprite sprite) || sprite == null) {
            sprite = Resources.Load<Sprite>("UI/" + name);
            SpriteCache[name] = sprite;
        }
        return sprite;
    }

    /// <summary>Tambah Image dengan sprite UI. cornerScale > 1 = sudut lebih bulat (2.5 ≈ pil untuk tinggi 80).</summary>
    private static Image AddShape(GameObject target, Color color, string sprite = "ui-rounded", float cornerScale = 1f) {
        Image image = target.AddComponent<Image>();
        image.sprite = UiSprite(sprite);
        image.type = sprite == "ui-dashed" ? Image.Type.Tiled
                   : sprite.EndsWith("circle") ? Image.Type.Simple
                   : Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = 1f / cornerScale;
        image.color = color;
        return image;
    }

    private static void AddOutline(RectTransform parent, Color color, string sprite = "ui-outline", float cornerScale = 1f) {
        RectTransform outline = CreateRect(parent.name + " Outline", parent, Vector2.zero);
        Stretch(outline, 0f);
        AddShape(outline.gameObject, color, sprite, cornerScale).raycastTarget = false;
    }

    private static void AddLine(RectTransform parent, string name, float x, float y, float width) {
        RectTransform line = Place(CreateRect(name, parent, Vector2.zero), x, y, width, 2f);
        line.gameObject.AddComponent<Image>().color = LineColor;
    }

    /// <summary>Posisikan dari pojok kiri atas parent, sama seperti koordinat di file desain.</summary>
    private static RectTransform Place(RectTransform rect, float x, float y, float width, float height) {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
        return rect;
    }

    private static void PlaceTopRight(RectTransform rect, float right, float top, float width, float height) {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(-right, -top);
        rect.sizeDelta = new Vector2(width, height);
    }

    /// <summary>Untuk isi baris list: posisi horizontal dari kiri (x >= 0) atau kanan (x < 0), tengah secara vertikal.</summary>
    private static RectTransform PlaceInRow(RectTransform rect, float x, float width, float height) {
        float side = x < 0f ? 1f : 0f;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(side, 0.5f);
        rect.anchoredPosition = new Vector2(x, 0f);
        rect.sizeDelta = new Vector2(width, height);
        return rect;
    }

    private static RectTransform CreateRect(string name, Transform parent, Vector2 size) {
        GameObject item = new GameObject(name, typeof(RectTransform));
        RectTransform rect = item.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = size;
        return rect;
    }

    private static RectTransform Stretch(RectTransform rect, float padding) {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(padding, 0f);
        rect.offsetMax = new Vector2(-padding, 0f);
        return rect;
    }

    private static Text CreateText(string name, Transform parent, string content, int size, FontStyle style,
                                   TextAnchor alignment, Vector2 rectSize, Vector2 position, Color color) {
        RectTransform rect = CreateRect(name, parent, rectSize);
        rect.anchoredPosition = position;
        Text text = rect.gameObject.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = content;
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = alignment;
        text.color = color;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        return text;
    }

    /// <summary>
    /// Tombol utama (latar = color) atau sekunder (outline = true). Tombol sekunder TIDAK transparan: latarnya
    /// solid gelap bernuansa warna aksen + garis tepi dan teks berwarna, supaya tetap terbaca di atas
    /// background apa pun. Tint hanya menggelapkan saat ditekan.
    /// </summary>
    private static Button CreateButton(string name, Transform parent, string label, Color color,
                                       int fontSize = ButtonFontSize, bool outline = false) {
        RectTransform rect = CreateRect(name, parent, Vector2.zero);
        Image image = AddShape(rect.gameObject, outline ? SecondaryFill(color) : color);
        if (outline)
            AddOutline(rect, color);
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.colors = PressTint();

        Text text = CreateText(name + " Label", rect, label, fontSize, FontStyle.Bold, TextAnchor.MiddleCenter,
                               Vector2.zero, Vector2.zero, outline ? color : Color.white);
        Stretch(text.rectTransform, 12f);
        return button;
    }

    /// <summary>Latar solid untuk tombol sekunder: warna gelap dicampur sedikit warna aksennya.</summary>
    private static Color SecondaryFill(Color accent) {
        Color fill = Color.Lerp(InputColor, accent, 0.2f);
        fill.a = 1f;
        return fill;
    }

    private static ColorBlock PressTint() {
        ColorBlock colors = ColorBlock.defaultColorBlock;
        colors.highlightedColor = new Color(0.92f, 0.92f, 0.92f);
        colors.pressedColor = new Color(0.72f, 0.72f, 0.72f);
        colors.selectedColor = Color.white;
        colors.disabledColor = new Color(1f, 1f, 1f, 0.45f);
        return colors;
    }

    private static InputField CreateInputField(string name, Transform parent, string placeholder) {
        RectTransform rect = CreateRect(name, parent, Vector2.zero);
        Image image = AddShape(rect.gameObject, InputColor);
        AddOutline(rect, LineColor);
        InputField input = rect.gameObject.AddComponent<InputField>();
        input.targetGraphic = image;
        input.colors = PressTint();

        Text valueText = CreateText(name + " Text", rect, "", BodyFontSize, FontStyle.Normal, TextAnchor.MiddleLeft,
                                    Vector2.zero, Vector2.zero, Color.white);
        Stretch(valueText.rectTransform, 24f);
        valueText.supportRichText = false;

        // Placeholder cukup terang supaya tetap terbaca di atas background lobby.
        Text placeholderText = CreateText(name + " Placeholder", rect, placeholder, BodyFontSize, FontStyle.Normal,
                                          TextAnchor.MiddleLeft, Vector2.zero, Vector2.zero, MutedColor);
        Stretch(placeholderText.rectTransform, 24f);
        input.textComponent = valueText;
        input.placeholder = placeholderText;
        input.characterLimit = name == "Lobby Code Input" ? 16 : 64;
        return input;
    }

    /// <summary>Hapus semua anak. Saat Play, anak dilepas dulu supaya FindChild di frame yang sama tidak menemukannya.</summary>
    private static void ClearChildren(Transform parent) {
        for (int i = parent.childCount - 1; i >= 0; i--) {
            GameObject child = parent.GetChild(i).gameObject;
            if (Application.isPlaying) {
                child.SetActive(false);
                child.transform.SetParent(null, false);
                Destroy(child);
            } else {
                DestroyImmediate(child);
            }
        }
    }

    /// <summary>Baris list selebar parent (dikurangi inset kiri-kanan), ditumpuk dari atas.</summary>
    private static RectTransform CreateListRow(Transform content, string name, int index, float height,
                                               float spacing, float inset) {
        RectTransform row = CreateRect(name, content, Vector2.zero);
        row.anchorMin = new Vector2(0f, 1f);
        row.anchorMax = new Vector2(1f, 1f);
        row.pivot = new Vector2(0.5f, 1f);
        row.sizeDelta = new Vector2(-2f * inset, height);
        row.anchoredPosition = new Vector2(0f, -(inset + index * (height + spacing)));
        return row;
    }

    private static void EnsureEventSystem() {
        if (FindFirstObjectByType<EventSystem>() != null)
            return;

        new GameObject("Lobby EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }

    private void ResolveCanvasReferences() {
        GameObject canvasObject = FindCanvasInScene();
        if (canvasObject == null)
            return;

        Transform canvas = canvasObject.transform;
        Transform panel = FindChild(canvas, "Lobby Panel");
        Transform roomInput = FindChild(canvas, "Lobby Room Input");
        Transform codeInput = FindChild(canvas, "Lobby Code Input");
        Transform statusText = FindChild(canvas, "Lobby Status");
        Transform recentList = FindChild(canvas, "Lobby Recent Rooms");
        Transform recentContent = recentList != null ? FindChild(recentList, "Lobby Recent Rooms Content") : null;
        Transform mainMenu = FindChild(canvas, "Main Menu");
        if (panel == null || roomInput == null || codeInput == null || statusText == null ||
            recentList == null || recentContent == null || mainMenu == null) {
            Debug.LogError("Loby Canvas belum lengkap. Tutup dan buka kembali MainScene agar UI dibuat ulang.");
            return;
        }

        _panel = panel.gameObject;
        _roomInput = roomInput.GetComponent<InputField>();
        _codeInput = codeInput.GetComponent<InputField>();
        _statusText = statusText.GetComponent<Text>();
        _statusDot = FindChild(canvas, "Lobby Status Dot")?.GetComponent<Image>();
        _accountChip = FindChild(canvas, "Lobby Account Chip")?.gameObject;
        _accountName = FindChild(canvas, "Lobby Account Name")?.GetComponent<Text>();
        _accountInitial = FindChild(canvas, "Lobby Account Initial")?.GetComponent<Text>();
        _signOutButton = FindChild(canvas, "Lobby Sign Out Button")?.gameObject;
        _recentList = recentContent;
        _recentScroll = recentList.GetComponent<ScrollRect>();
        _mainMenu = mainMenu.gameObject;
        _mainMenuGreeting = FindChild(canvas, "Main Menu Greeting")?.GetComponent<Text>();
        _settingsOverlay = FindChild(canvas, "Settings Overlay")?.gameObject;
        _exitOverlay = FindChild(canvas, "Exit Confirm Overlay")?.gameObject;
        _sensitivitySlider = FindChild(canvas, "Settings Sensitivity Slider")?.GetComponent<Slider>();
        _sensitivityValue = FindChild(canvas, "Settings Sensitivity Value")?.GetComponent<Text>();
        _settingsAccount = FindChild(canvas, "Settings Account")?.GetComponent<Text>();
        _exitConfirmLabel = FindChild(canvas, "Exit Confirm Button Label")?.GetComponent<Text>();
        if (_sensitivitySlider != null) {
            _sensitivitySlider.onValueChanged.RemoveListener(OnSensitivityChanged);
            _sensitivitySlider.onValueChanged.AddListener(OnSensitivityChanged);
        }
        // Apple melarang tombol keluar aplikasi di iOS; pemain menutup app lewat sistem.
        if (Application.platform == RuntimePlatform.IPhonePlayer)
            FindChild(canvas, "Main Menu Quit Button")?.gameObject.SetActive(false);

        _waitingCanvas = FindWaitingCanvas();
        if (_waitingCanvas != null) {
            Transform waitingRoot = _waitingCanvas.transform;
            _waitingPanel = FindChild(waitingRoot, "Room Waiting Panel")?.gameObject;
            _waitingCodeText = FindChild(waitingRoot, "Waiting Room Code")?.GetComponent<Text>();
            _waitingStatusText = FindChild(waitingRoot, "Waiting Status")?.GetComponent<Text>();
            _playerCountText = FindChild(waitingRoot, "Waiting Player Count")?.GetComponent<Text>();
            _playerListContent = FindChild(waitingRoot, "Waiting Player List Content") as RectTransform;
            _pausePlayerCountText = FindChild(waitingRoot, "Pause Player Count")?.GetComponent<Text>();
            _pausePlayerListContent = FindChild(waitingRoot, "Pause Player List Content") as RectTransform;
            _pausePlayerScroll = FindChild(waitingRoot, "Pause Player Box")?.GetComponent<ScrollRect>();
            _copyButtonLabel = FindChild(waitingRoot, "Waiting Copy Code Button Label")?.GetComponent<Text>();
            _waitingJoinButton = FindChild(waitingRoot, "Waiting Join Game Button")?.GetComponent<Button>();
            _waitingRosterToggleButton = FindChild(waitingRoot, "Waiting Roster Toggle Button")?.GetComponent<Button>();
            _pauseButton = FindChild(waitingRoot, "Pause Button")?.gameObject;
            _pauseOverlay = FindChild(waitingRoot, "Pause Overlay")?.gameObject;
            _pauseMainPanel = FindChild(waitingRoot, "Pause Main Panel")?.gameObject;
            _pausePlayersPanel = FindChild(waitingRoot, "Pause Players Panel")?.gameObject;
            _pauseSettingsPanel = FindChild(waitingRoot, "Pause Settings Panel")?.gameObject;
            _pauseLeaveConfirmPanel = FindChild(waitingRoot, "Pause Leave Confirm Panel")?.gameObject;
            _pauseSensitivitySlider = FindChild(waitingRoot, "Pause Sensitivity Slider")?.GetComponent<Slider>();
            _pauseSensitivityValue = FindChild(waitingRoot, "Pause Sensitivity Value")?.GetComponent<Text>();
            if (_pauseSensitivitySlider != null) {
                _pauseSensitivitySlider.onValueChanged.RemoveListener(OnPauseSensitivityChanged);
                _pauseSensitivitySlider.onValueChanged.AddListener(OnPauseSensitivityChanged);
            }
            AddClickListenerIn(_waitingCanvas, "Waiting Join Game Button", BeginGame);
            AddClickListenerIn(_waitingCanvas, "Waiting Disconnect Button", Disconnect);
            AddClickListenerIn(_waitingCanvas, "Waiting Copy Code Button", CopyQuickmatchCode);
            AddClickListenerIn(_waitingCanvas, "Waiting Close Roster Button", CloseWaitingRoster);
            AddClickListenerIn(_waitingCanvas, "Waiting Roster Toggle Button", ToggleWaitingRoster);
            AddClickListenerIn(_waitingCanvas, "Pause Button", OpenPauseMenu);
            AddClickListenerIn(_waitingCanvas, "Pause Resume Button", ClosePauseMenu);
            AddClickListenerIn(_waitingCanvas, "Pause Players Button", OpenPausePlayers);
            AddClickListenerIn(_waitingCanvas, "Pause Settings Button", OpenPauseSettings);
            AddClickListenerIn(_waitingCanvas, "Pause Players Back Button", ReturnToPauseMenu);
            AddClickListenerIn(_waitingCanvas, "Pause Settings Back Button", ClosePauseSettings);
            AddClickListenerIn(_waitingCanvas, "Pause Leave Room Button", OpenLeaveRoomConfirm);
            AddClickListenerIn(_waitingCanvas, "Pause Leave Cancel Button", ReturnToPauseMenu);
            AddClickListenerIn(_waitingCanvas, "Pause Leave Confirm Button", Disconnect);
        }
    }

    // ---------------------------------------------------------------- Akun (chip + keluar akun)

    private void OnAccountChanged() {
        RefreshAccountChip();
        // Login ulang setelah keluar akun selalu mulai dari menu utama.
        if (!AuthService.IsSignedIn)
            ShowMainMenu();
    }

    private void RefreshAccountChip() {
        bool signedIn = AuthService.IsSignedIn;
        if (_accountChip != null)
            _accountChip.SetActive(signedIn);
        if (_signOutButton != null)
            _signOutButton.SetActive(signedIn);
        if (!signedIn)
            return;

        string username = AuthService.DisplayName;
        if (_accountName != null)
            _accountName.text = username.Length > 14 ? username.Substring(0, 13) + "…" : username;
        if (_accountInitial != null)
            _accountInitial.text = username.Length > 0 ? username.Substring(0, 1).ToUpperInvariant() : "?";
        if (_mainMenuGreeting != null)
            _mainMenuGreeting.text = "Selamat datang, " + username + "!";
        if (_settingsAccount != null)
            _settingsAccount.text = "Masuk sebagai " + username;
    }

    private void SignOutAccount() {
        // Akun tamu hilang permanen saat keluar, jadi minta tap kedua sebagai konfirmasi.
        if (AuthService.IsGuest && _guestSignOutConfirmRoutine == null) {
            _guestSignOutConfirmRoutine = StartCoroutine(ConfirmGuestSignOut());
            return;
        }
        if (_guestSignOutConfirmRoutine != null) {
            StopCoroutine(_guestSignOutConfirmRoutine);
            _guestSignOutConfirmRoutine = null;
            SetSignOutLabel("Keluar akun");
        }

        LoginUIController login = FindFirstObjectByType<LoginUIController>();
        if (login != null)
            login.SignOut();
    }

    private IEnumerator ConfirmGuestSignOut() {
        SetSignOutLabel("Yakin? Tap lagi");
        SetStatus("Kamu masuk sebagai tamu. Keluar akan menghapus progres tamu di perangkat ini.", StatusKind.Error);
        yield return new WaitForSeconds(3f);
        SetSignOutLabel("Keluar akun");
        SetStatus("Siap. Buat room atau bergabung.", StatusKind.Success);
        _guestSignOutConfirmRoutine = null;
    }

    private void SetSignOutLabel(string label) {
        Text text = _signOutButton != null ? _signOutButton.GetComponentInChildren<Text>() : null;
        if (text != null)
            text.text = label;
    }

    private static Transform FindChild(Transform parent, string name) {
        Transform found = parent.Find(name);
        if (found != null)
            return found;
        foreach (Transform child in parent) {
            found = FindChild(child, name);
            if (found != null)
                return found;
        }
        return null;
    }

    private void AddClickListener(string objectName, UnityEngine.Events.UnityAction action) {
        GameObject canvas = FindCanvasInScene();
        if (canvas == null)
            return;
        AddClickListenerIn(canvas, objectName, action);
    }

    private static void AddClickListenerIn(GameObject canvas, string objectName,
                                          UnityEngine.Events.UnityAction action) {
        Transform item = FindChild(canvas.transform, objectName);
        if (item == null)
            return;
        Button button = item.GetComponent<Button>();
        if (button == null)
            return;
        button.onClick.RemoveListener(action);
        button.onClick.AddListener(action);
    }

    private void ConnectByRoomName() {
        string requestedRoom = ReadRoomName();
        if (requestedRoom != null)
            Connect(() => realtime.Connect(requestedRoom), "Menghubungkan ke room…");
    }

    private void CreateRoom() {
        string requestedRoom = ReadRoomName();
        if (requestedRoom != null)
            Connect(() => realtime.Connect(requestedRoom), "Membuat room dan menghubungkan ke server…");
    }

    /// <summary>Nama room dari input, atau null (dengan pesan error) kalau tidak valid.</summary>
    private string ReadRoomName() {
        string requestedRoom = _roomInput != null ? _roomInput.text.Trim() : "";
        if (string.IsNullOrEmpty(requestedRoom)) {
            SetStatus("Nama room harus diisi.", StatusKind.Error);
            return null;
        }
        if (requestedRoom.Length >= 512) {
            SetStatus("Nama room harus kurang dari 512 karakter.", StatusKind.Error);
            return null;
        }
        return requestedRoom;
    }

    private void ConnectQuickmatch() {
        Connect(() => realtime.ConnectToNextAvailableQuickmatchRoom("go-vista", 10), "Mencari atau membuat room…");
    }

    private void ConnectQuickmatchCode() {
        string code = _codeInput != null ? _codeInput.text.Trim() : "";
        if (string.IsNullOrEmpty(code)) {
            SetStatus("Masukkan kode Quickmatch terlebih dahulu.", StatusKind.Error);
            return;
        }
        Connect(() => realtime.ConnectDirectlyToQuickmatchRoom("go-vista", code), "Memeriksa kode dan menghubungkan…");
    }

    private void JoinRecentRoom(string room) {
        if (room.StartsWith("qm:")) {
            string code = room.Substring(3);
            Connect(() => realtime.ConnectDirectlyToQuickmatchRoom("go-vista", code), "Memeriksa kode dan menghubungkan…");
        } else {
            Connect(() => realtime.Connect(room), "Menghubungkan ke room…");
        }
    }

    private void Connect(System.Action connect, string status) {
        if (realtime == null || _busy)
            return;
        _busy = true;
        SetStatus(status, StatusKind.Busy);
        try {
            connect();
        } catch (System.Exception exception) {
            _busy = false;
            SetStatus("Gagal memulai koneksi: " + exception.Message, StatusKind.Error);
            Debug.LogException(exception);
        }
    }

    private void DidConnectToRoom(Realtime connectedRealtime) {
        _busy = false;
        _roomFullDisconnectRequested = false;
        _leaveRoomPending = false;
        ResetInGameMenu();
        string roomCode = connectedRealtime.room != null ? connectedRealtime.room.quickmatchRoomCode : null;
        SetStatus("Terhubung.", StatusKind.Success);
        _waitingRosterSignature = null;
        _pauseRosterSignature = null;
        SpawnPresence(connectedRealtime);
        // Yang dibagikan ke teman: kode Quickmatch kalau ada, kalau tidak nama room.
        if (_waitingCodeText != null)
            _waitingCodeText.text = !string.IsNullOrEmpty(roomCode) ? roomCode : connectedRealtime.roomName;
        SetWaitingStatus(!string.IsNullOrEmpty(roomCode)
            ? "Teman bisa masuk lewat GABUNG DENGAN KODE."
            : "Teman bisa masuk dengan mengetik nama room ini lalu GABUNG.");
        if (_codeInput != null && !string.IsNullOrEmpty(roomCode))
            _codeInput.text = roomCode;
        if (_panel != null)
            _panel.SetActive(false);
        if (_mainMenu != null)
            _mainMenu.SetActive(false);
        SetWaitingJoinButtonJoined(false);
        if (_waitingPanel != null)
            _waitingPanel.SetActive(true);
        if (_waitingRosterToggleButton != null)
            _waitingRosterToggleButton.gameObject.SetActive(false);
        if (_waitingCanvas != null)
            _waitingCanvas.SetActive(true);
        if (_playerListRoutine != null)
            StopCoroutine(_playerListRoutine);
        _playerListRoutine = StartCoroutine(RefreshWaitingPlayerList());
        string recentRoom = !string.IsNullOrEmpty(roomCode)
            ? "qm:" + roomCode
            : connectedRealtime.roomName;
        RememberRoom(recentRoom);
        RefreshRecentRooms();
        PrepareWorldPreview();
    }

    private void DidDisconnectFromRoom(Realtime disconnectedRealtime) {
        _busy = false;
        ResetInGameMenu();
        _leaveRoomPending = false;
        if (ReturnToLobbyIfRequested())
            return;
        if (_worldLoading)
            _unloadWorldWhenReady = true;
        else
            UnloadWorldScene();
        if (_waitingCanvas != null)
            _waitingCanvas.SetActive(false);
        SetWaitingJoinButtonJoined(false);
        if (_waitingPanel != null)
            _waitingPanel.SetActive(true);
        if (_waitingRosterToggleButton != null)
            _waitingRosterToggleButton.gameObject.SetActive(false);
        OpenLobby();
        SetStatus("Terputus dari room.");
    }

    /// <summary>Daftarkan client ini di room supaya muncul di daftar pemain ruang tunggu (lihat LobbyPresence).</summary>
    private static void SpawnPresence(Realtime connectedRealtime) {
        if (LobbyPresence.HasLocal())
            return;
        // Default Normcore: dimiliki client ini dan otomatis dihapus saat client ini keluar.
        Realtime.InstantiateOptions options = Realtime.InstantiateOptions.defaults;
        options.useInstance = connectedRealtime;
        Realtime.Instantiate(LobbyPresence.PrefabName, options);
    }

    private void DidDisconnectFromRoomWithEvent(Realtime disconnectedRealtime, DisconnectEvent disconnectEvent) {
        _busy = false;
        if (ReturnToLobbyIfRequested())
            return;

        // Quickmatch rooms are temporary. Normcore reports this when a saved
        // room name/code no longer resolves, so remove that stale card locally.
        if (disconnectEvent is QuickmatchRoomNotFound roomNotFound) {
            string expiredRoom = !string.IsNullOrEmpty(disconnectEvent.quickmatchRoomCode)
                ? "qm:" + disconnectEvent.quickmatchRoomCode
                : roomNotFound.requestedRoomName;
            ForgetRoom(expiredRoom);
            RefreshRecentRooms();
            SetStatus("Room sudah kedaluwarsa dan dihapus dari daftar.", StatusKind.Error);
            return;
        }

        if (disconnectEvent != null && !string.IsNullOrEmpty(disconnectEvent.message))
            SetStatus("Gagal masuk room: " + disconnectEvent.message, StatusKind.Error);
    }

    // ---------------------------------------------------------------- Menu utama, pengaturan, keluar game

    private void ShowMainMenu() {
        if (_panel != null)
            _panel.SetActive(false);
        if (_mainMenu != null)
            _mainMenu.SetActive(true);
    }

    private void OpenLobby() {
        if (_mainMenu != null)
            _mainMenu.SetActive(false);
        if (_panel != null)
            _panel.SetActive(true);
    }

    private void OpenSettings() {
        // SetValueWithoutNotify supaya membuka panel tidak dianggap mengubah pengaturan.
        if (_sensitivitySlider != null)
            _sensitivitySlider.SetValueWithoutNotify(GameSettings.CameraSensitivity);
        UpdateSettingsLabels();
        if (_settingsOverlay != null)
            _settingsOverlay.SetActive(true);
    }

    private void CloseSettings() {
        GameSettings.Save();
        if (_settingsOverlay != null)
            _settingsOverlay.SetActive(false);
    }

    private void OnSensitivityChanged(float value) {
        GameSettings.CameraSensitivity = value;
        UpdateSettingsLabels();
    }

    private void UpdateSettingsLabels() {
        if (_sensitivityValue != null)
            _sensitivityValue.text = GameSettings.CameraSensitivity.ToString("0.0") + "x";
    }

    private void OpenExitConfirm() {
        if (_exitOverlay != null)
            _exitOverlay.SetActive(true);
    }

    private void CloseExitConfirm() {
        if (!_quitting && _exitOverlay != null)
            _exitOverlay.SetActive(false);
    }

    private async void QuitGame() {
        if (_quitting)
            return;
        _quitting = true;
        if (_exitConfirmLabel != null)
            _exitConfirmLabel.text = "MENYIMPAN…";

        // Simpan progres dulu, tapi jangan menahan pemain lebih dari 3 detik kalau jaringan lambat.
        GameSettings.Save();
        await Task.WhenAny(ProgressService.SaveAsync(), Task.Delay(3000));
#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private IEnumerator RefreshWaitingPlayerList() {
        while (realtime != null && realtime.connected) {
            // Semua client yang terhubung (LobbyPresence), lalu tandai yang sudah punya avatar di world.
            var players = new SortedDictionary<int, bool>();
            foreach (LobbyPresence presence in LobbyPresence.All) {
                if (presence.ClientID >= 0)
                    players[presence.ClientID] = false;
            }
            foreach (HoverbirdPlayer player in FindObjectsByType<HoverbirdPlayer>(FindObjectsSortMode.None)) {
                RealtimeView view = player.GetComponent<RealtimeView>();
                if (view != null && view.ownerIDSelf >= 0)
                    players[view.ownerIDSelf] = true;
            }

            // Quickmatch enforces this capacity on Normcore's server. For a
            // directly named room, eject the newest client if more than ten
            // clients are present, since named-room capacity is not exposed
            // by Normcore's client API.
            if (!_roomFullDisconnectRequested && players.Count > MaxRoomPlayers) {
                int newestClientId = -1;
                foreach (int playerId in players.Keys)
                    newestClientId = playerId;
                if (realtime.clientID == newestClientId) {
                    _roomFullDisconnectRequested = true;
                    SetStatus("Room penuh (maksimal 10 pemain).", StatusKind.Error);
                    realtime.Disconnect();
                    _playerListRoutine = null;
                    yield break;
                }
            }

            RefreshPlayerRows(players);
            yield return new WaitForSeconds(1f);
        }
        _playerListRoutine = null;
    }

    /// <summary>
    /// Gambar ulang daftar pemain (hanya kalau isinya berubah). Host = client yang paling lama di room
    /// (clientID terkecil), jadi kalau pembuat room keluar, host pindah ke pemain berikutnya.
    /// </summary>
    private void RefreshPlayerRows(SortedDictionary<int, bool> players) {
        if (_playerCountText != null)
            _playerCountText.text = players.Count + " / " + MaxRoomPlayers + " pemain";
        if (_pausePlayerCountText != null)
            _pausePlayerCountText.text = players.Count + " / " + MaxRoomPlayers + " pemain";

        var signature = new System.Text.StringBuilder();
        foreach (KeyValuePair<int, bool> player in players)
            signature.Append(player.Key).Append(player.Value ? '+' : '-');
        string currentSignature = signature.ToString();
        if (_waitingRosterSignature != currentSignature) {
            _waitingRosterSignature = currentSignature;
            PopulatePlayerRows(_playerListContent, players);
        }
        if (_pauseRosterSignature != currentSignature) {
            _pauseRosterSignature = currentSignature;
            PopulatePlayerRows(_pausePlayerListContent, players);
        }
    }

    private void PopulatePlayerRows(RectTransform content, SortedDictionary<int, bool> players) {
        if (content == null)
            return;

        ClearChildren(content);
        const float inset = 16f;
        int hostId = -1;
        foreach (int id in players.Keys) {
            hostId = id;
            break;
        }

        int index = 0;
        foreach (KeyValuePair<int, bool> player in players) {
            RectTransform row = CreateListRow(content, "Player Row " + player.Key, index++,
                                              PlayerRowHeight, PlayerRowSpacing, inset);
            bool isLocal = player.Key == realtime.clientID;
            AddShape(row.gameObject, isLocal ? CardColor : new Color(CardColor.r, CardColor.g, CardColor.b, 0.6f));

            RectTransform avatar = PlaceInRow(CreateRect("Avatar", row, Vector2.zero), 20f, 60f, 60f);
            AddShape(avatar.gameObject, QuickmatchColor, "ui-circle");
            Stretch(CreateText("Number", avatar, (player.Key + 1).ToString(), 28, FontStyle.Bold,
                               TextAnchor.MiddleCenter, Vector2.zero, Vector2.zero, Color.white).rectTransform, 0f);
            // TODO (next develop): ganti "Player N" dengan username dari LobbyPresence.
            string playerName = "Player " + (player.Key + 1) + (isLocal ? " (Kamu)" : "");
            PlaceInRow(CreateText("Name", row, playerName, BodyFontSize, FontStyle.Normal, TextAnchor.MiddleLeft,
                                  Vector2.zero, Vector2.zero, Color.white).rectTransform, 104f, 560f, 60f);

            // Status: titik + teks berwarna (hijau = sudah di world, kuning = menunggu).
            bool inWorld = player.Value;
            Color statusColor = inWorld ? SuccessTextColor : WarningColor;
            PlaceInRow(CreateText("Status", row, inWorld ? "Di world" : "Menunggu", SmallFontSize, FontStyle.Normal,
                                  TextAnchor.MiddleLeft, Vector2.zero, Vector2.zero, statusColor).rectTransform,
                       -20f, 130f, 50f);
            AddShape(PlaceInRow(CreateRect("Status Dot", row, Vector2.zero), -158f, 16f, 16f).gameObject,
                     statusColor, "ui-circle");

            if (player.Key == hostId) {
                RectTransform host = PlaceInRow(CreateRect("Host Badge", row, Vector2.zero), -194f, 108f, 42f);
                AddShape(host.gameObject, WarningColor, cornerScale: 1.4f);
                Stretch(CreateText("Label", host, "HOST", 22, FontStyle.Bold, TextAnchor.MiddleCenter,
                                   Vector2.zero, Vector2.zero, new Color(0.15f, 0.12f, 0.05f)).rectTransform, 0f);
            }
        }

        // Slot kosong supaya jelas masih ada tempat (kapasitas MaxRoomPlayers).
        int emptySlots = Mathf.Min(EmptySlotsShown, MaxRoomPlayers - players.Count);
        for (int i = 0; i < emptySlots; i++) {
            RectTransform row = CreateListRow(content, "Empty Slot " + i, index++,
                                              PlayerRowHeight, PlayerRowSpacing, inset);
            AddShape(row.gameObject, new Color(1f, 1f, 1f, 0.18f), "ui-dashed");
            AddShape(PlaceInRow(CreateRect("Avatar", row, Vector2.zero), 20f, 60f, 60f).gameObject,
                     new Color(1f, 1f, 1f, 0.25f), "ui-dashed-circle");
            PlaceInRow(CreateText("Name", row, "Menunggu pemain…", BodyFontSize, FontStyle.Italic,
                                  TextAnchor.MiddleLeft, Vector2.zero, Vector2.zero, MutedColor).rectTransform,
                       104f, 560f, 60f);
        }

        content.sizeDelta = new Vector2(0f, 2f * inset + index * PlayerRowHeight +
                                            Mathf.Max(0, index - 1) * PlayerRowSpacing);
    }

    private void BeginGame() {
        if (_startingGame)
            return;
        HoverbirdPlayerManager playerManager = FindFirstObjectByType<HoverbirdPlayerManager>();
        if (playerManager == null) {
            SetStatus("Hoverbird Player Manager tidak ditemukan.", StatusKind.Error);
            return;
        }

        StartCoroutine(LoadWorldAndBeginGame(playerManager));
    }

    private void PrepareWorldPreview() {
        _unloadWorldWhenReady = false;
        if (_worldLoading)
            return;

        _worldLoading = true;
        SetWaitingJoinButtonLoading(true);
        StartCoroutine(LoadWorldPreview());
    }

    private IEnumerator LoadWorldPreview() {
        while (_worldUnloadOperation != null && !_worldUnloadOperation.isDone)
            yield return null;
        _worldUnloadOperation = null;

        Scene worldScene = SceneManager.GetSceneByName(WorldSceneName);
        if (!worldScene.isLoaded) {
            AsyncOperation load = SceneManager.LoadSceneAsync(WorldSceneName, LoadSceneMode.Additive);
            if (load == null) {
                _worldLoading = false;
                SetWaitingJoinButtonLoading(false);
                SetWaitingStatus("Gagal memuat world pratinjau. Tekan MULAI untuk mencoba lagi.");
                yield break;
            }
            yield return load;
            worldScene = SceneManager.GetSceneByName(WorldSceneName);
        }

        if (!worldScene.IsValid() || !worldScene.isLoaded) {
            _worldLoading = false;
            SetWaitingJoinButtonLoading(false);
            SetWaitingStatus("World pratinjau belum tersedia. Tekan MULAI untuk mencoba lagi.");
            yield break;
        }

        DisableWorldCameras(worldScene);
        if (_unloadWorldWhenReady || realtime == null || !realtime.connected) {
            _unloadWorldWhenReady = false;
            _worldUnloadOperation = SceneManager.UnloadSceneAsync(worldScene);
            if (_worldUnloadOperation != null)
                yield return _worldUnloadOperation;
            _worldUnloadOperation = null;
            _worldLoading = false;
            if (realtime != null && realtime.connected)
                PrepareWorldPreview();
            yield break;
        }

        _worldLoading = false;
        SetWaitingJoinButtonLoading(false);
    }

    private void SetWaitingJoinButtonLoading(bool loading) {
        if (_waitingJoinButton == null)
            return;

        _waitingJoinButton.interactable = !loading;
        Transform label = _waitingJoinButton.transform.Find("Waiting Join Game Button Label");
        Text labelText = label != null ? label.GetComponent<Text>() : null;
        if (labelText != null)
            labelText.text = loading ? "MEMUAT WORLD…" : "MULAI";
    }

    private static void DisableWorldCameras(Scene worldScene) {
        foreach (GameObject root in worldScene.GetRootGameObjects()) {
            foreach (Camera worldCamera in root.GetComponentsInChildren<Camera>(true))
                worldCamera.enabled = false;
            foreach (AudioListener listener in root.GetComponentsInChildren<AudioListener>(true))
                listener.enabled = false;
        }
    }

    private void UnloadWorldScene() {
        if (_worldUnloadOperation != null && !_worldUnloadOperation.isDone)
            return;

        Scene worldScene = SceneManager.GetSceneByName(WorldSceneName);
        if (worldScene.IsValid() && worldScene.isLoaded)
            _worldUnloadOperation = SceneManager.UnloadSceneAsync(worldScene);
    }

    private IEnumerator LoadWorldAndBeginGame(HoverbirdPlayerManager playerManager) {
        _startingGame = true;
        SetStatus("Memuat world…", StatusKind.Busy);

        Scene worldScene = SceneManager.GetSceneByName(WorldSceneName);
        if (!worldScene.isLoaded) {
            AsyncOperation load = SceneManager.LoadSceneAsync(WorldSceneName, LoadSceneMode.Additive);
            if (load == null) {
                _startingGame = false;
                SetStatus("Gagal memuat WorldScene. Pastikan scene ada di Build Settings.", StatusKind.Error);
                yield break;
            }
            yield return load;
            worldScene = SceneManager.GetSceneByName(WorldSceneName);
        }

        if (worldScene.IsValid() && worldScene.isLoaded)
            DisableWorldCameras(worldScene);

        playerManager.BeginGame();
        _startingGame = false;
        _gameplayStarted = true;
        SetWaitingJoinButtonJoined(true);
        if (_waitingPanel != null)
            _waitingPanel.SetActive(false);
        if (_waitingRosterToggleButton != null)
            _waitingRosterToggleButton.gameObject.SetActive(false);
        if (_pauseButton != null)
            _pauseButton.SetActive(true);
        if (_pauseOverlay != null)
            _pauseOverlay.SetActive(false);
        _inGameMenuPage = InGameMenuPage.Closed;
        if (_panel != null)
            _panel.SetActive(false);
    }

    private void ToggleWaitingRoster() {
        bool open = _waitingPanel != null && !_waitingPanel.activeSelf;
        if (_waitingPanel != null)
            _waitingPanel.SetActive(open);
        if (_waitingRosterToggleButton != null)
            _waitingRosterToggleButton.gameObject.SetActive(!open);
    }

    private void CloseWaitingRoster() {
        if (_waitingPanel != null)
            _waitingPanel.SetActive(false);
        if (_waitingRosterToggleButton != null)
            _waitingRosterToggleButton.gameObject.SetActive(true);
    }

    private void OpenPauseMenu() {
        if (!_gameplayStarted || _inGameMenuPage != InGameMenuPage.Closed)
            return;
        if (DialogueUI.Instance != null && DialogueUI.Instance.IsActive)
            return;

        PlayerInputHub input = PlayerInputHub.Instance;
        if (input != null && input.InputLocked)
            return;

        if (input != null)
            input.SetPauseLocked(true);
        HoverbirdMobileControls.SetVisible(false);
        ShowInGameMenuPage(InGameMenuPage.Main);
    }

    private void ClosePauseMenu() {
        if (!_gameplayStarted || _inGameMenuPage != InGameMenuPage.Main)
            return;

        GameSettings.Save();
        ShowInGameMenuPage(InGameMenuPage.Closed);
        if (PlayerInputHub.Instance != null)
            PlayerInputHub.Instance.SetPauseLocked(false);
        HoverbirdMobileControls.SetVisible(true);
    }

    private void OpenPausePlayers() {
        if (_inGameMenuPage != InGameMenuPage.Main)
            return;
        ShowInGameMenuPage(InGameMenuPage.Players);
        if (_pausePlayerScroll != null)
            _pausePlayerScroll.verticalNormalizedPosition = 1f;
    }

    private void OpenPauseSettings() {
        if (_inGameMenuPage != InGameMenuPage.Main)
            return;
        if (_pauseSensitivitySlider != null)
            _pauseSensitivitySlider.SetValueWithoutNotify(GameSettings.CameraSensitivity);
        UpdatePauseSensitivityLabel();
        ShowInGameMenuPage(InGameMenuPage.Settings);
    }

    private void OnPauseSensitivityChanged(float value) {
        GameSettings.CameraSensitivity = value;
        UpdatePauseSensitivityLabel();
    }

    private void UpdatePauseSensitivityLabel() {
        if (_pauseSensitivityValue != null)
            _pauseSensitivityValue.text = GameSettings.CameraSensitivity.ToString("0.0") + "x";
    }

    private void ClosePauseSettings() {
        if (_inGameMenuPage != InGameMenuPage.Settings)
            return;
        GameSettings.Save();
        ReturnToPauseMenu();
    }

    private void OpenLeaveRoomConfirm() {
        if (_inGameMenuPage == InGameMenuPage.Main)
            ShowInGameMenuPage(InGameMenuPage.LeaveConfirm);
    }

    private void ReturnToPauseMenu() {
        if (!_gameplayStarted || _leaveRoomPending || _inGameMenuPage == InGameMenuPage.Closed)
            return;
        if (_inGameMenuPage == InGameMenuPage.Settings)
            GameSettings.Save();
        ShowInGameMenuPage(InGameMenuPage.Main);
    }

    private void ShowInGameMenuPage(InGameMenuPage page) {
        _inGameMenuPage = page;
        if (_pauseOverlay != null)
            _pauseOverlay.SetActive(page != InGameMenuPage.Closed);
        if (_pauseMainPanel != null)
            _pauseMainPanel.SetActive(page == InGameMenuPage.Main);
        if (_pausePlayersPanel != null)
            _pausePlayersPanel.SetActive(page == InGameMenuPage.Players);
        if (_pauseSettingsPanel != null)
            _pauseSettingsPanel.SetActive(page == InGameMenuPage.Settings);
        if (_pauseLeaveConfirmPanel != null)
            _pauseLeaveConfirmPanel.SetActive(page == InGameMenuPage.LeaveConfirm);
    }

    private void ResetInGameMenu() {
        _gameplayStarted = false;
        _inGameMenuPage = InGameMenuPage.Closed;
        _leaveRoomPending = false;
        if (_pauseOverlay != null)
            _pauseOverlay.SetActive(false);
        if (_pauseButton != null)
            _pauseButton.SetActive(false);
        if (_pauseMainPanel != null)
            _pauseMainPanel.SetActive(true);
        if (_pausePlayersPanel != null)
            _pausePlayersPanel.SetActive(false);
        if (_pauseSettingsPanel != null)
            _pauseSettingsPanel.SetActive(false);
        if (_pauseLeaveConfirmPanel != null)
            _pauseLeaveConfirmPanel.SetActive(false);
        Button confirmButton = _pauseLeaveConfirmPanel != null
            ? FindChild(_pauseLeaveConfirmPanel.transform, "Pause Leave Confirm Button")?.GetComponent<Button>()
            : null;
        if (confirmButton != null) {
            confirmButton.interactable = true;
            Text label = confirmButton.GetComponentInChildren<Text>(true);
            if (label != null)
                label.text = "KELUAR ROOM";
        }
        Button cancelButton = _pauseLeaveConfirmPanel != null
            ? FindChild(_pauseLeaveConfirmPanel.transform, "Pause Leave Cancel Button")?.GetComponent<Button>()
            : null;
        if (cancelButton != null)
            cancelButton.interactable = true;
        Text confirmBody = _pauseLeaveConfirmPanel != null
            ? FindChild(_pauseLeaveConfirmPanel.transform, "Pause Leave Confirm Body")?.GetComponent<Text>()
            : null;
        if (confirmBody != null)
            confirmBody.text = "Kamu akan meninggalkan permainan dan kembali ke lobby.";
        if (PlayerInputHub.Instance != null)
            PlayerInputHub.Instance.SetPauseLocked(false);
        HoverbirdMobileControls.SetVisible(false);
    }

    private void SetWaitingJoinButtonJoined(bool joined) {
        if (_waitingJoinButton == null)
            return;

        _waitingJoinButton.interactable = !joined;
        Transform label = _waitingJoinButton.transform.Find("Waiting Join Game Button Label");
        Text labelText = label != null ? label.GetComponent<Text>() : null;
        if (labelText != null)
            labelText.text = joined ? "SEDANG BERMAIN" : "MULAI";
    }

    private void Disconnect() {
        if (_leaveRoomPending)
            return;

        _leaveRoomPending = true;
        _returnToLobbyOnDisconnect = true;
        s_openLobbyOnNextLoad = true;
        GameSettings.Save();
        Button confirmButton = _pauseLeaveConfirmPanel != null
            ? FindChild(_pauseLeaveConfirmPanel.transform, "Pause Leave Confirm Button")?.GetComponent<Button>()
            : null;
        if (confirmButton != null) {
            confirmButton.interactable = false;
            Text label = confirmButton.GetComponentInChildren<Text>(true);
            if (label != null)
                label.text = "KELUAR…";
        }
        Button cancelButton = _pauseLeaveConfirmPanel != null
            ? FindChild(_pauseLeaveConfirmPanel.transform, "Pause Leave Cancel Button")?.GetComponent<Button>()
            : null;
        if (cancelButton != null)
            cancelButton.interactable = false;
        Text confirmBody = _pauseLeaveConfirmPanel != null
            ? FindChild(_pauseLeaveConfirmPanel.transform, "Pause Leave Confirm Body")?.GetComponent<Text>()
            : null;
        if (confirmBody != null)
            confirmBody.text = "Sedang meninggalkan room…";
        SetWaitingStatus("Meninggalkan room…");
        if (realtime != null && realtime.connected)
            realtime.Disconnect();
        else
            SceneManager.LoadScene("LobbyReturnScene", LoadSceneMode.Single);
    }

    private bool ReturnToLobbyIfRequested() {
        if (!_returnToLobbyOnDisconnect)
            return false;
        _returnToLobbyOnDisconnect = false;
        SceneManager.LoadScene("LobbyReturnScene", LoadSceneMode.Single);
        return true;
    }

    private void CopyQuickmatchCode() {
        if (realtime == null || !realtime.connected) {
            SetWaitingStatus("Belum terhubung ke room.");
            return;
        }
        string code = realtime.room != null ? realtime.room.quickmatchRoomCode : null;
        bool hasCode = !string.IsNullOrEmpty(code);
        GUIUtility.systemCopyBuffer = hasCode ? code : realtime.roomName;
        SetWaitingStatus(hasCode ? "Kode room disalin. Kirim ke temanmu." : "Nama room disalin. Kirim ke temanmu.");
        if (_copyFeedbackRoutine != null)
            StopCoroutine(_copyFeedbackRoutine);
        _copyFeedbackRoutine = StartCoroutine(ShowCopiedFeedback());
    }

    private IEnumerator ShowCopiedFeedback() {
        if (_copyButtonLabel != null)
            _copyButtonLabel.text = "TERSALIN";
        yield return new WaitForSeconds(2f);
        if (_copyButtonLabel != null)
            _copyButtonLabel.text = "SALIN";
        _copyFeedbackRoutine = null;
    }

    /// <summary>Status bar lobby: titik + teks berubah warna sesuai kondisi.</summary>
    private void SetStatus(string status, StatusKind kind = StatusKind.Info) {
        Color dotColor = kind == StatusKind.Success ? SuccessTextColor
                       : kind == StatusKind.Busy ? WarningColor
                       : kind == StatusKind.Error ? DangerTextColor
                       : MutedColor;
        if (_statusText != null) {
            _statusText.text = status;
            _statusText.color = kind == StatusKind.Error ? DangerTextColor : Color.white;
        }
        if (_statusDot != null)
            _statusDot.color = dotColor;
    }

    private void SetWaitingStatus(string status) {
        if (_waitingStatusText != null)
            _waitingStatusText.text = status;
    }

    /// <summary>Kartu room terakhir: ketuk kartu = langsung gabung, × = hapus dari daftar.</summary>
    private void RefreshRecentRooms() {
        if (_recentList == null || _recentScroll == null)
            return;

        ClearChildren(_recentList);
        List<string> rooms = GetRecentRooms();
        int visibleCount = Mathf.Min(rooms.Count, MaxRecentRooms);
        var contentRect = (RectTransform)_recentList;
        contentRect.sizeDelta = new Vector2(0f, visibleCount * RecentRowHeight +
                                                Mathf.Max(0, visibleCount - 1) * RecentRowSpacing);

        if (visibleCount == 0) {
            RectTransform empty = CreateListRow(_recentList, "Recent Rooms Empty", 0, RecentRowHeight, 0f, 0f);
            AddShape(empty.gameObject, new Color(1f, 1f, 1f, 0.18f), "ui-dashed");
            Stretch(CreateText("Label", empty, "Belum ada. Room yang kamu masuki muncul di sini.", SmallFontSize,
                               FontStyle.Normal, TextAnchor.MiddleLeft, Vector2.zero, Vector2.zero, MutedColor)
                        .rectTransform, 28f);
            contentRect.sizeDelta = new Vector2(0f, RecentRowHeight);
        }

        for (int i = 0; i < visibleCount; i++) {
            string room = rooms[i];
            bool isQuickmatch = room.StartsWith("qm:");
            string label = isQuickmatch ? "Kode " + room.Substring(3) : room;

            RectTransform card = CreateListRow(_recentList, "Recent Room " + i, i, RecentRowHeight, RecentRowSpacing, 0f);
            Image background = AddShape(card.gameObject, CardColor);
            Button join = card.gameObject.AddComponent<Button>();
            join.targetGraphic = background;
            join.colors = PressTint();
            join.onClick.AddListener(() => JoinRecentRoom(room));

            PlaceInRow(CreateText("Name", card, label, BodyFontSize, FontStyle.Bold, TextAnchor.MiddleLeft,
                                  Vector2.zero, Vector2.zero, Color.white).rectTransform, 28f, 360f, 60f);
            PlaceInRow(CreateText("Join Hint", card, "Gabung ›", SmallFontSize, FontStyle.Normal, TextAnchor.MiddleRight,
                                  Vector2.zero, Vector2.zero, LinkColor).rectTransform, -84f, 130f, 60f);
            Button remove = CreateButton("Remove", card, "×", InputColor, 40);
            remove.GetComponentInChildren<Text>().color = MutedColor;
            PlaceInRow(remove.GetComponent<RectTransform>(), -15f, 64f, 64f);
            remove.onClick.AddListener(() => {
                ForgetRoom(room);
                RefreshRecentRooms();
                SetStatus("Room dihapus dari daftar.");
            });
        }
        _recentScroll.verticalNormalizedPosition = 1f;
    }

    private static List<string> GetRecentRooms() {
        string saved = PlayerPrefs.GetString(RecentRoomsKey, "");
        var rooms = new List<string>();
        foreach (string room in saved.Split('|')) {
            string trimmed = room.Trim();
            if (!string.IsNullOrEmpty(trimmed) && !rooms.Contains(trimmed))
                rooms.Add(trimmed);
        }
        return rooms;
    }

    private static void RememberRoom(string room) {
        if (string.IsNullOrEmpty(room))
            return;
        List<string> rooms = GetRecentRooms();
        rooms.Remove(room);
        rooms.Insert(0, room);
        if (rooms.Count > MaxRecentRooms)
            rooms.RemoveRange(MaxRecentRooms, rooms.Count - MaxRecentRooms);
        PlayerPrefs.SetString(RecentRoomsKey, string.Join("|", rooms));
        PlayerPrefs.Save();
    }

    private static void ForgetRoom(string room) {
        if (string.IsNullOrEmpty(room))
            return;

        List<string> rooms = GetRecentRooms();
        if (!rooms.Remove(room))
            return;

        PlayerPrefs.SetString(RecentRoomsKey, string.Join("|", rooms));
        PlayerPrefs.Save();
    }
}

#if UNITY_EDITOR
[InitializeOnLoad]
internal static class LobyUISceneCanvasBootstrap {
    private const string RoomWaitingScenePath = "Assets/Scenes/Multiplayer/RoomWaitingScene.unity";
    private static bool _isEnsuringRoomWaitingAssets;

    static LobyUISceneCanvasBootstrap() {
        EditorSceneManager.sceneOpened += OnSceneOpened;
        EditorSceneManager.sceneSaved += OnSceneSaved;
        EditorApplication.delayCall += BuildForOpenScenes;
    }

    private static void OnSceneSaved(UnityEngine.SceneManagement.Scene scene) {
        if (_isEnsuringRoomWaitingAssets || EditorApplication.isPlayingOrWillChangePlaymode ||
            scene.path != RoomWaitingScenePath)
            return;

        GameObject canvas = null;
        foreach (GameObject root in scene.GetRootGameObjects()) {
            if (root.name == "Room Waiting Canvas") {
                canvas = root;
                break;
            }
        }
        if (canvas == null || canvas.GetComponent<Canvas>() == null)
            return;

        bool canvasWasActive = canvas.activeSelf;
        if (canvasWasActive)
            canvas.SetActive(false);
        PrefabUtility.SaveAsPrefabAsset(canvas, "Assets/Prefab/RoomWaitingCanvas.prefab");
        if (canvasWasActive)
            canvas.SetActive(true);
    }

    private static void EnsureRoomWaitingSceneAndPrefab() {
        if (_isEnsuringRoomWaitingAssets || EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        _isEnsuringRoomWaitingAssets = true;
        try {
            EnsureRoomWaitingSceneAndPrefabInternal();
        } finally {
            _isEnsuringRoomWaitingAssets = false;
        }
    }

    private static void EnsureRoomWaitingSceneAndPrefabInternal() {

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/RoomWaitingCanvas.prefab");
        if (prefab == null)
            return;

        UnityEngine.SceneManagement.Scene originalActiveScene =
            UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        bool openedHere = false;
        UnityEngine.SceneManagement.Scene scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(RoomWaitingScenePath);
        if (!scene.IsValid() || !scene.isLoaded) {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(RoomWaitingScenePath) != null) {
                scene = EditorSceneManager.OpenScene(RoomWaitingScenePath, OpenSceneMode.Additive);
                openedHere = true;
            } else {
                scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                openedHere = true;
            }
        }

        if (!scene.IsValid() || !scene.isLoaded)
            return;

        bool sceneChanged = openedHere;
        GameObject canvas = null;
        foreach (GameObject root in scene.GetRootGameObjects()) {
            if (root.name == "Room Waiting Canvas") {
                canvas = root;
                break;
            }
        }

        if (canvas == null) {
            canvas = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
            if (canvas != null)
                canvas.SetActive(true);
            sceneChanged = true;
        }
        if (canvas == null)
            return;

        if (canvas.name != "Room Waiting Canvas") {
            canvas.name = "Room Waiting Canvas";
            sceneChanged = true;
        }
        if (PrefabUtility.IsPartOfPrefabInstance(canvas)) {
            PrefabUtility.UnpackPrefabInstance(canvas, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            sceneChanged = true;
        }

        if (canvas.GetComponent<Canvas>() == null) {
            if (openedHere)
                EditorSceneManager.CloseScene(scene, true);
            return;
        }

        if (LobyUISceneController.EnsureWaitingCanvasControls(canvas.transform))
            sceneChanged = true;

        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene() != scene)
            UnityEngine.SceneManagement.SceneManager.SetActiveScene(scene);
        bool sceneHasEventSystem = false;
        foreach (GameObject root in scene.GetRootGameObjects()) {
            if (root.GetComponentInChildren<EventSystem>(true) != null) {
                sceneHasEventSystem = true;
                break;
            }
        }
        if (!sceneHasEventSystem) {
            new GameObject("Room Waiting EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            sceneChanged = true;
        }

        bool canvasWasActive = canvas.activeSelf;
        if (canvasWasActive)
            canvas.SetActive(false);
        PrefabUtility.SaveAsPrefabAsset(canvas, "Assets/Prefab/RoomWaitingCanvas.prefab");
        if (canvasWasActive) {
            canvas.SetActive(true);
            sceneChanged = true;
        }
        if (sceneChanged) {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, RoomWaitingScenePath);
        }

        if (openedHere)
            EditorSceneManager.CloseScene(scene, true);
        if (originalActiveScene.IsValid() && originalActiveScene.isLoaded)
            UnityEngine.SceneManagement.SceneManager.SetActiveScene(originalActiveScene);
    }

    private static void OnSceneOpened(UnityEngine.SceneManagement.Scene scene, OpenSceneMode mode) {
        EditorApplication.delayCall += () => BuildForScene(scene);
    }

    private static void BuildForOpenScenes() {
        EnsureRoomWaitingSceneAndPrefab();
        LobyUISceneController.UpgradeCanvasPrefabs();
        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            BuildForScene(UnityEngine.SceneManagement.SceneManager.GetSceneAt(i));
    }

    private static void BuildForScene(UnityEngine.SceneManagement.Scene scene) {
        if (!scene.IsValid() || !scene.isLoaded || (scene.name != "MainScene" && scene.name != "LobyUIScene"))
            return;

        foreach (GameObject root in scene.GetRootGameObjects()) {
            LobyUISceneController controller = root.GetComponentInChildren<LobyUISceneController>(true);
            if (controller == null)
                continue;

            controller.EnsureCanvasInEditor();
            return;
        }
    }
}
#endif
