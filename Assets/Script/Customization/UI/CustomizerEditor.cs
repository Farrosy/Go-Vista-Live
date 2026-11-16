using System;
using System.Collections;
using System.Collections.Generic;
using Bozo.ModularCharacters;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Pengendali editor karakter (tahap 1c) di scene KARAKTER. Sumber kebenaran = CustomizationSession
/// (record kerja + URUNGKAN). Avatar 3D di panggung diubah langsung supaya cepat:
///  - ganti baju  → instance outfit BoZo di bawah avatar (BoZo mengganti slot + merge), lalu warna record;
///  - ganti warna → Outfit.SetColor (jalur UpdateTexture BoZo, tanpa merge);
///  - URUNGKAN / ACAK → avatar dirakit ulang dari record.
/// Kulit, Wajah, Stiker & Motif menyusul di tahap 1d.
/// </summary>
public sealed class CustomizerEditor : MonoBehaviour {
    private const float ToastSeconds = 2.2f;
    private const float AttachTimeout = 3f;
    private const int DefaultCategory = 3; // Atasan, seperti mockup layar 2

    private CustomizerEditorRefs _ui;
    private ChatUiAssets _a;
    private CharacterPreviewStage _stage;
    private CustomizationSession _session;
    private CharacterTemplate _template;
    private Action _closed;
    private Coroutine _toast;

    private int _category = -1;
    private int _subTab;
    private int _channel;
    private bool _keepColors = true;
    private readonly List<GameObject> _subTabSelected = new List<GameObject>();
    private readonly List<(string path, GameObject selected)> _items = new List<(string, GameObject)>();
    private readonly List<(Image swatch, GameObject selected)> _channels = new List<(Image, GameObject)>();

    public bool IsOpen => _ui != null && _ui.Root.activeSelf;

    private string CurrentType => CurrentCategory.Kind == EditorCategoryKind.Outfits
        ? CurrentCategory.SubTabs[_subTab].OutfitType : "";
    private EditorCategory CurrentCategory => EditorCategories.All[_category];

    public void Init(CustomizerEditorRefs ui, ChatUiAssets assets, CharacterPreviewStage stage) {
        _ui = ui;
        _a = assets;
        _stage = stage;
        _ui.BackButton.onClick.AddListener(Back);
        _ui.UndoButton.onClick.AddListener(Undo);
        _ui.RandomButton.onClick.AddListener(Randomize);
        _ui.SaveButton.onClick.AddListener(Save);
        _ui.KeepColorsButton.onClick.AddListener(() => {
            _keepColors = !_keepColors;
            _ui.KeepColorsOn.SetActive(_keepColors);
        });
        _ui.MatchButton.onClick.AddListener(OpenMatch);
        _ui.Discard.Cancel.onClick.AddListener(() => _ui.Discard.Overlay.SetActive(false));
        _ui.Discard.Confirm.onClick.AddListener(Close);
        _ui.Picker.Changed += OnColorDragged;
        _ui.Picker.Committed += OnColorCommitted;
        for (int i = 0; i < _ui.Tabs.Length; i++) {
            int index = i;
            _ui.Tabs[i].Button.onClick.AddListener(() => SelectCategory(index));
        }
        CharacterPreviewDrag drag = _ui.Preview.gameObject.AddComponent<CharacterPreviewDrag>();
        drag.SetStage(stage);
    }

    /// <param name="previewReady">Panggung sudah menampilkan record ini (tidak perlu dirakit ulang).</param>
    public void Open(CustomCharacterRecord record, int slot, bool previewReady, Action closed) {
        if (record == null || !CharacterTemplates.TryGet(record.templateId, out _template)) {
            closed?.Invoke();
            return;
        }
        _session = new CustomizationSession(record, slot);
        _closed = closed;
        _ui.Root.SetActive(true);
        _ui.Discard.Overlay.SetActive(false);
        _ui.MatchPopup.SetActive(false);
        _ui.KeepColorsOn.SetActive(_keepColors);
        if (_stage) _ui.Preview.texture = _stage.Texture;
        if (!previewReady) RebuildLive();
        SelectCategory(DefaultCategory);
        RefreshTopBar();
    }

    void Update() {
        if (!IsOpen) return;
        if (_stage) _ui.Loading.SetActive(_stage.IsBusy);
        if (!Input.GetKeyDown(KeyCode.Escape)) return;
        if (_ui.MatchPopup.activeSelf) _ui.MatchPopup.SetActive(false);
        else if (_ui.Discard.Overlay.activeSelf) _ui.Discard.Overlay.SetActive(false);
        else Back();
    }

    // ---------------------------------------------------------------- Bar atas

    private void Back() {
        if (_session != null && _session.IsDirty) {
            _ui.Discard.Overlay.SetActive(true);
            return;
        }
        Close();
    }

    private void Close() {
        _ui.Discard.Overlay.SetActive(false);
        _ui.Root.SetActive(false);
        _session = null;
        Action closed = _closed;
        _closed = null;
        closed?.Invoke();
    }

    private void Undo() {
        if (!_session.Undo()) return;
        RebuildLive();
        RefreshPanel();
    }

    private void Randomize() {
        _session.Replace(CustomizerRandomizer.Randomize(_session.Current, _template));
        RebuildLive();
        RefreshPanel();
    }

    private void Save() {
        int slot = CustomCharacterStore.Save(_session.Slot, _session.Current);
        if (slot < 0) {
            ShowToast("Gagal menyimpan karakter");
            return;
        }
        _session.MarkSaved(slot);
        RefreshTopBar();
        ShowToast("Penampilan tersimpan");
    }

    private void RefreshTopBar() {
        string name = _session.Slot >= 0 ? CustomSlotRules.SlotName(_session.Slot) : "Karakter baru";
        _ui.NameText.text = name + " · " + _template.Label;
        _ui.DirtyPill.SetActive(_session.IsDirty);
        _ui.UndoButton.interactable = _session.CanUndo;
    }

    // ---------------------------------------------------------------- Kategori & sub-tab

    private void SelectCategory(int index) {
        _category = index;
        for (int i = 0; i < _ui.Tabs.Length; i++) {
            bool on = i == index;
            _ui.Tabs[i].Selected.SetActive(on);
            _ui.Tabs[i].Icon.color = on ? Color.white : FriendsTheme.Muted;
            _ui.Tabs[i].Label.color = on ? Color.white : FriendsTheme.Muted;
        }
        EditorCategory category = CurrentCategory;
        _ui.HeaderTitle.text = category.Label.ToUpperInvariant();
        _ui.HeaderIcon.sprite = ProfileMenuBuilder.LoadIcon(category.Icon);

        bool outfits = category.Kind == EditorCategoryKind.Outfits;
        _ui.OutfitPanel.SetActive(outfits);
        _ui.PlaceholderPanel.SetActive(!outfits);
        _ui.PlaceholderText.text = category.Label + " bisa diatur di langkah berikutnya.";
        BuildSubTabs(category);
        if (outfits) SelectSubTab(0);
    }

    private void BuildSubTabs(EditorCategory category) {
        ChatUiKit.DestroyChildren(_ui.SubTabRow);
        _subTabSelected.Clear();
        for (int i = 0; i < category.SubTabs.Count; i++) {
            int index = i;
            Button chip = CustomizerEditorView.CreateChip(_ui.SubTabRow, _a, category.SubTabs[i].Label, out GameObject selected);
            chip.onClick.AddListener(() => SelectSubTab(index));
            _subTabSelected.Add(selected);
        }
    }

    private void SelectSubTab(int index) {
        _subTab = index;
        _channel = 0;
        for (int i = 0; i < _subTabSelected.Count; i++) _subTabSelected[i].SetActive(i == index);
        BuildGrid();
        RefreshColors();
        _ui.GridScroll.verticalNormalizedPosition = 1f;
    }

    /// Grid, centang, dan panel warna mengikuti record saat ini (setelah URUNGKAN / ACAK).
    private void RefreshPanel() {
        if (CurrentCategory.Kind == EditorCategoryKind.Outfits) {
            RefreshItemSelection();
            RefreshColors();
        }
        RefreshTopBar();
    }

    // ---------------------------------------------------------------- Grid item

    private void BuildGrid() {
        ChatUiKit.DestroyChildren(_ui.GridContent);
        _items.Clear();
        string type = CurrentType;
        if (CustomizationRules.CanBeEmpty(type, _template.IsMale)) {
            Button empty = CustomizerEditorView.CreateItem(_ui.GridContent, _a, ProfileMenuBuilder.LoadIcon("icon-none"),
                                                           "Kosong", out GameObject selected);
            empty.onClick.AddListener(RemoveOutfit);
            _items.Add(("", selected));
        }
        foreach (Outfit outfit in OutfitCatalog.ForType(type, _template.IsAnime)) {
            string label = string.IsNullOrWhiteSpace(outfit.OutfitName) ? outfit.name : outfit.OutfitName;
            Button item = CustomizerEditorView.CreateItem(_ui.GridContent, _a, outfit.OutfitIcon, label, out GameObject selected);
            Outfit captured = outfit;
            item.onClick.AddListener(() => WearOutfit(captured));
            _items.Add((OutfitCatalog.PathOf(type, outfit), selected));
        }
        RefreshItemSelection();
    }

    private void RefreshItemSelection() {
        string worn = RecordEdits.Find(_session.Current, CurrentType)?.path ?? "";
        foreach ((string path, GameObject selected) in _items) selected.SetActive(path == worn);
    }

    private void WearOutfit(Outfit outfit) {
        string type = CurrentType;
        OutfitRecord next = OutfitCatalog.DefaultRecord(type, outfit);
        OutfitRecord previous = RecordEdits.Find(_session.Current, type);
        if (previous != null && previous.path == next.path) return;
        if (_keepColors && previous != null) {
            for (int i = 0; i < next.colors.Count && i < previous.colors.Count; i++) next.colors[i] = previous.colors[i];
        }
        _session.Apply(r => RecordEdits.WithOutfit(r, next));
        StartCoroutine(AttachLive(outfit, next.colors));
        _channel = 0;
        RefreshPanel();
    }

    private void RemoveOutfit() {
        string type = CurrentType;
        if (RecordEdits.Find(_session.Current, type) == null) return;
        _session.Apply(r => RecordEdits.WithoutOutfit(r, type));
        OutfitSystem avatar = _stage ? _stage.LiveAvatar : null;
        if (avatar) avatar.RemoveOutfit(type);
        RefreshPanel();
    }

    /// Outfit BoZo memasang dirinya sendiri di Start (lalu memakai warna bawaan); warna record dipasang setelahnya.
    private IEnumerator AttachLive(Outfit prefab, List<string> colors) {
        OutfitSystem avatar = _stage ? _stage.LiveAvatar : null;
        if (!avatar) {
            RebuildLive();
            yield break;
        }
        Outfit instance = Instantiate(prefab, avatar.transform);
        float until = Time.unscaledTime + AttachTimeout;
        while (instance && !instance.attached && Time.unscaledTime < until) yield return null;
        yield return null; // Start (SetColorInital) sudah lewat
        if (!instance) yield break;
        for (int i = 0; i < colors.Count; i++) instance.SetColor(CharacterRecordConverter.FromHex(colors[i]), i + 1);
    }

    // ---------------------------------------------------------------- Warna

    private void RefreshColors() {
        ChatUiKit.DestroyChildren(_ui.ChannelList);
        _channels.Clear();
        OutfitRecord record = RecordEdits.Find(_session.Current, CurrentType);
        Outfit prefab = OutfitCatalog.Load(record?.path);
        _ui.ColorSection.SetActive(record != null && prefab != null);
        if (record == null || prefab == null) return;

        string itemName = string.IsNullOrWhiteSpace(prefab.OutfitName) ? prefab.name : prefab.OutfitName;
        _ui.ColorTitle.text = "Warna · <color=#FFFFFF>" + itemName + "</color>";
        int count = OutfitCatalog.ChannelCount(prefab);
        _channel = Mathf.Clamp(_channel, 0, count - 1);
        for (int i = 0; i < count; i++) {
            int index = i;
            string name = prefab.ColorChannels != null && i < prefab.ColorChannels.Length ? prefab.ColorChannels[i] : "";
            Button button = CustomizerEditorView.CreateChannel(_ui.ChannelList, _a, CustomizationRules.ColorChannelLabel(name, i),
                                                               out Image swatch, out GameObject selected);
            swatch.color = ChannelColor(record, i);
            button.onClick.AddListener(() => SelectChannel(index));
            _channels.Add((swatch, selected));
        }
        SelectChannel(_channel);
    }

    private void SelectChannel(int index) {
        _channel = index;
        for (int i = 0; i < _channels.Count; i++) _channels[i].selected.SetActive(i == index);
        OutfitRecord record = RecordEdits.Find(_session.Current, CurrentType);
        if (record != null) _ui.Picker.SetColor(ChannelColor(record, index));
    }

    private static Color ChannelColor(OutfitRecord record, int channel) {
        return channel < record.colors.Count ? CharacterRecordConverter.FromHex(record.colors[channel]) : Color.white;
    }

    /// Saat digeser: avatar & bulatan warna langsung berubah, belum masuk riwayat URUNGKAN.
    private void OnColorDragged(Color color) {
        if (_channel < _channels.Count) _channels[_channel].swatch.color = color;
        SetLiveColor(CurrentType, _channel, color);
    }

    private void OnColorCommitted(Color color) {
        string type = CurrentType;
        int channel = _channel;
        _session.Apply(r => RecordEdits.WithChannelColor(r, type, channel, CharacterRecordConverter.ToHex(color)));
        // BoZo melewatkan update tekstur saat masih sibuk: pasang sekali lagi setelah selesai.
        StartCoroutine(ApplyWhenIdle(type, channel, color));
        RefreshTopBar();
    }

    private void SetLiveColor(string type, int channel, Color color) {
        OutfitSystem avatar = _stage ? _stage.LiveAvatar : null;
        Outfit outfit = avatar ? avatar.GetOutfit(type) : null;
        if (outfit) outfit.SetColor(color, channel + 1);
    }

    private IEnumerator ApplyWhenIdle(string type, int channel, Color color) {
        float until = Time.unscaledTime + AttachTimeout;
        while (_stage && _stage.IsBusy && Time.unscaledTime < until) yield return null;
        SetLiveColor(type, channel, color);
    }

    // ---------------------------------------------------------------- Samakan dengan

    private void OpenMatch() {
        ChatUiKit.DestroyChildren(_ui.MatchList);
        string type = CurrentType;
        foreach (OutfitRecord other in _session.Current.outfits) {
            string otherType = RecordEdits.TypeOf(other.path);
            string label = SubTabLabel(otherType);
            if (otherType == type || label == null) continue;
            Button option = CustomizerEditorView.CreateListOption(_ui.MatchList, _a, label);
            option.onClick.AddListener(() => MatchColors(otherType));
        }
        _ui.MatchPopup.SetActive(true);
    }

    private void MatchColors(string sourceType) {
        _ui.MatchPopup.SetActive(false);
        string type = CurrentType;
        OutfitRecord source = RecordEdits.Find(_session.Current, sourceType);
        OutfitRecord target = RecordEdits.Find(_session.Current, type);
        if (source == null || target == null) return;
        int count = Mathf.Min(source.colors.Count, OutfitCatalog.ChannelCount(OutfitCatalog.Load(target.path)));
        _session.Apply(r => {
            CustomCharacterRecord result = r;
            for (int i = 0; i < count; i++) result = RecordEdits.WithChannelColor(result, type, i, source.colors[i]);
            return result;
        });
        for (int i = 0; i < count; i++) SetLiveColor(type, i, CharacterRecordConverter.FromHex(source.colors[i]));
        RefreshColors();
        RefreshTopBar();
    }

    /// Label sub-tab untuk tipe outfit, atau null kalau tipe ini tidak punya tab (kepala, badan, mata, ...).
    private static string SubTabLabel(string type) {
        foreach (EditorCategory category in EditorCategories.All) {
            foreach (EditorSubTab tab in category.SubTabs) {
                if (tab.OutfitType == type) return category.Label == tab.Label ? tab.Label : category.Label + " · " + tab.Label;
            }
        }
        return null;
    }

    // ---------------------------------------------------------------- Bantuan

    private void RebuildLive() {
        if (!_stage || !CustomizerSettings.Instance || !CharacterDatabase.Instance) return;
        _stage.ShowLive(CustomizerSettings.Instance.characterBase, CharacterRecordConverter.ToCharacterData(_session.Current),
                        CharacterDatabase.Instance.Get(_template.Id));
    }

    private void ShowToast(string message) {
        if (_toast != null) StopCoroutine(_toast);
        _toast = StartCoroutine(ToastRoutine(message));
    }

    private IEnumerator ToastRoutine(string message) {
        _ui.Toast.text = message;
        GameObject pill = _ui.Toast.transform.parent.gameObject;
        pill.SetActive(true);
        yield return new WaitForSecondsRealtime(ToastSeconds);
        pill.SetActive(false);
        _toast = null;
    }
}
