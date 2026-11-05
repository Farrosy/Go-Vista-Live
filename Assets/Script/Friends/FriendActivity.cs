using System;
using UnityEngine.Scripting;

/// Data aktivitas yang dibagikan ke teman lewat Presence Unity Friends (diserialisasi JSON oleh SDK).
/// room = string room yang sama dengan "room terakhir" di lobby: nama room privat, atau "qm:KODE" untuk
/// room Quickmatch. Kosong = sedang di menu (tidak di room). Dipakai tombol GABUNG di menu teman.
[Serializable, Preserve]
public sealed class FriendActivity {
    /// Kapasitas room (sama dengan LobyUISceneController.RoomCapacity). Room penuh = tombol PENUH.
    public const int RoomCapacity = 10;

    public string room = "";
    public int players;

    [Preserve]
    public FriendActivity() { }

    public FriendActivity(string room, int players) {
        this.room = room ?? "";
        this.players = players;
    }

    public bool IsInRoom => !string.IsNullOrEmpty(room);
    public bool IsFull => players >= RoomCapacity;

    /// Teks room untuk ditampilkan: kode Quickmatch tanpa awalan "qm:", atau nama room privat.
    public string DisplayRoom => room != null && room.StartsWith("qm:") ? room.Substring(3) : room ?? "";
}
