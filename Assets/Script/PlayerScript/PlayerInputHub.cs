using UnityEngine;

/// Pusat input mobile. Semua UI nulis ke sini, Player & Camera baca dari sini.
public class PlayerInputHub : MonoBehaviour
{
    public static PlayerInputHub Instance { get; private set; }

    [Header("Drag Fixed Joystick dari Hierarchy ke sini")]
    [SerializeField] private Joystick joystick;

    public Vector2 Move => joystick ? new Vector2(joystick.Horizontal, joystick.Vertical) : Vector2.zero;
    public bool Sprint { get; set; }

    private Vector2 lookDelta;

    void Awake() => Instance = this;

    public void AddLook(Vector2 delta) => lookDelta += delta;

    /// Dipanggil kamera tiap frame; ngambil lalu reset.
    public Vector2 ConsumeLook()
    {
        var d = lookDelta;
        lookDelta = Vector2.zero;
        return d;
    }
}