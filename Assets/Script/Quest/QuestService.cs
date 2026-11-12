using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Logika quest untuk pemain lokal. Progres disimpan di PlayerProgress (Cloud Save) lewat ProgressService.
///
/// Alur:
///  1. QuestGiver (nanti di NPC) memanggil Accept(quest). Tanda ! muncul di tombol Quest.
///  2. Tujuan selesai lewat QuestLocation (GoToLocation), DialogueUI.DialogueFinished (FinishDialogue),
///     atau CompleteObjective dari kode (Manual).
///  3. Semua tujuan selesai: quest pindah ke completedQuests dan hilang dari Daftar Quest.
/// </summary>
public static class QuestService {
    /// <summary>Dipicu setiap daftar quest, progres, pin, atau tanda ! berubah. UI cukup render ulang.</summary>
    public static event Action Changed;
    public static event Action<QuestData> QuestAccepted;
    public static event Action<QuestData> QuestCompleted;
    /// <summary>Quest dan index tujuan yang baru selesai.</summary>
    public static event Action<QuestData, int> ObjectiveCompleted;

    /// <summary>Satu quest aktif beserta datanya, untuk UI.</summary>
    public readonly struct Entry {
        public readonly QuestData Data;
        public readonly QuestProgress Progress;
        public Entry(QuestData data, QuestProgress progress) { Data = data; Progress = progress; }
    }

    private static PlayerProgress Save => ProgressService.Current;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() {
        Changed = null;
        QuestAccepted = null;
        QuestCompleted = null;
        ObjectiveCompleted = null;
        DialogueUI.DialogueFinished -= OnDialogueFinished;
        ProgressService.Loaded -= OnProgressLoaded;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize() {
        DialogueUI.DialogueFinished += OnDialogueFinished;
        ProgressService.Loaded += OnProgressLoaded;
    }

    private static void OnProgressLoaded() => Changed?.Invoke();

    // ================================================================ Status

    public static bool HasNewQuest => Save.hasNewQuest;
    public static string PinnedQuestId => Save.pinnedQuestId;

    public static bool IsActive(string questId) => FindProgress(questId) != null;
    public static bool IsCompleted(string questId) => Save.completedQuests.Contains(questId);

    /// <summary>Quest aktif untuk Daftar Quest: yang dipin paling atas, lalu urut waktu diterima.</summary>
    public static List<Entry> GetActiveQuests() {
        var result = new List<Entry>();
        foreach (QuestProgress progress in Save.activeQuests) {
            QuestData data = QuestDatabase.Instance != null ? QuestDatabase.Instance.Find(progress.questId) : null;
            if (data == null) {
                Debug.LogWarning($"[Quest] QuestData '{progress.questId}' tidak ada di QuestDatabase, quest disembunyikan.");
                continue;
            }
            if (progress.questId == Save.pinnedQuestId) result.Insert(0, new Entry(data, progress));
            else result.Add(new Entry(data, progress));
        }
        return result;
    }

    public static bool IsObjectiveDone(QuestData quest, QuestProgress progress, int index) =>
        progress.completedObjectives.Contains(quest.ObjectiveId(index));

    /// <summary>Tujuan bisa dikerjakan: belum selesai dan, kalau quest berurutan, semua tujuan sebelumnya selesai.</summary>
    public static bool IsObjectiveAvailable(QuestData quest, QuestProgress progress, int index) {
        if (IsObjectiveDone(quest, progress, index)) return false;
        if (!quest.sequentialObjectives) return true;
        for (int i = 0; i < index; i++)
            if (!IsObjectiveDone(quest, progress, i)) return false;
        return true;
    }

    // ================================================================ Aksi

    /// <summary>Terima quest. False kalau quest sudah aktif, sudah selesai, atau tidak punya tujuan.</summary>
    public static bool Accept(QuestData quest) {
        if (quest == null || IsActive(quest.Id) || IsCompleted(quest.Id)) return false;
        if (quest.objectives == null || quest.objectives.Length == 0) {
            Debug.LogWarning($"[Quest] '{quest.Id}' tidak punya tujuan, tidak bisa diterima.", quest);
            return false;
        }
        if (QuestDatabase.Instance == null || QuestDatabase.Instance.Find(quest.Id) == null)
            Debug.LogWarning($"[Quest] '{quest.Id}' belum ada di QuestDatabase; setelah login ulang quest ini tidak akan tampil. " +
                             "Jalankan Go Vista > Quest > Perbarui QuestDatabase.", quest);

        Save.activeQuests.Add(new QuestProgress { questId = quest.Id });
        Save.hasNewQuest = true;
        QuestAccepted?.Invoke(quest);
        // Tujuan lokasi yang pemainnya sudah berdiri di sana langsung selesai.
        if (!CompleteReachedLocations(quest)) SaveNow();
        Changed?.Invoke();
        return true;
    }

    /// <summary>Selesaikan satu tujuan dari kode (untuk tujuan Manual, atau memaksa tujuan lain).</summary>
    public static bool CompleteObjective(string questId, string objectiveId) {
        QuestProgress progress = FindProgress(questId);
        QuestData quest = progress != null && QuestDatabase.Instance != null ? QuestDatabase.Instance.Find(questId) : null;
        if (quest == null) return false;
        for (int i = 0; i < quest.objectives.Length; i++)
            if (quest.ObjectiveId(i) == objectiveId) return TryComplete(quest, progress, i);
        Debug.LogWarning($"[Quest] Tujuan '{objectiveId}' tidak ada di quest '{questId}'.", quest);
        return false;
    }

    /// <summary>Pin quest (hanya satu). Memanggil lagi pada quest yang sedang dipin akan melepas pin.</summary>
    public static void TogglePin(string questId) {
        if (!IsActive(questId)) return;
        Save.pinnedQuestId = Save.pinnedQuestId == questId ? "" : questId;
        ProgressService.MarkDirty();
        Changed?.Invoke();
    }

    /// <summary>Dipanggil saat Daftar Quest dibuka: tanda ! hilang.</summary>
    public static void MarkQuestsSeen() {
        if (!Save.hasNewQuest) return;
        Save.hasNewQuest = false;
        ProgressService.MarkDirty();
        Changed?.Invoke();
    }

    // ================================================================ Pemicu tujuan

    /// <summary>Dipanggil QuestLocation saat pemain lokal masuk area.</summary>
    public static void ReportLocationReached(string locationId) {
        if (string.IsNullOrEmpty(locationId)) return;
        CompleteMatching(o => o.type == QuestObjectiveType.GoToLocation && o.locationId == locationId);
    }

    private static void OnDialogueFinished(DialogueData dialogue) {
        if (dialogue == null) return;
        CompleteMatching(o => o.type == QuestObjectiveType.FinishDialogue && o.dialogue != null && o.dialogue.Id == dialogue.Id);
    }

    private static void CompleteMatching(Predicate<QuestObjective> match) {
        // Salinan, karena quest yang selesai dihapus dari activeQuests.
        foreach (Entry entry in GetActiveQuests())
            for (int i = 0; i < entry.Data.objectives.Length; i++)
                if (match(entry.Data.objectives[i])) TryComplete(entry.Data, entry.Progress, i);
    }

    private static bool TryComplete(QuestData quest, QuestProgress progress, int index) {
        if (!IsObjectiveAvailable(quest, progress, index)) return false;
        progress.completedObjectives.Add(quest.ObjectiveId(index));
        ObjectiveCompleted?.Invoke(quest, index);

        if (AllObjectivesDone(quest, progress)) {
            Save.activeQuests.Remove(progress);
            Save.completedQuests.Add(quest.Id);
            if (Save.pinnedQuestId == quest.Id) Save.pinnedQuestId = "";
            Debug.Log($"[Quest] Selesai: {quest.title}");
            QuestCompleted?.Invoke(quest);
        } else if (quest.sequentialObjectives) {
            // Tujuan berikutnya bisa jadi lokasi tempat pemain sedang berdiri.
            if (CompleteReachedLocations(quest)) return true;
        }
        SaveNow();
        Changed?.Invoke();
        return true;
    }

    /// <summary>Selesaikan tujuan lokasi yang sedang bisa dikerjakan dan pemainnya sudah di dalam area.</summary>
    private static bool CompleteReachedLocations(QuestData quest) {
        QuestProgress progress = FindProgress(quest.Id);
        if (progress == null) return false;
        for (int i = 0; i < quest.objectives.Length; i++) {
            QuestObjective objective = quest.objectives[i];
            if (objective.type == QuestObjectiveType.GoToLocation &&
                QuestLocation.IsLocalPlayerInside(objective.locationId) &&
                TryComplete(quest, progress, i))
                return true;
        }
        return false;
    }

    private static bool AllObjectivesDone(QuestData quest, QuestProgress progress) {
        for (int i = 0; i < quest.objectives.Length; i++)
            if (!IsObjectiveDone(quest, progress, i)) return false;
        return true;
    }

    private static QuestProgress FindProgress(string questId) =>
        Save.activeQuests.Find(q => q.questId == questId);

    private static void SaveNow() {
        ProgressService.MarkDirty();
        _ = ProgressService.SaveAsync(); // momen penting, langsung simpan
    }
}
