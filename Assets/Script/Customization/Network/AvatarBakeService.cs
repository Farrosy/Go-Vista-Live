using System;
using System.Collections;
using Bozo.ModularCharacters;
using UnityEngine;

/// <summary>
/// Antrean bake avatar custom di perangkat ini (Tahap 3, opsi A dari Tahap 0b). Satu avatar per giliran
/// dengan jeda antar-bake, supaya lonjakan merge BoZo (~120–200 ms) tidak menumpuk di satu frame.
///  - Avatar sendiri di-bake di lobby (<see cref="PrebakeLocal"/>, tekstur 1024) lalu diambil saat spawn.
///  - Avatar pemain lain di-bake saat datanya tiba (<see cref="Request"/>, tekstur 512).
/// Lobby menahan tombol MULAI sampai <see cref="ReadyForSpawn"/> (keputusan user: per perangkat, tidak menunggu
/// konfirmasi pemain lain). Bake gagal = pemain tampil sebagai template asalnya, tanpa coba ulang.
/// </summary>
public sealed class AvatarBakeService : MonoBehaviour {
    public const int LocalTextureSize = 1024;
    public const int RemoteTextureSize = 512;
    // Di HP bake ~1–3 detik; batas longgar supaya perangkat lambat (atau editor di latar belakang) tidak
    // jatuh ke template terlalu cepat. Lewat batas = gagal = tampil template.
    private const float BakeTimeoutSeconds = 45f;
    private const int FramesBetweenBakes = 10;
    // Avatar sementara dirakit jauh di bawah dunia supaya tidak terlihat kamera mana pun.
    private static readonly Vector3 BakePosition = new Vector3(0f, -5000f, 0f);

    private sealed class Job {
        public CustomCharacterRecord Record;
        public int TextureSize;
        public Action<OutfitSystem> Baked;
        public Action Failed;
    }

    private static AvatarBakeService _instance;
    private readonly AvatarBakeTracker<Job> _tracker = new AvatarBakeTracker<Job>();
    private Coroutine _worker;
    private string _localPayload = "";
    private OutfitSystem _localBaked;

    public static AvatarBakeService Instance {
        get {
            if (_instance) return _instance;
            var go = new GameObject("Avatar Bake Service");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<AvatarBakeService>();
            return _instance;
        }
    }

    public static bool Exists => _instance;

    /// <summary>Avatar sendiri + semua pemain lain yang sudah ada sudah selesai di-bake di perangkat ini.</summary>
    public bool ReadyForSpawn => _tracker.AllDone;

    public (int done, int total) Progress => _tracker.Progress();

    // ---------------------------------------------------------------- Avatar sendiri (lobby)

    /// <summary>
    /// Mulai bake karakter custom yang sedang dipakai (dipanggil saat masuk lobby room). Tidak melakukan apa pun
    /// kalau hasil untuk data yang sama sudah ada / sedang diproses. Template biasa = tidak perlu bake.
    /// </summary>
    public void PrebakeLocal() {
        string payload = AppearanceCodec.EncodeLocal();
        if (payload == _localPayload && (_localBaked || _tracker.StateOf(AvatarBakeTracker<Job>.LocalKey) != null)) return;
        DiscardLocal();
        _localPayload = payload;
        CustomCharacterRecord record = AppearanceCodec.Decode(payload);
        if (record == null) return;
        Enqueue(AvatarBakeTracker<Job>.LocalKey, new Job {
            Record = record, TextureSize = LocalTextureSize,
            Baked = avatar => {
                _localBaked = avatar;
                avatar.transform.parent.gameObject.SetActive(false); // simpan tersembunyi sampai spawn
            }
        });
    }

    /// <summary>Ambil hasil bake lobby untuk avatar sendiri kalau datanya sama. Pemanggil yang memiliki hasilnya.</summary>
    public bool TryTakeLocal(string payload, out OutfitSystem baked) {
        baked = null;
        if (!_localBaked || payload != _localPayload) return false;
        baked = _localBaked;
        baked.transform.parent.gameObject.SetActive(true);
        _localBaked = null;
        _localPayload = "";
        _tracker.Forget(AvatarBakeTracker<Job>.LocalKey);
        return true;
    }

    private void DiscardLocal() {
        _tracker.Forget(AvatarBakeTracker<Job>.LocalKey);
        if (_localBaked) Destroy(_localBaked.transform.parent.gameObject);
        _localBaked = null;
        _localPayload = "";
    }

    // ---------------------------------------------------------------- Avatar di room

    /// <summary>Minta bake untuk satu avatar (kunci unik per avatar). <paramref name="baked"/> menerima hasilnya.</summary>
    public void Request(int key, CustomCharacterRecord record, int textureSize, Action<OutfitSystem> baked, Action failed) {
        Enqueue(key, new Job { Record = record, TextureSize = textureSize, Baked = baked, Failed = failed });
    }

    /// <summary>Avatar keluar room / tidak perlu bake lagi.</summary>
    public void Forget(int key) => _tracker.Forget(key);

    private void Enqueue(int key, Job job) {
        _tracker.Request(key, job);
        if (_worker == null) _worker = StartCoroutine(Work());
    }

    // ---------------------------------------------------------------- Bake

    private IEnumerator Work() {
        while (_tracker.TryStartNext(out int key, out Job job)) {
            OutfitSystem avatar = SpawnAvatar(job.Record);
            float until = Time.unscaledTime + BakeTimeoutSeconds;
            yield return null;
            while (avatar && IsBusy(avatar) && Time.unscaledTime < until) yield return null;
            yield return null; // update tekstur terakhir (stiker, warna) selesai

            bool success = avatar && !IsBusy(avatar) && avatar.GetCharacterBody();
            if (!success) {
                string reason = !avatar ? "avatar tidak bisa dibuat" : IsBusy(avatar) ? "timeout" : "mesh gabungan tidak ada";
                Debug.LogWarning($"[Appearance] Bake avatar gagal ({reason}, template {job.Record.templateId}).");
            }
            else {
                AvatarTextureShrinker.Shrink(avatar, job.TextureSize);
                // Bobot blendshape mesh gabungan ikut disalin AvatarMeshTransplant ke rig pemain.
                LiveAvatarEdits.ApplyExpression(avatar.gameObject, job.Record.expression);
            }

            bool current = _tracker.Complete(key, success);
            if (current && success) Deliver(job, avatar);
            else {
                if (avatar) Destroy(avatar.transform.parent.gameObject);
                if (current) job.Failed?.Invoke();
            }
            for (int i = 0; i < FramesBetweenBakes; i++) yield return null;
        }
        _worker = null;
    }

    private static void Deliver(Job job, OutfitSystem avatar) {
        try {
            job.Baked?.Invoke(avatar);
        } catch (Exception e) {
            Debug.LogException(e);
            if (avatar) Destroy(avatar.transform.parent.gameObject);
            job.Failed?.Invoke();
        }
    }

    private static bool IsBusy(OutfitSystem avatar) => avatar.isloading || avatar.isMerging || avatar.isUpdatingTexture;

    /// Pola sama dengan CharacterPreviewStage.ShowLive: dibuat di bawah induk nonaktif supaya Awake BoZo tidak
    /// memuat karakter bawaan prefab, lalu skala = skala model template (tinggi sama dengan saat main).
    private OutfitSystem SpawnAvatar(CustomCharacterRecord record) {
        CustomizerSettings settings = CustomizerSettings.Instance;
        CharacterData data = CharacterRecordConverter.ToCharacterData(record);
        if (!settings || !settings.characterBase || data == null || !CharacterDatabase.Instance) return null;

        var holder = new GameObject("Avatar Bake " + record.templateId);
        holder.transform.SetParent(transform, false);
        holder.transform.position = BakePosition;
        holder.SetActive(false);
        OutfitSystem avatar = Instantiate(settings.characterBase, holder.transform);
        avatar.characterData = null;
        avatar.loadMode = OutfitSystem.LoadMode.Manual;
        foreach (Camera cam in avatar.GetComponentsInChildren<Camera>(true)) cam.enabled = false;
        foreach (AudioListener listener in avatar.GetComponentsInChildren<AudioListener>(true)) Destroy(listener);
        GameObject source = CharacterDatabase.Instance.Get(record.templateId)?.PreviewSource;
        if (source) avatar.transform.localScale = source.transform.localScale;
        holder.SetActive(true);
        _ = BMAC_SaveSystem.LoadCharacter(avatar, data, false, avatar.async);
        return avatar;
    }
}
