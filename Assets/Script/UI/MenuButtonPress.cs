using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Efek tombol "3D" di menu utama: saat ditekan, bagian depan tombol (Face) turun menutupi
/// bayangan bawahnya (Lip), seperti tombol fisik yang dipencet. Dilepas = kembali naik.
///
/// Struktur tombol (dibuat otomatis oleh LobyUISceneController.BuildMainMenu):
///   [Nama Button]   ← Button + MenuButtonPress
///     ├ ... Lip     ← bayangan gelap di bawah
///     └ ... Face    ← isi tombol (warna, ikon, teks). Diisi ke slot "Face".
/// </summary>
[RequireComponent(typeof(Button))]
public sealed class MenuButtonPress : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler {
    [Tooltip("Bagian depan tombol yang bergerak turun saat ditekan.")]
    [SerializeField] private RectTransform face;
    [Tooltip("Seberapa jauh Face turun (px, referensi 1920x1080). Samakan dengan tebal Lip.")]
    [SerializeField] private float pressDepth = 6f;

    private Button _button;
    private Vector2 _restPosition;
    private bool _pressed;

    public void Init(RectTransform faceRect, float depth) {
        face = faceRect;
        pressDepth = depth;
        if (face != null)
            _restPosition = face.anchoredPosition;
    }

    private void Awake() {
        _button = GetComponent<Button>();
        if (face != null)
            _restPosition = face.anchoredPosition;
    }

    private void OnDisable() => SetPressed(false);

    public void OnPointerDown(PointerEventData eventData) {
        if (_button == null || _button.IsInteractable())
            SetPressed(true);
    }

    public void OnPointerUp(PointerEventData eventData) => SetPressed(false);

    public void OnPointerExit(PointerEventData eventData) => SetPressed(false);

    private void SetPressed(bool pressed) {
        if (face == null || _pressed == pressed)
            return;
        _pressed = pressed;
        face.anchoredPosition = pressed ? _restPosition + Vector2.down * pressDepth : _restPosition;
    }
}
