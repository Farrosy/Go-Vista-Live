using Normal.Realtime;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Siklus siang-malam WorldScene: memutar matahari (Directional Light), mengatur warna langit, ambient,
/// fog, dan menyalakan lampu jalan (StreetLamp) saat senja.
///
/// Jam game = jam server Normcore (room.time) kalau sudah konek, kalau belum pakai jam UTC perangkat.
/// Satu siklus default 24 menit (1 jam game = 1 menit). Semua pemain melihat jam yang sama tanpa sinkron.
///
/// Tampilan tiap fase diatur lewat tiga "look" (Siang, Senja/Subuh, Malam) di Inspector; nilai di antaranya
/// di-blend otomatis dari tinggi matahari. Untuk cek tampilan, centang Override Jam saat Play.
/// </summary>
[DefaultExecutionOrder(-50)]
public sealed class DayNightCycle : MonoBehaviour {
    [System.Serializable]
    public struct Look {
        public Color sunColor;
        [ColorUsage(false)] public Color ambientSky;
        [ColorUsage(false)] public Color ambientEquator;
        [ColorUsage(false)] public Color ambientGround;
        [ColorUsage(false)] public Color fogColor;
        [ColorUsage(false)] public Color skyTint;
        public float skyExposure;
        public float atmosphereThickness;
        [Range(0f, 1f)] public float reflectionIntensity;
        [Header("Langit cubemap (GoVista/SkyboxDayNight)")]
        [Range(0f, 1f)] public float skyGrade;
        [ColorUsage(false)] public Color skyTop;
        [ColorUsage(false)] public Color skyHorizon;
        [ColorUsage(false, true)] public Color sunGlow;

        public static Look Lerp(Look a, Look b, float t) => new Look {
            sunColor = Color.Lerp(a.sunColor, b.sunColor, t),
            ambientSky = Color.Lerp(a.ambientSky, b.ambientSky, t),
            ambientEquator = Color.Lerp(a.ambientEquator, b.ambientEquator, t),
            ambientGround = Color.Lerp(a.ambientGround, b.ambientGround, t),
            fogColor = Color.Lerp(a.fogColor, b.fogColor, t),
            skyTint = Color.Lerp(a.skyTint, b.skyTint, t),
            skyExposure = Mathf.Lerp(a.skyExposure, b.skyExposure, t),
            atmosphereThickness = Mathf.Lerp(a.atmosphereThickness, b.atmosphereThickness, t),
            reflectionIntensity = Mathf.Lerp(a.reflectionIntensity, b.reflectionIntensity, t),
            skyGrade = Mathf.Lerp(a.skyGrade, b.skyGrade, t),
            skyTop = Color.Lerp(a.skyTop, b.skyTop, t),
            skyHorizon = Color.Lerp(a.skyHorizon, b.skyHorizon, t),
            sunGlow = Color.Lerp(a.sunGlow, b.sunGlow, t),
        };
    }

    public static DayNightCycle Instance { get; private set; }

    [Header("Waktu")]
    [Tooltip("Lama satu siklus 24 jam game, dalam menit nyata.")]
    [SerializeField] private float cycleMinutes = (float)(DayNightRules.DefaultCycleSeconds / 60.0);
    [Tooltip("Geser jam game (jam). Mengubah ini mengubah jam untuk semua pemain sekaligus.")]
    [SerializeField] private float hourOffset;

    [Header("Debug (Play mode)")]
    [SerializeField] private bool overrideHour;
    [Range(0f, 24f)] [SerializeField] private float debugHour = 17.75f;

    [Header("Matahari")]
    [SerializeField] private Light sun;
    [Tooltip("Tinggi matahari maksimal jam 12 (derajat).")]
    [SerializeField] private float maxElevation = 55f;
    [Tooltip("Arah (Y) Directional Light di jam 12.")]
    [SerializeField] private float noonLightYaw = 320f;
    [SerializeField] private float dayIntensity = 1.6f;

    [Header("Tampilan")]
    [SerializeField] private Look day = new Look {
        sunColor = new Color(1f, 0.92f, 0.8f),
        ambientSky = new Color(0.52f, 0.62f, 0.78f),
        ambientEquator = new Color(0.48f, 0.5f, 0.53f),
        ambientGround = new Color(0.3f, 0.27f, 0.24f),
        fogColor = new Color(0.7f, 0.82f, 0.95f),
        skyTint = new Color(0.5f, 0.5f, 0.5f),
        skyExposure = 1.3f,
        atmosphereThickness = 1f,
        reflectionIntensity = 1f,
        skyGrade = 0f,
        skyTop = new Color(0.42f, 0.42f, 0.55f),
        skyHorizon = new Color(1f, 0.55f, 0.28f),
        sunGlow = Color.black,
    };
    [SerializeField] private Look golden = new Look {
        sunColor = new Color(1f, 0.6f, 0.32f),
        ambientSky = new Color(0.55f, 0.47f, 0.45f),
        ambientEquator = new Color(0.55f, 0.42f, 0.33f),
        ambientGround = new Color(0.24f, 0.19f, 0.16f),
        fogColor = new Color(0.9f, 0.64f, 0.45f),
        skyTint = new Color(0.72f, 0.45f, 0.33f),
        skyExposure = 1.15f,
        atmosphereThickness = 1.3f,
        reflectionIntensity = 0.7f,
        skyGrade = 0.9f,
        skyTop = new Color(0.4f, 0.4f, 0.52f),
        skyHorizon = new Color(1f, 0.56f, 0.3f),
        sunGlow = new Color(1f, 0.45f, 0.15f) * 1.5f,
    };
    [SerializeField] private Look night = new Look {
        sunColor = new Color(0.5f, 0.6f, 1f),
        ambientSky = new Color(0.36f, 0.42f, 0.6f),
        ambientEquator = new Color(0.25f, 0.27f, 0.38f),
        ambientGround = new Color(0.13f, 0.13f, 0.17f),
        fogColor = new Color(0.08f, 0.1f, 0.18f),
        skyTint = new Color(0.3f, 0.35f, 0.5f),
        skyExposure = 0.35f,
        atmosphereThickness = 1f,
        reflectionIntensity = 0.25f,
        skyGrade = 0.95f,
        skyTop = new Color(0.02f, 0.03f, 0.08f),
        skyHorizon = new Color(0.09f, 0.11f, 0.22f),
        sunGlow = Color.black,
    };

    [Tooltip("Shader GoVista/SkyboxDayNight. Dipakai kalau skybox scene berupa cubemap (Skybox_5); referensi di sini juga memastikan shader ikut ke build.")]
    [SerializeField] private Shader cubemapSkyShader;

    [Header("Lampu jalan")]
    [Tooltip("Light lampu yang lebih jauh dari ini (m) dari kamera dimatikan; bohlamnya tetap menyala.")]
    [SerializeField] private float lampCullDistance = 75f;

    private const float CullInterval = 0.5f;
    private static readonly int SkyTintId = Shader.PropertyToID("_SkyTint");
    private static readonly int ExposureId = Shader.PropertyToID("_Exposure");
    private static readonly int AtmosphereId = Shader.PropertyToID("_AtmosphereThickness");
    private static readonly int TintId = Shader.PropertyToID("_Tint");
    private static readonly int CubeTexId = Shader.PropertyToID("_Tex");
    private static readonly int RotationId = Shader.PropertyToID("_Rotation");
    private static readonly int GradeId = Shader.PropertyToID("_Grade");
    private static readonly int TopColorId = Shader.PropertyToID("_TopColor");
    private static readonly int HorizonColorId = Shader.PropertyToID("_HorizonColor");
    private static readonly int SunGlowColorId = Shader.PropertyToID("_SunGlowColor");
    private static readonly int SunDirectionId = Shader.PropertyToID("_SunDirection");

    private struct EnvironmentSnapshot {
        public bool taken;
        public Scene scene;
        public UnityEngine.Rendering.AmbientMode ambientMode;
        public Color ambientSky, ambientEquator, ambientGround, fogColor;
        public float reflectionIntensity;
        public Material skybox;
        public float skyExposure;
        public bool cubemap;
    }

    private EnvironmentSnapshot _snapshot;
    private Material _skybox;
    private Realtime _realtime;
    private float _nextRealtimeLookup;
    private float _nextCull;

    /// <summary>Jam game sekarang (0..24), untuk UI jam nanti.</summary>
    public float CurrentHour { get; private set; }

    /// <summary>0 = siang, 1 = malam.</summary>
    public float Night { get; private set; }

    private void OnEnable() {
        Instance = this;
        if (sun == null) sun = RenderSettings.sun;
    }

    private void OnDisable() {
        if (Instance == this) Instance = null;
        RestoreEnvironment();
    }

    private void OnDestroy() {
        if (_skybox != null) Destroy(_skybox);
    }

    private void Update() {
        CurrentHour = overrideHour ? debugHour : ClockHour();
        float elevation = DayNightRules.SunElevation(CurrentHour, maxElevation);
        Night = DayNightRules.NightFactor(elevation);
        float goldenT = DayNightRules.GoldenFactor(elevation);
        Look look = Look.Lerp(Look.Lerp(day, night, Night), golden, goldenT);

        ApplySun(elevation, look);
        ApplyEnvironment(look);
        ApplyLamps(DayNightRules.StreetLightLevel(elevation));
    }

    private float ClockHour() {
        double cycleSeconds = Mathf.Max(1f, cycleMinutes) * 60.0;
        return DayNightRules.HourFromUnixSeconds(ServerSeconds(), cycleSeconds, hourOffset);
    }

    /// Jam server Normcore kalau konek (tidak terpengaruh jam HP yang salah), kalau belum: UTC perangkat.
    private double ServerSeconds() {
        if (_realtime == null && Time.unscaledTime >= _nextRealtimeLookup) {
            _nextRealtimeLookup = Time.unscaledTime + 2f;
            _realtime = FindAnyObjectByType<Realtime>();
        }
        if (_realtime != null && _realtime.connected && _realtime.room != null)
            return _realtime.room.time;
        return (System.DateTime.UtcNow - System.DateTime.UnixEpoch).TotalSeconds;
    }

    private void ApplySun(float elevation, Look look) {
        if (sun == null) return;
        float azimuth = noonLightYaw + DayNightRules.SunAzimuthOffset(CurrentHour);
        // Directional Light menghadap ke bawah dari arah matahari: pitch = tinggi matahari.
        sun.transform.rotation = Quaternion.Euler(elevation, azimuth, 0f);
        float intensity = dayIntensity * DayNightRules.SmoothStep(-3f, 7f, elevation);
        sun.intensity = intensity;
        sun.color = look.sunColor;
        sun.enabled = intensity > 0.01f; // malam: tidak ada render shadow matahari, lebih ringan
    }

    /// <summary>
    /// RenderSettings (ambient, fog, skybox) milik scene AKTIF. Saat main itu MainMultiplayerScene (WorldScene
    /// di-load additive), jadi nilai asli scene aktif disimpan dulu lalu dikembalikan saat WorldScene di-unload
    /// (mis. kembali ke lobby). Kalau scene aktif berganti (masuk venue, punya lighting sendiri), berhenti.
    /// </summary>
    private void ApplyEnvironment(Look look) {
        Scene active = SceneManager.GetActiveScene();
        if (!_snapshot.taken) TakeSnapshot(active);
        else if (active != _snapshot.scene) return;

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = look.ambientSky;
        RenderSettings.ambientEquatorColor = look.ambientEquator;
        RenderSettings.ambientGroundColor = look.ambientGround;
        RenderSettings.fogColor = look.fogColor;
        RenderSettings.reflectionIntensity = look.reflectionIntensity;

        if (_skybox == null) return;
        if (_snapshot.cubemap) {
            // Cubemap (Skybox_5): warna diatur lewat gradasi; exposure tetap asli.
            _skybox.SetFloat(GradeId, look.skyGrade);
            _skybox.SetColor(TopColorId, look.skyTop);
            _skybox.SetColor(HorizonColorId, look.skyHorizon);
            _skybox.SetColor(SunGlowColorId, look.sunGlow);
            if (sun != null) _skybox.SetVector(SunDirectionId, -sun.transform.forward);
        } else {
            // Procedural: exposure relatif ke nilai asli skybox, jadi siang tampil persis seperti sebelumnya.
            float exposure = _snapshot.skyExposure * look.skyExposure / Mathf.Max(0.01f, day.skyExposure);
            _skybox.SetFloat(ExposureId, exposure);
            if (_skybox.HasProperty(SkyTintId)) _skybox.SetColor(SkyTintId, look.skyTint);
            if (_skybox.HasProperty(AtmosphereId)) _skybox.SetFloat(AtmosphereId, look.atmosphereThickness);
        }
    }

    private void TakeSnapshot(Scene active) {
        _snapshot = new EnvironmentSnapshot {
            taken = true,
            scene = active,
            ambientMode = RenderSettings.ambientMode,
            ambientSky = RenderSettings.ambientSkyColor,
            ambientEquator = RenderSettings.ambientEquatorColor,
            ambientGround = RenderSettings.ambientGroundColor,
            fogColor = RenderSettings.fogColor,
            reflectionIntensity = RenderSettings.reflectionIntensity,
            skybox = RenderSettings.skybox,
            skyExposure = 1f,
        };
        Material original = RenderSettings.skybox;
        if (original == null || !original.HasProperty(ExposureId)) return;
        _snapshot.skyExposure = original.GetFloat(ExposureId);
        if (_skybox != null) Destroy(_skybox);
        // Material runtime supaya material skybox asli (asset) tidak ikut berubah.
        Texture cube = original.HasProperty(CubeTexId) ? original.GetTexture(CubeTexId) : null;
        if (cube is Cubemap && cubemapSkyShader != null) {
            _snapshot.cubemap = true;
            _skybox = new Material(cubemapSkyShader) { name = original.name + " (DayNight)" };
            _skybox.SetTexture(CubeTexId, cube);
            _skybox.SetFloat(ExposureId, _snapshot.skyExposure);
            if (original.HasProperty(TintId)) _skybox.SetColor(TintId, original.GetColor(TintId));
            if (original.HasProperty(RotationId)) _skybox.SetFloat(RotationId, original.GetFloat(RotationId));
        } else {
            _skybox = new Material(original) { name = original.name + " (DayNight)" };
        }
        RenderSettings.skybox = _skybox;
    }

    private void RestoreEnvironment() {
        if (!_snapshot.taken) return;
        if (SceneManager.GetActiveScene() == _snapshot.scene) {
            RenderSettings.ambientMode = _snapshot.ambientMode;
            RenderSettings.ambientSkyColor = _snapshot.ambientSky;
            RenderSettings.ambientEquatorColor = _snapshot.ambientEquator;
            RenderSettings.ambientGroundColor = _snapshot.ambientGround;
            RenderSettings.fogColor = _snapshot.fogColor;
            RenderSettings.reflectionIntensity = _snapshot.reflectionIntensity;
            if (RenderSettings.skybox == _skybox) RenderSettings.skybox = _snapshot.skybox;
        }
        _snapshot = default;
    }

    private void ApplyLamps(float level) {
        var lamps = StreetLamp.All;
        bool cull = Time.unscaledTime >= _nextCull;
        Vector3 camPos = Vector3.zero;
        if (cull) {
            _nextCull = Time.unscaledTime + CullInterval;
            Camera cam = Camera.main;
            cull = cam != null;
            if (cull) camPos = cam.transform.position;
        }
        float maxSqr = lampCullDistance * lampCullDistance;
        for (int i = 0; i < lamps.Count; i++) {
            StreetLamp lamp = lamps[i];
            if (cull) lamp.SetCulled((lamp.LightPosition - camPos).sqrMagnitude > maxSqr);
            lamp.SetLevel(level);
        }
    }
}
