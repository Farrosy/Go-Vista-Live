using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// Pasang di SprintButton. Tidak perlu OnClick, cukup script ini.
/// Saat sprint aktif, tombol memakai Active Sprite (kalau diisi) dan/atau warna Pressed Color,
/// supaya pemain tahu sedang sprint.
/// Scale tombol tidak diubah, jadi ukuran tetap sesuai yang diatur di Inspector.
[RequireComponent(typeof(Image))]
public class SprintButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler,
    IPointerEnterHandler, IPointerExitHandler
{
    [Tooltip("OFF = tahan tombol untuk sprint. ON = tap sekali nyala, tap lagi mati.")]
    [SerializeField] private bool toggleMode = false;

    [Header("Feedback visual (Color Tint, sama seperti Button biasa)")]
    [Tooltip("Kosong = otomatis pakai Image di object ini (lingkaran tombol).")]
    [SerializeField] private Image backgroundImage;

    [Tooltip("Opsional. Sprite saat sprint AKTIF (misalnya btn-run-pressed). Kosong = hanya warna yang berubah.")]
    [SerializeField] private Sprite activeSprite;

    [Tooltip("Normal = sprint mati. Pressed = sprint AKTIF. Highlighted = kursor di atas tombol (hanya terasa di PC). " +
             "Selected dan Disabled tidak dipakai.")]
    [SerializeField] private ColorBlock colors = new ColorBlock
    {
        normalColor      = Color.white,
        highlightedColor = new Color(0.9f, 0.9f, 0.9f, 1f),
        pressedColor     = new Color(0.35f, 0.35f, 0.35f, 1f), // gelap
        selectedColor    = Color.white,
        disabledColor    = new Color(0.78f, 0.78f, 0.78f, 0.5f),
        colorMultiplier  = 1f,
        fadeDuration     = 0.1f
    };

    private Sprite normalSprite;
    private bool active;
    private bool movedWhileActive;
    private bool pointerInside;

    void Awake()
    {
        CacheBackground();
        ApplyVisualState(true);
    }

    void OnDisable() => SetSprint(false);

    void Update()
    {
        if (!active || !PlayerInputHub.Instance) return;

        // Sprint tetap "siap" kalau dipilih sebelum bergerak, tapi dimatikan begitu
        // pemain sudah bergerak lalu joystick kembali ke netral.
        if (PlayerInputHub.Instance.Move.sqrMagnitude > 0.0001f)
        {
            movedWhileActive = true;
        }
        else if (movedWhileActive)
        {
            SetSprint(false);
        }
    }

    public void OnPointerDown(PointerEventData e)
    {
        if (toggleMode) SetSprint(!active);
        else SetSprint(true);
    }

    public void OnPointerUp(PointerEventData e)
    {
        if (!toggleMode) SetSprint(false);
    }

    public void OnPointerEnter(PointerEventData e)
    {
        pointerInside = true;
        ApplyVisualState(false);
    }

    public void OnPointerExit(PointerEventData e)
    {
        pointerInside = false;
        ApplyVisualState(false);
    }

    private void SetSprint(bool value)
    {
        active = value;
        if (!value) movedWhileActive = false;
        if (PlayerInputHub.Instance) PlayerInputHub.Instance.Sprint = value;
        ApplyVisualState(false);
    }

    public void ResetSprint() => SetSprint(false);

    private void CacheBackground()
    {
        if (!backgroundImage) backgroundImage = GetComponent<Image>();
        if (!normalSprite && backgroundImage && backgroundImage.sprite != activeSprite)
            normalSprite = backgroundImage.sprite;
    }

    private void ApplyVisualState(bool instant)
    {
        // MobileControls.Awake bisa memanggil ResetSprint sebelum Awake tombol ini, jadi cache di sini juga.
        CacheBackground();
        if (!backgroundImage) return;

        if (activeSprite && normalSprite) backgroundImage.sprite = active ? activeSprite : normalSprite;

        Color target = active ? colors.pressedColor
                     : pointerInside ? colors.highlightedColor
                     : colors.normalColor;

        backgroundImage.CrossFadeColor(
            target * colors.colorMultiplier,
            instant ? 0f : colors.fadeDuration,
            true, true);
    }
}