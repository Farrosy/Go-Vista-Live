using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Tampilan joystick saat disentuh: latar dan kenop berganti ke sprite "aktif" (cincin cyan), lalu kembali
/// normal saat jari diangkat. Pasang di objek yang sama dengan Joystick (Fixed Joystick di MobileUI); Unity
/// mengirim event sentuh ke semua komponen di objek itu, jadi Joystick.cs tidak perlu diubah.
/// Dipasang oleh Go Vista > Desain Baru > Kontrol Layar.
/// </summary>
public sealed class JoystickPressVisual : MonoBehaviour, IPointerDownHandler, IPointerUpHandler {
    [SerializeField] private Image background;
    [SerializeField] private Sprite backgroundActive;
    [SerializeField] private Image knob;
    [SerializeField] private Sprite knobPressed;

    private Sprite _backgroundNormal;
    private Sprite _knobNormal;
    private bool _pressed;

    public void Init(Image backgroundImage, Sprite backgroundActiveSprite, Image knobImage, Sprite knobPressedSprite) {
        background = backgroundImage;
        backgroundActive = backgroundActiveSprite;
        knob = knobImage;
        knobPressed = knobPressedSprite;
    }

    private void Awake() {
        if (background) _backgroundNormal = background.sprite;
        if (knob) _knobNormal = knob.sprite;
    }

    // Jari bisa keluar dari area joystick saat menggeser, jadi hanya PointerUp yang mengembalikan tampilan.
    public void OnPointerDown(PointerEventData eventData) => SetPressed(true);

    public void OnPointerUp(PointerEventData eventData) => SetPressed(false);

    private void OnDisable() => SetPressed(false);

    private void SetPressed(bool pressed) {
        if (_pressed == pressed)
            return;
        _pressed = pressed;
        if (background && backgroundActive) background.sprite = pressed ? backgroundActive : _backgroundNormal;
        if (knob && knobPressed) knob.sprite = pressed ? knobPressed : _knobNormal;
    }
}
