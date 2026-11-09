using TMPro;
using UnityEngine;

/// Isi layar mati untuk satu penyebab. Tambah penyebab baru cukup dengan menambah satu aset ini.
[CreateAssetMenu(menuName = "Go Vista/Death Screen Content", fileName = "DeathScreen_")]
public sealed class DeathScreenContent : ScriptableObject {
    public enum IntroStyle { Slam, Drop }

    public DeathCause cause;

    [Header("Teks")]
    public string title;
    public float titleSize = 159f;
    public float titleSpacing = 4f;
    public Material titleMaterial;
    public string subtitle;
    public Color subtitleColor = Color.white;
    [TextArea] public string tip;
    public Color tipColor = Color.white;
    public Color cardLabelColor = Color.white;
    public string reviveLabel;
    [Tooltip("Kosong = baris info tidak ditampilkan.")]
    public string respawnInfo;
    public Color respawnInfoColor = Color.white;
    public Color countdownColor = Color.white;

    [Header("Gambar")]
    public Sprite badge;
    public Sprite icon;
    public float iconSize = 88f;
    public Sprite card;
    public Sprite lobbyButton;
    public Sprite lobbyButtonPressed;
    public Sprite lobbyButtonDisabled;
    public Color backdropColor = Color.black;
    public Sprite vignette;
    public bool showBubbles;

    [Header("Alur")]
    public IntroStyle intro = IntroStyle.Slam;
    [Tooltip("Kilat, goyang kamera, debu, dan gelombang benturan.")]
    public bool impactEffects;
    public float hideAvatarAfterSeconds = 0.5f;
    [Tooltip("Bar HP dan O₂ tetap terlihat selama ini setelah mati.")]
    public float hideHudAfterSeconds;
}
