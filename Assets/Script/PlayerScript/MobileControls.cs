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
    private static bool driving;
    private static RectTransform controlsRoot;

    /// Parent joystick & tombol (GameplayHUD). HUD menyetir dipasang di sini supaya ikut disembunyikan bersama
    /// kontrol lain (menu, dialog, layar mati).
    public static RectTransform ControlsRoot => controlsRoot;

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
        ApplyGroups();
        if (InteractButtonUI.Instance) InteractButtonUI.Instance.SetDisabled(value);
    }

    /// Saat di dalam mobil: joystick, lompat, dan lari disembunyikan (diganti HUD menyetir).
    /// Tombol Interact tetap ada untuk Keluar, geser layar untuk kamera juga tetap jalan.
    public static void SetDriving(bool value)
    {
        driving = value;
        if (value && sprintButton) sprintButton.ResetSprint();
        ApplyGroups();
    }

    private static void ApplyGroups()
    {
        foreach (CanvasGroup group in dimGroups)
        {
            if (!group) continue;
            group.alpha = driving ? 0f : dimmed ? DimmedAlpha : 1f;
            group.interactable = !driving && !dimmed;
            group.blocksRaycasts = !driving && !dimmed;
        }
    }

    private void Awake()
    {
        controlsCanvas = GetComponent<Canvas>();
        controlsRaycaster = GetComponent<GraphicRaycaster>();
        sprintButton = GetComponentInChildren<SprintButton>(true);
        JumpButton jumpButton = GetComponentInChildren<JumpButton>(true);
        Joystick joystick = GetComponentInChildren<Joystick>(true);
        controlsRoot = joystick ? joystick.transform.parent as RectTransform : transform as RectTransform;
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
