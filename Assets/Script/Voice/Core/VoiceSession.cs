using System.Collections.Generic;

/// Status voice pemain lokal selama satu room. Direset PlayerManager tiap masuk room baru,
/// jadi mic selalu mulai mati. Pemain yang dibisukan dicatat per clientID Normcore.
public static class VoiceSession {
    private static readonly HashSet<int> MutedPlayers = new HashSet<int>();

    public static bool MicEnabled { get; private set; }

    public static void ResetForNewRoom() {
        MicEnabled = false;
        MutedPlayers.Clear();
    }

    public static void ToggleMic() => MicEnabled = !MicEnabled;

    public static bool IsPlayerMuted(int clientId) => MutedPlayers.Contains(clientId);

    public static void TogglePlayerMuted(int clientId) {
        if (!MutedPlayers.Remove(clientId))
            MutedPlayers.Add(clientId);
    }
}
