using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Menampilkan nama pemain yang sedang login dan huruf depannya (untuk avatar bulat).
/// Diperbarui otomatis saat panel muncul dan setiap kali login/logout (LoginUIController.AccountChanged).
/// Dipasang oleh Go Vista > Desain Baru di kartu menu utama dan dialog pengaturan.
/// Tidak butuh perubahan di LobyUISceneController.
/// </summary>
public sealed class PlayerNameDisplay : MonoBehaviour {
    [Tooltip("Opsional. Teks nama pemain.")]
    [SerializeField] private Text nameText;
    [Tooltip("Opsional. Teks huruf depan nama (di dalam avatar).")]
    [SerializeField] private Text initialText;
    [SerializeField] private int maxNameLength = 18;
    [SerializeField] private string fallbackName = "Pemain";

    public void Init(Text name, Text initial) {
        nameText = name;
        initialText = initial;
    }

    private void OnEnable() {
        LoginUIController.AccountChanged += Refresh;
        Refresh();
    }

    private void OnDisable() {
        LoginUIController.AccountChanged -= Refresh;
    }

    public void Refresh() {
        string username = AuthService.DisplayName;
        if (string.IsNullOrEmpty(username))
            username = fallbackName;
        if (nameText != null)
            nameText.text = username.Length > maxNameLength ? username.Substring(0, maxNameLength - 1) + "…" : username;
        if (initialText != null)
            initialText.text = username.Substring(0, 1).ToUpperInvariant();
    }
}
