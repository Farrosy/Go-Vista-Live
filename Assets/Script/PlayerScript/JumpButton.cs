using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class JumpButton : MonoBehaviour
{
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
        if (!player)
            player = FindAnyObjectByType<PlayerController>();

        if (player)
            player.TryJump();
    }
}
