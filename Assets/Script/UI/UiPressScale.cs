using UnityEngine;
using UnityEngine.EventSystems;

/// Umpan balik tekan: elemen sedikit mengecil saat ditekan lalu memantul kembali saat dilepas.
/// Memakai waktu tak terskala, jadi tetap jalan walau game di-pause.
[DisallowMultipleComponent]
public sealed class UiPressScale : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler {
    [Tooltip("Yang diskalakan. Kosong = objek ini sendiri.")]
    [SerializeField] private RectTransform target;
    [SerializeField] private float pressedScale = 0.92f;
    [SerializeField] private float speed = 18f;

    private bool _pressed;
    private float _scale = 1f;

    public void Configure(RectTransform scaled, float scaleWhenPressed) {
        target = scaled;
        pressedScale = scaleWhenPressed;
    }

    private RectTransform Target => target != null ? target : (RectTransform)transform;

    private void OnDisable() {
        _pressed = false;
        _scale = 1f;
        Target.localScale = Vector3.one;
    }

    public void OnPointerDown(PointerEventData eventData) => _pressed = true;
    public void OnPointerUp(PointerEventData eventData) => _pressed = false;
    public void OnPointerExit(PointerEventData eventData) => _pressed = false;

    private void Update() {
        float goal = _pressed ? pressedScale : 1f;
        if (Mathf.Approximately(_scale, goal)) return;
        _scale = Mathf.Lerp(_scale, goal, 1f - Mathf.Exp(-speed * Time.unscaledDeltaTime));
        if (Mathf.Abs(_scale - goal) < 0.002f) _scale = goal;
        Target.localScale = new Vector3(_scale, _scale, 1f);
    }
}
