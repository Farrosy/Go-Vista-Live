using System.Collections.Generic;
using System.Linq;
using Bozo.ModularCharacters;
using UnityEngine;

/// <summary>
/// Sub-tab Wajah · Bentuk: slider blendshape kepala (FaceShapeCatalog). Digeser = avatar langsung berubah
/// (OutfitSystem.SetShape); dilepas = satu langkah URUNGKAN di session. Daftar slider dibuat ulang kalau
/// kepala berganti (kepala Anime dan Stylized punya blendshape berbeda).
/// </summary>
public sealed class CustomizerFaceShapes {
    private readonly CustomizerEditor _editor;
    private readonly ShapePanelRefs _ui;
    private readonly ChatUiAssets _a;
    private readonly List<(FaceShapeEntry entry, SliderRow row)> _rows = new List<(FaceShapeEntry, SliderRow)>();

    public CustomizerFaceShapes(CustomizerEditor editor, ShapePanelRefs ui, ChatUiAssets assets) {
        _editor = editor;
        _ui = ui;
        _a = assets;
    }

    public bool IsVisible => _ui.Root.activeSelf;

    public void Show(bool visible) {
        _ui.Root.SetActive(visible);
        if (!visible) return;
        Build();
        _ui.Scroll.verticalNormalizedPosition = 1f;
    }

    /// <summary>Bangun ulang daftar slider dari kepala yang sedang dipakai + nilai record.</summary>
    public void Build() {
        ChatUiKit.DestroyChildren(_ui.List);
        _rows.Clear();
        CustomCharacterRecord record = _editor.Session.Current;
        OutfitSystem avatar = _editor.Avatar;
        IEnumerable<string> keys = avatar && avatar.GetFaceShapes().Count > 0
            ? avatar.GetFaceShapes().Keys
            : record.faceShapes.Select(s => s.key);

        CustomizerEditorPanels.Section(_ui.List, _a, "Ukuran Iris & Kelopak Mata maksimal 50%", null);
        foreach (FaceShapeEntry entry in FaceShapeCatalog.Entries(keys, _editor.Template.IsMale)) {
            SliderRow row = CustomizerEditorPanels.CreateSliderRow(_ui.List, _a, entry.Label, 0f, entry.Max);
            row.SetEnabled(entry.Enabled);
            string key = entry.Key;
            row.Slider.onValueChanged.AddListener(v => {
                OutfitSystem live = _editor.Avatar;
                if (live) live.SetShape(key, v);
            });
            row.Commit.Committed += v => Commit(key, v);
            _rows.Add((entry, row));
        }
        Refresh();
    }

    /// <summary>Nilai slider mengikuti record (setelah URUNGKAN); slider nonaktif tetap 0.</summary>
    public void Refresh() {
        CustomCharacterRecord record = _editor.Session.Current;
        OutfitSystem avatar = _editor.Avatar;
        foreach ((FaceShapeEntry entry, SliderRow row) in _rows) {
            float live = avatar && entry.Enabled ? avatar.GetShapeValue(entry.Key) : 0f;
            float value = entry.Enabled ? RecordEdits.FaceShape(record, entry.Key, live) : 0f;
            row.SetValue(Mathf.Clamp(value, 0f, entry.Max));
        }
    }

    private void Commit(string key, float value) {
        _editor.Session.Apply(r => RecordEdits.WithFaceShape(r, key, value));
        _editor.AfterEdit();
    }
}
