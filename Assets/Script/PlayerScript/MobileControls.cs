using UnityEngine;
using UnityEngine.UI;

/// Controls the visibility of the shared mobile-controls Canvas prefab.
public sealed class MobileControls : MonoBehaviour
{
    private static Canvas controlsCanvas;
    private static GraphicRaycaster controlsRaycaster;
    private static SprintButton sprintButton;

    public static void SetVisible(bool visible)
    {
        if (!visible)
        {
            // if (PlayerInputHub.Instance) PlayerInputHub.Instance.ResetInput();
            if (sprintButton) sprintButton.ResetSprint();
        }
        if (controlsCanvas) controlsCanvas.enabled = visible;
        if (controlsRaycaster) controlsRaycaster.enabled = visible;
    }

    private void Awake()
    {
        controlsCanvas = GetComponent<Canvas>();
        controlsRaycaster = GetComponent<GraphicRaycaster>();
        sprintButton = GetComponentInChildren<SprintButton>(true);
        SetVisible(false);
    }
}
