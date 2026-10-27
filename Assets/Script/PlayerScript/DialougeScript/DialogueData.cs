using UnityEngine;

[System.Serializable]
public class DialogueLine
{
    [TextArea(2, 5)] public string text;
    [Tooltip("Opsional: ganti ekspresi wajah hanya untuk baris ini")]
    public Sprite portraitOverride;
}

/// Data satu percakapan. Dibuat sebagai asset: Create > Dialogue > Dialogue Data.
[CreateAssetMenu(menuName = "Dialogue/Dialogue Data")]
public class DialogueData : ScriptableObject
{
    [Header("Pembicara")]
    public string speakerName = "NPC";
    public Sprite portrait;

    [Header("Isi dialog (satu elemen = satu kotak teks)")]
    public DialogueLine[] lines;

    [Header("Typing")]
    public float charactersPerSecond = 30f;
    [Tooltip("Jeda tambahan setelah . , ! ? ; :")]
    public float punctuationPause = 0.15f;

    [Header("Suara (satu bunyi per KATA). Kosongkan kalau belum ada audio")]
    public AudioClip[] voiceClips;
    [Range(0.5f, 2f)] public float pitchMin = 0.9f;
    [Range(0.5f, 2f)] public float pitchMax = 1.1f;
    [Range(0f, 1f)] public float voiceVolume = 0.8f;

    [Header("Kamera")]
    public bool focusCamera = true;
    [Tooltip("Jarak kamera dari NPC")]
    public float cameraDistance = 2.5f;
    [Tooltip("Tinggi kamera relatif ke titik fokus NPC")]
    public float cameraHeight = 0.2f;
    [Tooltip("Geser ke samping supaya terasa over-the-shoulder. 0 = lurus")]
    public float cameraSideOffset = 0.6f;
}
