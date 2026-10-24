using UnityEngine;
using UnityEngine.EventSystems;

/// Pasang di SprintButton. Tidak perlu OnClick, cukup script ini.
public class SprintButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    [Tooltip("OFF = tahan tombol untuk sprint. ON = tap sekali nyala, tap lagi mati.")]
    [SerializeField] private bool toggleMode = false;

    private bool toggled;

    public void OnPointerDown(PointerEventData e)
    {
        if (toggleMode)
        {
            toggled = !toggled;
            PlayerInputHub.Instance.Sprint = toggled;
        }
        else PlayerInputHub.Instance.Sprint = true;
    }

    public void OnPointerUp(PointerEventData e)
    {
        if (!toggleMode) PlayerInputHub.Instance.Sprint = false;
    }
}