using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// Pasang di SprintButton. Tidak perlu OnClick, cukup script ini.
/// Saat sprint aktif: warna tombol digelapkan + sedikit mengecil, jadi kelihatan jelas.
public class SprintButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    [Tooltip("OFF = tahan tombol untuk sprint. ON = tap sekali nyala, tap lagi mati.")]
    [SerializeField] private bool toggleMode = false;

    [Header("Feedback visual")]
    [Tooltip("Kosong = otomatis pakai Image di object ini")]
    [SerializeField] private Image buttonImage;
    [Range(0.2f, 1f)]
    [Tooltip("Makin kecil = makin gelap saat aktif")]
    [SerializeField] private float activeBrightness = 0.55f;
    [SerializeField] private float activeScale = 0.92f;

    private Color normalColor;
    private Color activeColor;
    private bool active;

    void Awake()
    {
        if (!buttonImage) buttonImage = GetComponent<Image>();
        if (buttonImage)
        {
            normalColor = buttonImage.color;   // pakai warna aslimu, tidak dipaksa putih
            activeColor = new Color(
                normalColor.r * activeBrightness,
                normalColor.g * activeBrightness,
                normalColor.b * activeBrightness,
                normalColor.a);
        }
        Apply(false);
    }

    void OnDisable() => SetSprint(false);

    public void OnPointerDown(PointerEventData e)
    {
        if (toggleMode) SetSprint(!active);
        else SetSprint(true);
    }

    public void OnPointerUp(PointerEventData e)
    {
        if (!toggleMode) SetSprint(false);
    }

    private void SetSprint(bool value)
    {
        active = value;
        if (PlayerInputHub.Instance) PlayerInputHub.Instance.Sprint = value;
        Apply(value);
    }

    private void Apply(bool value)
    {
        if (buttonImage) buttonImage.color = value ? activeColor : normalColor;
        transform.localScale = Vector3.one * (value ? activeScale : 2f);
    }
}