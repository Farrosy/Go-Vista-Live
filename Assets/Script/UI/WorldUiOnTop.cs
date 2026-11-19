using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

/// UI world-space yang selalu tergambar di atas geometri (tidak bisa tertutup dinding/fasad).
/// Shader UI/Default dan TMP memakai "ZTest [unity_GUIZTestMode]"; salinan material dengan nilai Always
/// membuat grafik itu lolos depth test. Salinan di-cache per material sumber, jadi semua prompt berbagi
/// material yang sama (batching tetap jalan).
public static class WorldUiOnTop
{
    private static readonly int ZTestModeId = Shader.PropertyToID("unity_GUIZTestMode");
    private static readonly Dictionary<Material, Material> Cache = new Dictionary<Material, Material>();

    public static void Apply(GameObject root)
    {
        if (root == null) return;
        foreach (Graphic graphic in root.GetComponentsInChildren<Graphic>(true))
        {
            if (graphic is TMP_Text text)
                text.fontSharedMaterial = OnTop(text.fontSharedMaterial);
            else
                graphic.material = OnTop(graphic.material);
        }
    }

    private static Material OnTop(Material source)
    {
        if (source == null) return null;
        if (Cache.TryGetValue(source, out Material cached) && cached != null) return cached;

        var copy = new Material(source) { name = source.name + " (OnTop)" };
        copy.SetInt(ZTestModeId, (int)CompareFunction.Always);
        Cache[source] = copy;
        return copy;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCache() => Cache.Clear();
}
