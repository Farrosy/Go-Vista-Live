using Normal.Realtime;
using UnityEngine;

/// Pasang di root prefab avatar (sebelah RealtimeView). Satu-satunya tempat yang menulis RealtimeAvatarVoice.mute:
///  - Avatar lokal: mute = mic pemain mati. Device mic tetap terbuka, tapi yang terkirim hanya hening.
///  - Avatar lain : mute = suaranya tidak diputar di device ini (beda venue, layar mati, atau dibisukan).
/// Dimulai dalam keadaan mute dan dinilai ulang tiap frame sebelum RealtimeAvatarVoice mengirim audio
/// (execution order -2 vs -1), supaya suara tidak pernah terkirim sebelum pemain menyalakan mic.
[DefaultExecutionOrder(-2)]
[RequireComponent(typeof(RealtimeAvatarVoice), typeof(PlayerVenueSync), typeof(AudioSource))]
public sealed class ProximityVoice : MonoBehaviour {
    /// Avatar pemain lokal, null selama belum ada avatar. Dipakai tombol mic.
    public static ProximityVoice Local { get; private set; }

    private RealtimeAvatarVoice _voice;
    private PlayerVenueSync _venueSync;

    public float VoiceVolume => _voice.voiceVolume;

    private void Awake() {
        _voice = GetComponent<RealtimeAvatarVoice>();
        _venueSync = GetComponent<PlayerVenueSync>();
        _voice.mute = true;
    }

    private void OnDisable() {
        if (Local == this) Local = null;
    }

    private void Update() {
        int ownerClientId = _venueSync.OwnerClientId;
        if (ownerClientId < 0) return; // Model Normcore belum siap: tetap mute.

        if (_venueSync.IsLocalAvatar) {
            Local = this;
            _voice.mute = !VoiceSession.MicEnabled;
            return;
        }

        bool sameVenue = _venueSync.Venue == VenueTravelManager.LocalVenue;
        _voice.mute = VoiceRules.ShouldSilenceRemote(sameVenue, _venueSync.IsHidden,
                                                     VoiceSession.IsPlayerMuted(ownerClientId));
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Local = null;
}
