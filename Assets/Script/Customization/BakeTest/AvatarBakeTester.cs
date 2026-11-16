using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bozo.ModularCharacters;
using UnityEngine;

/// <summary>
/// Tahap 0/0b (tes bake): memunculkan avatar BoZo acak, merakitnya dengan OutfitSystem, lalu menunggu
/// BoZo selesai menggabungkan mesh + tekstur (MergeCharacter) saat runtime. Waktu tiap bake dan
/// frame terlama selama bake dicatat untuk ditampilkan BakeStatsOverlay.
///
/// Opsi 0b (bisa dinyalakan/matikan dari overlay untuk dibandingkan):
///  - Pemanasan: katalog outfit dimuat + 1 bake tersembunyi per template sebelum spawn pertama
///    (di game nanti terjadi di loading screen).
///  - Antrean: maksimal 1 bake berjalan, diberi jeda beberapa frame antar bake.
///  - Kecilkan tekstur: RenderTexture 2024 diganti Texture2D 1024/512 setelah bake.
///  - Mode pemain lain: outfit sumber yang sudah digabung dihapus setelah bake (AvatarOutfitStripper).
/// Hanya dipakai scene AvatarBakeTest.
/// </summary>
public class AvatarBakeTester : MonoBehaviour {
    public readonly struct BakeRecord {
        public readonly string TemplateId;
        public readonly float BakeMs;
        public readonly float ShrinkMs;
        public readonly bool TimedOut;

        public BakeRecord(string templateId, float bakeMs, float shrinkMs, bool timedOut) {
            TemplateId = templateId;
            BakeMs = bakeMs;
            ShrinkMs = shrinkMs;
            TimedOut = timedOut;
        }
    }

    public static readonly int[] TextureSizes = { 0, 1024, 512 }; // 0 = biarkan RenderTexture asli

    private const float HitchThresholdMs = 50f;
    private const float BytesPerMegabyte = 1024f * 1024f;
    private static readonly Vector3 HiddenOffset = new Vector3(0f, -500f, 0f);

    [SerializeField] private OutfitSystem characterBasePrefab;
    [SerializeField] private float spacing = 1.3f;
    [SerializeField] private int columns = 5;
    [SerializeField] private float rowDepth = 1.6f;
    [SerializeField] private float timeoutSeconds = 60f;
    [SerializeField] private int randomSeed = 1234;
    [SerializeField] private int queueGapFrames = 10;

    public bool WithDecalAndPattern = true;
    public bool Prewarm = true;
    public bool Queue = true;
    public int TextureSize = 1024;
    public bool CleanOutfits = true;

    private readonly List<OutfitSystem> spawned = new List<OutfitSystem>();
    private readonly List<BakeRecord> records = new List<BakeRecord>();
    private System.Random rng;
    private int activeBakes;
    private bool prewarmed;
    private long shrunkBytes;

    public IReadOnlyList<BakeRecord> Records => records;
    public int AvatarCount => spawned.Count;
    public int ActiveBakes => activeBakes;
    public float WorstFrameMs { get; private set; }
    public int HitchCount { get; private set; }
    public bool IsPrewarming { get; private set; }
    public float PrewarmMs { get; private set; } = -1f;
    public float ShrunkTextureMb => shrunkBytes / BytesPerMegabyte;

    private void Awake() {
        rng = new System.Random(randomSeed);
    }

    private void Update() {
        if (activeBakes == 0 || IsPrewarming) return;
        float frameMs = Time.unscaledDeltaTime * 1000f;
        if (frameMs > WorstFrameMs) WorstFrameMs = frameMs;
        if (frameMs > HitchThresholdMs) HitchCount++;
    }

    public void Spawn(int count) {
        if (characterBasePrefab == null) {
            Debug.LogError("[BakeTest] Character Base Prefab belum diisi di AvatarBakeTester.", this);
            return;
        }
        if (IsPrewarming) return;
        WorstFrameMs = 0f;
        HitchCount = 0;
        StartCoroutine(SpawnRoutine(count));
    }

    public void ClearAll() {
        StopAllCoroutines();
        foreach (OutfitSystem avatar in spawned) {
            if (avatar != null) Destroy(avatar.gameObject);
        }
        spawned.Clear();
        records.Clear();
        activeBakes = 0;
        shrunkBytes = 0;
        IsPrewarming = false;
        WorstFrameMs = 0f;
        HitchCount = 0;
        StartCoroutine(UnloadNextFrame());
    }

    private static IEnumerator UnloadNextFrame() {
        yield return null;
        yield return Resources.UnloadUnusedAssets();
    }

    private IEnumerator SpawnRoutine(int count) {
        if (Prewarm && !prewarmed) yield return PrewarmRoutine();

        for (int i = 0; i < count; i++) {
            if (!Queue) {
                StartCoroutine(BakeOne(RandomCharacterFactory.Create(rng, WithDecalAndPattern), true));
                continue;
            }
            yield return BakeOne(RandomCharacterFactory.Create(rng, WithDecalAndPattern), true);
            for (int f = 0; f < queueGapFrames; f++) yield return null;
        }
    }

    /// Simulasi loading screen: katalog outfit dimuat, lalu 1 avatar per template di-bake tersembunyi
    /// dan langsung dibuang, supaya aset + shader sudah "hangat" sebelum avatar sungguhan muncul.
    private IEnumerator PrewarmRoutine() {
        IsPrewarming = true;
        float start = Time.realtimeSinceStartup;
        RandomCharacterFactory.WarmCatalog();
        yield return null;

        foreach (string templateId in RandomCharacterFactory.TemplateIds) {
            CharacterData data = RandomCharacterFactory.CreateFromTemplate(templateId, rng, WithDecalAndPattern);
            yield return BakeOne(data, false);
        }

        PrewarmMs = (Time.realtimeSinceStartup - start) * 1000f;
        prewarmed = true;
        IsPrewarming = false;
    }

    private IEnumerator BakeOne(CharacterData data, bool keep) {
        if (data == null) yield break;

        OutfitSystem avatar = CreateAvatar(keep ? spawned.Count : 0);
        if (keep) spawned.Add(avatar);
        activeBakes++;

        float start = Time.realtimeSinceStartup;
        Task load = BMAC_SaveSystem.LoadCharacter(avatar, data, false, avatar.async);
        bool timedOut = false;
        while (!timedOut && (!load.IsCompleted || IsBusy(avatar))) {
            timedOut = Time.realtimeSinceStartup - start > timeoutSeconds || avatar == null;
            yield return null;
        }
        float bakeMs = (Time.realtimeSinceStartup - start) * 1000f;
        if (load.IsFaulted) Debug.LogException(load.Exception, this);
        if (timedOut) Debug.LogWarning($"[BakeTest] Timeout bake {data.characterName} setelah {timeoutSeconds} detik.", this);

        if (avatar != null && !timedOut && CleanOutfits) AvatarOutfitStripper.StripMergedOutfits(avatar);
        float shrinkMs = 0f;
        if (avatar != null && !timedOut && TextureSize > 0) shrinkMs = ShrinkTextures(avatar, keep);

        activeBakes--;
        if (!keep) {
            if (avatar != null) Destroy(avatar.gameObject);
            yield break;
        }
        records.Add(new BakeRecord(data.characterName.Replace("_Random", ""), bakeMs, shrinkMs, timedOut));
        if (avatar != null) avatar.transform.position -= HiddenOffset;
    }

    private float ShrinkTextures(OutfitSystem avatar, bool keep) {
        AvatarTextureShrinker.Result result = AvatarTextureShrinker.Shrink(avatar, TextureSize);
        if (keep) shrunkBytes += result.Bytes;
        return result.Ms;
    }

    private static bool IsBusy(OutfitSystem avatar) {
        return avatar != null && (avatar.isloading || avatar.isMerging || avatar.isUpdatingTexture);
    }

    /// Dibuat di bawah parent nonaktif supaya Awake BoZo tidak memuat karakter bawaan prefab,
    /// lalu disembunyikan di bawah tanah sampai bake selesai (sama seperti alat ekspor NPC).
    private OutfitSystem CreateAvatar(int index) {
        var holder = new GameObject("__BakeTestHolder");
        holder.SetActive(false);

        OutfitSystem avatar = Instantiate(characterBasePrefab, SlotPosition(index) + HiddenOffset,
                                          Quaternion.Euler(0f, 180f, 0f), holder.transform);
        avatar.name = $"Avatar {index + 1}";
        avatar.characterData = null;
        avatar.loadMode = OutfitSystem.LoadMode.Manual;
        foreach (Camera cam in avatar.GetComponentsInChildren<Camera>(true)) cam.enabled = false;
        foreach (AudioListener listener in avatar.GetComponentsInChildren<AudioListener>(true)) listener.enabled = false;

        avatar.transform.SetParent(transform, true);
        Destroy(holder);
        return avatar;
    }

    private Vector3 SlotPosition(int index) {
        int column = index % columns;
        int row = index / columns;
        float x = (column - (columns - 1) * 0.5f) * spacing;
        return transform.position + new Vector3(x, 0f, row * rowDepth);
    }

    public float AverageShrinkMs => records.Count > 0 ? records.Average(r => r.ShrinkMs) : 0f;
}
