using System;
using System.Collections.Generic;

/// <summary>
/// Satu sesi edit karakter custom di editor: record kerja, riwayat URUNGKAN, dan status "Belum disimpan".
/// Record di dalam tidak pernah diubah langsung: setiap perubahan lewat Apply/Replace menghasilkan record
/// baru, dan Current selalu memberi salinan.
/// </summary>
public sealed class CustomizationSession {
    /// <summary>Batas riwayat urungkan supaya memori tidak membengkak saat warna digeser terus.</summary>
    public const int MaxUndo = 50;

    private readonly LinkedList<CustomCharacterRecord> _history = new LinkedList<CustomCharacterRecord>();
    private CustomCharacterRecord _current;
    // Objek record saat terakhir disimpan/dibuka. Undo mengembalikan objek yang persis sama, jadi
    // "bersih" cukup dicek dengan ReferenceEquals, tanpa membandingkan isi.
    private CustomCharacterRecord _saved;

    /// <param name="slot">Slot custom yang diedit (0/1), atau -1 untuk karakter baru yang belum disimpan.</param>
    public CustomizationSession(CustomCharacterRecord start, int slot) {
        _current = start?.Clone() ?? throw new ArgumentNullException(nameof(start));
        _saved = _current;
        Slot = slot;
    }

    public int Slot { get; private set; }
    public CustomCharacterRecord Current => _current.Clone();
    public bool CanUndo => _history.Count > 0;
    public bool IsDirty => !ReferenceEquals(_current, _saved);

    /// <summary>Terapkan perubahan. Kalau fungsi mengembalikan record yang sama, tidak dihitung perubahan.</summary>
    public void Apply(Func<CustomCharacterRecord, CustomCharacterRecord> change) {
        CustomCharacterRecord next = change(_current);
        if (next == null || ReferenceEquals(next, _current)) return;
        Push(next);
    }

    /// <summary>Ganti seluruh record (mis. ACAK), tetap satu langkah urungkan.</summary>
    public void Replace(CustomCharacterRecord record) {
        if (record == null) return;
        Push(record.Clone());
    }

    public bool Undo() {
        if (_history.Count == 0) return false;
        _current = _history.Last.Value;
        _history.RemoveLast();
        return true;
    }

    /// <summary>Dipanggil setelah SIMPAN berhasil; slot bisa berubah kalau karakter baru dibuat.</summary>
    public void MarkSaved(int slot) {
        Slot = slot;
        _saved = _current;
    }

    private void Push(CustomCharacterRecord next) {
        _history.AddLast(_current);
        if (_history.Count > MaxUndo) _history.RemoveFirst();
        _current = next;
    }
}
