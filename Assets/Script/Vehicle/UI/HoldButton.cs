using System;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Tombol tahan (pedal gas/rem, belok kiri/kanan). Mengecil sedikit selama ditekan.</summary>
public class HoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler {
    private const float PressedScale = 0.92f;

    public event Action<bool> Changed;
    public bool IsHeld { get; private set; }

    public void OnPointerDown(PointerEventData eventData) => Set(true);
    public void OnPointerUp(PointerEventData eventData) => Set(false);
    public void OnPointerExit(PointerEventData eventData) => Set(false);

    void OnDisable() => Set(false);

    private void Set(bool held) {
        if (IsHeld == held) return;
        IsHeld = held;
        transform.localScale = Vector3.one * (held ? PressedScale : 1f);
        Changed?.Invoke(held);
    }
}
