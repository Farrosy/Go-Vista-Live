using UnityEngine;
using UnityEngine.EventSystems;

/// Pasang di RawImage preview karakter: geser kiri/kanan untuk memutar karakter.
public class CharacterPreviewDrag : MonoBehaviour, IDragHandler
{
    [SerializeField] private CharacterPreviewStage stage;
    [SerializeField] private float degreesPerPixel = 0.4f;

    public void OnDrag(PointerEventData eventData)
    {
        if (stage) stage.Rotate(-eventData.delta.x * degreesPerPixel);
    }
}
