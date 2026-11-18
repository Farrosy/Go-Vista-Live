/// Posisi bersama HUD kiri atas (referensi 1920x1080, diukur dari kiri atas safe area), mengikuti storyboard:
/// minimap di pojok, koin GoVista di kanannya, lalu tombol chat dan mic berdampingan di bawah minimap.
/// Dipakai GameHudBuilder (minimap, koin), ChatPanelBuilder/ChatPanel (tombol chat), dan VoiceMicButton (tombol mic).
public static class HudLayout {
    /// Sprite minimap 372 px (@2x mockup); awalnya 264, diperbesar ke 300 (2026-11-18).
    public const float MinimapSize = 300f;
    public const float MinimapLeft = 40f;
    public const float MinimapTop = 16f;

    /// Koin GoVista di kanan minimap, atasnya rata dengan atas minimap.
    private const float CoinGapFromMinimap = 20f;
    public const float CoinLeft = MinimapLeft + MinimapSize + CoinGapFromMinimap;
    public const float CoinTop = MinimapTop;

    private const float ButtonsGapBelowMinimap = 16f;
    private const float ButtonsGap = 20f;
    public const float ButtonsTop = MinimapTop + MinimapSize + ButtonsGapBelowMinimap;

    /// Ukuran tombol tidak diubah: chat 104 (ChatTheme.ToggleSize), mic 96.
    public const float ChatButtonSize = 104f;
    public const float MicButtonSize = 96f;

    /// Kedua tombol dibuat rata tengah terhadap minimap.
    public const float ChatButtonLeft =
        MinimapLeft + (MinimapSize - (ChatButtonSize + ButtonsGap + MicButtonSize)) / 2f;
    public const float MicButtonLeft = ChatButtonLeft + ChatButtonSize + ButtonsGap;
    public const float MicButtonTop = ButtonsTop + (ChatButtonSize - MicButtonSize) / 2f;
}
