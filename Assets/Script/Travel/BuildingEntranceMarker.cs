using UnityEngine;

/// Penanda "gedung ini bisa dimasuki": karpet teal berpendar di lantai depan pintu (WorldScene), dengan cahaya
/// pendek di tepi depan/samping yang memudar ke atas supaya terlihat dari jauh. Selalu terlihat (tidak menunggu
/// pemain dekat), dan tidak menjorok ke dalam bangunan dari sudut mana pun. Saat pemain lokal berdiri di atas karpet, warnanya
/// menyala lebih terang dan denyutnya lebih cepat, tanda bahwa tombol "Masuk" bisa ditekan.
/// Hanya visual lokal; tidak perlu sinkron jaringan. Dipasang oleh menu
/// Go Vista > Interior > Pasang Penanda Pintu Gedung (BuildingEntranceBuilder).
[DisallowMultipleComponent]
public class BuildingEntranceMarker : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    [SerializeField] private Renderer doormat;
    [Tooltip("Cahaya pendek di tepi karpet (memudar ke atas) supaya area masuk terlihat dari jauh.")]
    [SerializeField] private Renderer[] glow = new Renderer[0];
    [Range(0f, 1f)] [SerializeField] private float glowIdleAlpha = 0.9f;
    [Range(0f, 1f)] [SerializeField] private float glowActiveAlpha = 1f;

    [Header("Warna")]
    [Tooltip("Warna karpet saat tidak ada yang berdiri di atasnya.")]
    [SerializeField] private Color idleColor = new Color32(0, 190, 170, 255);
    [Tooltip("Warna karpet saat pemain berdiri di atasnya.")]
    [SerializeField] private Color activeColor = new Color32(150, 255, 240, 255);

    [Header("Denyut")]
    [SerializeField] private float idlePulseSpeed = 2.2f;
    [SerializeField] private float activePulseSpeed = 5f;
    [Range(0f, 1f)] [SerializeField] private float idleAlphaMin = 0.7f;
    [Range(0f, 1f)] [SerializeField] private float idleAlphaMax = 0.95f;
    [Range(0f, 1f)] [SerializeField] private float activeAlphaMin = 0.9f;

    [Header("Area berdiri")]
    [Tooltip("Tambahan (meter) di sekeliling karpet yang masih dihitung 'di atas karpet'.")]
    [SerializeField] private float areaPadding = 0.25f;
    [Tooltip("Kecepatan transisi normal ↔ terang (per detik).")]
    [SerializeField] private float blendSpeed = 6f;

    private MaterialPropertyBlock block;
    private float active;   // 0 = normal, 1 = pemain di atas karpet
    private float phase;

    void Awake() => block = new MaterialPropertyBlock();

    void LateUpdate()
    {
        if (!doormat) return;

        float target = IsLocalPlayerOnMat() ? 1f : 0f;
        active = Mathf.MoveTowards(active, target, Time.deltaTime * blendSpeed);
        phase += Time.deltaTime * Mathf.Lerp(idlePulseSpeed, activePulseSpeed, active);

        float wave = 0.5f + 0.5f * Mathf.Sin(phase);
        float alpha = Mathf.Lerp(Mathf.Lerp(idleAlphaMin, idleAlphaMax, wave),
                                 Mathf.Lerp(activeAlphaMin, 1f, wave), active);
        Color color = Color.Lerp(idleColor, activeColor, active);
        color.a = alpha;

        Apply(doormat, color);

        Color glowColor = color;
        glowColor.a = alpha * Mathf.Lerp(glowIdleAlpha, glowActiveAlpha, active);
        foreach (Renderer r in glow) Apply(r, glowColor);
    }

    private void Apply(Renderer target, Color color)
    {
        if (!target) return;
        target.GetPropertyBlock(block);
        block.SetColor(BaseColorId, color);
        target.SetPropertyBlock(block);
    }

    /// Karpet = quad 1×1 di ruang lokalnya (rebah), jadi cukup cek x/y lokal kaki pemain.
    private bool IsLocalPlayerOnMat()
    {
        PlayerController local = PlayerController.Local;
        if (!local) return false;

        Transform mat = doormat.transform;
        Vector3 p = mat.InverseTransformPoint(local.transform.position);
        Vector3 scale = mat.lossyScale;
        float padX = areaPadding / Mathf.Max(0.01f, Mathf.Abs(scale.x));
        float padY = areaPadding / Mathf.Max(0.01f, Mathf.Abs(scale.y));
        // Sumbu z lokal quad = tegak lurus lantai; batasi supaya lantai atas/bawah tidak ikut terhitung.
        float height = Mathf.Abs(p.z * scale.z);
        return Mathf.Abs(p.x) <= 0.5f + padX && Mathf.Abs(p.y) <= 0.5f + padY && height <= 1.5f;
    }
}
