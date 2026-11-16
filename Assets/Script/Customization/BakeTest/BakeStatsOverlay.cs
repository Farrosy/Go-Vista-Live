using System.Linq;
using UnityEngine;

/// <summary>
/// Tahap 0 (tes bake): panel angka di layar untuk AvatarBakeTester. Menampilkan FPS, frame terlama
/// selama bake, waktu bake per avatar, dan memori RenderTexture (BoZo membuat beberapa RT 2024x2024
/// per avatar). Pakai IMGUI karena ini alat ukur sementara, bukan UI game.
/// </summary>
[RequireComponent(typeof(AvatarBakeTester))]
public class BakeStatsOverlay : MonoBehaviour {
    private const float ReferenceHeight = 1080f;
    private const float FpsWindowSeconds = 0.5f;
    private const float MemoryRefreshSeconds = 1f;
    private const float BytesPerMegabyte = 1024f * 1024f;
    private const int BytesPerPixel = 4;
    private const float PanelWidth = 560f;
    private const float PanelHeight = 900f;

    private AvatarBakeTester tester;
    private int frames;
    private float fpsTimer;
    private float fps;
    private float memoryTimer;
    private int renderTextureCount;
    private float renderTextureMb;
    private float baselineMb = -1f;
    private GUIStyle labelStyle;
    private GUIStyle buttonStyle;
    private GUIStyle toggleStyle;

    private void Awake() {
        tester = GetComponent<AvatarBakeTester>();
    }

    /// Baseline diambil sebelum avatar apa pun dibuat, supaya angka "tambahan" murni dari avatar.
    private void Start() {
        RefreshRenderTextureMemory();
    }

    private void Update() {
        frames++;
        fpsTimer += Time.unscaledDeltaTime;
        if (fpsTimer >= FpsWindowSeconds) {
            fps = frames / fpsTimer;
            frames = 0;
            fpsTimer = 0f;
        }

        memoryTimer += Time.unscaledDeltaTime;
        if (memoryTimer >= MemoryRefreshSeconds) {
            memoryTimer = 0f;
            RefreshRenderTextureMemory();
        }
    }

    /// Perkiraan kasar: lebar x tinggi x 4 byte, tanpa mipmap dan depth. Cukup untuk membandingkan.
    /// RT yang sudah di-Release (setelah tekstur dikecilkan) tidak dihitung.
    private void RefreshRenderTextureMemory() {
        RenderTexture[] all = Resources.FindObjectsOfTypeAll<RenderTexture>().Where(rt => rt.IsCreated()).ToArray();
        renderTextureCount = all.Length;
        long bytes = all.Sum(rt => (long)rt.width * rt.height * BytesPerPixel);
        renderTextureMb = bytes / BytesPerMegabyte;
        if (baselineMb < 0f) baselineMb = renderTextureMb;
    }

    private void OnGUI() {
        float scale = Screen.height / ReferenceHeight;
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
        EnsureStyles();

        float right = Screen.width / scale - PanelWidth - 20f;
        GUILayout.BeginArea(new Rect(right, 20f, PanelWidth, PanelHeight), GUI.skin.box);
        DrawStats();
        GUILayout.Space(12f);
        DrawControls();
        GUILayout.EndArea();
    }

    private void DrawStats() {
        var records = tester.Records;
        float lastMs = records.Count > 0 ? records[records.Count - 1].BakeMs : 0f;
        float avgMs = records.Count > 0 ? records.Average(r => r.BakeMs) : 0f;
        float maxMs = records.Count > 0 ? records.Max(r => r.BakeMs) : 0f;
        int timeouts = records.Count(r => r.TimedOut);

        Line($"FPS: {fps:0}   ({(fps > 0f ? 1000f / fps : 0f):0.0} ms/frame)");
        Line($"Avatar: {tester.AvatarCount}   sedang bake: {tester.ActiveBakes}");
        Line($"Frame terlama saat bake: {tester.WorstFrameMs:0} ms");
        Line($"Frame > 50 ms saat bake: {tester.HitchCount}x");
        Line($"Bake terakhir: {lastMs:0} ms");
        Line($"Bake rata-rata: {avgMs:0} ms   terlama: {maxMs:0} ms");
        Line($"Kecilkan tekstur rata-rata: {tester.AverageShrinkMs:0} ms");
        if (timeouts > 0) Line($"Timeout: {timeouts}");
        string prewarm = tester.IsPrewarming ? "berjalan..." : tester.PrewarmMs < 0f ? "belum" : $"{tester.PrewarmMs:0} ms";
        Line($"Pemanasan (loading screen): {prewarm}");
        float extraRt = Mathf.Max(0f, renderTextureMb - baselineMb);
        float perAvatar = tester.AvatarCount > 0 ? extraRt / tester.AvatarCount : 0f;
        Line($"RenderTexture tambahan: ~{extraRt:0} MB ({renderTextureCount} RT)");
        Line($"  per avatar: ~{perAvatar:0} MB   (hasil kecilkan: ~{tester.ShrunkTextureMb:0} MB)");
    }

    private void DrawControls() {
        tester.WithDecalAndPattern = GUILayout.Toggle(tester.WithDecalAndPattern, " Pakai decal + pattern", toggleStyle);
        tester.Prewarm = GUILayout.Toggle(tester.Prewarm, " Pemanasan dulu (sekali per Play)", toggleStyle);
        tester.Queue = GUILayout.Toggle(tester.Queue, " Antrean (matikan = sekaligus)", toggleStyle);
        tester.CleanOutfits = GUILayout.Toggle(tester.CleanOutfits, " Mode pemain lain (buang outfit)", toggleStyle);
        string sizeLabel = tester.TextureSize == 0 ? "Asli (RT 2024)" : tester.TextureSize.ToString();
        if (GUILayout.Button($"Tekstur: {sizeLabel}  (klik untuk ganti)", buttonStyle)) CycleTextureSize();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Spawn 1", buttonStyle)) tester.Spawn(1);
        if (GUILayout.Button("Spawn 5", buttonStyle)) tester.Spawn(5);
        if (GUILayout.Button("Spawn 10", buttonStyle)) tester.Spawn(10);
        GUILayout.EndHorizontal();

        if (GUILayout.Button("Hapus semua", buttonStyle)) tester.ClearAll();
    }

    private void CycleTextureSize() {
        int index = System.Array.IndexOf(AvatarBakeTester.TextureSizes, tester.TextureSize);
        tester.TextureSize = AvatarBakeTester.TextureSizes[(index + 1) % AvatarBakeTester.TextureSizes.Length];
    }

    private void Line(string text) {
        GUILayout.Label(text, labelStyle);
    }

    private void EnsureStyles() {
        if (labelStyle != null) return;
        labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 22 };
        buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 22, fixedHeight = 50f };
        toggleStyle = new GUIStyle(GUI.skin.toggle) { fontSize = 22 };
    }
}
