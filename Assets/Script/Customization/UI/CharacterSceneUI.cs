using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Pengendali scene KARAKTER (Assets/Scenes/Customization/CharacterScene.unity), dibuka dari tile KARAKTER
/// di menu utama lewat CharacterSceneLoader (additive di atas lobby). Layar pertama "Pilih Karakter"
/// (CustomizerPickView, mockup v2 layar 1): 4 template terkunci + 2 slot karakter custom milik akun
/// (CustomCharacterStore, Cloud Save). UBAH / Buat membuka editor (CustomizerEditor) di scene yang sama.
///
/// Scene berisi: root ini (+ Canvas full-screen) dan Preview Stage (kamera + lampu + meja putar).
/// Tampilan dibangun lewat kode saat runtime; posisi kamera/lampu bisa diatur langsung di scene.
/// </summary>
public class CharacterSceneUI : MonoBehaviour {
    [SerializeField] private CharacterPreviewStage stage;
    [Tooltip("Font & ikon. Diisi otomatis oleh Go Vista > Customization > Buat CharacterScene; kosong = dari Resources.")]
    [SerializeField] private ChatUiAssets assets = new ChatUiAssets();
    [SerializeField] private float toastSeconds = 2.2f;

    private const int ThumbnailWidth = 240;
    private const int ThumbnailHeight = 360;
    // Jeda setelah karakter selesai dirakit sebelum difoto (tekstur + pose idle sudah stabil).
    private const float ThumbnailSettleSeconds = 0.4f;

    private enum SelectionKind { Template, Slot }

    private CustomizerPickRefs ui;
    private SelectionKind selectedKind;
    private int selectedIndex;
    private int previewKey = int.MinValue;
    private int pendingDelete = -1;
    private Coroutine toastRoutine;
    private CustomizerEditor editor;
    // Frame saat editor ditutup: tombol Back di frame yang sama jangan ikut menutup scene.
    private int editorClosedFrame = -1;
    // Jumlah slot saat "Buat" ditekan; -1 = editor dibuka untuk slot yang sudah ada.
    private int slotsBeforeCreate = -1;
    private float previewIdleSince = -1f;
    private int thumbnailCheckedKey = int.MinValue;
    // Dibuka dari ruang ganti di toko (CharacterSceneLoader.FittingRoom); null = dari lobby.
    private FittingRoomView fittingRoom;
    private CustomizerEditorRefs editorUi;
    private readonly Vector3[] stageCorners = new Vector3[4];

    private bool IsOpen => ui != null && ui.Root.activeSelf;

    void Awake() {
        assets.ResolveFallbacks();
        Canvas canvas = GetComponentInChildren<Canvas>(true);
        if (canvas == null) {
            Debug.LogError("[CharacterScene] Canvas tidak ditemukan di CharacterScene.", this);
            return;
        }
        EnsureEventSystem();
        fittingRoom = CharacterSceneLoader.FittingRoom;
        ui = CustomizerPickView.Build(canvas.transform, assets);
        WireButtons();
        editor = gameObject.AddComponent<CustomizerEditor>();
        IReadOnlyList<EditorCategory> categories = fittingRoom != null ? EditorCategories.FittingRoom : EditorCategories.All;
        editorUi = CustomizerEditorView.Build(canvas.transform, assets, categories, fittingRoom != null);
        editor.Init(editorUi, assets, stage, categories);
        CharacterPreviewDrag drag = ui.Preview.gameObject.AddComponent<CharacterPreviewDrag>();
        drag.SetStage(stage);
        if (stage && fittingRoom == null) stage.UseTransparentBackground();
    }

    void Start() {
        if (fittingRoom != null) OpenFittingRoom();
        else Show();
    }

    void LateUpdate() {
        if (fittingRoom != null && stage && editor.IsOpen) CenterOnStageArea();
    }

    // ---------------------------------------------------------------- Ruang ganti

    /// <summary>
    /// Ruang ganti: langsung ke editor karakter yang sedang dipakai, karakter berdiri di bilik toko. Pemain
    /// yang memakai template mengedit salinannya; SIMPAN membuat slot baru (atau menimpa kalau penuh).
    /// Tidak ada layar Pilih Karakter: ganti template tetap di lobby.
    /// </summary>
    private void OpenFittingRoom() {
        int slot = CustomCharacterStore.ActiveSlot;
        CustomCharacterRecord record = CustomCharacterStore.Get(slot);
        if (record == null) {
            slot = -1;
            record = CharacterTemplates.TryGet(CurrentTemplateId, out CharacterTemplate template)
                ? CharacterRecordConverter.FromTemplate(template)
                : null;
        }
        if (record == null || !stage) {
            Debug.LogWarning("[CharacterScene] Ruang ganti: karakter aktif tidak dikenal, editor tidak dibuka.");
            CharacterSceneLoader.Close();
            return;
        }
        stage.EnterWorldMode(fittingRoom.Subject, fittingRoom.View, fittingRoom.FieldOfView, Camera.main,
                             fittingRoom.CullingMask);
        CenterOnStageArea();
        editorUi.Preview.color = new Color(1f, 1f, 1f, 0f); // tetap jadi area geser, gambarnya = toko di belakang UI
        editor.OfferOverwriteWhenFull = true;
        editor.Saved += OnFittingRoomSaved;
        editor.Open(record, slot, false, CharacterSceneLoader.Close);
    }

    /// Penampilan baru langsung dipakai: slot itu jadi aktif, pemain lain menerima data barunya.
    private static void OnFittingRoomSaved(int slot) {
        CustomCharacterStore.Activate(slot);
        PlayerAppearanceSync.RefreshLocal();
    }

    /// Sumbu karakter ke tengah area kosong di antara rail, bar atas, dan panel kanan (koordinat NDC).
    private void CenterOnStageArea() {
        RectTransform area = editorUi?.StageArea;
        if (!area || Screen.width <= 0 || Screen.height <= 0) return;
        Vector3[] corners = stageCorners;
        area.GetWorldCorners(corners); // canvas Screen Space Overlay: world = piksel layar
        stage.ScreenCenterX = (corners[0].x + corners[2].x) * 0.5f / Screen.width * 2f - 1f;
        stage.ScreenCenterY = (corners[0].y + corners[2].y) * 0.5f / Screen.height * 2f - 1f;
    }

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

    void OnEnable() {
        ProgressService.Loaded += OnProgressLoaded;
        SlotThumbnails.Changed += OnThumbnailsChanged;
    }

    void OnDisable() {
        ProgressService.Loaded -= OnProgressLoaded;
        SlotThumbnails.Changed -= OnThumbnailsChanged;
    }

    private void OnThumbnailsChanged() {
        if (IsOpen) RefreshSlots();
    }

    /// Progres Cloud baru selesai dimuat (mis. login otomatis) saat menu terbuka: tampilkan data akun.
    private void OnProgressLoaded() {
        if (!IsOpen) return;
        previewKey = int.MinValue;
        SelectActive();
    }

    void Update() {
        if (!IsOpen) return;
        if (stage) ui.Loading.SetActive(stage.IsBusy);
        CaptureMissingThumbnail();
        // Tombol Back di Android: tutup dialog dulu, baru menu.
        if (!Input.GetKeyDown(KeyCode.Escape) || Time.frameCount == editorClosedFrame) return;
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
            // Foto karakter ini (SlotThumbnails); sebelum pernah difoto di perangkat ini, ikon template asalnya.
            Texture photo = SlotThumbnails.Get(record);
            slot.Thumbnail.texture = photo ? photo : TemplateIcon(template);
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
        thumbnailCheckedKey = int.MinValue;

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
                       CharacterDatabase.Instance.Get(record.templateId), record.expression);
    }

    /// Slot yang sedang dipreview belum punya foto (mis. dibuat di perangkat lain): foto begitu selesai dirakit.
    private void CaptureMissingThumbnail() {
        bool waiting = stage && selectedKind == SelectionKind.Slot && previewKey == 100 + selectedIndex &&
                       stage.LiveAvatar && !stage.IsBusy;
        if (!waiting) {
            previewIdleSince = -1f;
            return;
        }
        if (previewIdleSince < 0f) previewIdleSince = Time.unscaledTime;
        if (Time.unscaledTime - previewIdleSince < ThumbnailSettleSeconds || thumbnailCheckedKey == previewKey) return;
        thumbnailCheckedKey = previewKey; // sekali per preview, bukan setiap frame
        CustomCharacterRecord record = CustomCharacterStore.Get(selectedIndex);
        if (record == null || SlotThumbnails.Has(record)) return;
        Texture2D shot = stage.CaptureThumbnail(ThumbnailWidth, ThumbnailHeight);
        if (shot) SlotThumbnails.Store(record, shot, CustomCharacterStore.All());
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
        if (!CustomCharacterStore.CanCreate) {
            ShowToast("Tidak bisa membuat karakter baru");
            return;
        }
        bool fromSlot = selectedKind == SelectionKind.Slot;
        CustomCharacterRecord source = fromSlot
            ? CustomCharacterStore.Get(selectedIndex)
            : CharacterRecordConverter.FromTemplate(CharacterTemplates.All[selectedIndex]);
        if (source == null) return;
        // Belum disimpan apa pun: slot baru baru dibuat saat SIMPAN di editor (slot -1). Kalau dibuang,
        // tidak ada slot "template polos" yang tertinggal di Cloud Save.
        bool previewReady = fromSlot && previewKey == 100 + selectedIndex;
        slotsBeforeCreate = CustomCharacterStore.Count;
        ui.Root.SetActive(false);
        editor.Open(source, -1, previewReady, OnEditorClosed);
    }

    /// UBAH: buka editor untuk slot ini. Kalau preview sudah menampilkan slot ini, tidak dirakit ulang.
    private void EditSlot(int slot) {
        CustomCharacterRecord record = CustomCharacterStore.Get(slot);
        if (record == null) return;
        slotsBeforeCreate = -1;
        bool previewReady = selectedKind == SelectionKind.Slot && selectedIndex == slot && previewKey == 100 + slot;
        ui.Root.SetActive(false);
        editor.Open(record, slot, previewReady, OnEditorClosed);
    }

    /// Kembali dari editor ke Pilih Karakter: preview dirakit ulang dari data tersimpan (perubahan yang
    /// dibuang tidak boleh tetap terlihat).
    private void OnEditorClosed() {
        editorClosedFrame = Time.frameCount;
        ui.Root.SetActive(true);
        if (stage) ui.Preview.texture = stage.Texture;
        previewKey = int.MinValue;
        // Karakter baru yang disimpan: langsung pilih slot barunya. Kalau dibuang, pilihan tetap seperti semula.
        bool created = slotsBeforeCreate >= 0 && CustomCharacterStore.Count > slotsBeforeCreate;
        slotsBeforeCreate = -1;
        if (created) {
            Select(SelectionKind.Slot, CustomCharacterStore.Count - 1);
            return;
        }
        Refresh();
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
        CustomizerUi.SetToastText(ui.Toast, message);
        GameObject pill = ui.Toast.transform.parent.gameObject;
        pill.SetActive(true);
        yield return new WaitForSecondsRealtime(toastSeconds);
        pill.SetActive(false);
        toastRoutine = null;
    }
}
