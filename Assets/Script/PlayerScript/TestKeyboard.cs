using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Baca keyboard untuk kontrol testing di PC. Pakai Input System kalau ada, karena Input Manager lama
/// (Input.GetKey) tidak menerima keyboard di Device Simulator. Cadangannya tetap Input.GetKey.
/// </summary>
public static class TestKeyboard
{
    public enum Btn { W, A, S, D, Up, Down, Left, Right, LeftShift, RightShift, Space, J, K }

    public static bool Held(Btn key)
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard kb = Keyboard.current;
        if (kb != null && kb[ToKey(key)].isPressed) return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKey(ToKeyCode(key))) return true;
#endif
        return false;
    }

    public static bool Pressed(Btn key)
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard kb = Keyboard.current;
        if (kb != null && kb[ToKey(key)].wasPressedThisFrame) return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(ToKeyCode(key))) return true;
#endif
        return false;
    }

#if ENABLE_INPUT_SYSTEM
    private static Key ToKey(Btn key)
    {
        switch (key)
        {
            case Btn.W: return Key.W;
            case Btn.A: return Key.A;
            case Btn.S: return Key.S;
            case Btn.D: return Key.D;
            case Btn.Up: return Key.UpArrow;
            case Btn.Down: return Key.DownArrow;
            case Btn.Left: return Key.LeftArrow;
            case Btn.Right: return Key.RightArrow;
            case Btn.LeftShift: return Key.LeftShift;
            case Btn.RightShift: return Key.RightShift;
            case Btn.Space: return Key.Space;
            case Btn.J: return Key.J;
            default: return Key.K;
        }
    }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
    private static KeyCode ToKeyCode(Btn key)
    {
        switch (key)
        {
            case Btn.W: return KeyCode.W;
            case Btn.A: return KeyCode.A;
            case Btn.S: return KeyCode.S;
            case Btn.D: return KeyCode.D;
            case Btn.Up: return KeyCode.UpArrow;
            case Btn.Down: return KeyCode.DownArrow;
            case Btn.Left: return KeyCode.LeftArrow;
            case Btn.Right: return KeyCode.RightArrow;
            case Btn.LeftShift: return KeyCode.LeftShift;
            case Btn.RightShift: return KeyCode.RightShift;
            case Btn.Space: return KeyCode.Space;
            case Btn.J: return KeyCode.J;
            default: return KeyCode.K;
        }
    }
#endif
}
