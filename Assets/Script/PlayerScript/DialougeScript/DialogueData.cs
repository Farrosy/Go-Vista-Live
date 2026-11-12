using UnityEngine;

[System.Serializable]
public class DialogueLine
{
    [TextArea(2, 5)] public string text;
    [Tooltip("Opsional: ganti ekspresi wajah hanya untuk baris ini")]
    public Sprite portraitOverride;

    [Tooltip("ON = baris ini adalah pertanyaan. Teks di atas = teks pertanyaannya, " +
             "lalu tombol opsi muncul setelah teks selesai diketik.")]
    public bool isQuestion;
    public DialogueQuestion question;
}

/// Pertanyaan dengan beberapa opsi jawaban (UI sekarang punya 2 tombol).
[System.Serializable]
public class DialogueQuestion
{
    [Tooltip("ID unik pertanyaan di dalam dialog ini (mis. 'ibukota'). Dipakai untuk simpan jawaban. " +
             "JANGAN diganti setelah rilis. Kosong = 'q<nomor baris>' (berubah kalau urutan baris digeser).")]
    public string questionId;
    public DialogueOption[] options = new DialogueOption[2];
}

[System.Serializable]
public class DialogueOption
{
    [Tooltip("ID opsi untuk disimpan (mis. 'a'). JANGAN diganti setelah rilis. Kosong = huruf urutan (a, b, ...).")]
    public string optionId;
    [Tooltip("Teks di tombol")]
    public string text;
    public bool isCorrect;
    [Tooltip("Balasan NPC kalau opsi ini dipilih. Setelah habis, dialog lanjut ke baris berikutnya.")]
    public DialogueReplyLine[] reply;
    [Tooltip("ON = kalau pemain memilih opsi ini, dialog TIDAK dianggap tamat: NPC menanyakannya lagi saat diajak " +
             "bicara berikutnya, dan QuestGiver yang menunggu dialog ini tidak memberi quest (mis. jawaban 'No').")]
    public bool askAgainLater;
}

[System.Serializable]
public class DialogueReplyLine
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

    [Header("Progres")]
    [Tooltip("ID unik untuk simpan progres. Kosong = pakai nama asset. JANGAN diganti setelah rilis, " +
             "nanti progres pemain untuk dialog ini dianggap belum selesai.")]
    public string dialogueId;

    public string Id => string.IsNullOrEmpty(dialogueId) ? name : dialogueId;

    [Header("Isi dialog (satu elemen = satu kotak teks, boleh berupa pertanyaan)")]
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

    /// Kunci simpan jawaban: "<dialogId>/<questionId>".
    public string QuestionKey(int lineIndex)
    {
        var q = lines[lineIndex].question;
        string qid = q != null && !string.IsNullOrEmpty(q.questionId) ? q.questionId : "q" + lineIndex;
        return Id + "/" + qid;
    }

    public static string OptionId(DialogueOption option, int optionIndex) =>
        option != null && !string.IsNullOrEmpty(option.optionId) ? option.optionId : ((char)('a' + optionIndex)).ToString();
}
