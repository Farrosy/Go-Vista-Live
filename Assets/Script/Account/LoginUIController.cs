using System.Threading.Tasks;
using Normal.Realtime;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Layar login/register yang menutupi lobby sampai pemain masuk.
/// UI-nya dibuat lewat menu "Go Vista > Buat Login Canvas" dan bisa di-styling bebas dari Inspector;
/// logika akun ada di AuthService, progres di ProgressService.
/// </summary>
public sealed class LoginUIController : MonoBehaviour {
    [Header("Layar Login")]
    [SerializeField] private GameObject loginScreen;
    [SerializeField] private Text titleText;
    [SerializeField] private Text subtitleText;
    [SerializeField] private InputField usernameInput;
    [SerializeField] private InputField passwordInput;
    [SerializeField] private InputField confirmPasswordInput;
    [Tooltip("Objek yang hanya tampil di mode Daftar (label & input konfirmasi, petunjuk password).")]
    [SerializeField] private GameObject[] registerOnlyObjects;
    [SerializeField] private Text statusText;
    [SerializeField] private Button submitButton;
    [SerializeField] private Text submitLabel;
    [SerializeField] private Button switchModeButton;
    [SerializeField] private Text switchModeLabel;
    [SerializeField] private Button guestButton;
    [Tooltip("Objek yang hanya tampil di mode Masuk (tombol tamu dan catatannya).")]
    [SerializeField] private GameObject[] loginOnlyObjects;

    [Header("Event")]
    [Tooltip("Dipanggil setelah login DAN progres berhasil di-load.")]
    [SerializeField] private UnityEvent onSignedIn;
    [SerializeField] private UnityEvent onSignedOut;

    /// <summary>Dipanggil setelah login atau logout. Lobby memakai ini untuk memperbarui chip akun.</summary>
    public static event System.Action AccountChanged;

    private static readonly Color InfoColor = new Color(0.83f, 0.9f, 1f);
    private static readonly Color ErrorColor = new Color(1f, 0.55f, 0.5f);
    private static readonly Color SuccessColor = new Color(0.55f, 0.95f, 0.7f);

    private bool _registerMode;
    private bool _busy;
    private bool _alreadySignedIn;
    private Realtime _realtime;

    private void Awake() {
        if (!HasAllReferences()) {
            Debug.LogError("LoginUIController: ada referensi UI yang kosong. Hapus Login Canvas lalu buat ulang " +
                           "lewat menu Go Vista > Buat Login Canvas.", this);
            enabled = false;
            return;
        }

        submitButton.onClick.AddListener(Submit);
        switchModeButton.onClick.AddListener(() => SetMode(!_registerMode));
        guestButton.onClick.AddListener(SignInAsGuest);
        usernameInput.characterLimit = 20;
        passwordInput.characterLimit = 30;
        confirmPasswordInput.characterLimit = 30;

        // Scene dimuat ulang (misalnya kembali ke lobby setelah keluar room): akun dan progres masih aktif
        // di memori, jadi layar login disembunyikan sebelum frame pertama dan tidak sempat terlihat.
        _alreadySignedIn = AuthService.IsSignedIn && ProgressService.IsLoaded;
        if (_alreadySignedIn)
            loginScreen.SetActive(false);
    }

    private async void Start() {
        _realtime = FindFirstObjectByType<Realtime>();
        usernameInput.text = AuthService.LastUsername;
        SetMode(false);
        if (_alreadySignedIn) {
            AccountChanged?.Invoke();
            onSignedIn.Invoke();
            return;
        }
        ShowLoginScreen();

        SetBusy(true);
        SetStatus("Memeriksa sesi login...", InfoColor);
        bool restored = await AuthService.TryRestoreSessionAsync();
        if (this == null)
            return; // scene sudah ditutup saat menunggu

        SetBusy(false);
        if (restored)
            await FinishSignInAsync();
        else
            SetStatus("", InfoColor);
    }

    private async void Submit() {
        if (_busy)
            return;

        // Login sudah berhasil tapi load progres gagal: tombol MASUK cukup mencoba load lagi.
        if (AuthService.IsSignedIn && !ProgressService.IsLoaded) {
            await FinishSignInAsync();
            return;
        }

        string username = usernameInput.text;
        string password = passwordInput.text;
        if (_registerMode && password != confirmPasswordInput.text) {
            SetStatus("Konfirmasi password tidak sama.", ErrorColor);
            return;
        }

        bool registering = _registerMode;
        SetBusy(true);
        SetStatus(registering ? "Membuat akun..." : "Sedang masuk...", InfoColor);
        ServiceResult result = registering
            ? await AuthService.RegisterAsync(username, password)
            : await AuthService.SignInAsync(username, password);
        if (this == null)
            return;

        SetBusy(false);
        if (!result.Success) {
            SetStatus(result.Message, ErrorColor);
            return;
        }
        await FinishSignInAsync();
    }

    private async void SignInAsGuest() {
        if (_busy)
            return;

        SetBusy(true);
        SetStatus("Masuk sebagai tamu...", InfoColor);
        ServiceResult result = await AuthService.SignInAsGuestAsync();
        if (this == null)
            return;

        SetBusy(false);
        if (!result.Success) {
            SetStatus(result.Message, ErrorColor);
            return;
        }
        await FinishSignInAsync();
    }

    /// <summary>Setelah akun masuk: load progres dulu, baru buka lobby.</summary>
    private async Task FinishSignInAsync() {
        SetBusy(true);
        SetStatus("Memuat progres...", InfoColor);
        ServiceResult load = await ProgressService.LoadAsync();
        if (this == null)
            return;

        SetBusy(false);
        if (!load.Success) {
            // Tetap di layar login: tanpa progres ter-load, save diblokir supaya data cloud tidak tertimpa.
            SetStatus(load.Message + " Tekan MASUK untuk mencoba lagi.", ErrorColor);
            return;
        }

        passwordInput.text = "";
        confirmPasswordInput.text = "";
        loginScreen.SetActive(false);
        Debug.Log($"[Auth] Masuk sebagai {AuthService.DisplayName} (PlayerId: {AuthService.PlayerId})");
        AccountChanged?.Invoke();
        onSignedIn.Invoke();
    }

    /// <summary>Simpan progres, keluar dari room dan akun, lalu tampilkan layar login. Dipanggil dari lobby.</summary>
    public async void SignOut() {
        if (_busy)
            return;

        SetBusy(true);
        // Simpan dulu selagi masih login, baru keluar.
        await ProgressService.SaveAsync();
        if (this == null)
            return;

        if (_realtime != null && _realtime.connected)
            _realtime.Disconnect();
        ProgressService.Clear();
        AuthService.SignOut();
        SetBusy(false);
        ShowLoginScreen();
        SetMode(false);
        SetStatus("Kamu sudah keluar dari akun.", SuccessColor);
        AccountChanged?.Invoke();
        onSignedOut.Invoke();
    }

    private void SetMode(bool register) {
        _registerMode = register;
        titleText.text = register ? "DAFTAR AKUN" : "MASUK";
        subtitleText.text = register
            ? "Buat akun baru supaya progres kamu tersimpan."
            : "Masuk ke akun Go Vista kamu.";
        submitLabel.text = register ? "DAFTAR" : "MASUK";
        switchModeLabel.text = register ? "Sudah punya akun? Masuk" : "Belum punya akun? Daftar";
        foreach (GameObject item in registerOnlyObjects) {
            if (item != null)
                item.SetActive(register);
        }
        foreach (GameObject item in loginOnlyObjects) {
            if (item != null)
                item.SetActive(!register);
        }
        confirmPasswordInput.text = "";
        SetStatus("", InfoColor);
    }

    private void ShowLoginScreen() {
        loginScreen.SetActive(true);
    }

    private void SetBusy(bool busy) {
        _busy = busy;
        usernameInput.interactable = !busy;
        passwordInput.interactable = !busy;
        confirmPasswordInput.interactable = !busy;
        submitButton.interactable = !busy;
        switchModeButton.interactable = !busy;
        guestButton.interactable = !busy;
    }

    private void SetStatus(string message, Color color) {
        statusText.text = message;
        statusText.color = color;
    }

    private bool HasAllReferences() {
        return loginScreen != null && titleText != null && subtitleText != null && usernameInput != null &&
               passwordInput != null && confirmPasswordInput != null && statusText != null &&
               submitButton != null && submitLabel != null && switchModeButton != null &&
               switchModeLabel != null && guestButton != null && loginOnlyObjects != null;
    }
}
