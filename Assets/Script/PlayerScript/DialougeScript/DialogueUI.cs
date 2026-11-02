using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// Pasang di DialogueRoot (full-screen, Image transparan dengan Raycast Target ON).
/// Tap di mana saja: selesaikan ketikan -> tap lagi: baris berikutnya -> habis: tutup.
/// Baris pertanyaan: setelah teks selesai diketik, tombol opsi muncul dan tap biasa diabaikan
/// sampai pemain memilih. Lalu balasan NPC untuk opsi itu diputar, kemudian lanjut ke baris berikutnya.
[RequireComponent(typeof(CanvasGroup))]
public class DialogueUI : MonoBehaviour, IPointerClickHandler
{
    public static DialogueUI Instance { get; private set; }

    [Header("UI (child DialogueRoot)")]
    [SerializeField] private Image portraitImage;
    [SerializeField] private TMP_Text dialogueText;
    [SerializeField] private TMP_Text nameLabel;            // opsional
    [SerializeField] private GameObject continueIndicator;  // opsional (mis. segitiga kecil)

    [Header("Opsi jawaban (child DialogueRoot)")]
    [Tooltip("Tombol opsi, urut sesuai opsi di DialogueData (elemen 0 = opsi pertama).")]
    [SerializeField] private Button[] optionButtons;
    [Tooltip("Teks di tiap tombol, urutan sama dengan Option Buttons. Kosong = dicari otomatis di child tombol.")]
    [SerializeField] private TMP_Text[] optionLabels;
    [Tooltip("Opsional: parent semua tombol, ikut disembunyikan/ditampilkan")]
    [SerializeField] private GameObject optionsRoot;

    [Header("Audio")]
    [SerializeField] private AudioSource voiceSource;

    [Header("Disembunyikan saat dialog")]
    [SerializeField] private CanvasGroup gameplayHud;       // object GameplayHUD

    public bool IsActive { get; private set; }

    /// Dipanggil saat dialog dibaca sampai baris terakhir (dipakai ProgressService).
    public static event System.Action<DialogueData> DialogueFinished;
    /// Dipanggil saat pemain memilih opsi: (dialog, kunci pertanyaan, id opsi, benar?). Dipakai ProgressService.
    public static event System.Action<DialogueData, string, string, bool> AnswerChosen;

    private CanvasGroup group;
    private CameraOrbit cameraOrbit;
    private DialogueData data;
    private Transform npcPoint;
    private Coroutine typing;
    private int index;                    // baris utama
    private DialogueReplyLine[] reply;    // balasan opsi yang sedang diputar (null = tidak ada)
    private int replyIndex;
    private bool isTyping;
    private bool awaitingChoice;
    private System.Action onEnded;
    private readonly List<DialogueData> queue = new List<DialogueData>();   // stage berikutnya dalam satu interaksi
    private int queueIndex;

    void Awake()
    {
        Instance = this;
        group = GetComponent<CanvasGroup>();

        if (optionButtons != null)
        {
            for (int i = 0; i < optionButtons.Length; i++)
            {
                if (!optionButtons[i]) continue;
                int captured = i;
                optionButtons[i].onClick.AddListener(() => ChooseOption(captured));
            }
        }

        SetVisible(false);
        HideOptions();
    }

    void Start()
    {
        cameraOrbit = FindAnyObjectByType<CameraOrbit>();
    }

    /// Return true kalau dialog benar-benar dimulai. 'ended' dipanggil saat dialog ditutup (dipakai NpcController).
    public bool StartDialogue(DialogueData d, Transform focusPoint, System.Action ended = null) =>
        StartDialogue(new[] { d }, focusPoint, ended);

    /// Putar beberapa dialog berurutan dalam satu interaksi (mis. stage 1 -> 2 -> 3) tanpa menutup UI.
    /// Tiap dialog yang habis dibaca langsung ditandai selesai (DialogueFinished), lalu lanjut ke berikutnya.
    public bool StartDialogue(IList<DialogueData> sequence, Transform focusPoint, System.Action ended = null)
    {
        if (IsActive || sequence == null) return false;

        queue.Clear();
        foreach (var d in sequence)
            if (IsPlayable(d)) queue.Add(d);
        if (queue.Count == 0) return false;

        queueIndex = 0;
        npcPoint = focusPoint;
        IsActive = true;
        onEnded = ended;

        PlayerInputHub.Instance.SetLocked(true);
        if (InteractButtonUI.Instance) InteractButtonUI.Instance.SetSuppressed(true);
        SetHud(false);
        FaceNpc();

        SetVisible(true);
        BeginData(queue[0]);
        return true;
    }

    private static bool IsPlayable(DialogueData d) => d != null && d.lines != null && d.lines.Length > 0;

    private void BeginData(DialogueData d)
    {
        data = d;
        index = 0;
        reply = null;
        awaitingChoice = false;

        if (d.focusCamera && cameraOrbit && npcPoint)
            cameraOrbit.FocusOn(npcPoint, d.cameraDistance, d.cameraHeight, d.cameraSideOffset);

        if (nameLabel) nameLabel.text = d.speakerName;
        HideOptions();
        ShowMainLine();
    }

    public void OnPointerClick(PointerEventData e)
    {
        if (!IsActive || awaitingChoice) return;

        if (isTyping)   // tap saat mengetik = langsung tampilkan semua
        {
            if (typing != null) StopCoroutine(typing);
            typing = null;
            dialogueText.maxVisibleCharacters = int.MaxValue;
            isTyping = false;
            OnLineShown();
            return;
        }

        Advance();
    }

    private void Advance()
    {
        if (reply != null)
        {
            replyIndex++;
            if (replyIndex < reply.Length) { ShowReplyLine(); return; }
            reply = null;   // balasan habis, lanjut ke baris utama berikutnya
        }

        index++;
        if (index < data.lines.Length) { ShowMainLine(); return; }

        // Dialog ini habis: tandai selesai, lalu lanjut ke dialog berikutnya di urutan (kalau ada).
        DialogueFinished?.Invoke(data);
        queueIndex++;
        if (queueIndex < queue.Count) BeginData(queue[queueIndex]);
        else EndDialogue();
    }

    private DialogueLine CurrentMain => data.lines[index];
    private bool CurrentIsQuestion =>
        reply == null && CurrentMain.isQuestion && CurrentMain.question != null &&
        CurrentMain.question.options != null && CurrentMain.question.options.Length > 0;

    private void ShowMainLine()
    {
        var line = CurrentMain;
        ShowText(line.text, line.portraitOverride);
    }

    private void ShowReplyLine()
    {
        var line = reply[replyIndex];
        ShowText(line.text, line.portraitOverride);
    }

    private void ShowText(string text, Sprite portraitOverride)
    {
        Sprite face = portraitOverride ? portraitOverride : data.portrait;
        if (portraitImage)
        {
            portraitImage.sprite = face;
            portraitImage.enabled = face != null;
        }

        if (typing != null) StopCoroutine(typing);
        typing = StartCoroutine(TypeLine(text ?? ""));
    }

    /// Teks selesai tampil: pertanyaan -> munculkan opsi, selain itu -> indikator lanjut.
    private void OnLineShown()
    {
        if (CurrentIsQuestion) ShowOptions(CurrentMain.question);
        else ShowContinue(true);
    }

    private void ShowOptions(DialogueQuestion q)
    {
        awaitingChoice = true;
        ShowContinue(false);
        if (optionsRoot) optionsRoot.SetActive(true);

        int buttons = optionButtons != null ? optionButtons.Length : 0;
        if (buttons < q.options.Length)
            Debug.LogWarning($"[Dialogue] '{data.name}' baris {index}: {q.options.Length} opsi tapi hanya {buttons} tombol. Opsi sisanya tidak tampil.");
        if (buttons == 0)
        {
            Debug.LogError("[Dialogue] Option Buttons di DialogueUI belum diisi. Pertanyaan dilewati.");
            awaitingChoice = false;
            ShowContinue(true);
            return;
        }

        for (int i = 0; i < buttons; i++)
        {
            var b = optionButtons[i];
            if (!b) continue;
            bool used = i < q.options.Length && q.options[i] != null;
            b.gameObject.SetActive(used);
            if (!used) continue;
            var label = GetLabel(i);
            if (label) label.text = q.options[i].text;
        }
    }

    private TMP_Text GetLabel(int i)
    {
        if (optionLabels != null && i < optionLabels.Length && optionLabels[i]) return optionLabels[i];
        return optionButtons[i] ? optionButtons[i].GetComponentInChildren<TMP_Text>(true) : null;
    }

    private void HideOptions()
    {
        if (optionButtons != null)
            foreach (var b in optionButtons)
                if (b) b.gameObject.SetActive(false);
        if (optionsRoot) optionsRoot.SetActive(false);
    }

    private void ChooseOption(int i)
    {
        if (!IsActive || !awaitingChoice || !CurrentIsQuestion) return;
        var q = CurrentMain.question;
        if (i < 0 || i >= q.options.Length || q.options[i] == null) return;

        awaitingChoice = false;
        HideOptions();

        var option = q.options[i];
        AnswerChosen?.Invoke(data, data.QuestionKey(index), DialogueData.OptionId(option, i), option.isCorrect);

        if (option.reply != null && option.reply.Length > 0)
        {
            reply = option.reply;
            replyIndex = 0;
            ShowReplyLine();
        }
        else
        {
            Advance();
        }
    }

    private IEnumerator TypeLine(string text)
    {
        isTyping = true;
        ShowContinue(false);

        dialogueText.text = text;
        dialogueText.maxVisibleCharacters = 0;
        dialogueText.ForceMeshUpdate();

        TMP_TextInfo info = dialogueText.textInfo;
        int total = info.characterCount;
        float delay = 1f / Mathf.Max(1f, data.charactersPerSecond);

        for (int i = 0; i < total; i++)
        {
            dialogueText.maxVisibleCharacters = i + 1;
            char c = info.characterInfo[i].character;
            if (char.IsWhiteSpace(c)) continue;

            bool wordStart = i == 0 || char.IsWhiteSpace(info.characterInfo[i - 1].character);
            if (wordStart) PlayVoice();

            float wait = delay + (IsPunctuation(c) ? data.punctuationPause : 0f);
            yield return new WaitForSeconds(wait);
        }

        dialogueText.maxVisibleCharacters = int.MaxValue;
        isTyping = false;
        typing = null;
        OnLineShown();
    }

    private void PlayVoice()
    {
        if (!voiceSource || data.voiceClips == null || data.voiceClips.Length == 0) return;
        AudioClip clip = data.voiceClips[Random.Range(0, data.voiceClips.Length)];
        if (!clip) return;
        voiceSource.pitch = Random.Range(data.pitchMin, data.pitchMax);
        voiceSource.PlayOneShot(clip, data.voiceVolume);
    }

    private static bool IsPunctuation(char c) =>
        c == '.' || c == ',' || c == '!' || c == '?' || c == ';' || c == ':';

    private void EndDialogue()
    {
        IsActive = false;
        isTyping = false;
        awaitingChoice = false;
        reply = null;
        if (typing != null) StopCoroutine(typing);
        typing = null;

        HideOptions();
        SetVisible(false);
        SetHud(true);
        if (cameraOrbit) cameraOrbit.ReleaseFocus();
        PlayerInputHub.Instance.SetLocked(false);
        if (InteractButtonUI.Instance) InteractButtonUI.Instance.SetSuppressed(false);
        queue.Clear();

        var cb = onEnded;
        onEnded = null;
        cb?.Invoke();
    }

    private void FaceNpc()
    {
        var player = PlayerController.Local ? PlayerController.Local.transform : null;
        if (!player || !npcPoint) return;
        Vector3 d = npcPoint.position - player.position;
        d.y = 0f;
        if (d.sqrMagnitude > 0.001f) player.rotation = Quaternion.LookRotation(d);
    }

    private void ShowContinue(bool v)
    {
        if (continueIndicator) continueIndicator.SetActive(v);
    }

    private void SetVisible(bool v)
    {
        group.alpha = v ? 1f : 0f;
        group.interactable = v;
        group.blocksRaycasts = v;
    }

    private void SetHud(bool v)
    {
        if (!gameplayHud) return;
        gameplayHud.alpha = v ? 1f : 0f;
        gameplayHud.interactable = v;
        gameplayHud.blocksRaycasts = v;
    }
}
