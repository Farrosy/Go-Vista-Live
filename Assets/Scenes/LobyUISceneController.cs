using System.Collections.Generic;
using System.Collections;
using System.Threading.Tasks;
using Normal.Realtime;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

/// <summary>
/// Normcore lobby UI. UI-nya ada di dua prefab yang bisa diedit bebas:
///  - Assets/Prefab/LobbyCanvas.prefab       : edit di Assets/Scenes/Multiplayer/LobyUIScene.unity
///  - Assets/Prefab/RoomWaitingCanvas.prefab : edit di Assets/Scenes/Multiplayer/RoomWaitingScene.unity
/// Simpan scene edit (Ctrl+S) = perubahan di-Apply ke prefab (LobbyUIEditScenes), atau edit langsung di
/// Prefab Mode. Kode TIDAK membangun ulang canvas yang sudah ada; Build*Contents di bawah hanya dipakai
/// kalau canvas tidak ada sama sekali, atau lewat menu "Go Vista > Lobby UI > Reset ... ke default".
/// Kode mencari objek UI lewat nama (FindChild), jadi jangan ganti nama objek yang dipakai di sini.
/// Normcore doesn't expose an API to enumerate arbitrary named rooms, so the
/// list here contains local room history plus manual and Quickmatch joins.
/// </summary>
[ExecuteAlways]
public sealed partial class LobyUISceneController : MonoBehaviour {
    [SerializeField] private Realtime realtime;

    private const string CanvasRootName = "Loby Canvas";
    private const string WaitingCanvasRootName = "Room Waiting Canvas";
    private const string WaitingCanvasPrefabRootName = "RoomWaitingCanvas";
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
    // Penanda versi layout default yang dibuat kode (hanya informasi, tidak memicu rebuild lagi).
    private const string LobbyLayoutVersion = "Layout v11"; // v11: CARI ROOM CEPAT / BUAT ROOM publik / kode / room privat
    private const string WaitingCanvasLayoutVersion = "Layout v14"; // v14: kode room + SALIN di panel PEMAIN (pause)
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
    private Text _mainMenuName;
    private Text _mainMenuInitial;
    private GameObject _settingsOverlay;
    private Canvas _lobbyCanvas;
    private bool _lobbyControlLayoutOpen;
    private GameObject _exitOverlay;
    private SettingsPanel _settingsPanel;
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
    private Text _pauseCodeText;
    private Text _pauseCopyButtonLabel;
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
    private SettingsPanel _pauseSettingsPanel;
    private GameObject _pauseLeaveConfirmPanel;
    private InGameMenuPage _inGameMenuPage;
    private Text _copyButtonLabel;
    private Coroutine _copyFeedbackRoutine;
    private Coroutine _pauseCopyFeedbackRoutine;
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

    private enum InGameMenuPage { Closed, Main, Players, Settings, LeaveConfirm, ControlLayout }

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
        ProgressService.Loaded -= RefreshAccountChip;
        ProgressService.PlayerNameChanged -= OnPlayerNameChanged;
        if (!Application.isPlaying || realtime == null)
            return;

        LoginUIController.AccountChanged -= OnAccountChanged;
        realtime.didConnectToRoom -= DidConnectToRoom;
        realtime.didDisconnectFromRoom -= DidDisconnectFromRoom;
        realtime.didDisconnectFromRoomWithEvent -= DidDisconnectFromRoomWithEvent;
    }

    private void Update() {
        // Tombol Back HP saat ATUR KONTROL dibuka dari Pengaturan lobby.
        if (Application.isPlaying && _lobbyControlLayoutOpen && Input.GetKeyDown(KeyCode.Escape)) {
            FindControlLayoutEditor()?.Cancel();
            return;
        }
        // Tombol Back HP saat PENGATURAN lobby terbuka = SELESAI.
        if (Application.isPlaying && !_gameplayStarted && _settingsPanel != null && _settingsPanel.IsOpen &&
            Input.GetKeyDown(KeyCode.Escape)) {
            _settingsPanel.Close();
            return;
        }
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
            case InGameMenuPage.Settings:
                // Sama dengan SELESAI: simpan, lalu callback kembali ke menu pause.
                if (_pauseSettingsPanel != null)
                    _pauseSettingsPanel.Close();
                else
                    ReturnToPauseMenu();
                break;
            case InGameMenuPage.Players:
            case InGameMenuPage.LeaveConfirm:
                ReturnToPauseMenu();
                break;
            case InGameMenuPage.ControlLayout:
                FindControlLayoutEditor()?.Cancel();
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
        ResolveModal();
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

        AddClickListener("Lobby Quickmatch Button", SearchQuickRoom);
        AddClickListener("Lobby Create Button", CreatePublicRoom);
        AddClickListener("Lobby Join Button", ConnectByRoomName);
        AddClickListener("Lobby Join Code Button", ConnectQuickmatchCode);
        AddClickListener("Lobby Close Button", ShowMainMenu);
        AddClickListener("Lobby Sign Out Button", SignOutAccount);
        AddClickListener("Main Menu Play Button", OpenLobby);
        AddClickListener("Main Menu Character Button", OpenCharacterSelect);
        AddClickListener("Main Menu Settings Button", OpenSettings);
        AddClickListener("Main Menu Quit Button", OpenExitConfirm);
        AddClickListener("Exit Cancel Button", CloseExitConfirm);
        AddClickListener("Exit Confirm Button", QuitGame);
        LoginUIController.AccountChanged -= OnAccountChanged;
        LoginUIController.AccountChanged += OnAccountChanged;
        // Sapaan menu utama memakai nama pemain dari progres: segarkan saat progres ter-load / nama diganti.
        ProgressService.Loaded += RefreshAccountChip;
        ProgressService.PlayerNameChanged += OnPlayerNameChanged;
        RefreshAccountChip();

        SetStatus(ReadyStatusMessage, StatusKind.Success);
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

    // Scene edit LobbyCanvas. RoomWaitingCanvas diedit terpisah di RoomWaitingScene.
    private static bool IsLobbyScene(UnityEngine.SceneManagement.Scene scene) {
        return scene.IsValid() && scene.name == "LobyUIScene";
    }

    /// <summary>
    /// Pastikan LobyUIScene punya instance prefab LobbyCanvas (kalau terhapus). Canvas yang sudah ada
    /// tidak pernah dibangun ulang, dan scene tidak disimpan otomatis.
    /// </summary>
    internal void EnsureCanvasInEditor() {
        if (this == null || Application.isPlaying || !IsLobbyScene(gameObject.scene) || FindCanvasInScene() != null)
            return;
        GameObject prefab = LoadCanvasPrefab(LobbyCanvasPrefabPath);
        if (prefab == null)
            return;
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, gameObject.scene);
        instance.name = CanvasRootName;
        EnsureEventSystem();
        EditorSceneManager.MarkSceneDirty(gameObject.scene);
        Debug.Log("[Lobby UI] LobbyCanvas ditambahkan ke LobyUIScene, silakan Ctrl+S.");
    }

    private static GameObject LoadCanvasPrefab(string path) {
        return AssetDatabase.LoadAssetAtPath<GameObject>(path);
    }

    /// <summary>
    /// Bangun ulang isi prefab dari kode (layout default). Semua editan manual di prefab itu hilang.
    /// Hanya lewat menu "Go Vista > Lobby UI > Reset ... ke default".
    /// </summary>
    public static void ResetCanvasPrefabToDefault(bool waitingCanvas) {
        string path = waitingCanvas ? WaitingCanvasPrefabPath : LobbyCanvasPrefabPath;
        if (LoadCanvasPrefab(path) == null)
            return;
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try {
            RebuildInPlace(root.transform, waitingCanvas
                ? (System.Action<Transform>)BuildWaitingCanvasContents
                : BuildCanvasContents);
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Debug.Log($"[Lobby UI] {path} di-reset ke layout default.");
        } finally {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
#endif

    // Canvas di scene (instance prefab) dipakai apa adanya. Layout default dari kode hanya dibuat
    // kalau scene sama sekali tidak punya canvas, supaya editan di prefab tidak pernah tertimpa.
    private void EnsureCanvasExists() {
        if (FindCanvasInScene() == null) {
            Canvas canvas = CreateCanvas();
            BuildCanvasContents(canvas.transform);
            EnsureEventSystem();
        } else if (!HasCompleteCanvas()) {
            Debug.LogWarning("[Lobby UI] LobbyCanvas tidak lengkap: objek \"Lobby Panel\", \"Lobby Room Input\", " +
                             "\"Lobby Code Input\", atau \"Lobby Status\" tidak ditemukan. Jangan ganti nama objek " +
                             "yang dicari kode di LobyUISceneController.");
        }
        EnsureWaitingCanvasExists();
    }

    private void EnsureWaitingCanvasExists() {
        if (FindWaitingCanvas() != null)
            return;
        Canvas canvas = CreateCanvas(WaitingCanvasRootName, 51);
        BuildWaitingCanvasContents(canvas.transform);
        canvas.gameObject.SetActive(false);
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
        // Kode/nama room supaya bisa mengajak teman walau sudah di dalam world.
        Place(CreateText("Pause Share Label", players, "BAGIKAN KE TEMAN", LabelFontSize, FontStyle.Bold,
                         TextAnchor.MiddleLeft, Vector2.zero, Vector2.zero, MutedColor).rectTransform, 52f, 134f, 800f, 34f);
        RectTransform pauseCodeBox = Place(CreateRect("Pause Room Code Box", players, Vector2.zero), 52f, 176f, 795f, 88f);
        AddShape(pauseCodeBox.gameObject, InputColor);
        AddOutline(pauseCodeBox, new Color(1f, 1f, 1f, 0.25f), "ui-dashed");
        Stretch(CreateText("Pause Room Code", pauseCodeBox, "-", 40, FontStyle.Bold, TextAnchor.MiddleLeft,
                           Vector2.zero, Vector2.zero, SuccessTextColor).rectTransform, 28f);
        Place(CreateButton("Pause Copy Code Button", players, "SALIN", SuccessColor).GetComponent<RectTransform>(),
              869f, 176f, 279f, 88f);
        RectTransform list = Place(CreateRect("Pause Player Box", players, Vector2.zero),
                                   52f, 288f, 1096f, 504f);
        AddShape(list.gameObject, ListColor);
        list.gameObject.AddComponent<RectMask2D>();
        ScrollRect scroll = list.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 28f;
        scroll.viewport = list;
        RectTransform content = CreateRect("Pause Player List Content", list, new Vector2(0f, 504f));
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        scroll.content = content;
        Place(CreateButton("Pause Players Back Button", players, "KEMBALI", PrimaryColor)
                  .GetComponent<RectTransform>(), 400f, 814f, 400f, 88f);
        players.gameObject.SetActive(false);

        // Dialog PENGATURAN yang sama dengan lobby (SettingsPanel.prefab).
        InstantiateSettingsPanel(pauseSafeArea, "Pause Settings Panel");

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

    // Layout mengikuti desain "Go Vista Lobby": kiri = cari room cepat / buat room / kode / room privat,
    // kanan = room terakhir,
    // bawah = status + keluar akun. Koordinat = jarak dari kiri/atas panel (lihat Place).
    private static void BuildCanvasContents(Transform canvas) {
        RectTransform safeArea = CreateSafeArea("Lobby Safe Area", canvas);
        RectTransform panel = CreateResponsivePanel("Lobby Panel", safeArea, new Vector2(1680f, 960f));
        AddShape(panel.gameObject, PanelColor, cornerScale: 1.8f);
        AddOutline(panel, LineColor, cornerScale: 1.8f);

        Place(CreateText("Lobby Title", panel, "GO VISTA LOBBY", 56, FontStyle.Bold, TextAnchor.MiddleLeft,
                         Vector2.zero, Vector2.zero, Color.white).rectTransform, 53f, 38f, 1000f, 72f);
        Place(CreateText("Lobby Subtitle", panel,
                         "Cari room cepat, buat room, atau masuk dengan kode / nama room privat.",
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

        // Kolom kiri: CARI ROOM CEPAT (hanya mencari) = aksi utama, lalu BUAT ROOM publik,
        // lalu cara manual: kode room dan room privat (nama). Lihat LobyUISceneController.Quickmatch.cs.
        Place(CreateButton("Lobby Quickmatch Button", panel, "CARI ROOM CEPAT", QuickmatchColor, 34)
                  .GetComponent<RectTransform>(), 53f, 178f, 910f, 110f);
        Place(CreateButton("Lobby Create Button", panel, "BUAT ROOM", PrimaryColor).GetComponent<RectTransform>(),
              53f, 308f, 910f, 84f);
        AddLine(panel, "Lobby Divider Left", 53f, 425f, 390f);
        Place(CreateText("Lobby Divider Text", panel, "ATAU", SmallFontSize, FontStyle.Normal, TextAnchor.MiddleCenter,
                         Vector2.zero, Vector2.zero, MutedColor).rectTransform, 443f, 408f, 130f, 36f);
        AddLine(panel, "Lobby Divider Right", 573f, 425f, 390f);
        Place(CreateText("Code Room Label", panel, "KODE ROOM", LabelFontSize, FontStyle.Bold, TextAnchor.MiddleLeft,
                         Vector2.zero, Vector2.zero, MutedColor).rectTransform, 53f, 452f, 910f, 32f);
        Place(CreateInputField("Lobby Code Input", panel, "Masukkan kode room").GetComponent<RectTransform>(),
              53f, 490f, 578f, 84f);
        Place(CreateButton("Lobby Join Code Button", panel, "GABUNG DENGAN KODE", SuccessTextColor, SmallFontSize, true)
                  .GetComponent<RectTransform>(), 656f, 490f, 307f, 84f);
        Place(CreateText("Room Name Label", panel, "ROOM PRIVAT", LabelFontSize, FontStyle.Bold, TextAnchor.MiddleLeft,
                         Vector2.zero, Vector2.zero, MutedColor).rectTransform, 53f, 600f, 910f, 32f);
        Place(CreateInputField("Lobby Room Input", panel, "Nama room privat").GetComponent<RectTransform>(),
              53f, 638f, 578f, 84f);
        Place(CreateButton("Lobby Join Button", panel, "MASUK", PrimaryColor).GetComponent<RectTransform>(),
              656f, 638f, 307f, 84f);

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
        Place(CreateText("Global Rooms Note", panel, "Room publik bisa ditemukan lewat CARI ROOM CEPAT.", SmallFontSize,
                         FontStyle.Normal, TextAnchor.MiddleLeft, Vector2.zero, Vector2.zero, MutedColor).rectTransform,
              1017f, 556f, 606f, 40f);

        // Footer: status bar (titik berwarna sesuai kondisi) + keluar akun.
        AddLine(panel, "Lobby Footer Line", 53f, 797f, 1574f);
        RectTransform dot = Place(CreateRect("Lobby Status Dot", panel, Vector2.zero), 53f, 858f, 20f, 20f);
        AddShape(dot.gameObject, SuccessTextColor, "ui-circle");
        Place(CreateText("Lobby Status", panel, ReadyStatusMessage, 28, FontStyle.Normal,
                         TextAnchor.MiddleLeft, Vector2.zero, Vector2.zero, Color.white).rectTransform,
              90f, 832f, 1130f, 72f);
        Place(CreateButton("Lobby Sign Out Button", panel, "Keluar akun", DangerTextColor, LabelFontSize, true)
                  .GetComponent<RectTransform>(), 1363f, 832f, 260f, 72f);

        BuildMainMenu(safeArea);
        BuildSettingsOverlay(canvas);
        BuildExitConfirmOverlay(canvas);
        AddLayoutMarker(canvas, LobbyLayoutVersion);
    }

    // Warna menu utama (desain v10). Tombol pakai gradasi atas->bawah (UIVerticalGradient) + "lip" gelap di bawah.
    private static readonly Color MenuCardColor = new Color32(13, 18, 34, 245);
    private static readonly Color PlayTopColor = new Color32(79, 140, 255, 255);
    private static readonly Color PlayBottomColor = new Color32(123, 77, 255, 255);
    private static readonly Color PlayLipColor = new Color32(59, 31, 143, 255);
    private static readonly Color PlayIconColor = new Color32(91, 108, 255, 255);
    private static readonly Color TileTopColor = new Color32(37, 48, 79, 255);
    private static readonly Color TileBottomColor = new Color32(26, 35, 64, 255);
    private static readonly Color TileLipColor = new Color32(10, 15, 30, 255);
    private static readonly Color TileSubColor = new Color32(142, 155, 187, 255);
    private static readonly Color PinkTopColor = new Color32(244, 114, 182, 255);
    private static readonly Color PinkBottomColor = new Color32(219, 39, 119, 255);
    private static readonly Color CyanTopColor = new Color32(34, 211, 238, 255);
    private static readonly Color CyanBottomColor = new Color32(8, 145, 178, 255);
    private static readonly Color AvatarBottomColor = new Color32(139, 92, 246, 255);
    private static readonly Color QuitFaceColor = new Color32(42, 21, 32, 255);
    private static readonly Color QuitLipColor = new Color32(30, 11, 18, 255);
    private static readonly Color QuitTextColor = new Color32(255, 122, 140, 255);
    private static readonly Color QuitBorderColor = new Color32(255, 107, 129, 115);
    private const float MenuLipDepth = 8f;

    /// <summary>
    /// Menu utama setelah login: kartu di kanan (kiri dibiarkan untuk logo dan karakter di background).
    /// Atas: avatar + "Selamat datang, nama". Lalu tombol besar MAIN, dua tile KARAKTER / PENGATURAN,
    /// dan KELUAR GAME yang sengaja dibuat paling kalem supaya tidak bersaing dengan MAIN.
    /// MAIN membuka panel lobby, KARAKTER membuka CharacterSelectCanvas (prefab terpisah),
    /// PENGATURAN membuka overlay pengaturan, KELUAR GAME minta konfirmasi dulu.
    /// Nama objek tombol tetap sama ("Main Menu ... Button"), jadi listener di InitializeRuntime tidak berubah.
    /// </summary>
    private static void BuildMainMenu(RectTransform safeArea) {
        RectTransform menu = Stretch(CreateRect("Main Menu", safeArea, Vector2.zero), 0f);
        RectTransform card = CreateRect("Main Menu Card", menu, new Vector2(640f, 668f));
        card.anchorMin = card.anchorMax = card.pivot = new Vector2(1f, 0.5f);
        card.anchoredPosition = new Vector2(-100f, -40f);
        AddShape(card.gameObject, MenuCardColor, cornerScale: 1.8f);
        AddOutline(card, LineColor, cornerScale: 1.8f);

        // Header: avatar inisial + sapaan. Teks diisi RefreshAccountChip (Main Menu Initial / Name):
        // nama pemain dari menu KARAKTER, atau username kalau belum diisi.
        RectTransform ring = Place(CreateRect("Main Menu Avatar Ring", card, Vector2.zero), 37f, 37f, 86f, 86f);
        AddShape(ring.gameObject, new Color(1f, 1f, 1f, 0.22f), "ui-circle").raycastTarget = false;
        RectTransform avatar = Place(CreateRect("Main Menu Avatar", card, Vector2.zero), 40f, 40f, 80f, 80f);
        AddGradientShape(avatar.gameObject, PinkTopColor, AvatarBottomColor, "ui-circle").raycastTarget = false;
        Stretch(CreateText("Main Menu Initial", avatar, "?", 38, FontStyle.Bold, TextAnchor.MiddleCenter,
                           Vector2.zero, Vector2.zero, Color.white).rectTransform, 0f);
        Place(CreateText("Main Menu Greeting", card, "Selamat datang,", 24, FontStyle.Normal,
                         TextAnchor.MiddleLeft, Vector2.zero, Vector2.zero, MutedColor).rectTransform,
              142f, 44f, 420f, 32f);
        Place(CreateText("Main Menu Name", card, "Pemain", 40, FontStyle.Bold,
                         TextAnchor.MiddleLeft, Vector2.zero, Vector2.zero, Color.white).rectTransform,
              142f, 76f, 420f, 48f);

        // MAIN: tombol utama, gradasi biru -> ungu, ikon play di lingkaran putih.
        Button play = CreateMenuButton("Main Menu Play Button", card, PlayTopColor, PlayBottomColor, PlayLipColor,
                                       new Color(1f, 1f, 1f, 0.18f), 1.2f, out RectTransform playFace);
        Place((RectTransform)play.transform, 40f, 152f, 560f, 156f);
        RectTransform badge = Place(CreateRect("Main Menu Play Badge", playFace, Vector2.zero), 32f, 28f, 92f, 92f);
        AddShape(badge.gameObject, Color.white, "ui-circle").raycastTarget = false;
        AddIcon("Main Menu Play Icon", badge, "icon-play", PlayIconColor, 26f, 22f, 48f);
        Place(CreateText("Main Menu Play Button Label", playFace, "MAIN", 56, FontStyle.Bold, TextAnchor.MiddleLeft,
                         Vector2.zero, Vector2.zero, Color.white).rectTransform, 152f, 26f, 320f, 64f);
        Place(CreateText("Main Menu Play Subtitle", playFace, "Jelajahi kota Go Vista", 24, FontStyle.Normal,
                         TextAnchor.MiddleLeft, Vector2.zero, Vector2.zero, new Color32(223, 227, 255, 255))
                  .rectTransform, 152f, 90f, 320f, 32f);
        AddIcon("Main Menu Play Chevron", playFace, "icon-chevron-right", new Color(1f, 1f, 1f, 0.75f),
                486f, 52f, 44f);

        // KARAKTER & PENGATURAN: dua tile berdampingan, ikon di kotak berwarna.
        BuildMenuTile("Main Menu Character Button", card, 40f, "KARAKTER", "Ganti avatar", "icon-user",
                      PinkTopColor, PinkBottomColor);
        BuildMenuTile("Main Menu Settings Button", card, 328f, "PENGATURAN", "Kontrol & suara", "icon-settings",
                      CyanTopColor, CyanBottomColor);

        // KELUAR GAME: merah kalem dengan ikon power. Isi diatur HorizontalLayoutGroup supaya selalu di tengah.
        Button quit = CreateMenuButton("Main Menu Quit Button", card, QuitFaceColor, QuitFaceColor, QuitLipColor,
                                       QuitBorderColor, 1.2f, out RectTransform quitFace);
        Place((RectTransform)quit.transform, 40f, 540f, 560f, 88f);
        RectTransform quitContent = Stretch(CreateRect("Main Menu Quit Content", quitFace, Vector2.zero), 0f);
        HorizontalLayoutGroup row = quitContent.gameObject.AddComponent<HorizontalLayoutGroup>();
        row.childAlignment = TextAnchor.MiddleCenter;
        row.spacing = 14f;
        row.childControlWidth = row.childControlHeight = true;
        row.childForceExpandWidth = row.childForceExpandHeight = false;
        Image quitIcon = AddIcon("Main Menu Quit Icon", quitContent, "icon-power", QuitTextColor, 0f, 0f, 34f);
        LayoutElement iconSize = quitIcon.gameObject.AddComponent<LayoutElement>();
        iconSize.preferredWidth = iconSize.preferredHeight = 34f;
        Text quitLabel = CreateText("Main Menu Quit Button Label", quitContent, "KELUAR GAME", 28, FontStyle.Bold,
                                    TextAnchor.MiddleCenter, Vector2.zero, Vector2.zero, QuitTextColor);
        quitLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
    }

    private static void BuildMenuTile(string name, RectTransform card, float x, string label, string subtitle,
                                      string icon, Color iconTop, Color iconBottom) {
        Button tile = CreateMenuButton(name, card, TileTopColor, TileBottomColor, TileLipColor,
                                       new Color(1f, 1f, 1f, 0.12f), 1.2f, out RectTransform face);
        Place((RectTransform)tile.transform, x, 330f, 272f, 188f);
        RectTransform iconBox = Place(CreateRect(name + " Icon Box", face, Vector2.zero), 98f, 22f, 76f, 76f);
        AddGradientShape(iconBox.gameObject, iconTop, iconBottom, cornerScale: 1.1f).raycastTarget = false;
        AddIcon(name + " Icon", iconBox, icon, Color.white, 18f, 18f, 40f);
        Place(CreateText(name + " Label", face, label, 28, FontStyle.Bold, TextAnchor.MiddleCenter,
                         Vector2.zero, Vector2.zero, Color.white).rectTransform, 0f, 108f, 272f, 36f);
        Place(CreateText(name + " Subtitle", face, subtitle, 20, FontStyle.Normal, TextAnchor.MiddleCenter,
                         Vector2.zero, Vector2.zero, TileSubColor).rectTransform, 0f, 144f, 272f, 26f);
    }

    /// <summary>
    /// Tombol menu bergaya "3D": Lip (bayangan gelap, terlihat 8 px di bawah) + Face (gradasi, isi tombol).
    /// Isi tombol (ikon/teks) ditaruh di <paramref name="face"/>. MenuButtonPress menurunkan Face saat ditekan.
    /// </summary>
    private static Button CreateMenuButton(string name, Transform parent, Color top, Color bottom, Color lip,
                                           Color border, float cornerScale, out RectTransform face) {
        RectTransform rect = CreateRect(name, parent, Vector2.zero);
        RectTransform lipRect = Inset(CreateRect(name + " Lip", rect, Vector2.zero), MenuLipDepth, 0f);
        AddShape(lipRect.gameObject, lip, cornerScale: cornerScale).raycastTarget = false;

        face = Inset(CreateRect(name + " Face", rect, Vector2.zero), 0f, MenuLipDepth);
        Image faceImage = AddGradientShape(face.gameObject, top, bottom, cornerScale: cornerScale);
        if (border.a > 0f)
            AddOutline(face, border, cornerScale: cornerScale);

        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = faceImage;
        button.colors = PressTint();
        rect.gameObject.AddComponent<MenuButtonPress>().Init(face, MenuLipDepth - 2f);
        return button;
    }

    private static Image AddGradientShape(GameObject target, Color top, Color bottom, string sprite = "ui-rounded",
                                          float cornerScale = 1f) {
        Image image = AddShape(target, Color.white, sprite, cornerScale);
        target.AddComponent<UIVerticalGradient>().SetColors(top, bottom);
        return image;
    }

    /// <summary>Ikon putih dari Assets/Resources/UI/Icons, diwarnai lewat color.</summary>
    private static Image AddIcon(string name, Transform parent, string icon, Color color, float x, float y, float size) {
        RectTransform rect = Place(CreateRect(name, parent, Vector2.zero), x, y, size, size);
        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = UiSprite("Icons/" + icon);
        image.preserveAspect = true;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    /// <summary>Isi penuh parent, dengan jarak dari atas dan bawah.</summary>
    private static RectTransform Inset(RectTransform rect, float top, float bottom) {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(0f, bottom);
        rect.offsetMax = new Vector2(0f, -top);
        return rect;
    }

#if UNITY_EDITOR
    /// <summary>
    /// Ganti HANYA "Main Menu" di LobbyCanvas.prefab dengan desain terbaru dari BuildMainMenu.
    /// Panel lobby, overlay pengaturan, dll. tidak disentuh. Lewat menu "Go Vista > Lobby UI > Pasang desain baru menu utama".
    /// </summary>
    public static void RebuildMainMenuInPrefab() {
        if (LoadCanvasPrefab(LobbyCanvasPrefabPath) == null) {
            Debug.LogWarning($"[Lobby UI] {LobbyCanvasPrefabPath} tidak ditemukan.");
            return;
        }
        GameObject root = PrefabUtility.LoadPrefabContents(LobbyCanvasPrefabPath);
        try {
            Transform oldMenu = FindChild(root.transform, "Main Menu");
            RectTransform safeArea = oldMenu != null
                ? oldMenu.parent as RectTransform
                : FindChild(root.transform, "Lobby Safe Area") as RectTransform;
            if (safeArea == null) {
                Debug.LogWarning("[Lobby UI] \"Main Menu\" / \"Lobby Safe Area\" tidak ditemukan di prefab.");
                return;
            }
            int siblingIndex = oldMenu != null ? oldMenu.GetSiblingIndex() : safeArea.childCount;
            bool wasActive = oldMenu == null || oldMenu.gameObject.activeSelf;
            if (oldMenu != null)
                Object.DestroyImmediate(oldMenu.gameObject);

            BuildMainMenu(safeArea);
            Transform newMenu = safeArea.GetChild(safeArea.childCount - 1);
            newMenu.SetSiblingIndex(Mathf.Min(siblingIndex, safeArea.childCount - 1));
            newMenu.gameObject.SetActive(wasActive);
            PrefabUtility.SaveAsPrefabAsset(root, LobbyCanvasPrefabPath);
            Debug.Log("[Lobby UI] Menu utama di LobbyCanvas.prefab diganti ke desain baru.");
        } finally {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
#endif

    private static void BuildSettingsOverlay(Transform canvas) {
        RectTransform overlay = CreateModalOverlay("Settings Overlay", canvas);
        // Dialog PENGATURAN yang sama dengan menu pause (SettingsPanel.prefab).
        InstantiateSettingsPanel(overlay, "Settings Dialog");
        overlay.gameObject.SetActive(false);
    }

    /// <summary>
    /// Pasang dialog PENGATURAN bersama (Assets/Resources/UI/SettingsPanel.prefab). Di Editor dipasang sebagai
    /// nested prefab, jadi perubahan di SettingsPanel.prefab ikut ke lobby dan menu pause.
    /// </summary>
    private static void InstantiateSettingsPanel(Transform parent, string name) {
        GameObject prefab = Resources.Load<GameObject>(SettingsPanel.ResourcePath);
        if (prefab == null) {
            Debug.LogWarning($"[Lobby UI] Resources/{SettingsPanel.ResourcePath}.prefab tidak ada. " +
                             "Jalankan Go Vista > Desain Baru > Panel Pengaturan.");
            return;
        }
        GameObject instance;
#if UNITY_EDITOR
        instance = !Application.isPlaying
            ? (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent)
            : Instantiate(prefab, parent, false);
#else
        instance = Instantiate(prefab, parent, false);
#endif
        instance.name = name;
        var rect = (RectTransform)instance.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        instance.SetActive(false);
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
        _mainMenuName = FindChild(canvas, "Main Menu Name")?.GetComponent<Text>();
        _mainMenuInitial = FindChild(canvas, "Main Menu Initial")?.GetComponent<Text>();
        _settingsOverlay = FindChild(canvas, "Settings Overlay")?.gameObject;
        _lobbyCanvas = canvasObject.GetComponent<Canvas>();
        _exitOverlay = FindChild(canvas, "Exit Confirm Overlay")?.gameObject;
        // Dialog PENGATURAN = SettingsPanel.prefab (sama dengan menu pause). Isi dialog diurus SettingsPanel.
        _settingsPanel = _settingsOverlay != null ? _settingsOverlay.GetComponentInChildren<SettingsPanel>(true) : null;
        _exitConfirmLabel = FindChild(canvas, "Exit Confirm Button Label")?.GetComponent<Text>();
        // Apple melarang tombol keluar aplikasi di iOS; pemain menutup app lewat sistem.
        if (Application.platform == RuntimePlatform.IPhonePlayer)
            FindChild(canvas, "Main Menu Quit Button")?.gameObject.SetActive(false);

        _waitingCanvas = FindWaitingCanvas();
        if (_waitingCanvas != null) {
            Transform waitingRoot = _waitingCanvas.transform;
            _waitingPanel = FindChild(waitingRoot, "Room Waiting Panel")?.gameObject;
            _waitingCodeText = FindChild(waitingRoot, "Waiting Room Code")?.GetComponent<Text>();
            _pauseCodeText = FindChild(waitingRoot, "Pause Room Code")?.GetComponent<Text>();
            _pauseCopyButtonLabel = FindChild(waitingRoot, "Pause Copy Code Button Label")?.GetComponent<Text>();
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
            _pauseSettingsPanel = waitingRoot.GetComponentInChildren<SettingsPanel>(true);
            _pauseLeaveConfirmPanel = FindChild(waitingRoot, "Pause Leave Confirm Panel")?.gameObject;
            AddClickListenerIn(_waitingCanvas, "Waiting Join Game Button", BeginGame);
            AddClickListenerIn(_waitingCanvas, "Waiting Disconnect Button", Disconnect);
            AddClickListenerIn(_waitingCanvas, "Waiting Copy Code Button", CopyQuickmatchCode);
            AddClickListenerIn(_waitingCanvas, "Pause Copy Code Button", CopyPauseRoomCode);
            AddClickListenerIn(_waitingCanvas, "Waiting Close Roster Button", CloseWaitingRoster);
            AddClickListenerIn(_waitingCanvas, "Waiting Roster Toggle Button", ToggleWaitingRoster);
            AddClickListenerIn(_waitingCanvas, "Pause Button", OpenPauseMenu);
            AddClickListenerIn(_waitingCanvas, "Pause Resume Button", ClosePauseMenu);
            AddClickListenerIn(_waitingCanvas, "Pause Players Button", OpenPausePlayers);
            AddClickListenerIn(_waitingCanvas, "Pause Settings Button", OpenPauseSettings);
            AddClickListenerIn(_waitingCanvas, "Pause Players Back Button", ReturnToPauseMenu);
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

    private void OnPlayerNameChanged(string playerName) => RefreshAccountChip();

    /// <summary>Nama untuk sapaan menu utama: nama pemain (menu KARAKTER), atau username kalau belum diisi.</summary>
    private static string GreetingName(string username) {
        string playerName = ProgressService.IsLoaded ? PlayerNameRules.Sanitize(ProgressService.Current.playerName) : "";
        return playerName.Length > 0 ? playerName : username;
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
        // Desain v10: sapaan dan nama di dua teks terpisah. Layout lama (tanpa "Main Menu Name"): satu kalimat.
        string greetingName = GreetingName(username);
        if (_mainMenuName != null) {
            _mainMenuName.text = greetingName.Length > 18 ? greetingName.Substring(0, 17) + "…" : greetingName;
            if (_mainMenuGreeting != null)
                _mainMenuGreeting.text = "Selamat datang,";
        } else if (_mainMenuGreeting != null) {
            _mainMenuGreeting.text = "Selamat datang, " + greetingName + "!";
        }
        if (_mainMenuInitial != null)
            _mainMenuInitial.text = greetingName.Length > 0 ? greetingName.Substring(0, 1).ToUpperInvariant() : "?";
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
        SetStatus(ReadyStatusMessage, StatusKind.Success);
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

    /// <summary>Room privat: masuk kalau nama sudah ada, kalau belum Normcore membuatnya.</summary>
    private void ConnectByRoomName() {
        string requestedRoom = ReadRoomName();
        if (requestedRoom != null)
            Connect(() => realtime.Connect(requestedRoom), "Masuk ke room privat…");
    }

    /// <summary>Nama room dari input, atau null (dengan pesan error) kalau tidak valid.</summary>
    private string ReadRoomName() {
        string requestedRoom = _roomInput != null ? _roomInput.text.Trim() : "";
        if (string.IsNullOrEmpty(requestedRoom)) {
            SetStatus("Isi nama room privat terlebih dahulu.", StatusKind.Error);
            return null;
        }
        if (requestedRoom.Length >= 512) {
            SetStatus("Nama room harus kurang dari 512 karakter.", StatusKind.Error);
            return null;
        }
        return requestedRoom;
    }

    private void ConnectQuickmatchCode() {
        string code = _codeInput != null ? _codeInput.text.Trim() : "";
        if (string.IsNullOrEmpty(code)) {
            SetStatus("Masukkan kode room terlebih dahulu.", StatusKind.Error);
            return;
        }
        Connect(() => realtime.ConnectDirectlyToQuickmatchRoom(QuickmatchRoomGroup, code), "Memeriksa kode dan menghubungkan…");
    }

    private void JoinRecentRoom(string room) {
        if (room.StartsWith("qm:")) {
            string code = room.Substring(3);
            Connect(() => realtime.ConnectDirectlyToQuickmatchRoom(QuickmatchRoomGroup, code), "Memeriksa kode dan menghubungkan…");
        } else {
            Connect(() => realtime.Connect(room), "Menghubungkan ke room…");
        }
    }

    private void Connect(System.Action connect, string status) {
        if (realtime == null || _busy)
            return;
        _busy = true;
        _connectCancelled = false;
        SetStatus(status, StatusKind.Busy);
        ShowConnectingModal(status);
        try {
            connect();
        } catch (System.Exception exception) {
            _busy = false;
            _quickmatchIntent = QuickmatchIntent.None;
            HideModal();
            SetStatus("Gagal memulai koneksi: " + exception.Message, StatusKind.Error);
            Debug.LogException(exception);
        }
    }

    private void DidConnectToRoom(Realtime connectedRealtime) {
        if (IgnoreCancelledConnect(connectedRealtime))
            return;
        if (InterceptQuickmatchConnect(connectedRealtime))
            return;
        _busy = false;
        HideModal();
        _roomFullDisconnectRequested = false;
        _leaveRoomPending = false;
        ResetInGameMenu();
        string roomCode = connectedRealtime.room != null ? connectedRealtime.room.quickmatchRoomCode : null;
        SetStatus("Terhubung.", StatusKind.Success);
        _waitingRosterSignature = null;
        _pauseRosterSignature = null;
        SpawnPresence(connectedRealtime);
        // Yang dibagikan ke teman: kode Quickmatch kalau ada, kalau tidak nama room.
        string shareCode = !string.IsNullOrEmpty(roomCode) ? roomCode : connectedRealtime.roomName;
        if (_waitingCodeText != null)
            _waitingCodeText.text = shareCode;
        if (_pauseCodeText != null)
            _pauseCodeText.text = shareCode;
        SetWaitingStatus(!string.IsNullOrEmpty(roomCode)
            ? "Teman bisa masuk lewat GABUNG DENGAN KODE."
            : "Teman bisa masuk dengan mengetik nama room ini di ROOM PRIVAT lalu MASUK.");
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
        if (IgnoreCancelledConnect(null) || HandleQuickmatchDisconnect())
            return;
        _busy = false;
        HideModal();
        ResetInGameMenu();
        _leaveRoomPending = false;
        ResetVenueState();
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
        if (IgnoreCancelledConnect(null) || HandleQuickmatchDisconnect())
            return;
        _busy = false;
        HideModal();
        ResetVenueState();
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
            SetStatus(RoomExpiredMessage, StatusKind.Error);
            ShowAlertModal(RoomNotFoundTitle, RoomExpiredMessage);
            return;
        }

        // Disconnect yang disengaja (keluar room, ganti akun, room penuh) tidak perlu alert.
        if (disconnectEvent == null || disconnectEvent is DisconnectCalledByLocalClient || _roomFullDisconnectRequested)
            return;

        if (disconnectEvent.didConnectToRoom) {
            SetStatus(ConnectionLostStatus, StatusKind.Error);
            ShowAlertModal(ConnectionLostTitle, ConnectionLostMessage);
            return;
        }
        string detail = !string.IsNullOrEmpty(disconnectEvent.message) ? disconnectEvent.message : JoinFailedFallback;
        SetStatus("Gagal masuk room: " + detail, StatusKind.Error);
        ShowAlertModal(JoinFailedTitle, detail);
    }

    /// <summary>
    /// Kembalikan state venue ke World (scene venue, panel pilihan, kunci input, venue lokal). Idempoten,
    /// jadi aman dipanggil dari kedua event disconnect. Kalau scene akan di-load ulang (keluar room),
    /// scene venue tidak perlu di-unload manual karena ikut hilang bersama load Single.
    /// </summary>
    private void ResetVenueState() {
        if (VenueTravelManager.Instance != null)
            VenueTravelManager.Instance.ResetToWorld(unloadVenueScenes: !_returnToLobbyOnDisconnect);
        else
            VenueTravelManager.SetLocalVenue(VenueId.World);
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

    private void OpenCharacterSelect() {
        CharacterSelectUI characterSelect = FindFirstObjectByType<CharacterSelectUI>(FindObjectsInactive.Include);
        if (characterSelect == null) {
            Debug.LogWarning("[CharacterSelect] CharacterSelectCanvas tidak ada di scene. " +
                             "Jalankan Go Vista > Setup Pilih Karakter.");
            return;
        }
        characterSelect.Open();
    }

    // Menu utama > PENGATURAN. Isi dialog (sensitivitas, akun, simpan) diurus SettingsPanel; di sini hanya
    // yang khusus lobby: overlay gelap di belakangnya dan ATUR KONTROL versi lobby.
    private void OpenSettings() {
        if (_settingsPanel == null) {
            Debug.LogWarning("[Lobby UI] SettingsPanel tidak ada di \"Settings Overlay\". " +
                             "Jalankan Go Vista > Desain Baru > Panel Pengaturan.");
            return;
        }
        if (_settingsOverlay != null)
            _settingsOverlay.SetActive(true);
        _settingsPanel.Open(OpenLobbyControlLayoutEditor, OnSettingsClosed);
    }

    private void OnSettingsClosed() {
        if (_settingsOverlay != null)
            _settingsOverlay.SetActive(false);
    }

    // Pengaturan (lobby) > ATUR KONTROL. Canvas lobby disembunyikan supaya kontrol mobile (MobileUI,
    // sorting order di bawah lobby) terlihat di atas background. Selesai = kembali ke Pengaturan.
    private void OpenLobbyControlLayoutEditor() {
        if (_lobbyControlLayoutOpen || ControlLayoutEditor.IsOpen)
            return;
        ControlLayoutEditor editor = FindControlLayoutEditor();
        if (editor == null) {
            Debug.LogWarning("[ControlLayout] Control Layout Editor tidak ada di MobileUI.");
            return;
        }

        _lobbyControlLayoutOpen = true;
        if (_lobbyCanvas != null)
            _lobbyCanvas.enabled = false;
        MobileControls.SetVisible(true);
        editor.Open(OnLobbyControlLayoutEditorClosed);
    }

    private void OnLobbyControlLayoutEditorClosed() {
        if (!_lobbyControlLayoutOpen)
            return;
        _lobbyControlLayoutOpen = false;
        MobileControls.SetVisible(false);
        if (_lobbyCanvas != null)
            _lobbyCanvas.enabled = true;
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
            foreach (PlayerController player in FindObjectsByType<PlayerController>(FindObjectsSortMode.None)) {
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
            signature.Append(player.Key).Append(player.Value ? '+' : '-')
                     .Append(LobbyPresence.NameOf(player.Key)).Append('\n'); // nama ikut, supaya ganti nama ter-refresh
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
            // Nama dari menu KARAKTER (LobbyPresence), atau "Player N" kalau pemain belum mengisi nama.
            string playerName = LobbyPresence.NameOf(player.Key) + (isLocal ? " (Kamu)" : "");
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
        PlayerManager playerManager = FindFirstObjectByType<PlayerManager>();
        if (playerManager == null) {
            SetStatus("Player Manager tidak ditemukan.", StatusKind.Error);
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
            SceneLoadPriority.UseLow(); // cicil integrasi supaya koneksi Normcore tidak putus saat load
            AsyncOperation load = SceneManager.LoadSceneAsync(WorldSceneName, LoadSceneMode.Additive);
            if (load == null) {
                SceneLoadPriority.Restore();
                _worldLoading = false;
                SetWaitingJoinButtonLoading(false);
                SetWaitingStatus("Gagal memuat world pratinjau. Tekan MULAI untuk mencoba lagi.");
                yield break;
            }
            yield return load;
            SceneLoadPriority.Restore();
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

    private IEnumerator LoadWorldAndBeginGame(PlayerManager playerManager) {
        _startingGame = true;
        SetStatus("Memuat world…", StatusKind.Busy);

        Scene worldScene = SceneManager.GetSceneByName(WorldSceneName);
        if (!worldScene.isLoaded) {
            SceneLoadPriority.UseLow();
            AsyncOperation load = SceneManager.LoadSceneAsync(WorldSceneName, LoadSceneMode.Additive);
            if (load == null) {
                SceneLoadPriority.Restore();
                _startingGame = false;
                SetStatus("Gagal memuat WorldScene. Pastikan scene ada di Build Settings.", StatusKind.Error);
                yield break;
            }
            yield return load;
            SceneLoadPriority.Restore();
            worldScene = SceneManager.GetSceneByName(WorldSceneName);
        }

        if (worldScene.IsValid() && worldScene.isLoaded)
            DisableWorldCameras(worldScene);

        playerManager.BeginGame();

        // Daftarkan WorldScene yang sudah ter-load sebagai venue awal; hop berikutnya (dermaga/pad)
        // lewat VenueTravelManager.TravelTo. LocalVenue sendiri sudah World lewat PlayerVenueSync avatar.
        if (VenueTravelManager.Instance != null)
            VenueTravelManager.Instance.AdoptInitialVenue(VenueId.World);

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
        MobileControls.SetVisible(false);
        ShowInGameMenuPage(InGameMenuPage.Main);
    }

    private void ClosePauseMenu() {
        if (!_gameplayStarted || _inGameMenuPage != InGameMenuPage.Main)
            return;

        GameSettings.Save();
        ShowInGameMenuPage(InGameMenuPage.Closed);
        if (PlayerInputHub.Instance != null)
            PlayerInputHub.Instance.SetPauseLocked(false);
        MobileControls.SetVisible(true);
    }

    private void OpenPausePlayers() {
        if (_inGameMenuPage != InGameMenuPage.Main)
            return;
        ShowInGameMenuPage(InGameMenuPage.Players);
        if (_pausePlayerScroll != null)
            _pausePlayerScroll.verticalNormalizedPosition = 1f;
    }

    // Pause > PENGATURAN: dialog yang sama dengan lobby (SettingsPanel). Ditutup = kembali ke menu pause.
    private void OpenPauseSettings() {
        if (_inGameMenuPage != InGameMenuPage.Main || _pauseSettingsPanel == null)
            return;
        ShowInGameMenuPage(InGameMenuPage.Settings);
        _pauseSettingsPanel.Open(OpenControlLayoutEditor, OnPauseSettingsClosed);
    }

    private void OnPauseSettingsClosed() {
        if (_inGameMenuPage == InGameMenuPage.Settings)
            ShowInGameMenuPage(InGameMenuPage.Main);
    }

    private static ControlLayoutEditor FindControlLayoutEditor() {
        return FindFirstObjectByType<ControlLayoutEditor>(FindObjectsInactive.Include);
    }

    // Pause > Pengaturan > ATUR KONTROL. Input tetap terkunci (pause lock) selama mengatur,
    // jadi joystick/tombol tidak menggerakkan karakter.
    private void OpenControlLayoutEditor() {
        if (_inGameMenuPage != InGameMenuPage.Settings)
            return;
        ControlLayoutEditor editor = FindControlLayoutEditor();
        if (editor == null) {
            Debug.LogWarning("[ControlLayout] Control Layout Editor tidak ada di MobileUI.");
            return;
        }

        GameSettings.Save();
        ShowInGameMenuPage(InGameMenuPage.ControlLayout);
        if (_pauseButton != null)
            _pauseButton.SetActive(false);
        MobileControls.SetVisible(true);
        editor.Open(OnControlLayoutEditorClosed);
    }

    private void OnControlLayoutEditorClosed() {
        if (!_gameplayStarted || _inGameMenuPage != InGameMenuPage.ControlLayout)
            return;
        MobileControls.SetVisible(false);
        if (_pauseButton != null)
            _pauseButton.SetActive(true);
        ShowInGameMenuPage(InGameMenuPage.Settings);
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
            _pauseOverlay.SetActive(page != InGameMenuPage.Closed && page != InGameMenuPage.ControlLayout);
        if (_pauseMainPanel != null)
            _pauseMainPanel.SetActive(page == InGameMenuPage.Main);
        if (_pausePlayersPanel != null)
            _pausePlayersPanel.SetActive(page == InGameMenuPage.Players);
        if (_pauseSettingsPanel != null)
            _pauseSettingsPanel.gameObject.SetActive(page == InGameMenuPage.Settings);
        if (_pauseLeaveConfirmPanel != null)
            _pauseLeaveConfirmPanel.SetActive(page == InGameMenuPage.LeaveConfirm);
    }

    private void ResetInGameMenu() {
        _gameplayStarted = false;
        if (ControlLayoutEditor.IsOpen)
            FindControlLayoutEditor()?.Cancel();
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
            _pauseSettingsPanel.Hide();
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
        MobileControls.SetVisible(false);
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
        if (!TryCopyRoomCode(out bool hasCode)) {
            SetWaitingStatus("Belum terhubung ke room.");
            return;
        }
        SetWaitingStatus(hasCode ? "Kode room disalin. Kirim ke temanmu." : "Nama room disalin. Kirim ke temanmu.");
        if (_copyFeedbackRoutine != null)
            StopCoroutine(_copyFeedbackRoutine);
        _copyFeedbackRoutine = StartCoroutine(ShowCopiedFeedback(_copyButtonLabel));
    }

    // Tombol SALIN di Pause > PEMAIN. Tidak ada teks status di panel itu, jadi feedback lewat label tombol.
    private void CopyPauseRoomCode() {
        if (!TryCopyRoomCode(out _))
            return;
        if (_pauseCopyFeedbackRoutine != null)
            StopCoroutine(_pauseCopyFeedbackRoutine);
        _pauseCopyFeedbackRoutine = StartCoroutine(ShowCopiedFeedback(_pauseCopyButtonLabel));
    }

    /// <summary>Salin kode Quickmatch (kalau ada) atau nama room ke clipboard.</summary>
    private bool TryCopyRoomCode(out bool hasCode) {
        hasCode = false;
        if (realtime == null || !realtime.connected)
            return false;
        string code = realtime.room != null ? realtime.room.quickmatchRoomCode : null;
        hasCode = !string.IsNullOrEmpty(code);
        GUIUtility.systemCopyBuffer = hasCode ? code : realtime.roomName;
        return true;
    }

    private static IEnumerator ShowCopiedFeedback(Text label) {
        if (label != null)
            label.text = "TERSALIN";
        yield return new WaitForSeconds(2f);
        if (label != null)
            label.text = "SALIN";
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
    static LobyUISceneCanvasBootstrap() {
        EditorSceneManager.sceneOpened += OnSceneOpened;
        EditorApplication.delayCall += BuildForOpenScenes;
    }

    private static void OnSceneOpened(UnityEngine.SceneManagement.Scene scene, OpenSceneMode mode) {
        EditorApplication.delayCall += () => BuildForScene(scene);
    }

    private static void BuildForOpenScenes() {
        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            BuildForScene(UnityEngine.SceneManagement.SceneManager.GetSceneAt(i));
    }

    private static void BuildForScene(UnityEngine.SceneManagement.Scene scene) {
        if (!scene.IsValid() || !scene.isLoaded || scene.name != "LobyUIScene")
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
