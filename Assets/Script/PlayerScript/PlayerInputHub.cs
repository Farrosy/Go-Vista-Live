using UnityEngine;

/// Pusat input mobile. Semua UI nulis ke sini, Player & Camera baca dari sini.
public class PlayerInputHub : MonoBehaviour
{
    public static PlayerInputHub Instance { get; private set; }

    [Header("Drag Fixed Joystick dari Hierarchy ke sini")]
    [SerializeField] private Joystick joystick;

    [Header("Keyboard fallback (demo/editor)")]
    [SerializeField] private bool keyboardFallback;

    private bool sprint;
    private Vector2 lookDelta;
    private bool inputLocked;
    private bool pauseLocked;
    private bool ignoreJoystickUntilNeutral;

    /// True saat dialog/cutscene atau menu pause: gerak, sprint, dan look dimatikan.
    public bool InputLocked => inputLocked || pauseLocked;

    public Vector2 Move
    {
        get
        {
            if (InputLocked) return Vector2.zero;

            if (joystick)
            {
                if (ignoreJoystickUntilNeutral)
                {
                    if (joystick.Direction.sqrMagnitude > 0.0001f) return Vector2.zero;
                    ignoreJoystickUntilNeutral = false;
                }
                return new Vector2(joystick.Horizontal, joystick.Vertical);
            }

            if (keyboardFallback)
                return new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));

            return Vector2.zero;
        }
    }

    public bool Sprint
    {
        get => !InputLocked && (sprint || (keyboardFallback && Input.GetKey(KeyCode.LeftShift)));
        set => sprint = value;
    }

    void Awake() => Instance = this;

    public void SetLocked(bool locked)
    {
        inputLocked = locked;
        if (locked) ignoreJoystickUntilNeutral = true;
        ClearPendingInput();
    }

    /// <summary>Lock terpisah supaya menutup dialog tidak membuka input ketika pause menu masih aktif.</summary>
    public void SetPauseLocked(bool locked)
    {
        pauseLocked = locked;
        if (locked) ignoreJoystickUntilNeutral = true;
        ClearPendingInput();
    }

    public void AddLook(Vector2 delta)
    {
        if (!InputLocked) lookDelta += delta;
    }

    /// Dipanggil kamera tiap frame; ngambil lalu reset.
    public Vector2 ConsumeLook()
    {
        var d = lookDelta;
        lookDelta = Vector2.zero;
        return d;
    }

    private void ClearPendingInput()
    {
        sprint = false;
        lookDelta = Vector2.zero;
        // The joystick package exposes its reset through IPointerUpHandler.
        // Clear it explicitly so disabling the controls canvas cannot leave movement held on resume.
        if (joystick) joystick.OnPointerUp(null);
    }
}
