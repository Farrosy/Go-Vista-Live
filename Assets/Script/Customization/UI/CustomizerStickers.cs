using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Tab Stiker (mockup v2 layar 4): pilih stiker untuk ditambahkan (maks CustomizationRules.MaxDecals), daftar
/// stiker terpasang, lalu posisi preset, warna, ukuran, dan putar untuk stiker yang dipilih. Pengganti
/// dropdown tulang + slider XYZ BoZo. Saat digeser, avatar diperbarui langsung (dibatasi supaya bake tekstur
/// tidak menumpuk); saat dilepas, perubahan masuk session sebagai satu langkah URUNGKAN.
/// </summary>
public sealed class CustomizerStickers {
    private const float PreviewInterval = 0.12f;

    private readonly CustomizerEditor _editor;
    private readonly StickerPanelRefs _ui;
    private readonly ColorPopupRefs _popup;
    private readonly ChatUiAssets _a;
    private readonly List<(string id, GameObject selected)> _grid = new List<(string, GameObject)>();
    private int _selected = -1;
    private float _lastPreview = -1f;
    private bool _popupOpen;

    public CustomizerStickers(CustomizerEditor editor, StickerPanelRefs ui, ColorPopupRefs popup, ChatUiAssets assets) {
        _editor = editor;
        _ui = ui;
        _popup = popup;
        _a = assets;
        for (int i = 0; i < _ui.PositionButtons.Length; i++) {
            string position = CustomizationRules.DecalPositions[i];
            _ui.PositionButtons[i].onClick.AddListener(() => Edit(d => {
                d.position = position;
                d.offsetX = d.offsetY = 0f; // geser relatif ke preset: ganti/ketuk posisi = mulai dari titiknya
            }, commit: true));
        }
        _ui.Size.Slider.onValueChanged.AddListener(v => Edit(d => d.size = v, commit: false));
        _ui.Size.Commit.Committed += v => Edit(d => d.size = v, commit: true);
        _ui.Rotation.Slider.onValueChanged.AddListener(v => Edit(d => d.rotation = v, commit: false));
        _ui.Rotation.Commit.Committed += v => Edit(d => d.rotation = v, commit: true);
        WireSlider(_ui.OffsetX, (d, v) => d.offsetX = v);
        WireSlider(_ui.OffsetY, (d, v) => d.offsetY = v);
        _ui.ColorButton.onClick.AddListener(OpenColor);
        _popup.Picker.Changed += c => { if (_popupOpen) Edit(d => d.color = Hex(c), commit: false); };
        _popup.Picker.Committed += c => { if (_popupOpen) Edit(d => d.color = Hex(c), commit: true); };
        _popup.Done.onClick.AddListener(CloseColor);
    }

    public bool IsColorOpen => _popup.Overlay.activeSelf;

    public void Show(bool visible) {
        _ui.Root.SetActive(visible);
        if (!visible) return;
        if (_grid.Count == 0) BuildGrid();
        Refresh();
    }

    public void CloseColor() {
        _popupOpen = false;
        _popup.Overlay.SetActive(false);
    }

    /// <summary>Daftar terpasang + kontrol mengikuti record (setelah tambah/lepas/URUNGKAN).</summary>
    public void Refresh() {
        List<DecalRecord> decals = _editor.Session.Current.decals;
        _selected = decals.Count == 0 ? -1 : Mathf.Clamp(_selected < 0 ? decals.Count - 1 : _selected, 0, decals.Count - 1);
        _ui.Count.text = decals.Count + " / " + CustomizationRules.MaxDecals;
        _ui.EmptyText.gameObject.SetActive(decals.Count == 0);
        BuildLayers(decals);
        _ui.EditArea.SetActive(_selected >= 0);
        if (_selected >= 0) ShowControls(decals[_selected]);
    }

    private void BuildGrid() {
        foreach (TexturePackageCatalog.Entry sticker in TexturePackageCatalog.Stickers) {
            Button item = CustomizerEditorView.CreateItem(_ui.StickerGrid, _a, sticker.Icon, sticker.Label, out GameObject selected);
            string id = sticker.Id;
            item.onClick.AddListener(() => Add(id));
            _grid.Add((id, selected));
        }
    }

    private void BuildLayers(List<DecalRecord> decals) {
        ChatUiKit.DestroyChildren(_ui.LayerList);
        for (int i = 0; i < decals.Count; i++) {
            int index = i;
            DecalRecord decal = decals[i];
            TexturePackageCatalog.TryGetSticker(decal.decal, out TexturePackageCatalog.Entry entry);
            string name = entry.Label ?? decal.decal;
            string label = name + " · " + CustomizerEditorPanels.StickerPositionLabel(decal.position);
            Button row = CustomizerEditorPanels.CreateLayer(_ui.LayerList, _a, entry.Icon, label, out Button remove,
                                                            out GameObject selected);
            selected.SetActive(i == _selected);
            row.onClick.AddListener(() => Select(index));
            remove.onClick.AddListener(() => Remove(index));
        }
        string current = _selected >= 0 ? decals[_selected].decal : null;
        foreach ((string id, GameObject selected) in _grid) selected.SetActive(id == current);
    }

    private void ShowControls(DecalRecord decal) {
        for (int i = 0; i < _ui.PositionSelected.Length; i++)
            _ui.PositionSelected[i].SetActive(CustomizationRules.DecalPositions[i] == decal.position);
        _ui.ColorSwatch.color = CharacterRecordConverter.FromHex(decal.color);
        _ui.Size.SetValue(decal.size);
        _ui.Rotation.SetValue(decal.rotation);
        _ui.OffsetX.SetValue(decal.offsetX);
        _ui.OffsetY.SetValue(decal.offsetY);
    }

    private void WireSlider(SliderRow row, Action<DecalRecord, float> set) {
        row.Slider.onValueChanged.AddListener(v => Edit(d => set(d, v), commit: false));
        row.Commit.Committed += v => Edit(d => set(d, v), commit: true);
    }

    private void Select(int index) {
        _selected = index;
        Refresh();
    }

    private void Add(string id) {
        if (_editor.Session.Current.decals.Count >= CustomizationRules.MaxDecals) {
            _editor.Toast($"Maksimal {CustomizationRules.MaxDecals} stiker");
            return;
        }
        _editor.Session.Apply(r => RecordEdits.WithDecalAdded(r, id));
        _selected = _editor.Session.Current.decals.Count - 1;
        Committed();
    }

    private void Remove(int index) {
        _editor.Session.Apply(r => RecordEdits.WithoutDecal(r, index));
        if (_selected >= index) _selected--;
        if (_selected < 0 && _editor.Session.Current.decals.Count > 0) _selected = 0;
        Committed();
    }

    /// Ubah stiker terpilih. commit=false: hanya pratinjau di avatar; commit=true: masuk session (URUNGKAN).
    private void Edit(Action<DecalRecord> change, bool commit) {
        CustomCharacterRecord record = _editor.Session.Current;
        if (_selected < 0 || _selected >= record.decals.Count) return;
        DecalRecord decal = record.decals[_selected].Clone();
        change(decal);
        int index = _selected;
        if (commit) {
            _editor.Session.Apply(r => RecordEdits.WithDecal(r, index, decal));
            Committed();
            return;
        }
        if (Time.unscaledTime - _lastPreview < PreviewInterval || _editor.IsAvatarBusy) return;
        _lastPreview = Time.unscaledTime;
        _ui.ColorSwatch.color = CharacterRecordConverter.FromHex(decal.color);
        LiveAvatarEdits.ApplyDecals(_editor.Avatar, RecordEdits.WithDecal(record, index, decal));
    }

    private void Committed() {
        Refresh();
        _editor.ApplyDecalsWhenIdle();
        _editor.AfterEdit();
    }

    private void OpenColor() {
        List<DecalRecord> decals = _editor.Session.Current.decals;
        if (_selected < 0 || _selected >= decals.Count) return;
        _popup.Title.text = "Warna stiker";
        _popup.Picker.SetColor(CharacterRecordConverter.FromHex(decals[_selected].color));
        _popupOpen = true;
        _popup.Overlay.SetActive(true);
    }

    private static string Hex(Color color) => CharacterRecordConverter.ToHex(color);
}
