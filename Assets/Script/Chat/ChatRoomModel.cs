using Normal.Realtime;
using Normal.Realtime.Serialization;

/// Datastore chat satu room Normcore. Dipakai RealtimeView scene di MainMultiplayerScene, jadi
/// tetap sama walau pemain pindah pulau/venue (pindah venue tidak mengganti room).
/// RealtimeSet (bukan RealtimeArray) supaya pesan lama bisa dihapus; urutan diambil dari sentAt.
[RealtimeModel]
public partial class ChatRoomModel {
    [RealtimeProperty(1, true)] private RealtimeSet<ChatMessageModel> _messages;
}
