using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class JumpButton : MonoBehaviour
{
    [Tooltip("Kosong = pakai avatar lokal (PlayerController.Local). Biarkan kosong untuk multiplayer.")]
    [SerializeField] private PlayerController player;

    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(OnJumpClicked);
    }

    private void OnDestroy()
    {
        if (button)
            button.onClick.RemoveListener(OnJumpClicked);
    }

    private void OnJumpClicked()
    {
        PlayerController target = player ? player : PlayerController.Local;
        if (target)
            target.TryJump();
    }
}