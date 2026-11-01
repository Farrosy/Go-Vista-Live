using UnityEngine;

[RequireComponent(typeof(PlayerController))]
public sealed class DemoKeyboardInput : MonoBehaviour
{
    [SerializeField] private float mouseSensitivity = 3f;
    [SerializeField] private bool lockCursorOnStart = true;

    private PlayerController controller;

    private void Awake()
    {
        controller = GetComponent<PlayerController>();
    }

    private void Start()
    {
        if (lockCursorOnStart) LockCursor();
    }

    private void Update()
    {
        PlayerInputHub hub = PlayerInputHub.Instance;
        if (hub == null) return;

        if (Input.GetKeyDown(KeyCode.Escape)) UnlockCursor();
        if (Input.GetMouseButtonDown(0) && Cursor.lockState != CursorLockMode.Locked) LockCursor();

        if (Cursor.lockState == CursorLockMode.Locked)
        {
            Vector2 look = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")) * mouseSensitivity;
            hub.AddLook(look);
        }

        if (Input.GetKeyDown(KeyCode.Space) && controller != null)
            controller.TryJump();
    }

    private static void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private static void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}
