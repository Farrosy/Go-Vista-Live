/// Aturan proximity voice, tanpa ketergantungan Unity supaya bisa dites di EditMode.
public static class VoiceRules {
    /// Di bawah jarak ini (meter) suara terdengar penuh.
    public const float FullVolumeDistance = 3f;
    /// Di jarak ini (meter) suara benar-benar hilang.
    public const float SilentDistance = 25f;
    /// Batas RealtimeAvatarVoice.voiceVolume (0-1) untuk dianggap sedang bicara.
    public const float SpeakingThreshold = 0.08f;

    /// True kalau suara pemain lain tidak boleh diputar di device ini.
    public static bool ShouldSilenceRemote(bool sameVenue, bool speakerHidden, bool mutedByListener) =>
        !sameVenue || speakerHidden || mutedByListener;

    public static bool IsSpeaking(float voiceVolume) => voiceVolume >= SpeakingThreshold;
}
