/// Posisi bersama HUD (referensi 1920x1080, diukur dari tepi safe area), mengikuti storyboard: kiri atas
/// minimap di pojok, koin GoVista di kanannya, tombol chat dan mic di bawah minimap; kanan atas chip ping di kiri tombol pause.
/// Dipakai GameHudBuilder (minimap, koin, ping), ChatPanelBuilder/ChatPanel (tombol chat), dan VoiceMicButton (tombol mic).
public static class HudLayout {
    /// Sprite minimap 372 px (@2x mockup); awalnya 264, diperbesar ke 300 (2026-11-18).
    public const float MinimapSize = 300f;
    public const float MinimapLeft = 40f;
    public const float MinimapTop = 16f;

    /// Koin GoVista di kanan minimap, atasnya rata dengan atas minimap.
    private const float CoinGapFromMinimap = 20f;
    public const float CoinLeft = MinimapLeft + MinimapSize + CoinGapFromMinimap;
    public const float CoinTop = MinimapTop;
    /// Lebar grup koin: ikon 37 + pill 240 (GameHudBuilder.BuildCoin).
    public const float CoinGroupWidth = 277f;

    /// Chip ping (ikon WiFi + "45 ms") di pojok kanan atas, di kiri tombol pause dan rata tengah terhadapnya
    /// (2026-11-25). Tombol pause: 96x96, 30 dari kanan/atas safe area (LobyUISceneController, RoomWaitingCanvas).
    private const float PauseButtonMargin = 30f;
    private const float PauseButtonSize = 96f;
    private const float PingGapFromPause = 16f;
    public const float PingWidth = 176f;
    // pill_ping 64 px / 1,41 (tinggi sprite asli, bayangan bawah ikut di sprite).
    public const float PingHeight = 45f;
    /// Jarak kanan chip dari tepi kanan safe area (anchor kanan atas).
    public const float PingRight = PauseButtonMargin + PauseButtonSize + PingGapFromPause;
    public const float PingTop = PauseButtonMargin + (PauseButtonSize - PingHeight) / 2f;
    /// Tengah vertikal tombol pause, dipakai untuk merata-tengahkan peringatan koneksi.
    public const float PauseButtonCenterY = PauseButtonMargin + PauseButtonSize / 2f;

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
