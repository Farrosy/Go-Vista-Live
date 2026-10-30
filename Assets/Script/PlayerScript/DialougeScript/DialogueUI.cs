using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// Pasang di DialogueRoot (full-screen, Image transparan dengan Raycast Target ON).
/// Tap di mana saja: selesaikan ketikan -> tap lagi: baris berikutnya -> habis: tutup.
[RequireComponent(typeof(CanvasGroup))]
public class DialogueUI : MonoBehaviour, IPointerClickHandler
{
    public static DialogueUI Instance { get; private set; }

    [Header("UI (child DialogueRoot)")]
    [SerializeField] private Image portraitImage;
    [SerializeField] private TMP_Text dialogueText;
    [SerializeField] private TMP_Text nameLabel;            // opsional
    [SerializeField] private GameObject continueIndicator;  // opsional (mis. segitiga kecil)

    [Header("Audio")]
    [SerializeField] private AudioSource voiceSource;

    [Header("Disembunyikan saat dialog")]
    [SerializeField] private CanvasGroup gameplayHud;       // object GameplayHUD

    public bool IsActive { get; private set; }

    /// Dipanggil saat dialog dibaca sampai baris terakhir (dipakai ProgressService).
    public static event System.Action<DialogueData> DialogueFinished;

    private CanvasGroup group;
    private CameraOrbit cameraOrbit;
    private DialogueData data;
    private Transform npcPoint;
    private Coroutine typing;
    private int index;
    private bool isTyping;

    void Awake()
    {
        Instance = this;
        group = GetComponent<CanvasGroup>();
        SetVisible(false);
    }

    void Start()
    {
        cameraOrbit = FindAnyObjectByType<CameraOrbit>();
    }

    public void StartDialogue(DialogueData d, Transform focusPoint)
    {
        if (IsActive || d == null || d.lines == null || d.lines.Length == 0) return;

        data = d;
        npcPoint = focusPoint;
        index = 0;
        IsActive = true;

        PlayerInputHub.Instance.SetLocked(true);
        if (InteractButtonUI.Instance) InteractButtonUI.Instance.SetSuppressed(true);
        SetHud(false);
        FaceNpc();

        if (d.focusCamera && cameraOrbit && focusPoint)
            cameraOrbit.FocusOn(focusPoint, d.cameraDistance, d.cameraHeight, d.cameraSideOffset);

        if (nameLabel) nameLabel.text = d.speakerName;
        SetVisible(true);
        ShowLine();
    }

    public void OnPointerClick(PointerEventData e)
    {
        if (!IsActive) return;

        if (isTyping)   // tap saat mengetik = langsung tampilkan semua
        {
            if (typing != null) StopCoroutine(typing);
            typing = null;
            dialogueText.maxVisibleCharacters = int.MaxValue;
            isTyping = false;
            ShowContinue(true);
            return;
        }

        index++;
        if (index >= data.lines.Length) EndDialogue();
        else ShowLine();
    }

    private void ShowLine()
    {
        var line = data.lines[index];
        Sprite face = line.portraitOverride ? line.portraitOverride : data.portrait;
        if (portraitImage)
        {
            portraitImage.sprite = face;
            portraitImage.enabled = face != null;
        }

        if (typing != null) StopCoroutine(typing);
        typing = StartCoroutine(TypeLine(line.text));
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
        ShowContinue(true);
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
        if (typing != null) StopCoroutine(typing);
        typing = null;

        SetVisible(false);
        SetHud(true);
        if (cameraOrbit) cameraOrbit.ReleaseFocus();
        PlayerInputHub.Instance.SetLocked(false);
        if (InteractButtonUI.Instance) InteractButtonUI.Instance.SetSuppressed(false);

        DialogueFinished?.Invoke(data);
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
