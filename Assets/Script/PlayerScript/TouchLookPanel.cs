using UnityEngine;
using UnityEngine.EventSystems;

/// Pasang di TouchPanel (harus punya Image, alpha 0, Raycast Target ON).
public class TouchLookPanel : MonoBehaviour, IDragHandler
{
    public void OnDrag(PointerEventData e)
    {
        PlayerInputHub.Instance.AddLook(e.delta);
    }
}