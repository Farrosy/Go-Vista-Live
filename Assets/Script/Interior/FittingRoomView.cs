using UnityEngine;

/// <summary>
/// Posisi editor ruang ganti di dunia (dikirim FittingRoomStation ke scene KARAKTER lewat CharacterSceneLoader):
/// tempat kaki karakter berdiri (menghadap ke kamera) dan pose kamera editor.
/// </summary>
public sealed class FittingRoomView {
    public readonly Pose Subject;
    public readonly Pose View;
    public readonly float FieldOfView;
    /// <summary>Layer yang dirender kamera utama SEBELUM editor dibuka (selama editor terbuka maskernya 0).</summary>
    public readonly int CullingMask;

    public FittingRoomView(Pose subject, Pose view, float fieldOfView, int cullingMask) {
        Subject = subject;
        View = view;
        FieldOfView = fieldOfView;
        CullingMask = cullingMask;
    }
}
