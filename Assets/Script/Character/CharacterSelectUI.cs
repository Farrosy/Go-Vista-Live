using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Menu GANTI KARAKTER (Docs/Profil/06a & 06b), dibuka dari tombol GANTI KARAKTER di menu PROFIL.
/// Pasang di root prefab Assets/Prefab/CharacterSelectCanvas.prefab. Prefab hanya berisi Canvas dan panggung
/// preview 3D (Preview Stage); tampilan menu dibangun lewat kode (CharacterSelectView) dengan gaya yang sama
/// seperti menu PROFIL/TEMAN. Daftar karakter dari Assets/Resources/CharacterDatabase.asset.
/// Nama pemain TIDAK lagi diubah di sini (pindah ke menu PROFIL).
/// </summary>
public class CharacterSelectUI : MonoBehaviour {
    [SerializeField] private CharacterPreviewStage stage;
    [Tooltip("Font & ikon. Diisi otomatis oleh Go Vista > Profile > Pasang Profile; kosong = dimuat dari Resources.")]
    [SerializeField] private ChatUiAssets assets = new ChatUiAssets();

    private static readonly Color DisabledTop = new Color32(0x33, 0x3B, 0x55, 0xFF);
    private static readonly Color DisabledBottom = new Color32(0x2A, 0x31, 0x4A, 0xFF);
    private static readonly Color DisabledLip = new Color32(0x1B, 0x22, 0x38, 0xFF);
    private static readonly Color DotOff = new Color(1f, 1f, 1f, 0.3f);
    private const float DotSize = 12f;
    private const float ActiveDotWidth = 36f;

    private CharacterSelectRefs ui;
    private int index;
    private Action onClosed;

    public bool IsOpen => ui != null && ui.Root.activeSelf;

    /// Panggung preview 3D yang sama dipakai menu PROFIL (kiri), supaya tidak ada dua kamera preview.
    public CharacterPreviewStage Stage => stage;

    void Awake() {
        assets.ResolveFallbacks();
        Canvas canvas = GetComponentInChildren<Canvas>(true);
        if (canvas == null) {
            Debug.LogError("[CharacterSelect] Canvas tidak ditemukan di CharacterSelectCanvas.prefab.", this);
            return;
        }
        ui = CharacterSelectView.Build(canvas.transform, assets);
        ui.PreviousButton.onClick.AddListener(() => Step(-1));
        ui.NextButton.onClick.AddListener(() => Step(1));
        ui.SelectButton.onClick.AddListener(SelectCurrent);
        ui.BackButton.onClick.AddListener(Close);
        ui.CloseButton.onClick.AddListener(Close);
        CharacterPreviewDrag drag = ui.Preview.gameObject.AddComponent<CharacterPreviewDrag>();
        drag.SetStage(stage);
        if (stage) {
            stage.UseTransparentBackground();
            stage.gameObject.SetActive(false);
        }
    }

    void Update() {
        // Tombol Back di Android.
        if (IsOpen && Input.GetKeyDown(KeyCode.Escape)) Close();
    }

    /// <param name="closed">Dipanggil setelah menu ditutup (KEMBALI / × / tombol Back).</param>
    public void Open(Action closed = null) {
        CharacterDatabase db = CharacterDatabase.Instance;
        if (ui == null || !db || db.Count == 0) {
            Debug.LogWarning("[CharacterSelect] CharacterDatabase kosong / tidak ada di Assets/Resources.");
            closed?.Invoke();
            return;
        }

        onClosed = closed;
        ui.Root.SetActive(true);
        if (stage) {
            stage.gameObject.SetActive(true);
            ui.Preview.texture = stage.Texture;
        }
        index = Mathf.Max(0, db.IndexOf(CharacterSelection.Current?.id));
        Refresh();
    }

    public void Close() {
        if (ui != null) ui.Root.SetActive(false);
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
        CharacterDatabase.Entry current = CharacterSelection.Current;
        bool used = character == current;

        ui.CounterText.text = "KARAKTER " + (index + 1) + " / " + db.Count;
        ui.NameText.text = character.displayName;
        ui.DescriptionText.text = character.description;
        ui.UsedBadge.SetActive(used);
        ui.CurrentText.gameObject.SetActive(!used && current != null);
        if (current != null)
            ui.CurrentText.text = "Sedang dipakai: <b><color=#FFFFFF>" + current.displayName + "</color></b>";
        SetSelectState(!used);
        ui.PreviousButton.interactable = db.Count > 1;
        ui.NextButton.interactable = db.Count > 1;
        RefreshDots(db.Count);
        if (stage) stage.Show(character);
    }

    /// PILIH (pink, bisa ditekan) atau DIPAKAI (abu-abu, nonaktif) untuk karakter yang sedang dipakai.
    private void SetSelectState(bool selectable) {
        Button button = ui.SelectButton;
        button.interactable = selectable;
        ui.SelectLabel.text = selectable ? "PILIH" : "DIPAKAI";
        Transform face = button.transform.Find("Face");
        face.GetComponent<UIVerticalGradient>().SetColors(selectable ? ProfileTheme.PinkTop : DisabledTop,
                                                          selectable ? ProfileTheme.PinkBottom : DisabledBottom);
        button.transform.Find("Lip").GetComponent<Image>().color = selectable ? ProfileTheme.PinkLip : DisabledLip;
        Color content = selectable ? Color.white : FriendsTheme.Muted;
        ui.SelectLabel.color = content;
        Image icon = face.Find("Content/Icon")?.GetComponent<Image>();
        if (icon != null) icon.color = content;
    }

    /// Titik halaman: karakter yang sedang dilihat = pil pink panjang, lainnya titik abu-abu.
    private void RefreshDots(int count) {
        Transform dots = ui.Dots;
        while (dots.childCount < count) {
            RectTransform dot = ChatUiKit.CreateRect("Dot", dots);
            ChatUiKit.AddShape(dot.gameObject, assets.rounded, DotOff, 4f).raycastTarget = false;
        }
        float width = 0f;
        for (int i = 0; i < dots.childCount; i++) {
            var dot = (RectTransform)dots.GetChild(i);
            bool visible = i < count;
            dot.gameObject.SetActive(visible);
            if (!visible) continue;
            bool active = i == index;
            dot.sizeDelta = new Vector2(active ? ActiveDotWidth : DotSize, DotSize);
            dot.GetComponent<Image>().color = active ? ProfileTheme.PinkTop : DotOff;
            width += dot.sizeDelta.x + (i > 0 ? 10f : 0f);
        }
        ((RectTransform)dots).sizeDelta = new Vector2(width, DotSize);
    }
}
