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
///  2. Selama main, data diubah lewat helper (MarkDialogueCompleted, posisi otomatis dari
///     SetLocalPlayer), dan data ditandai "dirty".
///  3. ProgressAutoSave menyimpan data yang dirty secara berkala, saat app di-pause/ditutup,
///     dan saat logout.
///
/// Save dari client bisa dicurangi. Lihat catatan KEAMANAN di PlayerProgress sebelum menambah
/// koin/item atau apa pun yang harus adil.
/// </summary>
public static class ProgressService {
    private const string CloudKey = "progress";
    // Posisi baru dianggap berubah kalau pemain bergeser lebih dari ini (meter). Mencegah save yang tidak perlu.
    private const float MinMoveToRecord = 1f;

    public static PlayerProgress Current { get; private set; } = new PlayerProgress();
    /// <summary>True setelah progres akun ini berhasil di-load. Sebelum itu, save diblokir
    /// supaya data cloud tidak tertimpa data kosong.</summary>
    public static bool IsLoaded { get; private set; }
    public static bool IsDirty { get; private set; }

    /// <summary>Dipanggil setelah LoadAsync berhasil. Pakai untuk menerapkan progres ke dunia game.</summary>
    public static event Action Loaded;

    private static Transform _localPlayer;
    private static CharacterController _localController;
    private static Rigidbody _localRigidbody;
    private static string _localWorld = "";
    private static bool _saving;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() {
        Current = new PlayerProgress();
        IsLoaded = false;
        IsDirty = false;
        Loaded = null;
        _localPlayer = null;
        _localController = null;
        _localRigidbody = null;
        _localWorld = "";
        _saving = false;
        DialogueUI.DialogueFinished -= OnDialogueFinished;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize() {
        DialogueUI.DialogueFinished += OnDialogueFinished;
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
            Debug.Log($"[Progress] Ter-load: {Current.completedDialogues.Count} dialog selesai, " +
                      $"posisi tersimpan: {Current.hasLastPosition}");
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

        RecordPlayerPosition();
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
        ClearLocalPlayer();
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
        MarkDialogueCompleted(dialogue.Id);
    }

    // TODO (next develop): tambahkan helper untuk data baru di sini, misalnya:
    //   public static void AddCoins(int amount) { Current.coins += amount; MarkDirty(); }
    //   public static void MarkDistrictVisited(string districtId) { ...pola sama dengan MarkDialogueCompleted... }

    // ================================================================ Posisi terakhir

    /// <summary>Daftarkan avatar milik pemain ini supaya posisinya ikut tersimpan.</summary>
    public static void SetLocalPlayer(Transform player, string worldName) {
        _localPlayer = player;
        _localController = player != null ? player.GetComponent<CharacterController>() : null;
        _localRigidbody = player != null ? player.GetComponent<Rigidbody>() : null;
        _localWorld = worldName ?? "";
    }

    public static void ClearLocalPlayer() {
        _localPlayer = null;
        _localController = null;
        _localRigidbody = null;
    }

    /// <summary>Posisi terakhir yang tersimpan untuk world ini, kalau ada.</summary>
    public static bool TryGetLastPose(string worldName, out Vector3 position, out Quaternion rotation) {
        PlayerProgress p = Current;
        bool valid = IsLoaded && p.hasLastPosition && p.lastWorld == worldName;
        position = valid ? new Vector3(p.lastPosX, p.lastPosY, p.lastPosZ) : Vector3.zero;
        rotation = valid ? Quaternion.Euler(0f, p.lastRotY, 0f) : Quaternion.identity;
        return valid;
    }

    /// <summary>Salin posisi avatar ke data progres (tanpa network). Dipanggil berkala oleh ProgressAutoSave.</summary>
    public static void RecordPlayerPosition() {
        if (!IsLoaded || _localPlayer == null)
            return;
        // Jangan rekam saat melayang/jatuh, supaya pemain tidak spawn di udara.
        if (_localController != null && !_localController.isGrounded)
            return;
        if (_localRigidbody != null && !_localRigidbody.isKinematic &&
            Mathf.Abs(_localRigidbody.linearVelocity.y) > 0.5f)
            return;

        Vector3 pos = _localPlayer.position;
        PlayerProgress p = Current;
        bool moved = !p.hasLastPosition || p.lastWorld != _localWorld ||
                     (pos - new Vector3(p.lastPosX, p.lastPosY, p.lastPosZ)).sqrMagnitude >
                     MinMoveToRecord * MinMoveToRecord;
        if (!moved)
            return;

        p.hasLastPosition = true;
        p.lastWorld = _localWorld;
        p.lastPosX = pos.x;
        p.lastPosY = pos.y;
        p.lastPosZ = pos.z;
        p.lastRotY = _localPlayer.eulerAngles.y;
        MarkDirty();
    }

    // ================================================================ Migrasi

    /// <summary>Perbaiki/upgrade data lama supaya cocok dengan versi PlayerProgress sekarang.</summary>
    private static void Migrate(PlayerProgress p) {
        p.completedDialogues ??= new List<string>();
        p.lastWorld ??= "";

        // TODO (next develop): saat menambah field list/class baru, beri default di sini juga, misalnya:
        //   p.visitedDistricts ??= new List<string>();
        // Kalau struktur berubah, misalnya: if (p.version < 2) { ...pindahkan data...; }
        p.version = PlayerProgress.CurrentVersion;
    }
}
