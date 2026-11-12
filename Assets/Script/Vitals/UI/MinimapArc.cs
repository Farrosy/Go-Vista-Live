using UnityEngine;
using UnityEngine.UI;

/// Busur HP dan O₂ di tepi minimap memakai Image Filled Radial360 yang mulai dari bawah.
/// Busur di sprite tidak penuh setengah lingkaran (ujungnya ±4° dari bawah dan atas), jadi nilai 0..1
/// dipetakan ke rentang sudut itu supaya busur benar-benar kosong di 0 dan penuh di 1.
public static class MinimapArc {
    private const float StartFill = 4f / 360f;
    private const float EndFill = 176f / 360f;

    public static void Setup(Image image, bool clockwise) {
        image.type = Image.Type.Filled;
        image.fillMethod = Image.FillMethod.Radial360;
        image.fillOrigin = (int)Image.Origin360.Bottom;
        image.fillClockwise = clockwise;
    }

    public static void SetFraction(Image image, float fraction) {
        fraction = Mathf.Clamp01(fraction);
        image.enabled = fraction > 0f;
        image.fillAmount = Mathf.Lerp(StartFill, EndFill, fraction);
    }
}
