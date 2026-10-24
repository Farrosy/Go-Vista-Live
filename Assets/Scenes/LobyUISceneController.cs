using System.Collections.Generic;
using Normal.Realtime;
using UnityEngine;

/// <summary>
/// A lightweight Normcore lobby. Normcore does not expose a public API for
/// enumerating arbitrary named rooms, so this UI offers recent local rooms,
/// direct room-name joins, and Normcore Quickmatch.
/// </summary>
public sealed class LobyUISceneController : MonoBehaviour {
    [SerializeField] private Realtime realtime;

    private const string RecentRoomsKey = "LobyUIScene.RecentRooms";
    private const int MaxRecentRooms = 8;
    private string _roomName = "";
    private string _quickmatchCode = "";
    private string _status = "Masukkan nama room untuk membuat atau bergabung.";
    private Vector2 _recentScroll;
    private bool _busy;
    private bool _showLobby = true;
    private GUIStyle _panelStyle;
    private GUIStyle _titleStyle;
    private GUIStyle _statusStyle;

    private void Awake() {
        if (realtime == null)
            realtime = GetComponent<Realtime>();

        if (realtime == null) {
            _status = "Komponen Normcore Realtime belum dipasang di scene.";
            return;
        }

        // Join Room On Start is disabled on this scene's Realtime component;
        // connections are started explicitly from the lobby UI.
        realtime.didConnectToRoom += DidConnectToRoom;
        realtime.didDisconnectFromRoom += DidDisconnectFromRoom;
    }

    private void OnDestroy() {
        if (realtime == null)
            return;

        realtime.didConnectToRoom -= DidConnectToRoom;
        realtime.didDisconnectFromRoom -= DidDisconnectFromRoom;
    }

    private void DidConnectToRoom(Realtime connectedRealtime) {
        _busy = false;
        _showLobby = false;
        _status = "Terhubung ke room: " + connectedRealtime.roomName;
        if (connectedRealtime.room != null && !string.IsNullOrEmpty(connectedRealtime.room.quickmatchRoomCode)) {
            _quickmatchCode = connectedRealtime.room.quickmatchRoomCode;
            _status += " · kode Quickmatch: " + _quickmatchCode;
        }
        RememberRoom(connectedRealtime.roomName);
    }

    private void DidDisconnectFromRoom(Realtime disconnectedRealtime) {
        _busy = false;
        _status = "Terputus dari room.";
    }

    private void OnGUI() {
        EnsureStyles();

        if (!_showLobby) {
            GUILayout.BeginArea(new Rect(16f, 16f, 480f, 86f), GUI.skin.box);
            GUILayout.Label("Room: " + (realtime != null ? realtime.roomName : ""));
            GUILayout.BeginHorizontal();
            if (!string.IsNullOrEmpty(_quickmatchCode)) {
                GUILayout.Label("Kode Quickmatch: " + _quickmatchCode);
                if (GUILayout.Button("Salin Kode", GUILayout.Width(100f)))
                    GUIUtility.systemCopyBuffer = _quickmatchCode;
            } else {
                GUILayout.Label("Bagikan nama room ini untuk mengundang pemain.");
            }
            if (GUILayout.Button("Lobby", GUILayout.Width(76f)))
                _showLobby = true;
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
            return;
        }

        float panelWidth = Mathf.Min(620f, Screen.width - 32f);
        float panelHeight = Mathf.Min(640f, Screen.height - 32f);
        Rect panel = new Rect((Screen.width - panelWidth) * 0.5f,
                              (Screen.height - panelHeight) * 0.5f,
                              panelWidth, panelHeight);
        GUILayout.BeginArea(panel, _panelStyle);
        GUILayout.Space(10f);
        GUILayout.Label("GO VISTA · LOBBY", _titleStyle);
        GUILayout.Label("Normcore room connection", GUILayout.Height(24f));
        GUILayout.Space(16f);

        GUILayout.Label("Nama room", GUILayout.Height(24f));
        _roomName = GUILayout.TextField(_roomName, 64, GUILayout.Height(42f));
        GUILayout.Space(8f);

        GUILayout.BeginHorizontal();
        GUI.enabled = !_busy && realtime != null;
        if (GUILayout.Button("Buat Room", GUILayout.Height(44f)))
            ConnectByName();
        if (GUILayout.Button("Gabung", GUILayout.Height(44f)))
            ConnectByName();
        GUI.enabled = true;
        GUILayout.EndHorizontal();

        GUILayout.Space(8f);
        GUI.enabled = !_busy && realtime != null;
        if (GUILayout.Button("Cari / Buat Room Otomatis (Quickmatch)", GUILayout.Height(42f)))
            ConnectQuickmatch();
        GUI.enabled = true;

        GUILayout.Space(8f);
        GUILayout.BeginHorizontal();
        _quickmatchCode = GUILayout.TextField(_quickmatchCode, 16, GUILayout.Height(38f));
        GUI.enabled = !_busy && realtime != null;
        if (GUILayout.Button("Gabung dengan Kode", GUILayout.Width(190f), GUILayout.Height(38f)))
            ConnectQuickmatchCode();
        GUI.enabled = true;
        GUILayout.EndHorizontal();

        GUILayout.Space(14f);
        GUILayout.Label(_busy ? "Menghubungkan…" : _status, _statusStyle);
        if (realtime != null && realtime.connected) {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Room aktif: " + realtime.roomName);
            if (GUILayout.Button("Putus", GUILayout.Width(90f), GUILayout.Height(32f)))
                realtime.Disconnect();
            GUILayout.EndHorizontal();
        }

        GUILayout.Space(18f);
        GUILayout.Label("Room terakhir di perangkat ini", GUILayout.Height(28f));
        GUILayout.Label("Normcore tidak menyediakan daftar global untuk room bernama biasa. Room ini tersimpan lokal.",
                        GUILayout.Height(40f));

        List<string> recentRooms = GetRecentRooms();
        _recentScroll = GUILayout.BeginScrollView(_recentScroll, GUILayout.ExpandHeight(true));
        if (recentRooms.Count == 0) {
            GUILayout.Label("Belum ada room terakhir.");
        } else {
            foreach (string room in recentRooms) {
                GUILayout.BeginHorizontal("box");
                GUILayout.Label(room, GUILayout.ExpandWidth(true));
                GUI.enabled = !_busy && realtime != null;
                if (GUILayout.Button("Gabung", GUILayout.Width(100f), GUILayout.Height(32f))) {
                    _roomName = room;
                    ConnectByName();
                }
                GUI.enabled = true;
                GUILayout.EndHorizontal();
            }
        }
        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    private void ConnectByName() {
        string requestedRoom = _roomName.Trim();
        if (string.IsNullOrEmpty(requestedRoom)) {
            _status = "Nama room harus diisi.";
            return;
        }

        if (requestedRoom.Length >= 512) {
            _status = "Nama room harus kurang dari 512 karakter.";
            return;
        }

        Connect(() => realtime.Connect(requestedRoom));
    }

    private void ConnectQuickmatch() {
        // Normcore reuses a room in this group when capacity remains, otherwise
        // it provisions a room with the requested capacity.
        Connect(() => realtime.ConnectToNextAvailableQuickmatchRoom("go-vista", 8));
    }

    private void ConnectQuickmatchCode() {
        string code = _quickmatchCode.Trim();
        if (string.IsNullOrEmpty(code)) {
            _status = "Masukkan kode Quickmatch terlebih dahulu.";
            return;
        }

        Connect(() => realtime.ConnectDirectlyToQuickmatchRoom("go-vista", code));
    }

    private void Connect(System.Action connect) {
        if (realtime == null || _busy)
            return;

        _busy = true;
        _status = "Menghubungkan ke Normcore…";
        try {
            connect();
        } catch (System.Exception exception) {
            _busy = false;
            _status = "Gagal memulai koneksi: " + exception.Message;
            Debug.LogException(exception);
        }
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

    private void EnsureStyles() {
        if (_panelStyle != null)
            return;

        _panelStyle = new GUIStyle(GUI.skin.box) {
            alignment = TextAnchor.UpperLeft,
            padding = new RectOffset(24, 24, 18, 22),
            fontSize = 16
        };
        _titleStyle = new GUIStyle(GUI.skin.label) {
            fontSize = 28,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft
        };
        _statusStyle = new GUIStyle(GUI.skin.label) {
            wordWrap = true,
            normal = { textColor = new Color(0.85f, 0.92f, 1f) }
        };
    }
}
