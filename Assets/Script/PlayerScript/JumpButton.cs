using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// Lompat saat tombol mulai ditekan. Button.onClick baru jalan saat jari dilepas, jadi terasa terlambat.
[RequireComponent(typeof(Button))]
public class JumpButton : MonoBehaviour, IPointerDownHandler
{
    [Tooltip("Kosong = pakai avatar lokal (PlayerController.Local). Biarkan kosong untuk multiplayer.")]
    [SerializeField] private PlayerController player;

    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!button.IsInteractable())
            return;

        PlayerController target = player ? player : PlayerController.Local;
        if (target)
            target.TryJump();
    }
}
