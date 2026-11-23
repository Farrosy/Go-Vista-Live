using System;
using System.Collections;
using System.Collections.Generic;
using Bozo.ModularCharacters;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Pengendali editor karakter di scene KARAKTER. Sumber kebenaran = CustomizationSession (record kerja +
/// URUNGKAN). Avatar 3D di panggung diubah langsung supaya cepat (LiveAvatarEdits):
///  - ganti baju  → instance outfit BoZo di bawah avatar (BoZo mengganti slot + merge), lalu warna record;
///  - ganti warna → Outfit.SetColor (tanpa merge); kulit ikut tersalin ke kepala lewat LinkedColorSets;
///  - motif, stiker, wajah → update tekstur/blendshape BoZo;
///  - URUNGKAN → avatar dirakit ulang dari record.
/// Wajah · Bentuk ada di CustomizerFaceShapes, Stiker di CustomizerStickers.
/// </summary>
public sealed class CustomizerEditor : MonoBehaviour {
    private const float ToastSeconds = 2.2f;
    private const float IdleTimeout = 5f;
    private const string DefaultCategory = "Atasan"; // seperti mockup layar 2
    private const int ThumbnailWidth = 240;
    private const int ThumbnailHeight = 360;
    private const string HeadType = "Head";

    private static readonly Dictionary<string, string> TypeLabels = new Dictionary<string, string> {
        { "Eyes", "Mata" }, { "Pupil", "Pupil" }, { "EyeShine", "Kilau Mata" },
        { "EyeBrows", "Alis" }, { "EyeLashes", "Bulu Mata" },
        { "MakeUpLips", "Bibir" }, { "MakeUpCheeks", "Pipi" }, { "MakeUpEyes", "Eyeliner" }, { "FaceDetails", "Bintik Wajah" },
    };

    private CustomizerEditorRefs _ui;
    private ChatUiAssets _a;
    private CharacterPreviewStage _stage;
    private CustomizationSession _session;
    private CharacterTemplate _template;
    /// Record template asli (cache per sesi editor) untuk warna alis/bulu mata.
    private CustomCharacterRecord _templateRecord;
    /// Alis/bulu mata terakhir di kepala anime (sesi editor ini), dipasang lagi saat kembali dari kepala Basic.
    private IReadOnlyDictionary<string, OutfitRecord> _animeFaceMemory;
    private CustomizerFaceShapes _faces;
    private CustomizerStickers _stickers;
    private CustomizerShop _shop;
    private Action _closed;
    private Coroutine _toast;
    private IReadOnlyList<EditorCategory> _categories = EditorCategories.All;

    private int _category = -1;
    private int _subTab;
    private string _focusType = "";
    private int _channel;
    private bool _patternMode;
    private bool _keepColors; // default mati: item baru memakai warna aslinya kecuali pemain menyalakan "Simpan warna"
    private bool _wasBusy;
    private readonly List<GameObject> _subTabSelected = new List<GameObject>();
    private readonly List<(string type, string path, GameObject selected)> _items = new List<(string, string, GameObject)>();
    private readonly List<(Image swatch, GameObject selected)> _channels = new List<(Image, GameObject)>();
    /// Item yang daftar bagian warnanya sedang tampil; scroll daftar itu kembali ke atas hanya saat item berganti.
    private string _channelsFor = "";
    private readonly List<(string id, GameObject selected)> _patterns = new List<(string, GameObject)>();

    public bool IsOpen => _ui != null && _ui.Root.activeSelf;

    /// <summary>Record berhasil disimpan ke slot ini (indeks slot).</summary>
    public event Action<int> Saved;

    /// <summary>
    /// true = karakter baru yang tidak muat (slot penuh) boleh menimpa slot yang ada: SIMPAN menampilkan
    /// pilihan Karakter 1/2. Dipakai ruang ganti, karena pemain yang memakai template tidak bisa ke layar pilih.
    /// </summary>
    public bool OfferOverwriteWhenFull { get; set; }

    // ---------------------------------------------------------------- Untuk panel Wajah/Stiker

    public CustomizationSession Session => _session;
    public CharacterTemplate Template => _template;
    public OutfitSystem Avatar => _stage ? _stage.LiveAvatar : null;
    public bool IsAvatarBusy => _stage && _stage.IsBusy;
    public void AfterEdit() => RefreshTopBar();
    public void Toast(string message) => ShowToast(message);
    /// <summary>Label harga + dialog beli (dipakai juga oleh grid Stiker).</summary>
    public CustomizerShop Shop => _shop;

    /// <summary>Pasang ulang stiker record SAAT INI ke avatar begitu BoZo selesai update (dipanggil setelah commit).</summary>
    public void ApplyDecalsWhenIdle() => StartCoroutine(WhenIdle(() => LiveAvatarEdits.ApplyDecals(Avatar, _session?.Current)));

    private EditorCategory CurrentCategory => _categories[_category];
    private EditorSubTab CurrentSubTab => CurrentCategory.SubTabs[_subTab];
    private bool ShowsOutfits => CurrentSubTab.Panel == EditorPanelKind.Outfits || CurrentSubTab.Panel == EditorPanelKind.Skin ||
                                 CurrentSubTab.Panel == EditorPanelKind.Expressions;

    /// <param name="categories">Tab yang sama dengan yang dipakai CustomizerEditorView.Build; null = semua.</param>
    public void Init(CustomizerEditorRefs ui, ChatUiAssets assets, CharacterPreviewStage stage,
                     IReadOnlyList<EditorCategory> categories = null) {
        _ui = ui;
        _categories = categories ?? EditorCategories.All;
        _a = assets;
        _stage = stage;
        _shop = new CustomizerShop(ui.Cart, assets);
        _shop.Purchased += () => ShowToast("Item terbeli");
        _faces = new CustomizerFaceShapes(this, ui.Shapes, assets);
        _stickers = new CustomizerStickers(this, ui.Stickers, ui.ColorPopup, assets);
        _ui.BackButton.onClick.AddListener(Back);
        _ui.UndoButton.onClick.AddListener(Undo);
        _ui.SaveButton.onClick.AddListener(Save);
        _ui.KeepColorsButton.onClick.AddListener(() => {
            _keepColors = !_keepColors;
            _ui.KeepColorsOn.SetActive(_keepColors);
        });
        _ui.MatchButton.onClick.AddListener(OpenMatch);
        _ui.ColorModeButton.onClick.AddListener(() => SetPatternMode(false));
        _ui.PatternModeButton.onClick.AddListener(() => SetPatternMode(true));
        _ui.BodyCamButton.onClick.AddListener(() => SetFaceCamera(false));
        _ui.FaceCamButton.onClick.AddListener(() => SetFaceCamera(true));
        _ui.Discard.Cancel.onClick.AddListener(() => _ui.Discard.Overlay.SetActive(false));
        _ui.Discard.Confirm.onClick.AddListener(Close);
        _ui.Overwrite.Cancel.onClick.AddListener(() => SaveOver(0));
        _ui.Overwrite.Confirm.onClick.AddListener(() => SaveOver(1));
        _ui.OverwriteClose.onClick.AddListener(() => _ui.Overwrite.Overlay.SetActive(false));
        _ui.Picker.Changed += OnColorDragged;
        _ui.Picker.Committed += OnColorCommitted;
        _ui.Pattern.Scale.Slider.onValueChanged.AddListener(OnPatternScaleDragged);
        _ui.Pattern.Scale.Commit.Committed += OnPatternScaleCommitted;
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
        _templateRecord = null;
        _animeFaceMemory = null;
        _closed = closed;
        _ui.Root.SetActive(true);
        _ui.Discard.Overlay.SetActive(false);
        _ui.Overwrite.Overlay.SetActive(false);
        _ui.MatchPopup.SetActive(false);
        _shop.Close();
        _stickers.CloseColor();
        _ui.KeepColorsOn.SetActive(_keepColors);
        RefreshCoins();
        if (_stage) {
            _ui.Preview.texture = _stage.Texture;
            _stage.AutoRotate = false; // karakter diam saat diedit; tetap bisa digeser
        }
        if (!previewReady) RebuildLive();
        SelectCategory(EditorCategories.IndexOf(_categories, DefaultCategory));
        RefreshTopBar();
    }

    void OnEnable() => PlayerCoins.Changed += OnCoinsChanged;
    void OnDisable() => PlayerCoins.Changed -= OnCoinsChanged;

    private void OnCoinsChanged(int delta) {
        if (IsOpen) RefreshCoins(); // mis. setelah membeli item
    }

    private void RefreshCoins() {
        if (_ui?.CoinText) _ui.CoinText.text = CoinRules.FormatBalance(PlayerCoins.Balance);
    }

    void Update() {
        if (!IsOpen) return;
        bool busy = IsAvatarBusy;
        _ui.Loading.SetActive(busy);
        // Kepala baru selesai digabung: daftar slider wajah ikut blendshape kepala itu.
        if (_wasBusy && !busy && _faces.IsVisible) _faces.Build();
        _wasBusy = busy;
        if (!Input.GetKeyDown(KeyCode.Escape)) return;
        if (_stickers.IsColorOpen) _stickers.CloseColor();
        else if (_ui.MatchPopup.activeSelf) _ui.MatchPopup.SetActive(false);
        else if (_shop.IsOpen) _shop.Close();
        else if (_ui.Overwrite.Overlay.activeSelf) _ui.Overwrite.Overlay.SetActive(false);
        else if (_ui.Discard.Overlay.activeSelf) _ui.Discard.Overlay.SetActive(false);
        else Back();
    }

    // ---------------------------------------------------------------- Bar atas

    private void Back() {
        // Karakter baru (slot -1) belum pernah disimpan: selalu konfirmasi, walau belum ada yang diubah.
        if (_session != null && (_session.IsDirty || _session.Slot < 0)) {
            _ui.Discard.Overlay.SetActive(true);
            return;
        }
        Close();
    }

    private void Close() {
        _ui.Discard.Overlay.SetActive(false);
        _stickers.CloseColor();
        _ui.Root.SetActive(false);
        if (_stage) {
            _stage.SetFaceFocus(false);
            _stage.AutoRotate = true;
        }
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

    /// Item yang belum dimiliki dibayar dulu (keranjang belanja), baru disimpan.
    private void Save() {
        if (_shop.InterceptSave(_session.Current, _session.Saved, SaveAfterCheckout)) return;
        SaveOwned();
    }

    /// <param name="final">Record tanpa item yang tidak dicentang di keranjang; null = tidak ada yang dilepas.</param>
    private void SaveAfterCheckout(CustomCharacterRecord final) {
        if (_session == null) return;
        if (final != null) {
            _session.Replace(final); // satu langkah URUNGKAN
            RebuildLive();
            RefreshPanel();
            if (ShowsOutfits) BuildGrid();
        }
        SaveOwned();
    }

    private void SaveOwned() {
        if (_session == null) return;
        if (_session.Slot < 0 && !CustomCharacterStore.CanCreate && OfferOverwriteWhenFull) {
            _ui.Overwrite.Overlay.SetActive(true);
            return;
        }
        SaveTo(_session.Slot);
    }

    /// Pilihan di dialog "Slot karakter penuh": timpa slot itu dengan penampilan ini.
    private void SaveOver(int slot) {
        _ui.Overwrite.Overlay.SetActive(false);
        SaveTo(slot);
    }

    private void SaveTo(int target) {
        int slot = CustomCharacterStore.Save(target, _session.Current);
        if (slot < 0) {
            ShowToast("Gagal menyimpan karakter");
            return;
        }
        _session.MarkSaved(slot);
        RefreshTopBar();
        ShowToast("Penampilan tersimpan");
        // Kunci thumbnail = record bersih seperti yang dibaca layar Pilih Karakter.
        StartCoroutine(CaptureThumbnail(CustomCharacterStore.Get(slot)));
        Saved?.Invoke(slot);
    }

    /// Foto untuk kartu slot di layar Pilih Karakter, diambil setelah BoZo selesai update tekstur.
    private IEnumerator CaptureThumbnail(CustomCharacterRecord saved) {
        yield return WhenIdle(null);
        yield return null;
        if (_session == null || _session.IsDirty || !_stage) yield break; // sudah diubah lagi: foto tidak cocok
        Texture2D shot = _stage.CaptureThumbnail(ThumbnailWidth, ThumbnailHeight);
        if (shot) SlotThumbnails.Store(saved, shot, CustomCharacterStore.All());
    }

    private void RefreshTopBar() {
        string name = _session.Slot >= 0 ? CustomSlotRules.SlotName(_session.Slot) : "Karakter baru";
        _ui.NameText.text = name + " · " + _template.Label;
        _ui.DirtyPill.SetActive(_session.IsDirty || _session.Slot < 0); // karakter baru = belum disimpan
        _ui.UndoButton.interactable = _session.CanUndo;
    }

    // ---------------------------------------------------------------- Kamera

    private void SetFaceCamera(bool face) {
        if (_stage) _stage.SetFaceFocus(face);
        _ui.BodyCamOn.SetActive(!face);
        _ui.FaceCamOn.SetActive(face);
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
        SetFaceCamera(category.FocusFace);
        BuildSubTabs(category);
        SelectSubTab(0);
    }

    private void BuildSubTabs(EditorCategory category) {
        ChatUiKit.DestroyChildren(_ui.SubTabRow);
        _subTabSelected.Clear();
        _ui.SubTabScroll.horizontalNormalizedPosition = 0f;
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
        _patternMode = false;
        _focusType = CurrentSubTab.OutfitType;
        for (int i = 0; i < _subTabSelected.Count; i++) _subTabSelected[i].SetActive(i == index);

        EditorPanelKind panel = CurrentSubTab.Panel;
        _ui.OutfitPanel.SetActive(ShowsOutfits);
        _faces.Show(panel == EditorPanelKind.FaceShapes);
        _stickers.Show(panel == EditorPanelKind.Stickers);
        if (!ShowsOutfits) return;
        BuildGrid();
        RefreshColors();
        _ui.GridScroll.verticalNormalizedPosition = 1f;
    }

    /// Panel aktif mengikuti record saat ini (setelah URUNGKAN).
    private void RefreshPanel() {
        switch (CurrentSubTab.Panel) {
            case EditorPanelKind.FaceShapes: _faces.Refresh(); break;
            case EditorPanelKind.Stickers: _stickers.Refresh(); break;
            default:
                RefreshItemSelection();
                RefreshColors();
                break;
        }
        RefreshTopBar();
    }

    // ---------------------------------------------------------------- Grid item

    private void BuildGrid() {
        ChatUiKit.DestroyChildren(_ui.GridContent);
        _items.Clear();
        _shop.ForgetDestroyedTags();
        if (CurrentSubTab.Panel == EditorPanelKind.Skin) {
            BuildSkinTones();
            return;
        }
        if (CurrentSubTab.Panel == EditorPanelKind.Expressions) {
            BuildExpressions();
            return;
        }
        IReadOnlyList<string> types = CurrentSubTab.OutfitTypes;
        bool animeHead = OutfitCatalog.IsAnimeHead(_session.Current);
        foreach (string type in types) {
            IReadOnlyList<Outfit> outfits = OutfitCatalog.ForType(type);
            bool canBeEmpty = CustomizationRules.CanBeEmpty(type, _template.IsMale);
            if (outfits.Count == 0 && types.Count > 1) continue;
            string title = types.Count > 1 && TypeLabels.TryGetValue(type, out string label) ? label : null;
            RectTransform grid = CustomizerEditorView.CreateItemSection(_ui.GridContent, _a, title);
            string sectionLabel = title ?? CurrentSubTab.Label;
            if (canBeEmpty) AddEmptyItem(grid, type, SectionBlockedMessage(type, sectionLabel, outfits, animeHead));
            foreach (Outfit outfit in outfits) AddOutfitItem(grid, type, outfit, animeHead);
        }
        RefreshItemSelection();
    }

    /// Bagian yang semua itemnya tidak bisa dipakai (mis. Kilau Mata di kepala Basic, Jenggot di karakter cewek):
    /// "Kosong" ikut disabled + toast, karena tidak ada pilihan lain. null = bagian ini bisa dipakai.
    private string SectionBlockedMessage(string type, string sectionLabel, IReadOnlyList<Outfit> outfits, bool animeHead) {
        if (outfits.Count == 0) return null;
        foreach (Outfit outfit in outfits) {
            if (BlockedReason(type, outfit, sectionLabel, animeHead) == null) return null;
        }
        return BlockedReason(type, outfits[0], sectionLabel, animeHead);
    }

    /// Alasan outfit ini tidak bisa dipakai karakter sekarang (isi toast), atau null kalau bisa.
    /// Gender template dicek dulu (GenderRules), lalu gaya kepala yang dipakai (FaceStyleRules).
    private string BlockedReason(string type, Outfit outfit, string label, bool animeHead) {
        string path = OutfitCatalog.PathOf(type, outfit);
        if (!GenderRules.Fits(path, _template.IsMale)) return GenderRules.BlockedMessage(label, GenderRules.GenderOf(path));
        FaceStyle required = OutfitCatalog.RequiredStyle(type, outfit);
        return FaceStyleRules.Fits(required, animeHead) ? null : FaceStyleRules.IncompatibleMessage(label, required);
    }

    private void AddEmptyItem(RectTransform grid, string type, string blockedMessage) {
        Button empty = CustomizerEditorView.CreateItem(grid, _a, ProfileMenuBuilder.LoadIcon("icon-none"), "Kosong",
                                                       out GameObject selected);
        if (blockedMessage != null) {
            CustomizerEditorView.DimItem(empty);
            empty.onClick.AddListener(() => ShowToast(blockedMessage));
        } else {
            empty.onClick.AddListener(() => RemoveOutfit(type));
        }
        _items.Add((type, "", selected));
    }

    /// Item yang tidak bisa dipakai (gender lain / tidak cocok dengan kepala) tetap tampil abu-abu;
    /// diketuk = toast alasannya, bukan dipakai.
    private void AddOutfitItem(RectTransform grid, string type, Outfit outfit, bool animeHead) {
        string label = string.IsNullOrWhiteSpace(outfit.OutfitName) ? outfit.name : outfit.OutfitName;
        Button item = CustomizerEditorView.CreateItem(grid, _a, outfit.OutfitIcon, label, out GameObject selected);
        _shop.Tag(item, OutfitCatalog.PathOf(type, outfit)); // belum dimiliki: label harga (tetap bisa dicoba)
        string blocked = BlockedReason(type, outfit, label, animeHead);
        if (blocked == null) {
            item.onClick.AddListener(() => WearOutfit(type, outfit));
        } else {
            CustomizerEditorView.DimItem(item);
            item.onClick.AddListener(() => ShowToast(blocked));
        }
        _items.Add((type, OutfitCatalog.PathOf(type, outfit), selected));
    }

    private void BuildSkinTones() {
        RectTransform grid = CustomizerEditorView.CreateItemSection(_ui.GridContent, _a, null);
        foreach (string hex in CustomizationRules.SkinTones) {
            Color color = CharacterRecordConverter.FromHex(hex);
            Button swatch = CustomizerEditorView.CreateSwatchItem(grid, _a, color, out GameObject selected);
            swatch.onClick.AddListener(() => {
                _focusType = CurrentSubTab.OutfitType;
                _channel = 0;
                CommitColor(_focusType, 0, color);
                RefreshItemSelection();
                RefreshColors();
            });
            _items.Add((CurrentSubTab.OutfitType, hex, selected));
        }
        RefreshItemSelection();
    }

    /// Wajah · Ekspresi: kotak bernomor 1..8; ekspresi langsung tampil di avatar (bukan outfit, tanpa bagian warna).
    private void BuildExpressions() {
        RectTransform grid = CustomizerEditorView.CreateItemSection(_ui.GridContent, _a, null);
        for (int i = 0; i < FaceExpressions.Count; i++) {
            int index = i;
            Button item = CustomizerEditorView.CreateNumberItem(grid, _a, (i + 1).ToString(), FaceExpressions.Label(i),
                                                                out GameObject selected);
            item.onClick.AddListener(() => SetExpression(index));
            _items.Add(("", index.ToString(), selected));
        }
        RefreshItemSelection();
    }

    private void SetExpression(int expression) {
        _session.Apply(r => RecordEdits.WithExpression(r, expression));
        if (_stage) _stage.SetExpression(_session.Current.expression);
        RefreshItemSelection();
        RefreshTopBar();
    }

    private void RefreshItemSelection() {
        CustomCharacterRecord record = _session.Current;
        if (CurrentSubTab.Panel == EditorPanelKind.Expressions) {
            string current = record.expression.ToString();
            foreach ((string type, string key, GameObject selected) in _items) selected.SetActive(key == current);
            return;
        }
        bool skin = CurrentSubTab.Panel == EditorPanelKind.Skin;
        foreach ((string type, string key, GameObject selected) in _items) {
            OutfitRecord worn = RecordEdits.Find(record, type);
            string current = skin ? (worn != null && worn.colors.Count > 0 ? worn.colors[0] : "") : worn?.path ?? "";
            selected.SetActive(string.Equals(current, key, StringComparison.OrdinalIgnoreCase));
        }
    }

    private void WearOutfit(string type, Outfit outfit) {
        _focusType = type;
        OutfitRecord next = OutfitCatalog.DefaultRecord(type, outfit);
        OutfitRecord previous = RecordEdits.Find(_session.Current, type);
        if (previous != null && previous.path == next.path) {
            RefreshColors(); // ketuk item yang sudah dipakai = pilih bagian warnanya
            return;
        }
        if (_keepColors && previous != null && CustomizationRules.KeepsPreviousColors(type)) {
            for (int i = 0; i < next.colors.Count && i < previous.colors.Count; i++) next.colors[i] = previous.colors[i];
        }
        if (CustomizationRules.UsesTemplateColors(type)) CopyTemplateColors(type, next);
        CopyLinkedColors(outfit, next);
        bool wasAnime = OutfitCatalog.IsAnimeHead(_session.Current);
        bool styleChanged = type == HeadType && OutfitCatalog.IsAnimeHead(next.path) != wasAnime;
        if (styleChanged) {
            WearHeadOfOtherStyle(next, !wasAnime);
        } else {
            _session.Apply(r => CharacterRecordConverter.WithOutfitClearingDecals(r, next));
            StartCoroutine(AttachLive(outfit, next));
        }
        _channel = 0;
        _patternMode = false;
        RefreshPanel();
    }

    /// Ganti kepala Anime ↔ Basic dalam satu langkah URUNGKAN: mata + pupil ikut gaya baru (tidak melotot/juling),
    /// bagian wajah yang tidak cocok dilepas, alis/bulu mata anime terakhir dipasang lagi saat kembali ke anime.
    private void WearHeadOfOtherStyle(OutfitRecord head, bool animeHead) {
        IReadOnlyDictionary<string, OutfitRecord> restore = null;
        if (animeHead) {
            _templateRecord ??= CharacterRecordConverter.FromTemplate(_template);
            restore = _animeFaceMemory ?? FaceStyleRules.RememberRestorable(_templateRecord);
        } else {
            _animeFaceMemory = FaceStyleRules.RememberRestorable(_session.Current);
        }
        _session.Apply(r => FaceStyleRules.ForHeadStyle(CharacterRecordConverter.WithOutfitClearingDecals(r, head), animeHead,
                                                        path => OutfitCatalog.Fits(path, animeHead),
                                                        OutfitCatalog.DefaultRecord, restore));
        RebuildLive(); // banyak bagian wajah berubah sekaligus
    }

    /// Alis/bulu mata baru memakai warna dari template (mis. alis cokelat template), bukan item sebelumnya.
    private void CopyTemplateColors(string type, OutfitRecord next) {
        _templateRecord ??= CharacterRecordConverter.FromTemplate(_template);
        OutfitRecord source = RecordEdits.Find(_templateRecord, type);
        if (source == null) return; // template tidak memakai tipe ini: tetap warna bawaan prefab
        for (int i = 0; i < next.colors.Count && i < source.colors.Count; i++) next.colors[i] = source.colors[i];
    }

    /// Kepala baru memakai warna kulit badan (dan sebaliknya), tidak warna bawaan prefab.
    private void CopyLinkedColors(Outfit prefab, OutfitRecord next) {
        foreach ((string linkedType, int range) in LiveAvatarEdits.LinkedTypes(prefab)) {
            OutfitRecord linked = RecordEdits.Find(_session.Current, linkedType);
            if (linked == null) continue;
            for (int i = 0; i < range && i < next.colors.Count && i < linked.colors.Count; i++) next.colors[i] = linked.colors[i];
        }
    }

    private void RemoveOutfit(string type) {
        _focusType = type;
        if (RecordEdits.Find(_session.Current, type) == null) {
            RefreshColors();
            return;
        }
        _session.Apply(r => CharacterRecordConverter.WithoutOutfitClearingDecals(r, type));
        OutfitSystem avatar = Avatar;
        if (avatar) avatar.RemoveOutfit(type);
        ApplyDecalsWhenIdle(); // stiker di baju yang dilepas ikut hilang
        RefreshPanel();
    }

    /// Outfit BoZo memasang dirinya sendiri di Start (lalu memakai warna bawaan); record dipasang setelahnya.
    private IEnumerator AttachLive(Outfit prefab, OutfitRecord record) {
        OutfitSystem avatar = Avatar;
        if (!avatar) {
            RebuildLive();
            yield break;
        }
        Outfit instance = Instantiate(prefab, avatar.transform);
        float until = Time.unscaledTime + IdleTimeout;
        while (instance && !instance.attached && Time.unscaledTime < until) yield return null;
        yield return null; // Start (SetColorInital) sudah lewat
        if (!instance) yield break;
        LiveAvatarEdits.ApplyColors(instance, record);
        if (instance.Type != null && instance.Type.name == "Head") {
            LiveAvatarEdits.ApplyFaceShapes(avatar, _session?.Current);
            if (_stage && _session != null) _stage.SetExpression(_session.Current.expression); // kepala baru = bobot 0
        }
        ApplyDecalsWhenIdle(); // stiker baju lama dibuang dari baju lain/kulit
    }

    // ---------------------------------------------------------------- Warna

    private void RefreshColors() {
        ChatUiKit.DestroyChildren(_ui.ChannelList);
        _channels.Clear();
        OutfitRecord record = RecordEdits.Find(_session.Current, _focusType);
        Outfit prefab = OutfitCatalog.Load(record?.path);
        _ui.ColorSection.SetActive(record != null && prefab != null);
        if (record == null || prefab == null) return;

        string itemName = string.IsNullOrWhiteSpace(prefab.OutfitName) ? prefab.name : prefab.OutfitName;
        _ui.ColorTitle.text = "Warna · <color=#FFFFFF>" + itemName + "</color>";
        bool canPattern = prefab.supportPatterns && TexturePackageCatalog.Patterns.Count > 0;
        if (!canPattern) _patternMode = false;
        // Tetap bisa ditekan: baju tanpa motif memberi tahu baju mana yang bisa (bukan tombol mati tanpa penjelasan).
        _ui.PatternModeGroup.alpha = canPattern ? 1f : 0.35f;
        _ui.ColorModeOn.SetActive(!_patternMode);
        _ui.PatternModeOn.SetActive(_patternMode);
        _ui.ColorArea.SetActive(!_patternMode);
        _ui.Pattern.Root.SetActive(_patternMode);
        if (_patternMode) {
            RefreshPatterns(record);
            return;
        }

        int count = OutfitCatalog.ChannelCount(prefab);
        _channel = Mathf.Clamp(_channel, 0, count - 1);
        string channelsFor = _focusType + "|" + record.path;
        if (channelsFor != _channelsFor) {
            _channelsFor = channelsFor;
            _ui.ChannelScroll.verticalNormalizedPosition = 1f;
        }
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
        OutfitRecord record = RecordEdits.Find(_session.Current, _focusType);
        if (record != null) _ui.Picker.SetColor(ChannelColor(record, index));
    }

    private static Color ChannelColor(OutfitRecord record, int channel) {
        return channel < record.colors.Count ? CharacterRecordConverter.FromHex(record.colors[channel]) : Color.white;
    }

    /// Saat digeser: avatar & bulatan warna langsung berubah, belum masuk riwayat URUNGKAN.
    private void OnColorDragged(Color color) {
        if (_channel < _channels.Count) _channels[_channel].swatch.color = color;
        SetLiveColor(_focusType, _channel, color);
    }

    private void OnColorCommitted(Color color) {
        CommitColor(_focusType, _channel, color);
        if (CurrentSubTab.Panel == EditorPanelKind.Skin) RefreshItemSelection();
    }

    /// Satu langkah URUNGKAN: warna bagian ini + tipe tertaut (kulit Body ↔ Head) seperti yang dilakukan BoZo.
    private void CommitColor(string type, int channel, Color color) {
        string hex = CharacterRecordConverter.ToHex(color);
        _session.Apply(r => WithLinkedColor(r, type, channel, hex));
        SetLiveColor(type, channel, color);
        // BoZo melewatkan update tekstur saat masih sibuk: pasang sekali lagi setelah selesai.
        StartCoroutine(WhenIdle(() => SetLiveColor(type, channel, color)));
        RefreshTopBar();
    }

    private static CustomCharacterRecord WithLinkedColor(CustomCharacterRecord record, string type, int channel, string hex) {
        CustomCharacterRecord result = RecordEdits.WithChannelColor(record, type, channel, hex);
        Outfit prefab = OutfitCatalog.Load(RecordEdits.Find(record, type)?.path);
        foreach ((string linkedType, int range) in LiveAvatarEdits.LinkedTypes(prefab)) {
            OutfitRecord linked = RecordEdits.Find(result, linkedType);
            if (linked == null || channel >= range || channel >= OutfitCatalog.ChannelCount(OutfitCatalog.Load(linked.path))) continue;
            result = RecordEdits.WithChannelColor(result, linkedType, channel, hex);
        }
        return result;
    }

    private void SetLiveColor(string type, int channel, Color color) {
        OutfitSystem avatar = Avatar;
        Outfit outfit = avatar ? avatar.GetOutfit(type) : null;
        if (outfit) outfit.SetColor(color, channel + 1);
    }

    // ---------------------------------------------------------------- Motif

    private void SetPatternMode(bool pattern) {
        if (pattern && !CanUsePattern()) {
            ShowToast(PatternHint());
            return;
        }
        if (_patternMode == pattern) return;
        _patternMode = pattern;
        RefreshColors();
    }

    private bool CanUsePattern() {
        Outfit prefab = OutfitCatalog.Load(RecordEdits.Find(_session.Current, _focusType)?.path);
        return prefab != null && prefab.supportPatterns && TexturePackageCatalog.Patterns.Count > 0;
    }

    /// "Motif hanya untuk: Casual Jacket, Dress Shirt" (baju bertipe sama yang mendukung motif BoZo).
    private string PatternHint() {
        var names = new List<string>();
        foreach (Outfit outfit in OutfitCatalog.ForType(_focusType, OutfitCatalog.IsAnimeHead(_session.Current))) {
            if (!outfit.supportPatterns || !GenderRules.Fits(OutfitCatalog.PathOf(_focusType, outfit), _template.IsMale)) continue;
            names.Add(string.IsNullOrWhiteSpace(outfit.OutfitName) ? outfit.name : outfit.OutfitName);
        }
        return names.Count == 0 ? "Bagian ini tidak punya motif" : "Motif hanya untuk: " + string.Join(", ", names);
    }

    private void RefreshPatterns(OutfitRecord record) {
        ChatUiKit.DestroyChildren(_ui.Pattern.Options);
        _patterns.Clear();
        Button none = CustomizerEditorPanels.CreatePatternOption(_ui.Pattern.Options, _a, ProfileMenuBuilder.LoadIcon("icon-none"),
                                                                 "Tanpa", out GameObject noneSelected);
        none.onClick.AddListener(() => SetPattern(""));
        _patterns.Add(("", noneSelected));
        float maxScale = CustomizationRules.MaxPatternScale;
        foreach (TexturePackageCatalog.Entry entry in TexturePackageCatalog.Patterns) {
            Button option = CustomizerEditorPanels.CreatePatternOption(_ui.Pattern.Options, _a, entry.Icon, entry.Label,
                                                                       out GameObject selected);
            string id = entry.Id;
            option.onClick.AddListener(() => SetPattern(id));
            _patterns.Add((id, selected));
            if (id == record.pattern && entry.MaxScale > 0f) maxScale = Mathf.Min(maxScale, entry.MaxScale);
        }
        foreach ((string id, GameObject selected) in _patterns) selected.SetActive(id == (record.pattern ?? ""));
        bool hasPattern = !string.IsNullOrEmpty(record.pattern);
        _ui.Pattern.Scale.Slider.maxValue = maxScale;
        _ui.Pattern.Scale.SetValue(record.patternScaleX);
        _ui.Pattern.Scale.SetEnabled(hasPattern);
    }

    private void SetPattern(string id) {
        string type = _focusType;
        OutfitRecord current = RecordEdits.Find(_session.Current, type);
        if (current == null || current.pattern == id) return;
        bool fresh = string.IsNullOrEmpty(current.pattern);
        _session.Apply(r => {
            CustomCharacterRecord result = RecordEdits.WithPattern(r, type, id);
            return fresh ? RecordEdits.WithPatternScale(result, type, 1f) : result;
        });
        ApplyPatternLive(type);
        RefreshColors();
        RefreshTopBar();
    }

    private void OnPatternScaleDragged(float scale) {
        OutfitSystem avatar = Avatar;
        Outfit outfit = avatar && !IsAvatarBusy ? avatar.GetOutfit(_focusType) : null;
        if (outfit) outfit.SetPatternSize(new Vector2(scale, scale));
    }

    private void OnPatternScaleCommitted(float scale) {
        string type = _focusType;
        _session.Apply(r => RecordEdits.WithPatternScale(r, type, scale));
        ApplyPatternLive(type);
        RefreshTopBar();
    }

    private void ApplyPatternLive(string type) {
        StartCoroutine(WhenIdle(() => {
            OutfitSystem avatar = Avatar;
            if (avatar && _session != null) LiveAvatarEdits.ApplyPattern(avatar.GetOutfit(type), RecordEdits.Find(_session.Current, type));
        }));
    }

    // ---------------------------------------------------------------- Samakan dengan

    private void OpenMatch() {
        ChatUiKit.DestroyChildren(_ui.MatchList);
        foreach (OutfitRecord other in _session.Current.outfits) {
            string otherType = RecordEdits.TypeOf(other.path);
            string label = EditorCategories.LabelOf(otherType);
            if (otherType == _focusType || label == null) continue;
            Button option = CustomizerEditorView.CreateListOption(_ui.MatchList, _a, label);
            option.onClick.AddListener(() => MatchColors(otherType));
        }
        _ui.MatchPopup.SetActive(true);
    }

    private void MatchColors(string sourceType) {
        _ui.MatchPopup.SetActive(false);
        string type = _focusType;
        OutfitRecord source = RecordEdits.Find(_session.Current, sourceType);
        OutfitRecord target = RecordEdits.Find(_session.Current, type);
        if (source == null || target == null) return;
        int count = Mathf.Min(source.colors.Count, OutfitCatalog.ChannelCount(OutfitCatalog.Load(target.path)));
        _session.Apply(r => {
            CustomCharacterRecord result = r;
            for (int i = 0; i < count; i++) result = WithLinkedColor(result, type, i, source.colors[i]);
            return result;
        });
        for (int i = 0; i < count; i++) SetLiveColor(type, i, CharacterRecordConverter.FromHex(source.colors[i]));
        RefreshColors();
        RefreshTopBar();
    }

    // ---------------------------------------------------------------- Bantuan

    private void RebuildLive() {
        if (!_stage || !CustomizerSettings.Instance || !CharacterDatabase.Instance) return;
        _stage.ShowLive(CustomizerSettings.Instance.characterBase, CharacterRecordConverter.ToCharacterData(_session.Current),
                        CharacterDatabase.Instance.Get(_template.Id), _session.Current.expression);
    }

    /// Tunggu BoZo selesai merakit/merge/update tekstur (maks IdleTimeout), lalu jalankan action.
    private IEnumerator WhenIdle(Action action) {
        float until = Time.unscaledTime + IdleTimeout;
        yield return null;
        while (IsAvatarBusy && Time.unscaledTime < until) yield return null;
        action?.Invoke();
    }

    private void ShowToast(string message) {
        if (_toast != null) StopCoroutine(_toast);
        _toast = StartCoroutine(ToastRoutine(message));
    }

    private IEnumerator ToastRoutine(string message) {
        CustomizerUi.SetToastText(_ui.Toast, message);
        GameObject pill = _ui.Toast.transform.parent.gameObject;
        pill.SetActive(true);
        yield return new WaitForSecondsRealtime(ToastSeconds);
        pill.SetActive(false);
        _toast = null;
    }
}
