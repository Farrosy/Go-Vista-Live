using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bozo.ModularCharacters;
using UnityEngine;

/// <summary>
/// Tahap 0 (tes bake): memunculkan avatar BoZo acak, merakitnya dengan OutfitSystem, lalu menunggu
/// BoZo selesai menggabungkan mesh + tekstur (MergeCharacter) saat runtime. Waktu tiap bake dan
/// frame terlama selama bake dicatat untuk ditampilkan BakeStatsOverlay.
/// Hanya dipakai scene AvatarBakeTest.
/// </summary>
public class AvatarBakeTester : MonoBehaviour {
    public readonly struct BakeRecord {
        public readonly string TemplateId;
        public readonly float BakeMs;
        public readonly bool TimedOut;

        public BakeRecord(string templateId, float bakeMs, bool timedOut) {
            TemplateId = templateId;
            BakeMs = bakeMs;
            TimedOut = timedOut;
        }
    }

    private const float HitchThresholdMs = 50f;
    private static readonly Vector3 HiddenOffset = new Vector3(0f, -500f, 0f);

    [SerializeField] private OutfitSystem characterBasePrefab;
    [SerializeField] private float spacing = 1.3f;
    [SerializeField] private int columns = 5;
    [SerializeField] private float rowDepth = 1.6f;
    [SerializeField] private float timeoutSeconds = 60f;
    [SerializeField] private int randomSeed = 1234;

    public bool WithDecalAndPattern = true;
    public bool Sequential = true;

    private readonly List<OutfitSystem> spawned = new List<OutfitSystem>();
    private readonly List<BakeRecord> records = new List<BakeRecord>();
    private System.Random rng;
    private int activeBakes;

    public IReadOnlyList<BakeRecord> Records => records;
    public int AvatarCount => spawned.Count;
    public int ActiveBakes => activeBakes;
    public float WorstFrameMs { get; private set; }
    public int HitchCount { get; private set; }

    private void Awake() {
        rng = new System.Random(randomSeed);
    }

    private void Update() {
        if (activeBakes == 0) return;
        float frameMs = Time.unscaledDeltaTime * 1000f;
        if (frameMs > WorstFrameMs) WorstFrameMs = frameMs;
        if (frameMs > HitchThresholdMs) HitchCount++;
    }

    public void Spawn(int count) {
        if (characterBasePrefab == null) {
            Debug.LogError("[BakeTest] Character Base Prefab belum diisi di AvatarBakeTester.", this);
            return;
        }
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
        WorstFrameMs = 0f;
        HitchCount = 0;
        StartCoroutine(UnloadNextFrame());
    }

    private static IEnumerator UnloadNextFrame() {
        yield return null;
        yield return Resources.UnloadUnusedAssets();
    }

    private IEnumerator SpawnRoutine(int count) {
        for (int i = 0; i < count; i++) {
            IEnumerator bake = BakeOne();
            if (Sequential) yield return bake;
            else StartCoroutine(bake);
        }
    }

    private IEnumerator BakeOne() {
        CharacterData data = RandomCharacterFactory.Create(rng, WithDecalAndPattern);
        if (data == null) yield break;

        OutfitSystem avatar = CreateAvatar(spawned.Count);
        spawned.Add(avatar);
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

        records.Add(new BakeRecord(data.characterName.Replace("_Random", ""), bakeMs, timedOut));
        activeBakes--;
        if (avatar != null) avatar.transform.position -= HiddenOffset;
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
}
