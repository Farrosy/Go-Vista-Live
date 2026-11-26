using UnityEngine;

/// <summary>Editor karakter mana yang dibuka di dunia (bukan dari lobby).</summary>
public enum WorldEditorKind {
    /// <summary>Ruang ganti clothing shop: karakter berdiri di bilik, tab pakaian (EditorCategories.FittingRoom).</summary>
    FittingRoom,
    /// <summary>Kursi barber: karakter duduk di kursi, kamera bisa digeser mengelilingi kepala, tab rambut + jenggot
    /// (EditorCategories.Barber).</summary>
    Barber,
}

/// <summary>
/// Posisi editor karakter di dunia (dikirim FittingRoomStation / BarberEditorStation ke scene KARAKTER lewat
/// CharacterSceneLoader): pose root karakter preview (kaki di lantai bilik, atau root avatar yang duduk di kursi)
/// dan pose kamera editor.
/// </summary>
public sealed class FittingRoomView {
    public readonly Pose Subject;
    public readonly Pose View;
    public readonly float FieldOfView;
    /// <summary>Layer yang dirender kamera utama SEBELUM editor dibuka (selama editor terbuka maskernya 0).</summary>
    public readonly int CullingMask;
    public readonly WorldEditorKind Kind;

    public FittingRoomView(Pose subject, Pose view, float fieldOfView, int cullingMask,
                           WorldEditorKind kind = WorldEditorKind.FittingRoom) {
        Subject = subject;
        View = view;
        FieldOfView = fieldOfView;
        CullingMask = cullingMask;
        Kind = kind;
    }

    /// <summary>Kursi barber: karakter duduk diam, geser layar memutar kamera mengelilingi kepala, tanpa zoom Wajah.</summary>
    public bool Seated => Kind == WorldEditorKind.Barber;
}
