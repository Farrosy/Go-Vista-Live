using System;
using Unity.Services.Friends.Models;
using UnityEngine;

public enum FriendPlace { Offline, InLobby, InRoom }

/// Status teman yang sudah dibaca dari Presence Unity Friends (lihat Docs/Friends/menu-teman-1-daftar.png):
///  - InRoom  : online di room  → titik hijau, "Di room GV-7KQ2 · 4/10 pemain", tombol GABUNG / PENUH.
///  - InLobby : online di menu  → titik kuning, "Di lobby".
///  - Offline : titik abu-abu, "Terakhir online 2 jam lalu", baris diredupkan.
public readonly struct FriendStatus {
    public static readonly Color RoomColor = new Color32(0x22, 0xD3, 0xEE, 0xFF);
    public static readonly Color LobbyColor = new Color32(0xFB, 0xBF, 0x24, 0xFF);
    public static readonly Color OnlineDot = new Color32(0x34, 0xD3, 0x99, 0xFF);
    public static readonly Color OfflineColor = new Color32(0x8E, 0x9B, 0xBB, 0xFF);

    public readonly FriendPlace Place;
    public readonly FriendActivity Activity;
    public readonly string Label;

    public bool Online => Place != FriendPlace.Offline;
    public bool CanJoin => Place == FriendPlace.InRoom && !Activity.IsFull;
    public Color DotColor => Place == FriendPlace.InRoom ? OnlineDot : Place == FriendPlace.InLobby ? LobbyColor : OfflineColor;
    public Color LabelColor => Place == FriendPlace.InRoom ? RoomColor : Place == FriendPlace.InLobby ? LobbyColor : OfflineColor;

    private FriendStatus(FriendPlace place, FriendActivity activity, string label) {
        Place = place;
        Activity = activity ?? new FriendActivity();
        Label = label;
    }

    public static FriendStatus From(Member member) {
        Presence presence = member?.Presence;
        if (presence == null || !IsOnline(presence.Availability))
            return new FriendStatus(FriendPlace.Offline, null,
                                    "Terakhir online " + LastSeenText(presence != null ? presence.LastSeen : default));
        FriendActivity activity = ReadActivity(presence);
        if (!activity.IsInRoom)
            return new FriendStatus(FriendPlace.InLobby, activity, "Di lobby");
        string label = "Di room " + activity.DisplayRoom;
        if (activity.players > 0)
            label += " · " + activity.players + "/" + FriendActivity.RoomCapacity + " pemain";
        return new FriendStatus(FriendPlace.InRoom, activity, label);
    }

    private static bool IsOnline(Availability availability) =>
        availability == Availability.Online || availability == Availability.Busy || availability == Availability.Away;

    private static FriendActivity ReadActivity(Presence presence) {
        try {
            return presence.GetActivity<FriendActivity>() ?? new FriendActivity();
        } catch (Exception) {
            return new FriendActivity(); // Teman belum pernah mengirim aktivitas.
        }
    }

    private static string LastSeenText(DateTime lastSeen) {
        if (lastSeen == default)
            return "sudah lama";
        DateTime utc = lastSeen.Kind == DateTimeKind.Local ? lastSeen.ToUniversalTime() : lastSeen;
        TimeSpan ago = DateTime.UtcNow - utc;
        if (ago.TotalMinutes < 1) return "baru saja";
        if (ago.TotalHours < 1) return (int)ago.TotalMinutes + " menit lalu";
        if (ago.TotalDays < 1) return (int)ago.TotalHours + " jam lalu";
        if (ago.TotalDays < 2) return "kemarin";
        return (int)ago.TotalDays + " hari lalu";
    }
}
