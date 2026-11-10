using System;
using System.Collections.Generic;
using Normal.Realtime;
using Normal.Realtime.Serialization;
using UnityEngine;

public enum ChatSendResult { Sent, Empty, TooFast, NotConnected }

/// Chat satu room Normcore. Pasang bersama RealtimeView di object SCENE MainMultiplayerScene
/// (menu "Go Vista > Chat > Pasang Chat di MainMultiplayerScene"). Karena RealtimeView-nya milik scene
/// dan MainMultiplayerScene tidak pernah di-unload, chat tetap sama walau pemain pindah pulau/venue.
///
/// - Identitas pengirim = owner model pesan (dijaga server), nama diambil dari LobbyPresence owner itu.
/// - Penyimpanan dibatasi: tiap pengirim memangkas pesan tertua MILIKNYA kalau lebih dari
///   ChatRules.MaxStoredPerSender. Riwayat dihapus server saat pemain terakhir keluar room.
/// - Pesan sistem "X bergabung/keluar" dibuat LOKAL di tiap client dari LobbyPresence (tidak ke server).
[RequireComponent(typeof(RealtimeView))]
public sealed class ChatRoom : RealtimeComponent<ChatRoomModel> {
    public static ChatRoom Instance { get; private set; }

    /// Presence yang muncul sesaat setelah masuk room = pemain lama, bukan yang baru bergabung.
    private const float JoinAnnounceDelay = 2f;
    private const int MaxSystemMessages = 20;

    public event Action<ChatMessage> MessageAdded;
    public event Action<ChatMessage> MessageRemoved;
    /// Seluruh riwayat diganti (baru masuk room / keluar room). Bangun ulang tampilan dari Messages.
    public event Action HistoryReset;

    private readonly Dictionary<ChatMessageModel, ChatMessage> _byModel = new Dictionary<ChatMessageModel, ChatMessage>();
    private readonly List<ChatMessage> _sorted = new List<ChatMessage>();
    private readonly Queue<ChatMessage> _systemMessages = new Queue<ChatMessage>();
    private readonly Dictionary<int, string> _knownPlayers = new Dictionary<int, string>();
    private static readonly Comparer<ChatMessage> TimeOrder = Comparer<ChatMessage>.Create(ChatMessage.CompareByTime);
    private double _lastSentAt = double.NegativeInfinity;
    private float _announceAfter = float.PositiveInfinity;

    /// Semua pesan (pemain + sistem), urut dari yang terlama.
    public IReadOnlyList<ChatMessage> Messages => _sorted;

    public bool IsConnected => model != null && realtime != null && realtime.connected;

    private void Awake() {
        if (Instance != null && Instance != this) {
            Debug.LogWarning("[Chat] Ada lebih dari satu ChatRoom di scene; yang ini diabaikan.", this);
            return;
        }
        Instance = this;
    }

    private void OnEnable() {
        LobbyPresence.Ready += OnPresenceReady;
        LobbyPresence.Gone += OnPresenceGone;
    }

    private void OnDisable() {
        LobbyPresence.Ready -= OnPresenceReady;
        LobbyPresence.Gone -= OnPresenceGone;
    }

    private void OnDestroy() {
        if (Instance == this)
            Instance = null;
    }

    protected override void OnRealtimeModelReplaced(ChatRoomModel previousModel, ChatRoomModel currentModel) {
        if (previousModel != null) {
            previousModel.messages.modelAdded -= OnModelAdded;
            previousModel.messages.modelRemoved -= OnModelRemoved;
        }
        ClearHistory();

        if (currentModel != null) {
            // Riwayat yang sudah ada: pesan dari pemain yang sudah keluar boleh tanpa owner.
            foreach (ChatMessageModel messageModel in currentModel.messages)
                Track(messageModel, allowUnowned: true);
            currentModel.messages.modelAdded += OnModelAdded;
            currentModel.messages.modelRemoved += OnModelRemoved;
            RememberCurrentPlayers();
            _announceAfter = Time.unscaledTime + JoinAnnounceDelay;
        }
        HistoryReset?.Invoke();
    }

    // ---------------------------------------------------------------- Kirim

    /// Kirim pesan dari pemain lokal. Teks dirapikan dulu (ChatRules.Sanitize).
    public ChatSendResult TrySend(string rawText) {
        if (!IsConnected)
            return ChatSendResult.NotConnected;
        string text = ChatRules.Sanitize(rawText);
        if (text.Length == 0)
            return ChatSendResult.Empty;
        double now = realtime.room.time;
        if (!ChatRules.CanSend(now, _lastSentAt))
            return ChatSendResult.TooFast;

        int clientId = realtime.clientID;
        var messageModel = new ChatMessageModel {
            senderName = LobbyPresence.NameOf(clientId),
            senderId = clientId,
            text = text,
            sentAt = now,
            senderAccount = ChatIdentity.LocalKey,
        };
        // Owner = pengirim (server menolak owner selain clientID asli) dan tidak bisa diambil alih client lain.
        // Penghapusan saat room kosong TIDAK diatur per pesan: Normcore hanya mengizinkan flag itu di
        // RealtimeView root. Pesan ikut terhapus bersama Chat Room ("Destroy When Last Client Leaves").
        messageModel.SetOwnership(clientId);
        messageModel.preventOwnershipTakeover = true;
        model.messages.Add(messageModel);
        _lastSentAt = now;
        PruneOwnMessages(clientId);
        return ChatSendResult.Sent;
    }

    private void PruneOwnMessages(int clientId) {
        var own = new List<ChatMessageModel>();
        foreach (ChatMessageModel messageModel in model.messages) {
            if (messageModel.ownerIDSelf == clientId)
                own.Add(messageModel);
        }
        int excess = own.Count - ChatRules.MaxStoredPerSender;
        if (excess <= 0)
            return;
        own.Sort((a, b) => a.sentAt.CompareTo(b.sentAt));
        for (int i = 0; i < excess; i++)
            model.messages.Remove(own[i]);
    }

    // ---------------------------------------------------------------- Pesan pemain

    private void OnModelAdded(RealtimeSet<ChatMessageModel> set, ChatMessageModel messageModel, bool remote) {
        // Pesan baru WAJIB punya owner; tanpa owner berarti dikirim client yang dimodifikasi.
        ChatMessage message = Track(messageModel, allowUnowned: false);
        if (message != null)
            MessageAdded?.Invoke(message);
    }

    private void OnModelRemoved(RealtimeSet<ChatMessageModel> set, ChatMessageModel messageModel, bool remote) {
        if (!_byModel.TryGetValue(messageModel, out ChatMessage message))
            return;
        _byModel.Remove(messageModel);
        _sorted.Remove(message);
        MessageRemoved?.Invoke(message);
    }

    /// Ubah model jaringan jadi ChatMessage yang aman ditampilkan, lalu sisipkan sesuai urutan waktu.
    private ChatMessage Track(ChatMessageModel messageModel, bool allowUnowned) {
        if (messageModel == null || _byModel.ContainsKey(messageModel))
            return null;
        int ownerId = messageModel.ownerIDSelf;
        if (ownerId < 0 && !allowUnowned) {
            Debug.LogWarning("[Chat] Pesan tanpa owner diabaikan.");
            return null;
        }

        string text = ChatRules.Sanitize(messageModel.text);
        if (text.Length == 0)
            return null;
        int senderId = ownerId >= 0 ? ownerId : messageModel.senderId;
        bool isLocal = IsOwnMessage(ownerId, messageModel);
        string name = isLocal && realtime != null
            ? LobbyPresence.NameOf(realtime.clientID)
            : ResolveSenderName(ownerId, messageModel);
        int colorSeed = string.IsNullOrEmpty(messageModel.senderAccount)
            ? senderId
            : ChatRules.StableHash(messageModel.senderAccount);
        var message = ChatMessage.FromPlayer(senderId, colorSeed, name, text, messageModel.sentAt, isLocal);

        _byModel.Add(messageModel, message);
        Insert(message);
        return message;
    }

    /// Pesan milik pemain lokal?
    ///  - Owner = clientID kita sekarang (dijaga server): pasti milik kita.
    ///  - Pesan dari sesi sebelumnya (kita rejoin, clientID berubah): dikenali dari kunci akun, TAPI hanya kalau
    ///    owner lamanya tidak sedang dipakai pemain lain yang ada di room. Jadi pemain lain tidak bisa membuat
    ///    pesannya tampil sebagai "pesan kita" dengan menyalin kunci akun.
    private bool IsOwnMessage(int ownerId, ChatMessageModel messageModel) {
        if (realtime == null)
            return false;
        int localId = realtime.clientID;
        if (ownerId >= 0 && ownerId == localId)
            return true;
        if (!ChatIdentity.IsLocalKey(messageModel.senderAccount))
            return false;
        return ownerId < 0 || !TryGetPresenceName(ownerId, out _);
    }

    /// Nama dari LobbyPresence milik owner (hanya owner yang bisa menulisnya, dijaga server). Nama yang ditulis
    /// di pesan hanya dipakai kalau pengirim sudah tidak ada di room.
    private static string ResolveSenderName(int ownerId, ChatMessageModel messageModel) {
        if (ownerId >= 0 && TryGetPresenceName(ownerId, out string presenceName))
            return presenceName;
        int fallbackId = ownerId >= 0 ? ownerId : messageModel.senderId;
        return PlayerNameRules.DisplayName(messageModel.senderName, fallbackId);
    }

    private static bool TryGetPresenceName(int clientId, out string name) {
        foreach (LobbyPresence presence in LobbyPresence.All) {
            if (presence.ClientID != clientId)
                continue;
            name = PlayerNameRules.DisplayName(presence.PlayerName, clientId);
            return true;
        }
        name = null;
        return false;
    }

    // ---------------------------------------------------------------- Pesan sistem (join/leave)

    private void RememberCurrentPlayers() {
        _knownPlayers.Clear();
        foreach (LobbyPresence presence in LobbyPresence.All) {
            if (presence.ClientID >= 0)
                _knownPlayers[presence.ClientID] = LobbyPresence.NameOf(presence.ClientID);
        }
    }

    private void OnPresenceReady(LobbyPresence presence) {
        int clientId = presence.ClientID;
        if (!IsConnected || clientId < 0 || _knownPlayers.ContainsKey(clientId))
            return;
        string name = LobbyPresence.NameOf(clientId);
        _knownPlayers[clientId] = name;
        // Pemain yang sudah ada saat kita masuk / diri sendiri tidak diumumkan.
        if (presence.IsLocal || Time.unscaledTime < _announceAfter)
            return;
        AddSystemMessage(name + " bergabung ke room");
    }

    private void OnPresenceGone(LobbyPresence presence) {
        int clientId = presence.ClientID;
        if (!_knownPlayers.TryGetValue(clientId, out string name))
            return;
        _knownPlayers.Remove(clientId);
        // Saat kita sendiri keluar/terputus, semua presence ikut hilang: jangan diumumkan.
        if (!IsConnected || presence.IsLocal || Time.unscaledTime < _announceAfter)
            return;
        AddSystemMessage(name + " keluar dari room");
    }

    private void AddSystemMessage(string text) {
        var message = ChatMessage.CreateSystem(text, realtime.room.time);
        Insert(message);
        _systemMessages.Enqueue(message);
        MessageAdded?.Invoke(message);

        while (_systemMessages.Count > MaxSystemMessages) {
            ChatMessage oldest = _systemMessages.Dequeue();
            _sorted.Remove(oldest);
            MessageRemoved?.Invoke(oldest);
        }
    }

    // ---------------------------------------------------------------- Helper

    private void Insert(ChatMessage message) {
        int index = _sorted.BinarySearch(message, TimeOrder);
        _sorted.Insert(index < 0 ? ~index : index, message);
    }

    private void ClearHistory() {
        _byModel.Clear();
        _sorted.Clear();
        _systemMessages.Clear();
        _knownPlayers.Clear();
        _announceAfter = float.PositiveInfinity;
    }
}
