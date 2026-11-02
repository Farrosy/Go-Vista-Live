using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Menu KARAKTER (dibuka dari tombol KARAKTER di menu utama, LobyUISceneController).
/// Pasang di root prefab Assets/Prefab/CharacterSelectCanvas.prefab. Semua tampilan (teks, warna,
/// posisi, ukuran) adalah objek biasa di prefab itu, bebas diedit di Inspector.
/// Daftar karakter diambil dari Assets/Resources/CharacterDatabase.asset.
/// </summary>
public class CharacterSelectUI : MonoBehaviour {
    [Tooltip("Isi menu yang ditampilkan/disembunyikan. Canvas root tetap aktif.")]
    [SerializeField] private GameObject root;
    [SerializeField] private CharacterPreviewStage stage;
    [SerializeField] private RawImage previewImage;

    [Header("Info karakter")]
    [SerializeField] private Text nameText;
    [SerializeField] private Text descriptionText;
    [Tooltip("Contoh: \"1 / 2\"")]
    [SerializeField] private Text counterText;
    [Tooltip("Ditampilkan kalau karakter yang sedang dilihat adalah karakter yang dipakai.")]
    [SerializeField] private GameObject usedBadge;

    [Header("Tombol")]
    [SerializeField] private Button previousButton;
    [SerializeField] private Button nextButton;
    [SerializeField] private Button selectButton;
    [SerializeField] private Text selectButtonLabel;
    [SerializeField] private string selectText = "PILIH";
    [SerializeField] private string usedText = "DIPAKAI";
    [SerializeField] private Button backButton;

    public bool IsOpen => root && root.activeSelf;

    private int index;
    private Action onClosed;

    void Awake() {
        if (previousButton) previousButton.onClick.AddListener(() => Step(-1));
        if (nextButton) nextButton.onClick.AddListener(() => Step(1));
        if (selectButton) selectButton.onClick.AddListener(SelectCurrent);
        if (backButton) backButton.onClick.AddListener(Close);
        if (root) root.SetActive(false);
        if (stage) stage.gameObject.SetActive(false);
    }

    void Update() {
        // Tombol Back di Android.
        if (IsOpen && Input.GetKeyDown(KeyCode.Escape)) Close();
    }

    /// <param name="closed">Dipanggil setelah menu ditutup (KEMBALI / tombol Back).</param>
    public void Open(Action closed = null) {
        CharacterDatabase db = CharacterDatabase.Instance;
        if (!db || db.Count == 0) {
            Debug.LogWarning("[CharacterSelect] CharacterDatabase kosong / tidak ada di Assets/Resources.");
            closed?.Invoke();
            return;
        }

        onClosed = closed;
        if (root) root.SetActive(true);
        if (stage) {
            stage.gameObject.SetActive(true);
            if (previewImage) previewImage.texture = stage.Texture;
        }
        index = Mathf.Max(0, db.IndexOf(CharacterSelection.Current?.id));
        Refresh();
    }

    public void Close() {
        if (root) root.SetActive(false);
        if (stage) {
            stage.Clear();
            stage.gameObject.SetActive(false);
        }
        Action closed = onClosed;
        onClosed = null;
        closed?.Invoke();
    }

    private void Step(int direction) {
        int count = CharacterDatabase.Instance.Count;
        index = (index + direction + count) % count;
        Refresh();
    }

    private void SelectCurrent() {
        CharacterSelection.Select(CharacterDatabase.Instance[index].id);
        Refresh();
    }

    private void Refresh() {
        CharacterDatabase db = CharacterDatabase.Instance;
        CharacterDatabase.Entry character = db[index];
        bool used = character == CharacterSelection.Current;

        if (nameText) nameText.text = character.displayName;
        if (descriptionText) descriptionText.text = character.description;
        if (counterText) counterText.text = (index + 1) + " / " + db.Count;
        if (usedBadge) usedBadge.SetActive(used);
        if (selectButton) selectButton.interactable = !used;
        if (selectButtonLabel) selectButtonLabel.text = used ? usedText : selectText;
        if (previousButton) previousButton.interactable = db.Count > 1;
        if (nextButton) nextButton.interactable = db.Count > 1;
        if (stage) stage.Show(character);
    }
}
