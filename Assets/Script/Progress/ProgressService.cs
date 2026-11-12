using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.CloudSave;
using Unity.Services.CloudSave.Models;
using UnityEngine;

/// <summary>
/// Load/save PlayerProgress ke Unity Cloud Save untuk akun yang sedang login (lihat AuthService).
///
/// Alur:
///  1. Login berhasil, lalu LoginUIController memanggil LoadAsync().
///  2. Selama main, data diubah lewat helper (MarkDialogueCompleted), dan data ditandai "dirty".
///  3. ProgressAutoSave menyimpan data yang dirty secara berkala, saat app di-pause/ditutup,
///     dan saat logout.
///
/// Posisi pemain sengaja TIDAK disimpan: pemain selalu spawn di PlayerSpawnPoint scene world.
///
/// Save dari client bisa dicurangi. Lihat catatan KEAMANAN di PlayerProgress sebelum menambah
/// koin/item atau apa pun yang harus adil.
/// </summary>
public static class ProgressService {
    private const string CloudKey = "progress";

    public static PlayerProgress Current { get; private set; } = new PlayerProgress();
    /// <summary>True setelah progres akun ini berhasil di-load. Sebelum itu, save diblokir
    /// supaya data cloud tidak tertimpa data kosong.</summary>
    public static bool IsLoaded { get; private set; }
    public static bool IsDirty { get; private set; }

    /// <summary>Dipanggil setelah LoadAsync berhasil. Pakai untuk menerapkan progres ke dunia game.</summary>
    public static event Action Loaded;
    /// <summary>Dipanggil saat nama pemain berubah (SetPlayerName). LobbyPresence memakai ini supaya
    /// nama baru langsung terkirim ke pemain lain di room.</summary>
    public static event Action<string> PlayerNameChanged;

    private static bool _saving;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() {
        Current = new PlayerProgress();
        IsLoaded = false;
        IsDirty = false;
        Loaded = null;
        PlayerNameChanged = null;
        _saving = false;
        DialogueUI.DialogueFinished -= OnDialogueFinished;
        DialogueUI.AnswerChosen -= OnAnswerChosen;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize() {
        DialogueUI.DialogueFinished += OnDialogueFinished;
        DialogueUI.AnswerChosen += OnAnswerChosen;
        ProgressAutoSave.Create();
    }

    // ================================================================ Load / Save

    public static async Task<ServiceResult> LoadAsync() {
        if (!AuthService.IsSignedIn)
            return ServiceResult.Fail("Belum login.");

        try {
            Dictionary<string, Item> result =
                await CloudSaveService.Instance.Data.Player.LoadAsync(new HashSet<string> { CloudKey });
            Current = result.TryGetValue(CloudKey, out Item item)
                ? item.Value.GetAs<PlayerProgress>() ?? new PlayerProgress()
                : new PlayerProgress(); // akun baru, belum pernah save
            Migrate(Current);
            IsLoaded = true;
            IsDirty = false;
            Debug.Log($"[Progress] Ter-load: {Current.completedDialogues.Count} dialog selesai");
            Loaded?.Invoke();
            return ServiceResult.Ok();
        } catch (Exception e) {
            Debug.LogWarning($"[Progress] Gagal load: {e.Message}");
            return ServiceResult.Fail("Gagal memuat progres. Cek internet lalu coba lagi.");
        }
    }

    /// <summary>Simpan ke cloud kalau ada perubahan. Aman dipanggil berkali-kali.</summary>
    public static async Task<bool> SaveAsync() {
        if (!IsLoaded || !AuthService.IsSignedIn)
            return false;

        if (!IsDirty)
            return true;
        if (_saving)
            return false; // save sebelumnya masih jalan, perubahan ini ikut di save berikutnya

        _saving = true;
        IsDirty = false;
        try {
            await CloudSaveService.Instance.Data.Player.SaveAsync(
                new Dictionary<string, object> { { CloudKey, Current } });
            return true;
        } catch (Exception e) {
            IsDirty = true; // coba lagi di save berikutnya
            Debug.LogWarning($"[Progress] Gagal save: {e.Message}");
            return false;
        } finally {
            _saving = false;
        }
    }

    /// <summary>Lupakan progres di memori (dipanggil saat logout, SETELAH SaveAsync).</summary>
    public static void Clear() {
        Current = new PlayerProgress();
        IsLoaded = false;
        IsDirty = false;
    }

    public static void MarkDirty() {
        IsDirty = true;
    }

    // ================================================================ Dialog

    public static bool IsDialogueCompleted(string dialogueId) {
        return Current.completedDialogues.Contains(dialogueId);
    }

    public static void MarkDialogueCompleted(string dialogueId) {
        if (string.IsNullOrEmpty(dialogueId) || Current.completedDialogues.Contains(dialogueId))
            return;
        Current.completedDialogues.Add(dialogueId);
        MarkDirty();
        _ = SaveAsync(); // momen penting, langsung simpan
    }

    private static void OnDialogueFinished(DialogueData dialogue) {
        if (ShouldAskAgain(dialogue)) return; // pemain memilih jawaban "tanya lagi nanti"
        MarkDialogueCompleted(dialogue.Id);
    }

    /// <summary>True kalau jawaban terakhir pemain di salah satu pertanyaan dialog ini adalah opsi
    /// askAgainLater. Dialog seperti ini tidak dianggap tamat dan tidak memberi quest.</summary>
    public static bool ShouldAskAgain(DialogueData dialogue) {
        if (dialogue == null || dialogue.lines == null) return false;
        for (int i = 0; i < dialogue.lines.Length; i++) {
            DialogueLine line = dialogue.lines[i];
            if (line == null || !line.isQuestion || line.question?.options == null) continue;
            DialogueAnswer answer = GetDialogueAnswer(dialogue.QuestionKey(i));
            if (answer == null) continue;
            DialogueOption[] options = line.question.options;
            for (int o = 0; o < options.Length; o++)
                if (options[o] != null && options[o].askAgainLater && DialogueData.OptionId(options[o], o) == answer.optionId)
                    return true;
        }
        return false;
    }

    /// <summary>Jawaban terakhir untuk pertanyaan ini, atau null kalau belum pernah dijawab.</summary>
    public static DialogueAnswer GetDialogueAnswer(string questionKey) {
        return Current.dialogueAnswers.Find(a => a.questionKey == questionKey);
    }

    /// <summary>Simpan jawaban (selalu menimpa jawaban sebelumnya).</summary>
    public static void SetDialogueAnswer(string questionKey, string optionId, bool correct) {
        if (string.IsNullOrEmpty(questionKey))
            return;
        DialogueAnswer a = GetDialogueAnswer(questionKey);
        if (a == null) {
            a = new DialogueAnswer { questionKey = questionKey };
            Current.dialogueAnswers.Add(a);
        } else if (a.optionId == optionId && a.correct == correct) {
            return;
        }
        a.optionId = optionId ?? "";
        a.correct = correct;
        MarkDirty(); // ikut tersimpan saat dialog selesai / auto-save
    }

    private static void OnAnswerChosen(DialogueData dialogue, string questionKey, string optionId, bool correct) {
        SetDialogueAnswer(questionKey, optionId, correct);
    }

    // ================================================================ Karakter

    public static void SetCharacter(string characterId) {
        if (Current.characterId == characterId)
            return;
        Current.characterId = characterId ?? "";
        MarkDirty();
        _ = SaveAsync(); // jarang terjadi, langsung simpan
    }

    // ================================================================ Nama pemain

    /// <summary>Simpan nama pemain (dirapikan dulu lewat PlayerNameRules). Mengembalikan nama yang disimpan.</summary>
    public static string SetPlayerName(string playerName) {
        string clean = PlayerNameRules.Sanitize(playerName);
        if (Current.playerName == clean)
            return clean;
        Current.playerName = clean;
        MarkDirty();
        _ = SaveAsync(); // jarang terjadi, langsung simpan
        PlayerNameChanged?.Invoke(clean);
        return clean;
    }

    // TODO (next develop): tambahkan helper untuk data baru di sini, misalnya:
    //   public static void MarkDistrictVisited(string districtId) { ...pola sama dengan MarkDialogueCompleted... }

    // ================================================================ Migrasi

    /// <summary>Perbaiki/upgrade data lama supaya cocok dengan versi PlayerProgress sekarang.</summary>
    private static void Migrate(PlayerProgress p) {
        p.completedDialogues ??= new List<string>();
        p.dialogueAnswers ??= new List<DialogueAnswer>();
        p.characterId ??= "";
        p.activeQuests ??= new List<QuestProgress>();
        p.activeQuests.RemoveAll(q => q == null || string.IsNullOrEmpty(q.questId));
        foreach (QuestProgress quest in p.activeQuests) quest.completedObjectives ??= new List<string>();
        p.completedQuests ??= new List<string>();
        p.pinnedQuestId ??= "";
        p.coins = CoinRules.Sanitize(p.coins);
        p.playerName = PlayerNameRules.Sanitize(p.playerName);
        // Posisi terakhir tidak dipakai lagi (spawn selalu di PlayerSpawnPoint). Kosongkan data
        // lama supaya ikut terhapus dari cloud di save berikutnya.
        p.hasLastPosition = false;
        p.lastWorld = "";
        p.lastPosX = p.lastPosY = p.lastPosZ = p.lastRotY = 0f;

        // TODO (next develop): saat menambah field list/class baru, beri default di sini juga, misalnya:
        //   p.visitedDistricts ??= new List<string>();
        // Kalau struktur berubah, misalnya: if (p.version < 2) { ...pindahkan data...; }
        p.version = PlayerProgress.CurrentVersion;
    }
}
