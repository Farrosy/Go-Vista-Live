using UnityEngine;
using UnityEngine.UI;

/// Controls the visibility of the shared mobile-controls Canvas prefab.
public sealed class MobileControls : MonoBehaviour
{
    // Seberapa pudar joystick/tombol saat di-grey out (tertabrak mobil).
    private const float DimmedAlpha = 0.4f;

    private static Canvas controlsCanvas;
    private static GraphicRaycaster controlsRaycaster;
    private static SprintButton sprintButton;
    private static CanvasGroup[] dimGroups = new CanvasGroup[0];
    private static bool dimmed;

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

    /// Grey out joystick, lompat, lari, dan interaksi: masih terlihat, tapi tidak bisa ditekan.
    /// Kamera (geser layar), chat, dan menu tidak ikut.
    public static void SetDimmed(bool value)
    {
        dimmed = value;
        if (value && sprintButton) sprintButton.ResetSprint();
        foreach (CanvasGroup group in dimGroups)
        {
            if (!group) continue;
            group.alpha = value ? DimmedAlpha : 1f;
            group.interactable = !value;
            group.blocksRaycasts = !value;
        }
        if (InteractButtonUI.Instance) InteractButtonUI.Instance.SetDisabled(value);
    }

    private void Awake()
    {
        controlsCanvas = GetComponent<Canvas>();
        controlsRaycaster = GetComponent<GraphicRaycaster>();
        sprintButton = GetComponentInChildren<SprintButton>(true);
        JumpButton jumpButton = GetComponentInChildren<JumpButton>(true);
        Joystick joystick = GetComponentInChildren<Joystick>(true);
        dimGroups = new[]
        {
            DimGroupFor(joystick ? joystick.gameObject : null),
            DimGroupFor(jumpButton ? jumpButton.gameObject : null),
            DimGroupFor(sprintButton ? sprintButton.gameObject : null),
        };
        SetVisible(false);
        SetDimmed(dimmed);
    }

    private static CanvasGroup DimGroupFor(GameObject target)
    {
        if (!target) return null;
        CanvasGroup group = target.GetComponent<CanvasGroup>();
        return group ? group : target.AddComponent<CanvasGroup>();
    }
}
