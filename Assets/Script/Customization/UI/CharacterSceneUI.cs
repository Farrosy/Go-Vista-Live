using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Pengendali scene KARAKTER (Assets/Scenes/Customization/CharacterScene.unity), dibuka dari tile KARAKTER
/// di menu utama lewat CharacterSceneLoader (additive di atas lobby). Layar pertama "Pilih Karakter"
/// (CustomizerPickView, mockup v2 layar 1): 4 template terkunci + 2 slot karakter custom milik akun
/// (CustomCharacterStore, Cloud Save). Editor (UBAH) menyusul di Tahap 1c, di scene yang sama.
///
/// Scene berisi: root ini (+ Canvas full-screen) dan Preview Stage (kamera + lampu + meja putar).
/// Tampilan dibangun lewat kode saat runtime; posisi kamera/lampu bisa diatur langsung di scene.
/// </summary>
public class CharacterSceneUI : MonoBehaviour {
    [SerializeField] private CharacterPreviewStage stage;
    [Tooltip("Font & ikon. Diisi otomatis oleh Go Vista > Customization > Buat CharacterScene; kosong = dari Resources.")]
    [SerializeField] private ChatUiAssets assets = new ChatUiAssets();
    [SerializeField] private float toastSeconds = 2.2f;

    private enum SelectionKind { Template, Slot }

    private CustomizerPickRefs ui;
    private SelectionKind selectedKind;
    private int selectedIndex;
    private int previewKey = int.MinValue;
    private int pendingDelete = -1;
    private Coroutine toastRoutine;

    private bool IsOpen => ui != null && ui.Root.activeSelf;

    void Awake() {
        assets.ResolveFallbacks();
        Canvas canvas = GetComponentInChildren<Canvas>(true);
        if (canvas == null) {
            Debug.LogError("[CharacterScene] Canvas tidak ditemukan di CharacterScene.", this);
            return;
        }
        EnsureEventSystem();
        ui = CustomizerPickView.Build(canvas.transform, assets);
        WireButtons();
        CharacterPreviewDrag drag = ui.Preview.gameObject.AddComponent<CharacterPreviewDrag>();
        drag.SetStage(stage);
        if (stage) stage.UseTransparentBackground();
    }

    void Start() => Show();

    /// Scene ini tidak membawa EventSystem (lobby sudah punya). Saat scene dibuka sendiri untuk dites
    /// di editor, buat sementara supaya tombol tetap bisa ditekan.
    private void EnsureEventSystem() {
        if (EventSystem.current != null || FindAnyObjectByType<EventSystem>() != null) return;
        var system = new GameObject("EventSystem (CharacterScene)", typeof(EventSystem), typeof(StandaloneInputModule));
        system.transform.SetParent(transform, false);
    }

    private void WireButtons() {
        ui.CloseButton.onClick.AddListener(Back);
        ui.UseButton.onClick.AddListener(UseSelected);
        ui.ConfirmCancel.onClick.AddListener(() => ui.ConfirmOverlay.SetActive(false));
        ui.ConfirmDelete.onClick.AddListener(ConfirmDelete);
        for (int i = 0; i < ui.Templates.Length; i++) {
            int index = i;
            ui.Templates[i].Button.onClick.AddListener(() => Select(SelectionKind.Template, index));
            ui.Templates[i].Thumbnail.texture = TemplateIcon(CharacterTemplates.All[i]);
        }
        for (int i = 0; i < ui.Slots.Length; i++) {
            int index = i;
            ui.Slots[i].Button.onClick.AddListener(() => Select(SelectionKind.Slot, index));
            ui.Slots[i].EditButton.onClick.AddListener(() => EditSlot(index));
            ui.Slots[i].DeleteButton.onClick.AddListener(() => AskDelete(index));
            ui.Slots[i].CreateButton.onClick.AddListener(CreateSlot);
        }
    }

    void OnEnable() => ProgressService.Loaded += OnProgressLoaded;

    void OnDisable() => ProgressService.Loaded -= OnProgressLoaded;

    /// Progres Cloud baru selesai dimuat (mis. login otomatis) saat menu terbuka: tampilkan data akun.
    private void OnProgressLoaded() {
        if (!IsOpen) return;
        previewKey = int.MinValue;
        SelectActive();
    }

    void Update() {
        if (!IsOpen) return;
        if (stage) ui.Loading.SetActive(stage.IsBusy);
        // Tombol Back di Android: tutup dialog dulu, baru menu.
        if (!Input.GetKeyDown(KeyCode.Escape)) return;
        if (ui.ConfirmOverlay.activeSelf) ui.ConfirmOverlay.SetActive(false);
        else Back();
    }

    private void Show() {
        if (ui == null) return;
        if (!CharacterDatabase.Instance) {
            Debug.LogWarning("[CharacterScene] CharacterDatabase tidak ada di Assets/Resources.");
            return;
        }
        ui.Root.SetActive(true);
        ui.ConfirmOverlay.SetActive(false);
        if (stage) ui.Preview.texture = stage.Texture;
        SelectActive();
    }

    /// KEMBALI / Back Android: unload scene ini, lobby menampilkan lagi menu utama.
    private void Back() => CharacterSceneLoader.Close();

    // ---------------------------------------------------------------- Pilihan

    /// Pilih yang sedang dipakai: slot custom aktif, atau template di characterId.
    private void SelectActive() {
        if (CustomCharacterStore.ActiveSlot != CustomSlotRules.NoActiveSlot) {
            Select(SelectionKind.Slot, CustomCharacterStore.ActiveSlot);
            return;
        }
        Select(SelectionKind.Template, Mathf.Max(0, TemplateIndex(CurrentTemplateId)));
    }

    private void Select(SelectionKind kind, int index) {
        selectedKind = kind;
        selectedIndex = index;
        Refresh();
    }

    private void Refresh() {
        RefreshTemplates();
        RefreshSlots();
        RefreshUseButton();
        RefreshPreview();
    }

    private void RefreshTemplates() {
        bool templateActive = CustomCharacterStore.ActiveSlot == CustomSlotRules.NoActiveSlot;
        int usedTemplate = TemplateIndex(CurrentTemplateId);
        for (int i = 0; i < ui.Templates.Length; i++) {
            ui.Templates[i].Selected.SetActive(selectedKind == SelectionKind.Template && selectedIndex == i);
            ui.Templates[i].UsedBadge.SetActive(templateActive && usedTemplate == i);
        }
    }

    private void RefreshSlots() {
        int count = CustomCharacterStore.Count;
        ui.SlotCounter.text = count + " / " + CustomSlotRules.MaxCustomSlots;
        for (int i = 0; i < ui.Slots.Length; i++) {
            SlotCardRefs slot = ui.Slots[i];
            CustomCharacterRecord record = i < count ? CustomCharacterStore.Get(i) : null;
            bool filled = record != null;
            slot.Filled.SetActive(filled);
            slot.CreateButton.gameObject.SetActive(!filled && i == count);
            if (!filled) continue;
            CharacterTemplates.TryGet(record.templateId, out CharacterTemplate template);
            slot.Origin.text = "dari " + template.Label;
            slot.Thumbnail.texture = TemplateIcon(template);
            slot.Selected.SetActive(selectedKind == SelectionKind.Slot && selectedIndex == i);
            slot.UsedBadge.SetActive(CustomCharacterStore.ActiveSlot == i);
        }
    }

    private void RefreshUseButton() {
        bool used = selectedKind == SelectionKind.Slot
            ? CustomCharacterStore.ActiveSlot == selectedIndex
            : CustomCharacterStore.ActiveSlot == CustomSlotRules.NoActiveSlot &&
              TemplateIndex(CurrentTemplateId) == selectedIndex;
        ui.UseButton.interactable = !used;
        ui.UseLabel.text = used ? "SEDANG DIPAKAI" : "PAKAI KARAKTER INI";
    }

    /// Preview hanya dibangun ulang kalau pilihannya berubah (merakit karakter custom itu mahal).
    private void RefreshPreview() {
        int key = selectedKind == SelectionKind.Template ? selectedIndex : 100 + selectedIndex;
        if (key == previewKey || !stage) return;
        previewKey = key;

        if (selectedKind == SelectionKind.Template) {
            CharacterTemplate template = CharacterTemplates.All[selectedIndex];
            ui.PreviewName.text = template.Label;
            stage.Show(CharacterDatabase.Instance.Get(template.Id));
            return;
        }
        CustomCharacterRecord record = CustomCharacterStore.Get(selectedIndex);
        ui.PreviewName.text = CustomSlotRules.SlotName(selectedIndex);
        if (record == null) {
            stage.Clear();
            return;
        }
        stage.ShowLive(CustomizerSettings.Instance ? CustomizerSettings.Instance.characterBase : null,
                       CharacterRecordConverter.ToCharacterData(record),
                       CharacterDatabase.Instance.Get(record.templateId));
    }

    // ---------------------------------------------------------------- Aksi

    private void UseSelected() {
        if (selectedKind == SelectionKind.Slot) {
            CustomCharacterStore.Activate(selectedIndex);
            ShowToast(CustomSlotRules.SlotName(selectedIndex) + " dipakai");
        } else {
            CharacterTemplate template = CharacterTemplates.All[selectedIndex];
            CustomCharacterStore.ActivateTemplate(template.Id);
            ShowToast(template.Label + " dipakai");
        }
        Refresh();
    }

    /// Slot baru dari template yang sedang dipilih, atau salinan slot yang sedang dipilih.
    private void CreateSlot() {
        CustomCharacterRecord source = selectedKind == SelectionKind.Slot
            ? CustomCharacterStore.Get(selectedIndex)
            : CharacterRecordConverter.FromTemplate(CharacterTemplates.All[selectedIndex]);
        int slot = CustomCharacterStore.Save(-1, source);
        if (slot < 0) {
            ShowToast("Tidak bisa membuat karakter baru");
            return;
        }
        ShowToast(CustomSlotRules.SlotName(slot) + " dibuat");
        Select(SelectionKind.Slot, slot);
    }

    private void EditSlot(int slot) {
        Select(SelectionKind.Slot, slot);
        ShowToast("Editor karakter menyusul di langkah berikutnya");
    }

    private void AskDelete(int slot) {
        pendingDelete = slot;
        ui.ConfirmTitle.text = "Hapus " + CustomSlotRules.SlotName(slot) + "?";
        ui.ConfirmOverlay.SetActive(true);
    }

    private void ConfirmDelete() {
        ui.ConfirmOverlay.SetActive(false);
        if (pendingDelete < 0) return;
        bool wasActive = CustomCharacterStore.ActiveSlot == pendingDelete;
        CustomCharacterStore.Delete(pendingDelete);
        ShowToast(CustomSlotRules.SlotName(pendingDelete) + " dihapus");
        pendingDelete = -1;
        previewKey = int.MinValue;
        if (wasActive) SelectActive(); // kembali ke template asal (characterId tidak berubah)
        else Select(SelectionKind.Template, Mathf.Max(0, TemplateIndex(CurrentTemplateId)));
    }

    // ---------------------------------------------------------------- Bantuan

    /// characterId kosong (akun baru) berarti karakter pertama di CharacterDatabase.
    private static string CurrentTemplateId => CharacterSelection.Current?.id ?? "";

    private static int TemplateIndex(string id) {
        for (int i = 0; i < CharacterTemplates.All.Count; i++) {
            if (CharacterTemplates.All[i].Id == id) return i;
        }
        return -1;
    }

    private static Texture TemplateIcon(CharacterTemplate template) {
        return CharacterRecordConverter.LoadTemplate(template)?.icon;
    }

    private void ShowToast(string message) {
        if (toastRoutine != null) StopCoroutine(toastRoutine);
        toastRoutine = StartCoroutine(ToastRoutine(message));
    }

    private IEnumerator ToastRoutine(string message) {
        ui.Toast.text = message;
        GameObject pill = ui.Toast.transform.parent.gameObject;
        pill.SetActive(true);
        yield return new WaitForSecondsRealtime(toastSeconds);
        pill.SetActive(false);
        toastRoutine = null;
    }
}
