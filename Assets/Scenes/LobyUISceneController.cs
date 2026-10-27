using System.Collections.Generic;
using System.Collections;
using Normal.Realtime;
using Normal.Realtime.Examples;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

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
    private const string PrefabFolderPath = "Assets/Prefab";
    private const string LobbyCanvasPrefabPath = "Assets/Prefab/LobbyCanvas.prefab";
    private const string WaitingCanvasPrefabPath = "Assets/Prefab/RoomWaitingCanvas.prefab";
    private const string RecentRoomsKey = "LobyUIScene.RecentRooms";
    private const int MaxRecentRooms = 3;
    private const int MaxRoomPlayers = 10;
    private static readonly Color PanelColor = new Color(0.055f, 0.085f, 0.14f, 0.97f);
    private static readonly Color FieldColor = new Color(0.12f, 0.16f, 0.23f, 1f);
    private static readonly Color AccentColor = new Color(0.16f, 0.55f, 0.95f, 1f);

    private GameObject _panel;
    private Button _reopenButton;
    private InputField _roomInput;
    private InputField _codeInput;
    private Text _statusText;
    private Text _activeRoomText;
    private Text _activeCodeText;
    private Transform _recentList;
    private ScrollRect _recentScroll;
    private bool _busy;
    private GameObject _waitingCanvas;
    private GameObject _waitingPanel;
    private Text _waitingRoomText;
    private Text _playersText;
    private Button _waitingJoinButton;
    private Button _waitingRosterToggleButton;
    private Coroutine _playerListRoutine;
    private bool _roomFullDisconnectRequested;

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

        realtime.didConnectToRoom -= DidConnectToRoom;
        realtime.didDisconnectFromRoom -= DidDisconnectFromRoom;
        realtime.didDisconnectFromRoomWithEvent -= DidDisconnectFromRoomWithEvent;
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
            SetStatus("Komponen Normcore Realtime tidak ditemukan di scene.");
            return;
        }

        realtime.didConnectToRoom -= DidConnectToRoom;
        realtime.didDisconnectFromRoom -= DidDisconnectFromRoom;
        realtime.didDisconnectFromRoomWithEvent -= DidDisconnectFromRoomWithEvent;
        realtime.didConnectToRoom += DidConnectToRoom;
        realtime.didDisconnectFromRoom += DidDisconnectFromRoom;
        realtime.didDisconnectFromRoomWithEvent += DidDisconnectFromRoomWithEvent;

        AddClickListener("Lobby Create Button", ConnectByRoomName);
        AddClickListener("Lobby Join Button", ConnectByRoomName);
        AddClickListener("Lobby Quickmatch Button", ConnectQuickmatch);
        AddClickListener("Lobby Join Code Button", ConnectQuickmatchCode);
        AddClickListener("Lobby Close Button", CloseLobby);
        AddClickListener("Lobby Reopen Button", OpenLobby);
        AddClickListener("Lobby Disconnect Button", Disconnect);
        AddClickListener("Lobby Copy Code Button", CopyQuickmatchCode);

        RefreshRecentRooms();
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
            bool changed = MigrateLegacyPanelLayout();
            changed |= EnsureLobbyCanvasPrefabInEditor();
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
            MigrateLegacyPanelLayout();
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

    private void BuildWaitingCanvasContents(Transform canvas) {
        RectTransform panel = CreateRect("Room Waiting Panel", canvas, new Vector2(620f, 520f));
        panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
        panel.pivot = new Vector2(0.5f, 0.5f);
        panel.anchoredPosition = Vector2.zero;
        panel.gameObject.AddComponent<Image>().color = PanelColor;
        _waitingPanel = panel.gameObject;
        CreateText("Waiting Title", panel, "ROOM LOBBY", 30, FontStyle.Bold, TextAnchor.MiddleLeft,
                   new Vector2(500f, 48f), new Vector2(0f, -38f), Color.white);
        _waitingRoomText = CreateText("Waiting Room Name", panel, "Menghubungkan...", 16, FontStyle.Normal,
                                      TextAnchor.MiddleLeft, new Vector2(520f, 32f), new Vector2(0f, -82f),
                                      new Color(0.65f, 0.84f, 1f));
        CreateText("Waiting Players Label", panel, "PLAYER YANG SUDAH JOIN (MAKSIMAL 10)", 16, FontStyle.Bold,
                   TextAnchor.MiddleLeft, new Vector2(520f, 32f), new Vector2(0f, -132f), Color.white);
        RectTransform playerBox = CreateRect("Waiting Player Box", panel, new Vector2(520f, 230f));
        playerBox.anchoredPosition = new Vector2(0f, -170f);
        playerBox.gameObject.AddComponent<Image>().color = FieldColor;
        _playersText = CreateText("Waiting Player List", playerBox, "Menunggu daftar player...", 17,
                                  FontStyle.Normal, TextAnchor.UpperLeft, new Vector2(490f, 210f),
                                  new Vector2(0f, -12f), Color.white);
        _playersText.rectTransform.anchorMin = new Vector2(0f, 1f);
        _playersText.rectTransform.anchorMax = new Vector2(0f, 1f);
        _playersText.rectTransform.pivot = new Vector2(0f, 1f);
        _waitingJoinButton = CreateButton("Waiting Join Game Button", panel, "JOIN GAME",
                                          new Vector2(320f, 52f), new Vector2(-95f, -456f), AccentColor);
        CreateButton("Waiting Disconnect Button", panel, "KELUAR ROOM", new Vector2(170f, 52f),
                     new Vector2(160f, -456f), new Color(0.65f, 0.22f, 0.2f));
        CreateButton("Waiting Close Roster Button", panel, "×", new Vector2(44f, 44f),
                     new Vector2(270f, -28f), new Color(0.75f, 0.2f, 0.22f));
        _waitingRosterToggleButton = CreateButton("Waiting Roster Toggle Button", canvas,
                                                  "PLAYER", new Vector2(150f, 42f), Vector2.zero, AccentColor);
        RectTransform toggleRect = _waitingRosterToggleButton.GetComponent<RectTransform>();
        toggleRect.anchorMin = toggleRect.anchorMax = new Vector2(1f, 1f);
        toggleRect.anchoredPosition = new Vector2(-95f, -30f);
        _waitingRosterToggleButton.gameObject.SetActive(false);
    }

    internal static bool EnsureWaitingCanvasControls(Transform canvas) {
        Transform panel = FindChild(canvas, "Room Waiting Panel");
        if (panel == null)
            return false;

        bool changed = false;
        if (FindChild(panel, "Waiting Close Roster Button") == null) {
            CreateButton("Waiting Close Roster Button", panel, "×", new Vector2(44f, 44f),
                         new Vector2(270f, -28f), new Color(0.75f, 0.2f, 0.22f));
            changed = true;
        }

        if (FindChild(canvas, "Waiting Roster Toggle Button") == null) {
            Button button = CreateButton("Waiting Roster Toggle Button", canvas, "PLAYER",
                                         new Vector2(150f, 42f), Vector2.zero, AccentColor);
            RectTransform toggleRect = button.GetComponent<RectTransform>();
            toggleRect.anchorMin = toggleRect.anchorMax = new Vector2(1f, 1f);
            toggleRect.anchoredPosition = new Vector2(-95f, -30f);
            button.gameObject.SetActive(false);
            changed = true;
        }
        return changed;
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
               FindChild(root, "Lobby Reopen Button") != null;
    }

    private bool MigrateLegacyPanelLayout() {
        GameObject canvas = FindCanvasInScene();
        if (canvas == null)
            return false;
        Transform panel = FindChild(canvas.transform, "Lobby Panel");
        if (panel == null)
            return false;

        RectTransform rect = panel.GetComponent<RectTransform>();
        if (rect == null)
            return false;

        bool changed = false;
        if (rect.anchorMin == new Vector2(0.5f, 0.5f) && rect.anchorMax == new Vector2(0.5f, 0.5f) &&
            Mathf.Approximately(rect.pivot.x, 0.5f) && Mathf.Approximately(rect.pivot.y, 1f) &&
            rect.anchoredPosition == Vector2.zero) {
            rect.pivot = new Vector2(0.5f, 0.5f);
            changed = true;
        }
        if (rect.sizeDelta == new Vector2(600f, 740f)) {
            rect.sizeDelta = new Vector2(600f, 800f);
            changed = true;
        }
        changed |= EnsureCodeFieldBox();

        changed |= MigrateRect(canvas.transform, "Lobby Active Room", new Vector2(420f, 28f), new Vector2(-40f, -426f),
                               new Vector2(330f, 28f), new Vector2(-85f, -426f));
        changed |= MigrateCodeContainer(canvas.transform);
        changed |= MigrateRect(canvas.transform, "Lobby Copy Code Button", new Vector2(130f, 34f), new Vector2(180f, -458f),
                               new Vector2(130f, 38f), new Vector2(165f, -455f));
        changed |= MigrateRect(canvas.transform, "Lobby Copy Code Button", new Vector2(130f, 34f), new Vector2(180f, -472f),
                               new Vector2(130f, 38f), new Vector2(165f, -455f));
        changed |= MigrateRect(canvas.transform, "Lobby Copy Code Button", new Vector2(120f, 34f), new Vector2(30f, -472f),
                               new Vector2(130f, 38f), new Vector2(165f, -455f));
        changed |= MigrateRect(canvas.transform, "Lobby Disconnect Button", new Vector2(100f, 34f), new Vector2(215f, -426f),
                               new Vector2(130f, 38f), new Vector2(165f, -503f));
        changed |= MigrateRect(canvas.transform, "Lobby Disconnect Button", new Vector2(100f, 34f), new Vector2(150f, -472f),
                               new Vector2(130f, 38f), new Vector2(165f, -503f));
        changed |= MigratePosition(canvas.transform, "Recent Rooms Label", new Vector2(0f, -500f), new Vector2(0f, -560f));
        changed |= MigratePosition(canvas.transform, "Recent Rooms Label", new Vector2(0f, -520f), new Vector2(0f, -560f));
        changed |= MigratePosition(canvas.transform, "Global Rooms Note", new Vector2(0f, -530f), new Vector2(0f, -590f));
        changed |= MigratePosition(canvas.transform, "Global Rooms Note", new Vector2(0f, -550f), new Vector2(0f, -590f));
        changed |= MigrateRect(canvas.transform, "Lobby Recent Rooms", new Vector2(500f, 150f), new Vector2(0f, -640f),
                               new Vector2(500f, 130f), new Vector2(0f, -660f));
        changed |= MigrateRect(canvas.transform, "Lobby Recent Rooms", new Vector2(500f, 130f), new Vector2(0f, -655f),
                               new Vector2(500f, 130f), new Vector2(0f, -660f));
        changed |= EnsureRecentRoomsViewport();
        return changed;
    }

    private static bool MigrateRect(Transform root, string name, Vector2 oldSize, Vector2 oldPosition,
                                    Vector2 newSize, Vector2 newPosition) {
        Transform child = FindChild(root, name);
        RectTransform rect = child != null ? child.GetComponent<RectTransform>() : null;
        if (rect == null || rect.sizeDelta != oldSize || rect.anchoredPosition != oldPosition)
            return false;
        rect.sizeDelta = newSize;
        rect.anchoredPosition = newPosition;
        return true;
    }

    private static bool MigratePosition(Transform root, string name, Vector2 oldPosition, Vector2 newPosition) {
        Transform child = FindChild(root, name);
        RectTransform rect = child != null ? child.GetComponent<RectTransform>() : null;
        if (rect == null || rect.anchoredPosition != oldPosition)
            return false;
        rect.anchoredPosition = newPosition;
        return true;
    }

    private bool EnsureCodeFieldBox() {
        GameObject canvas = FindCanvasInScene();
        if (canvas == null)
            return false;
        Transform codeTextTransform = FindChild(canvas.transform, "Lobby Active Code");
        if (codeTextTransform == null)
            return false;

        Transform currentParent = codeTextTransform.parent;
        if (currentParent != null && currentParent.name == "Lobby Active Code Container")
            return false;

        RectTransform oldTextRect = codeTextTransform.GetComponent<RectTransform>();
        if (oldTextRect == null || currentParent == null)
            return false;

        Vector2 oldSize = oldTextRect.sizeDelta;
        Vector2 oldPosition = oldTextRect.anchoredPosition;
        RectTransform container = CreateRect("Lobby Active Code Container", currentParent, oldSize);
        container.anchoredPosition = oldPosition;
        container.gameObject.AddComponent<Image>().color = FieldColor;

        oldTextRect.SetParent(container, false);
        oldTextRect.anchorMin = Vector2.zero;
        oldTextRect.anchorMax = Vector2.one;
        oldTextRect.pivot = new Vector2(0.5f, 0.5f);
        oldTextRect.anchoredPosition = Vector2.zero;
        oldTextRect.offsetMin = new Vector2(10f, 2f);
        oldTextRect.offsetMax = new Vector2(-10f, -2f);
        Text codeText = codeTextTransform.GetComponent<Text>();
        if (codeText != null) {
            codeText.text = "Belum ada kode";
            codeText.color = new Color(0.58f, 0.65f, 0.76f);
        }
        return true;
    }

    private static bool MigrateCodeContainer(Transform root) {
        Transform containerTransform = FindChild(root, "Lobby Active Code Container");
        RectTransform rect = containerTransform != null ? containerTransform.GetComponent<RectTransform>() : null;
        if (rect == null)
            return false;

        Vector2 oldSize = rect.sizeDelta;
        Vector2 oldPosition = rect.anchoredPosition;
        if ((oldSize == new Vector2(180f, 34f) && oldPosition == new Vector2(-155f, -472f)) ||
            (oldSize == new Vector2(180f, 28f) && oldPosition == new Vector2(-155f, -472f))) {
            rect.sizeDelta = new Vector2(310f, 76f);
            rect.anchoredPosition = new Vector2(-95f, -462f);
            return true;
        }
        if (oldSize == new Vector2(310f, 76f) && oldPosition == new Vector2(-75f, -462f)) {
            rect.anchoredPosition = new Vector2(-95f, -462f);
            return true;
        }
        return false;
    }

    private bool EnsureRecentRoomsViewport() {
        GameObject canvas = FindCanvasInScene();
        if (canvas == null)
            return false;
        Transform viewportTransform = FindChild(canvas.transform, "Lobby Recent Rooms");
        if (viewportTransform == null)
            return false;

        bool changed = false;
        RectMask2D mask = viewportTransform.GetComponent<RectMask2D>();
        if (mask == null) {
            viewportTransform.gameObject.AddComponent<RectMask2D>();
            changed = true;
        }

        Image background = viewportTransform.GetComponent<Image>();
        if (background == null) {
            background = viewportTransform.gameObject.AddComponent<Image>();
            background.color = new Color(FieldColor.r, FieldColor.g, FieldColor.b, 0.45f);
            changed = true;
        }
        background.raycastTarget = true;

        ScrollRect scroll = viewportTransform.GetComponent<ScrollRect>();
        if (scroll == null) {
            scroll = viewportTransform.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 28f;
            changed = true;
        }
        RectTransform viewport = viewportTransform.GetComponent<RectTransform>();
        if (scroll.viewport != viewport) {
            scroll.viewport = viewport;
            changed = true;
        }

        Transform contentTransform = FindChild(viewportTransform, "Lobby Recent Rooms Content");
        RectTransform content;
        if (contentTransform == null) {
            content = CreateRect("Lobby Recent Rooms Content", viewportTransform, new Vector2(0f, 130f));
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            contentTransform = content;
            changed = true;
        } else {
            content = contentTransform.GetComponent<RectTransform>();
        }
        if (content == null)
            return changed;

        // Keep any existing rows while moving them under the scrollable content.
        for (int i = viewportTransform.childCount - 1; i >= 0; i--) {
            Transform child = viewportTransform.GetChild(i);
            if (child != contentTransform)
                child.SetParent(content, false);
        }
        if (scroll.content != content) {
            scroll.content = content;
            changed = true;
        }
        _recentScroll = scroll;
        _recentList = content;
        return changed;
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
            return GameObject.Find(WaitingCanvasRootName);
        foreach (GameObject root in gameObject.scene.GetRootGameObjects()) {
            if (root.name == WaitingCanvasRootName)
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

    private void BuildCanvasContents(Transform canvas) {
        RectTransform panel = CreateRect("Lobby Panel", canvas, new Vector2(600f, 800f));
        panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
        panel.pivot = new Vector2(0.5f, 0.5f);
        panel.anchoredPosition = Vector2.zero;
        panel.gameObject.AddComponent<Image>().color = PanelColor;
        _panel = panel.gameObject;

        CreateText("Lobby Title", panel, "GO VISTA LOBBY", 30, FontStyle.Bold,
                   TextAnchor.MiddleLeft, new Vector2(480f, 52f), new Vector2(-28f, -34f), Color.white);
        CreateText("Lobby Subtitle", panel, "Buat room, masukkan nama room, atau gunakan kode Quickmatch.", 15,
                   FontStyle.Normal, TextAnchor.MiddleLeft, new Vector2(510f, 34f), new Vector2(0f, -80f),
                   new Color(0.68f, 0.76f, 0.88f));
        CreateButton("Lobby Close Button", panel, "×", new Vector2(44f, 44f), new Vector2(260f, -34f),
                     new Color(0.75f, 0.2f, 0.22f));

        CreateText("Room Name Label", panel, "NAMA ROOM", 14, FontStyle.Bold, TextAnchor.MiddleLeft,
                   new Vector2(500f, 26f), new Vector2(0f, -122f), Color.white);
        CreateInputField("Lobby Room Input", panel, "Masukkan nama room", new Vector2(500f, 48f),
                         new Vector2(0f, -160f));
        CreateButton("Lobby Create Button", panel, "BUAT ROOM", new Vector2(240f, 48f),
                     new Vector2(-130f, -218f), AccentColor);
        CreateButton("Lobby Join Button", panel, "GABUNG", new Vector2(240f, 48f),
                     new Vector2(130f, -218f), new Color(0.18f, 0.42f, 0.62f));

        CreateButton("Lobby Quickmatch Button", panel, "CARI / BUAT ROOM OTOMATIS · QUICKMATCH",
                     new Vector2(500f, 46f), new Vector2(0f, -278f), new Color(0.4f, 0.28f, 0.72f));
        CreateInputField("Lobby Code Input", panel, "Kode Quickmatch", new Vector2(300f, 44f),
                         new Vector2(-100f, -334f));
        CreateButton("Lobby Join Code Button", panel, "GABUNG DENGAN KODE", new Vector2(190f, 44f),
                     new Vector2(155f, -334f), new Color(0.12f, 0.57f, 0.44f));

        _statusText = CreateText("Lobby Status", panel, "Siap. Buat room atau bergabung.", 15, FontStyle.Normal,
                                 TextAnchor.MiddleLeft, new Vector2(500f, 38f), new Vector2(0f, -384f),
                                 new Color(0.83f, 0.9f, 1f));
        _activeRoomText = CreateText("Lobby Active Room", panel, "Belum terhubung", 14, FontStyle.Bold,
                                     TextAnchor.MiddleLeft, new Vector2(330f, 28f), new Vector2(-85f, -426f),
                                     new Color(0.48f, 0.84f, 1f));
        RectTransform codeContainer = CreateRect("Lobby Active Code Container", panel, new Vector2(310f, 76f));
        codeContainer.anchoredPosition = new Vector2(-95f, -462f);
        codeContainer.gameObject.AddComponent<Image>().color = FieldColor;
        _activeCodeText = CreateText("Lobby Active Code", codeContainer, "Belum ada kode", 14, FontStyle.Normal,
                                     TextAnchor.MiddleLeft, new Vector2(160f, 28f), Vector2.zero,
                                     new Color(0.58f, 0.65f, 0.76f));
        _activeCodeText.rectTransform.anchorMin = Vector2.zero;
        _activeCodeText.rectTransform.anchorMax = Vector2.one;
        _activeCodeText.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        _activeCodeText.rectTransform.offsetMin = new Vector2(10f, 2f);
        _activeCodeText.rectTransform.offsetMax = new Vector2(-10f, -2f);
        CreateButton("Lobby Copy Code Button", panel, "SALIN KODE", new Vector2(130f, 38f),
                     new Vector2(165f, -455f), new Color(0.12f, 0.48f, 0.38f));
        CreateButton("Lobby Disconnect Button", panel, "PUTUS", new Vector2(130f, 38f),
                     new Vector2(165f, -503f), new Color(0.65f, 0.22f, 0.2f));

        CreateText("Recent Rooms Label", panel, "ROOM TERAKHIR DI PERANGKAT INI", 14, FontStyle.Bold,
                   TextAnchor.MiddleLeft, new Vector2(500f, 28f), new Vector2(0f, -560f), Color.white);
        CreateText("Global Rooms Note", panel,
                   "Daftar global perlu backend. Normcore belum menyediakan API untuk menampilkan semua room.",
                   12, FontStyle.Normal, TextAnchor.MiddleLeft, new Vector2(500f, 36f), new Vector2(0f, -590f),
                   new Color(0.68f, 0.76f, 0.88f));
        RectTransform recentRect = CreateRect("Lobby Recent Rooms", panel, new Vector2(500f, 130f));
        recentRect.anchorMin = recentRect.anchorMax = new Vector2(0.5f, 1f);
        recentRect.anchoredPosition = new Vector2(0f, -660f);
        recentRect.gameObject.AddComponent<Image>().color = new Color(FieldColor.r, FieldColor.g, FieldColor.b, 0.45f);
        recentRect.gameObject.AddComponent<RectMask2D>();
        _recentScroll = recentRect.gameObject.AddComponent<ScrollRect>();
        _recentScroll.horizontal = false;
        _recentScroll.vertical = true;
        _recentScroll.movementType = ScrollRect.MovementType.Clamped;
        _recentScroll.scrollSensitivity = 28f;
        _recentScroll.viewport = recentRect;
        RectTransform recentContent = CreateRect("Lobby Recent Rooms Content", recentRect, new Vector2(0f, 130f));
        recentContent.anchorMin = new Vector2(0f, 1f);
        recentContent.anchorMax = new Vector2(1f, 1f);
        recentContent.pivot = new Vector2(0.5f, 1f);
        recentContent.anchoredPosition = Vector2.zero;
        _recentScroll.content = recentContent;
        _recentList = recentContent;

        Button reopen = CreateButton("Lobby Reopen Button", canvas, "BUKA LOBBY", new Vector2(150f, 42f),
                                     new Vector2(0f, 0f), AccentColor);
        RectTransform reopenRect = reopen.GetComponent<RectTransform>();
        reopenRect.anchorMin = reopenRect.anchorMax = new Vector2(1f, 1f);
        reopenRect.anchoredPosition = new Vector2(-95f, -30f);
        reopen.gameObject.SetActive(false);
        _reopenButton = reopen;
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
        return text;
    }

    private static Button CreateButton(string name, Transform parent, string label, Vector2 rectSize,
                                       Vector2 position, Color color) {
        RectTransform rect = CreateRect(name, parent, rectSize);
        rect.anchoredPosition = position;
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.highlightedColor = Color.Lerp(color, Color.white, 0.18f);
        colors.pressedColor = Color.Lerp(color, Color.black, 0.18f);
        button.colors = colors;

        Text text = CreateText(name + " Label", rect, label, 14, FontStyle.Bold, TextAnchor.MiddleCenter,
                               rectSize - new Vector2(12f, 8f), Vector2.zero, Color.white);
        RectTransform textRect = text.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(6f, 4f);
        textRect.offsetMax = new Vector2(-6f, -4f);
        return button;
    }

    private static InputField CreateInputField(string name, Transform parent, string placeholder,
                                               Vector2 rectSize, Vector2 position) {
        RectTransform rect = CreateRect(name, parent, rectSize);
        rect.anchoredPosition = position;
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = FieldColor;
        InputField input = rect.gameObject.AddComponent<InputField>();
        input.targetGraphic = image;

        Text valueText = CreateText(name + " Text", rect, "", 15, FontStyle.Normal, TextAnchor.MiddleLeft,
                                    rectSize - new Vector2(24f, 8f), Vector2.zero, Color.white);
        valueText.rectTransform.anchorMin = Vector2.zero;
        valueText.rectTransform.anchorMax = Vector2.one;
        valueText.rectTransform.offsetMin = new Vector2(12f, 4f);
        valueText.rectTransform.offsetMax = new Vector2(-12f, -4f);

        Text placeholderText = CreateText(name + " Placeholder", rect, placeholder, 15, FontStyle.Italic,
                                          TextAnchor.MiddleLeft, rectSize - new Vector2(24f, 8f), Vector2.zero,
                                          new Color(0.58f, 0.65f, 0.76f));
        placeholderText.rectTransform.anchorMin = Vector2.zero;
        placeholderText.rectTransform.anchorMax = Vector2.one;
        placeholderText.rectTransform.offsetMin = new Vector2(12f, 4f);
        placeholderText.rectTransform.offsetMax = new Vector2(-12f, -4f);
        input.textComponent = valueText;
        input.placeholder = placeholderText;
        input.characterLimit = name == "Lobby Code Input" ? 16 : 64;
        return input;
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
        Transform activeRoomText = FindChild(canvas, "Lobby Active Room");
        Transform activeCodeText = FindChild(canvas, "Lobby Active Code");
        Transform recentList = FindChild(canvas, "Lobby Recent Rooms");
        Transform recentContent = recentList != null ? FindChild(recentList, "Lobby Recent Rooms Content") : null;
        Transform reopenButton = FindChild(canvas, "Lobby Reopen Button");
        if (panel == null || roomInput == null || codeInput == null || statusText == null ||
            activeRoomText == null || activeCodeText == null || recentList == null || recentContent == null ||
            reopenButton == null) {
            Debug.LogError("Loby Canvas belum lengkap. Tutup dan buka kembali MainScene agar UI dibuat ulang.");
            return;
        }

        _panel = panel.gameObject;
        _roomInput = roomInput.GetComponent<InputField>();
        _codeInput = codeInput.GetComponent<InputField>();
        _statusText = statusText.GetComponent<Text>();
        _activeRoomText = activeRoomText.GetComponent<Text>();
        _activeCodeText = activeCodeText.GetComponent<Text>();
        _recentList = recentContent;
        _recentScroll = recentList.GetComponent<ScrollRect>();
        _reopenButton = reopenButton.GetComponent<Button>();

        _waitingCanvas = FindWaitingCanvas();
        if (_waitingCanvas != null) {
            Transform waitingPanel = FindChild(_waitingCanvas.transform, "Room Waiting Panel");
            Transform waitingRoom = FindChild(_waitingCanvas.transform, "Waiting Room Name");
            Transform players = FindChild(_waitingCanvas.transform, "Waiting Player List");
            Transform joinButton = FindChild(_waitingCanvas.transform, "Waiting Join Game Button");
            Transform rosterToggle = FindChild(_waitingCanvas.transform, "Waiting Roster Toggle Button");
            _waitingPanel = waitingPanel != null ? waitingPanel.gameObject : null;
            _waitingRoomText = waitingRoom != null ? waitingRoom.GetComponent<Text>() : null;
            _playersText = players != null ? players.GetComponent<Text>() : null;
            _waitingJoinButton = joinButton != null ? joinButton.GetComponent<Button>() : null;
            _waitingRosterToggleButton = rosterToggle != null ? rosterToggle.GetComponent<Button>() : null;
            AddClickListenerIn(_waitingCanvas, "Waiting Join Game Button", BeginGame);
            AddClickListenerIn(_waitingCanvas, "Waiting Disconnect Button", Disconnect);
            AddClickListenerIn(_waitingCanvas, "Waiting Close Roster Button", CloseWaitingRoster);
            AddClickListenerIn(_waitingCanvas, "Waiting Roster Toggle Button", ToggleWaitingRoster);
        }
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
        string requestedRoom = _roomInput != null ? _roomInput.text.Trim() : "";
        if (string.IsNullOrEmpty(requestedRoom)) {
            SetStatus("Nama room harus diisi.");
            return;
        }
        if (requestedRoom.Length >= 512) {
            SetStatus("Nama room harus kurang dari 512 karakter.");
            return;
        }
        Connect(() => realtime.Connect(requestedRoom));
    }

    private void ConnectQuickmatch() {
        Connect(() => realtime.ConnectToNextAvailableQuickmatchRoom("go-vista", 10));
    }

    private void ConnectQuickmatchCode() {
        string code = _codeInput != null ? _codeInput.text.Trim() : "";
        if (string.IsNullOrEmpty(code)) {
            SetStatus("Masukkan kode Quickmatch terlebih dahulu.");
            return;
        }
        Connect(() => realtime.ConnectDirectlyToQuickmatchRoom("go-vista", code));
    }

    private void Connect(System.Action connect) {
        if (realtime == null || _busy)
            return;
        _busy = true;
        SetStatus("Menghubungkan ke Normcore…");
        try {
            connect();
        } catch (System.Exception exception) {
            _busy = false;
            SetStatus("Gagal memulai koneksi: " + exception.Message);
            Debug.LogException(exception);
        }
    }

    private void DidConnectToRoom(Realtime connectedRealtime) {
        _busy = false;
        _roomFullDisconnectRequested = false;
        string roomCode = connectedRealtime.room != null ? connectedRealtime.room.quickmatchRoomCode : null;
        SetStatus("Terhubung.");
        if (_activeRoomText != null)
            _activeRoomText.text = "Room: " + connectedRealtime.roomName;
        if (_activeCodeText != null) {
            bool hasCode = !string.IsNullOrEmpty(roomCode);
            _activeCodeText.text = hasCode ? roomCode : "Belum ada kode";
            _activeCodeText.color = hasCode ? new Color(0.57f, 0.93f, 0.73f) : new Color(0.58f, 0.65f, 0.76f);
        }
        if (_codeInput != null && !string.IsNullOrEmpty(roomCode))
            _codeInput.text = roomCode;
        if (_panel != null)
            _panel.SetActive(false);
        if (_reopenButton != null)
            _reopenButton.gameObject.SetActive(false);
        if (_waitingRoomText != null)
            _waitingRoomText.text = "Room: " + connectedRealtime.roomName;
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
    }

    private void DidDisconnectFromRoom(Realtime disconnectedRealtime) {
        _busy = false;
        if (_waitingCanvas != null)
            _waitingCanvas.SetActive(false);
        SetWaitingJoinButtonJoined(false);
        if (_waitingPanel != null)
            _waitingPanel.SetActive(true);
        if (_waitingRosterToggleButton != null)
            _waitingRosterToggleButton.gameObject.SetActive(false);
        if (_panel != null)
            _panel.SetActive(true);
        SetStatus("Terputus dari room.");
        if (_activeRoomText != null)
            _activeRoomText.text = "Belum terhubung";
        if (_activeCodeText != null) {
            _activeCodeText.text = "Belum ada kode";
            _activeCodeText.color = new Color(0.58f, 0.65f, 0.76f);
        }
    }

    private void DidDisconnectFromRoomWithEvent(Realtime disconnectedRealtime, DisconnectEvent disconnectEvent) {
        _busy = false;

        // Quickmatch rooms are temporary. Normcore reports this when a saved
        // room name/code no longer resolves, so remove that stale card locally.
        if (disconnectEvent is QuickmatchRoomNotFound roomNotFound) {
            string expiredRoom = !string.IsNullOrEmpty(disconnectEvent.quickmatchRoomCode)
                ? "qm:" + disconnectEvent.quickmatchRoomCode
                : roomNotFound.requestedRoomName;
            ForgetRoom(expiredRoom);
            RefreshRecentRooms();
            SetStatus("Room sudah kedaluwarsa dan dihapus dari daftar.");
            return;
        }

        if (disconnectEvent != null && !string.IsNullOrEmpty(disconnectEvent.message))
            SetStatus("Gagal masuk room: " + disconnectEvent.message);
    }

    private void CloseLobby() {
        if (_panel != null)
            _panel.SetActive(false);
        if (_reopenButton != null)
            _reopenButton.gameObject.SetActive(true);
    }

    private IEnumerator RefreshWaitingPlayerList() {
        while (realtime != null && realtime.connected) {
            var playerIds = new SortedSet<int>();
            foreach (HoverbirdPlayer player in FindObjectsByType<HoverbirdPlayer>(FindObjectsSortMode.None)) {
                RealtimeView view = player.GetComponent<RealtimeView>();
                if (view != null && view.ownerIDSelf >= 0)
                    playerIds.Add(view.ownerIDSelf);
            }

            // Quickmatch enforces this capacity on Normcore's server. For a
            // directly named room, eject the newest client if more than ten
            // player prefabs appear, since named-room capacity is not exposed
            // by Normcore's client API.
            if (!_roomFullDisconnectRequested && playerIds.Count > MaxRoomPlayers) {
                int newestClientId = -1;
                foreach (int playerId in playerIds)
                    newestClientId = playerId;
                if (realtime.clientID == newestClientId) {
                    _roomFullDisconnectRequested = true;
                    SetStatus("Room penuh (maksimal 10 player).");
                    realtime.Disconnect();
                    _playerListRoutine = null;
                    yield break;
                }
            }

            if (_playersText != null) {
                if (playerIds.Count == 0) {
                    _playersText.text = "Menunggu player...";
                } else {
                    var lines = new List<string>();
                    foreach (int playerId in playerIds) {
                        string playerName = "Player " + (playerId + 1);
                        if (playerId == realtime.clientID)
                            playerName += " (Kamu)";
                        lines.Add(playerName + "  •  Joined");
                    }
                    _playersText.text = string.Join("\n", lines);
                }
            }
            yield return new WaitForSeconds(1f);
        }
        _playerListRoutine = null;
    }

    private void BeginGame() {
        HoverbirdPlayerManager playerManager = FindFirstObjectByType<HoverbirdPlayerManager>();
        if (playerManager == null) {
            SetStatus("Hoverbird Player Manager tidak ditemukan.");
            return;
        }

        playerManager.BeginGame();
        SetWaitingJoinButtonJoined(true);
        if (_waitingPanel != null)
            _waitingPanel.SetActive(false);
        if (_waitingRosterToggleButton != null)
            _waitingRosterToggleButton.gameObject.SetActive(true);
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

    private void SetWaitingJoinButtonJoined(bool joined) {
        if (_waitingJoinButton == null)
            return;

        _waitingJoinButton.interactable = !joined;
        Transform label = _waitingJoinButton.transform.Find("Waiting Join Game Button Label");
        Text labelText = label != null ? label.GetComponent<Text>() : null;
        if (labelText != null)
            labelText.text = joined ? "SUDAH JOIN GAME" : "JOIN GAME";
    }

    private void OpenLobby() {
        if (_panel != null)
            _panel.SetActive(true);
        if (_reopenButton != null)
            _reopenButton.gameObject.SetActive(false);
    }

    private void Disconnect() {
        if (realtime != null && realtime.connected)
            realtime.Disconnect();
    }

    private void CopyQuickmatchCode() {
        if (realtime == null || realtime.room == null || string.IsNullOrEmpty(realtime.room.quickmatchRoomCode)) {
            SetStatus("Room ini tidak memiliki kode Quickmatch.");
            return;
        }
        GUIUtility.systemCopyBuffer = realtime.room.quickmatchRoomCode;
        SetStatus("Kode room disalin.");
    }

    private void SetStatus(string status) {
        if (_statusText != null)
            _statusText.text = status;
    }

    private void RefreshRecentRooms() {
        if (_recentList == null || _recentScroll == null)
            return;

        for (int i = _recentList.childCount - 1; i >= 0; i--) {
            GameObject existing = _recentList.GetChild(i).gameObject;
            if (Application.isPlaying)
                Destroy(existing);
            else
                DestroyImmediate(existing);
        }

        List<string> rooms = GetRecentRooms();
        RectTransform contentRect = _recentList.GetComponent<RectTransform>();
        float rowHeight = 34f;
        float rowSpacing = 6f;
        contentRect.sizeDelta = new Vector2(0f, Mathf.Max(_recentScroll.viewport.rect.height,
                                                        8f + rooms.Count * (rowHeight + rowSpacing)));
        int visibleCount = Mathf.Min(rooms.Count, MaxRecentRooms);
        for (int i = 0; i < visibleCount; i++) {
            string room = rooms[i];
            bool isQuickmatch = room.StartsWith("qm:");
            string roomCode = isQuickmatch ? room.Substring(3) : null;
            string label = isQuickmatch ? "Kode: " + roomCode : room;
            Button button = CreateButton("Recent Room " + i, _recentList, label,
                                         new Vector2(0f, rowHeight), new Vector2(0f, -4f - i * (rowHeight + rowSpacing)),
                                         FieldColor);
            RectTransform buttonRect = button.GetComponent<RectTransform>();
            buttonRect.anchorMin = new Vector2(0f, 1f);
            buttonRect.anchorMax = new Vector2(1f, 1f);
            buttonRect.pivot = new Vector2(0.5f, 1f);
            button.onClick.AddListener(() => {
                if (_roomInput != null)
                    _roomInput.text = room;
                if (isQuickmatch)
                    Connect(() => realtime.ConnectDirectlyToQuickmatchRoom("go-vista", roomCode));
                else
                    Connect(() => realtime.Connect(room));
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
