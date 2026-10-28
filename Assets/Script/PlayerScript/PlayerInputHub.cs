using UnityEngine;

/// Pusat input mobile. Semua UI nulis ke sini, Player & Camera baca dari sini.
public class PlayerInputHub : MonoBehaviour
{
    public static PlayerInputHub Instance { get; private set; }

    [Header("Drag Fixed Joystick dari Hierarchy ke sini")]
    [SerializeField] private Joystick joystick;

    private bool sprint;
    private Vector2 lookDelta;

    /// True saat dialog/cutscene: gerak, sprint, dan look dimatikan.
    public bool InputLocked { get; private set; }

    public Vector2 Move => (joystick && !InputLocked)
        ? new Vector2(joystick.Horizontal, joystick.Vertical)
        : Vector2.zero;

    public bool Sprint
    {
        get => sprint && !InputLocked;
        set => sprint = value;
    }

    void Awake() => Instance = this;

    public void SetLocked(bool locked)
    {
        InputLocked = locked;
        lookDelta = Vector2.zero;
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
}