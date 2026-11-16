using System.Collections.Generic;
using Normal.Realtime;
using UnityEngine;

/// <summary>Cari avatar pemain berdasarkan clientID Normcore. Daftar avatar di-cache dan disegarkan tiap 0,5 detik.</summary>
public static class VehicleAvatars {
    private const float RefreshInterval = 0.5f;

    private static readonly Dictionary<int, PlayerController> byClient = new Dictionary<int, PlayerController>();
    private static float nextRefresh = float.NegativeInfinity;

    public static PlayerController Find(int clientId) {
        if (clientId < 0) return null;
        if (Time.unscaledTime >= nextRefresh) Refresh();
        return byClient.TryGetValue(clientId, out PlayerController avatar) && avatar ? avatar : null;
    }

    private static void Refresh() {
        nextRefresh = Time.unscaledTime + RefreshInterval;
        byClient.Clear();
        foreach (PlayerController avatar in Object.FindObjectsByType<PlayerController>(FindObjectsInactive.Exclude)) {
            RealtimeView view = avatar.GetComponent<RealtimeView>();
            int owner = view ? view.ownerIDInHierarchy : -1;
            if (owner >= 0) byClient[owner] = avatar;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() {
        byClient.Clear();
        nextRefresh = float.NegativeInfinity;
    }
}
