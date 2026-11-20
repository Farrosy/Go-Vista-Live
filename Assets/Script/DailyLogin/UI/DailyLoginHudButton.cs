using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Tombol "Hadiah" di HUD, tepat di bawah tombol Quest: membuka lagi popup HADIAH LOGIN HARIAN setelah ditutup.
/// Tanda ! tampil selama masih ada hadiah tertabung yang belum diklaim.
/// Dibangun GameHudBuilder (Go Vista > HUD > Pasang Tombol Hadiah Login) di GameHudCanvas.prefab.
/// Tampil/sembunyi mengikuti tombol Quest: hanya saat ada avatar lokal, tidak saat layar mati atau ATUR KONTROL.
/// </summary>
public sealed class DailyLoginHudButton : MonoBehaviour {
    [SerializeField] private Canvas buttonCanvas;
    [SerializeField] private Button openButton;
    [SerializeField] private GameObject badge;

    private bool _badgeDirty = true;

    private void Awake() {
        openButton.onClick.AddListener(DailyLoginPopup.Show);
        badge.SetActive(false);
    }

    private void OnEnable() {
        DailyLoginService.Changed += MarkBadgeDirty;
        ProgressService.Loaded += MarkBadgeDirty;
        _badgeDirty = true;
    }

    private void OnDisable() {
        DailyLoginService.Changed -= MarkBadgeDirty;
        ProgressService.Loaded -= MarkBadgeDirty;
    }

    private void MarkBadgeDirty() => _badgeDirty = true;

    private void Update() {
        bool visible = PlayerController.Local != null && ProgressService.IsLoaded &&
                       !PlayerInputHub.IsDeathLocked && !ControlLayoutEditor.IsOpen && !GameplayHud.Hidden;
        buttonCanvas.enabled = visible;
        if (!visible || !_badgeDirty) return;
        _badgeDirty = false;
        badge.SetActive(DailyLoginService.SavedGold > 0);
    }
}
