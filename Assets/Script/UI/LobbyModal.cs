using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Modal lobby yang memblokir input di belakangnya (overlay layar penuh menangkap semua klik), supaya
/// tombol koneksi tidak bisa ditekan dua kali. Dua mode:
///  - Loading: spinner + pesan ("Mencari room…") + tombol BATAL opsional.
///  - Alert:   judul + pesan + tombol OK.
///
/// Prefab: Assets/Prefab/LobbyModalCanvas.prefab, diedit di Assets/Scenes/Multiplayer/LobbyModalScene.unity
/// (lihat LobbyUIEditScenes). Dibuat ulang lewat Go Vista > Lobby UI > Buat ulang LobbyModalCanvas.
/// </summary>
public sealed class LobbyModal : MonoBehaviour {
    public const string OverlayName = "Modal Overlay";
    private const float SpinnerDegreesPerSecond = 300f;

    [SerializeField] private GameObject overlay;
    [SerializeField] private Text titleText;
    [SerializeField] private Text messageText;
    [SerializeField] private RectTransform spinner;
    [SerializeField] private Button actionButton;
    [SerializeField] private Text actionLabel;

    private Action _onAction;
    private GameObject _previousSelection;
    private bool _isLoading;

    public bool IsOpen => overlay != null && overlay.activeSelf;
    public bool IsLoading => IsOpen && _isLoading;

    /// <summary>Isi referensi dari builder editor (prefab dibuat dari kode).</summary>
    public void Bind(GameObject overlayObject, Text title, Text message, RectTransform spinnerRect, Button button,
                     Text buttonLabel) {
        overlay = overlayObject;
        titleText = title;
        messageText = message;
        spinner = spinnerRect;
        actionButton = button;
        actionLabel = buttonLabel;
    }

    private void Awake() {
        if (actionButton != null)
            actionButton.onClick.AddListener(HandleAction);
        // Di scene edit overlay boleh terlihat; saat game berjalan modal selalu mulai tertutup.
        if (overlay != null)
            overlay.SetActive(false);
    }

    private void OnDestroy() {
        if (actionButton != null)
            actionButton.onClick.RemoveListener(HandleAction);
    }

    private void Update() {
        if (spinner != null && spinner.gameObject.activeInHierarchy)
            spinner.Rotate(0f, 0f, -SpinnerDegreesPerSecond * Time.unscaledDeltaTime);
    }

    /// <summary>Spinner + pesan. onCancel null = tanpa tombol BATAL.</summary>
    public void ShowLoading(string message, Action onCancel) {
        Show(null, message, true, onCancel != null ? "BATAL" : null, onCancel);
    }

    public void ShowAlert(string title, string message, Action onOk = null) {
        Show(title, message, false, "OK", onOk);
    }

    /// <summary>Tutup hanya kalau sedang mode loading (alert yang sedang tampil tidak ikut tertutup).</summary>
    public void HideLoading() {
        if (IsLoading)
            Hide();
    }

    public void Hide() {
        if (!IsOpen)
            return;
        overlay.SetActive(false);
        _onAction = null;
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem != null && _previousSelection != null && _previousSelection.activeInHierarchy)
            eventSystem.SetSelectedGameObject(_previousSelection);
        _previousSelection = null;
    }

    private void Show(string title, string message, bool showSpinner, string actionText, Action onAction) {
        if (overlay == null) {
            Debug.LogWarning("[LobbyModal] Referensi overlay kosong. Buat ulang prefab lewat menu Go Vista > Lobby UI.");
            return;
        }
        EventSystem eventSystem = EventSystem.current;
        if (!IsOpen && eventSystem != null)
            _previousSelection = eventSystem.currentSelectedGameObject;

        bool hasTitle = !string.IsNullOrEmpty(title);
        if (titleText != null) {
            titleText.gameObject.SetActive(hasTitle);
            titleText.text = hasTitle ? title : "";
        }
        if (messageText != null)
            messageText.text = message ?? "";
        if (spinner != null)
            spinner.gameObject.SetActive(showSpinner);

        bool hasAction = !string.IsNullOrEmpty(actionText);
        if (actionButton != null)
            actionButton.gameObject.SetActive(hasAction);
        if (actionLabel != null)
            actionLabel.text = hasAction ? actionText : "";
        _onAction = onAction;
        _isLoading = showSpinner;

        overlay.SetActive(true);
        // Fokus keyboard/gamepad pindah ke modal, supaya tombol di belakang tidak bisa di-"Submit".
        if (eventSystem != null)
            eventSystem.SetSelectedGameObject(hasAction && actionButton != null ? actionButton.gameObject : null);
    }

    private void HandleAction() {
        Action action = _onAction;
        Hide();
        action?.Invoke();
    }
}
