using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Popup HADIAH LOGIN HARIAN. Prefab: Assets/Resources/UI/DailyLoginPopup.prefab
/// (dibangun Go Vista > Desain Baru > Hadiah Login Harian; tampilannya boleh diedit langsung di prefab).
///
/// Muncul setiap pemain masuk room (LobyUISceneController memanggil ShowIfAvailable) selama masih ada
/// hadiah di tabungan, dan bisa dibuka lagi kapan saja lewat tombol Hadiah di HUD (DailyLoginHudButton).
/// Satu tombol mengklaim semua tabungan. Aturannya: DailyLoginRules.
/// Selama terbuka, gerak pemain dikunci (PlayerInputHub.SetPanelLocked), sama seperti panel Quest.
/// </summary>
public sealed class DailyLoginPopup : MonoBehaviour {
    public const string ResourcePath = "UI/DailyLoginPopup";

    [Serializable]
    public sealed class DayTile {
        public Image background;
        public CanvasGroup group;
        public GameObject todayTag;
        public GameObject checkBadge;
        public TMP_Text amount;
    }

    [Tooltip("Tile Hari 1..7, urut. Hari 7 = tile besar.")]
    [SerializeField] private DayTile[] tiles = new DayTile[DailyLoginRules.CycleDays];

    [Header("Sprite tile")]
    [SerializeField] private Sprite tileNormal;
    [SerializeField] private Sprite tileReady;
    [SerializeField] private Sprite bigTileNormal;
    [SerializeField] private Sprite bigTileReady;

    [Header("Banner, tombol, toast")]
    [SerializeField] private TMP_Text bannerText;
    [SerializeField] private Button claimButton;
    [SerializeField] private Image claimImage;
    [SerializeField] private TMP_Text claimLabel;
    [SerializeField] private GameObject claimIcon;
    [SerializeField] private Sprite claimSprite;
    [SerializeField] private Sprite claimDisabledSprite;
    [SerializeField] private Button closeButton;
    [SerializeField] private GameObject toast;
    [SerializeField] private TMP_Text toastText;
    [Tooltip("Popup tertutup sendiri sekian detik setelah klaim.")]
    [SerializeField] private float closeAfterClaimSeconds = 1.6f;

    [Header("Tampilan hari")]
    [SerializeField] private float missedAlpha = 0.45f;
    [SerializeField] private float claimedAlpha = 0.6f;

    private static DailyLoginPopup _instance;
    private bool _open;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => _instance = null;

    /// <summary>
    /// Dipanggil saat pemain masuk room: catat login hari ini, lalu tampilkan popup kalau ada tabungan.
    /// Tidak melakukan apa-apa kalau progres akun belum dimuat.
    /// </summary>
    public static void ShowIfAvailable() {
        if (!ProgressService.IsLoaded) return;
        DailyLoginService.RegisterToday();
        if (DailyLoginService.SavedGold <= 0) return;
        if (EnsureInstance()) _instance.Open();
    }

    /// <summary>
    /// Tombol Hadiah di HUD: buka popup walau tabungan kosong (semua sudah diklaim), supaya pemain bisa melihat
    /// progres minggu ini. Login hari ini juga dicatat dulu, misalnya kalau hari sudah berganti selama bermain.
    /// </summary>
    public static void Show() {
        if (!ProgressService.IsLoaded || IsOpen) return;
        DailyLoginService.RegisterToday();
        if (EnsureInstance()) _instance.Open();
    }

    private static bool EnsureInstance() {
        if (_instance != null) return true;
        GameObject prefab = Resources.Load<GameObject>(ResourcePath);
        if (prefab == null) {
            Debug.LogWarning($"[DailyLogin] Resources/{ResourcePath}.prefab tidak ada. " +
                             "Jalankan Go Vista > Desain Baru > Hadiah Login Harian.");
            return false;
        }
        _instance = Instantiate(prefab).GetComponent<DailyLoginPopup>();
        _instance.name = prefab.name;
        DontDestroyOnLoad(_instance.gameObject);
        return true;
    }

    public static bool IsOpen => _instance != null && _instance._open;

    /// <summary>Tutup tanpa klaim (tombol Back HP, keluar room). Tabungan tetap ada.</summary>
    public static void HideIfOpen() {
        if (_instance != null && _instance._open) _instance.Close();
    }

    private void Awake() {
        if (claimButton != null) claimButton.onClick.AddListener(Claim);
        if (closeButton != null) closeButton.onClick.AddListener(Close);
    }

    private void Open() {
        StopAllCoroutines();
        _open = true;
        gameObject.SetActive(true);
        if (toast != null) toast.SetActive(false);
        if (claimButton != null) claimButton.interactable = true;
        if (PlayerInputHub.Instance != null) PlayerInputHub.Instance.SetPanelLocked(true);
        Refresh();
    }

    private void Close() {
        StopAllCoroutines();
        _open = false;
        if (PlayerInputHub.Instance != null) PlayerInputHub.Instance.SetPanelLocked(false);
        gameObject.SetActive(false);
    }

    private void Claim() {
        int gold = DailyLoginService.ClaimAll();
        Refresh();
        if (gold <= 0) return;
        if (claimButton != null) claimButton.interactable = false;
        if (toast != null) {
            if (toastText != null) toastText.text = $"+{CoinRules.FormatBalance(gold)} G masuk ke saldo kamu!";
            toast.SetActive(true);
        }
        StartCoroutine(CloseLater());
    }

    private IEnumerator CloseLater() {
        yield return new WaitForSecondsRealtime(closeAfterClaimSeconds);
        Close();
    }

    private void Refresh() {
        DailyLoginProgress progress = DailyLoginService.Progress;
        DailyLoginCheck check = DailyLoginService.LastCheck;
        int today = Mathf.Max(1, check.Today);

        for (int i = 0; i < tiles.Length; i++) {
            DayTile tile = tiles[i];
            if (tile == null) continue;
            int day = i + 1;
            DailyDayState state = DailyLoginRules.StateOf(progress, day, today);
            bool big = day == DailyLoginRules.CycleDays;
            bool ready = state == DailyDayState.Saved;
            if (tile.background != null)
                tile.background.sprite = big ? (ready ? bigTileReady : bigTileNormal) : (ready ? tileReady : tileNormal);
            if (tile.group != null)
                tile.group.alpha = state == DailyDayState.Missed ? missedAlpha
                                 : state == DailyDayState.Claimed ? claimedAlpha : 1f;
            if (tile.todayTag != null) tile.todayTag.SetActive(day == today);
            if (tile.checkBadge != null) tile.checkBadge.SetActive(state == DailyDayState.Claimed);
            if (tile.amount != null)
                tile.amount.text = state == DailyDayState.Claimed ? "Diklaim"
                                 : state == DailyDayState.Missed ? "Terlewat"
                                 : CoinRules.FormatBalance(DailyLoginRules.Reward(day)) + " G";
        }

        List<int> saved = DailyLoginRules.SavedDays(progress);
        int savedGold = DailyLoginRules.SavedGold(progress);
        if (bannerText != null) {
            bannerText.text = check.LostGold > 0
                ? $"Tabungan {CoinRules.FormatBalance(check.LostGold)} G hangus karena belum diklaim. Mulai lagi dari Hari 1."
                : savedGold > 0
                    ? $"Tabungan hadiah: {CoinRules.FormatBalance(savedGold)} G. Klaim sebelum hari ke-8, setelah itu hangus."
                    : "Semua hadiah sudah diklaim. Login lagi besok untuk menabung hadiah!";
        }

        bool canClaim = saved.Count > 0;
        if (claimImage != null) claimImage.sprite = canClaim ? claimSprite : claimDisabledSprite;
        if (claimIcon != null) claimIcon.SetActive(canClaim);
        if (claimButton != null && !canClaim) claimButton.interactable = false;
        if (claimLabel != null) {
            claimLabel.text = !canClaim ? "SUDAH DIKLAIM"
                            : saved.Count == 1 ? $"KLAIM HADIAH HARI {saved[0]}"
                            : $"KLAIM {saved.Count} HADIAH ({CoinRules.FormatBalance(savedGold)} G)";
        }
    }
}
