using UnityEngine;
using UnityEngine.EventSystems;

/// Pasang di RawImage preview karakter: geser kiri/kanan untuk memutar karakter.
public class CharacterPreviewDrag : MonoBehaviour, IDragHandler
{
    [SerializeField] private CharacterPreviewStage stage;
    [SerializeField] private float degreesPerPixel = 0.4f;

    /// Dipakai menu PROFIL yang memakai panggung preview yang sama dengan menu karakter.
    public void SetStage(CharacterPreviewStage previewStage)
    {
        stage = previewStage;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (stage) stage.Rotate(-eventData.delta.x * degreesPerPixel);
    }
}
