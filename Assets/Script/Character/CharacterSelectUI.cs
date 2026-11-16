using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Menu GANTI KARAKTER, dibuka dari tombol GANTI KARAKTER di menu PROFIL. Sejak customizer (Tahap 1b),
/// layar pertamanya adalah "Pilih Karakter" (CustomizerPickView): 4 template terkunci + 2 slot karakter
/// custom milik akun (CustomCharacterStore, Cloud Save). Editor (UBAH) menyusul di Tahap 1c.
///
/// Pasang di root prefab Assets/Prefab/CharacterSelectCanvas.prefab. Prefab hanya berisi Canvas dan panggung
/// preview 3D (Preview Stage); tampilan dibangun lewat kode. API Open/Close/Stage tetap sama, jadi
/// ProfileMenu tidak perlu diubah.
/// </summary>
public class CharacterSelectUI : MonoBehaviour {
    [SerializeField] private CharacterPreviewStage stage;
    [Tooltip("Font & ikon. Diisi otomatis oleh Go Vista > Profile > Pasang Profile; kosong = dimuat dari Resources.")]
    [SerializeField] private ChatUiAssets assets = new ChatUiAssets();
    [SerializeField] private float toastSeconds = 2.2f;

    private enum SelectionKind { Template, Slot }

    private CustomizerPickRefs ui;
    private SelectionKind selectedKind;
    private int selectedIndex;
    private int previewKey = int.MinValue;
    private int pendingDelete = -1;
    private Action onClosed;
    private Coroutine toastRoutine;

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
        ui = CustomizerPickView.Build(canvas.transform, assets);
        WireButtons();
        CharacterPreviewDrag drag = ui.Preview.gameObject.AddComponent<CharacterPreviewDrag>();
        drag.SetStage(stage);
        if (stage) {
            stage.UseTransparentBackground();
            stage.gameObject.SetActive(false);
        }
    }

    private void WireButtons() {
        ui.CloseButton.onClick.AddListener(Close);
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
        else Close();
    }

    /// <param name="closed">Dipanggil setelah menu ditutup (× / tombol Back).</param>
    public void Open(Action closed = null) {
        if (ui == null || !CharacterDatabase.Instance) {
            Debug.LogWarning("[CharacterSelect] CharacterDatabase tidak ada di Assets/Resources.");
            closed?.Invoke();
            return;
        }
        onClosed = closed;
        ui.Root.SetActive(true);
        ui.ConfirmOverlay.SetActive(false);
        if (stage) {
            stage.gameObject.SetActive(true);
            ui.Preview.texture = stage.Texture;
        }
        SelectActive();
    }

    public void Close() {
        if (ui != null) ui.Root.SetActive(false);
        if (stage) {
            stage.Clear();
            stage.gameObject.SetActive(false);
        }
        previewKey = int.MinValue;
        Action closed = onClosed;
        onClosed = null;
        closed?.Invoke();
    }

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
