using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// Implementasi ISceneTransition default: fade ke hitam saat Cover(), fade balik saat Reveal().
/// Membuat overlay full-screen hitam sendiri (di atas SEMUA canvas gameplay/venue), jadi tidak
/// perlu merakit UI di scene. Overlay mulai transparan & tidak memblok input.
///
/// Nanti bisa diganti LoadingScreenTransition tanpa mengubah VenueTravelManager.TravelTo.
public class FadeToBlackTransition : MonoBehaviour, ISceneTransition
{
    [Tooltip("Lama fade masuk/keluar (detik).")]
    [SerializeField] private float fadeDuration = 0.35f;

    [Tooltip("Sorting order canvas overlay. Harus di atas semua canvas lain.")]
    [SerializeField] private int sortingOrder = 30000;

    private CanvasGroup group;
    private bool built;

    void Awake() => Build();

    private void Build()
    {
        if (built && group != null) return;
        built = true;

        var canvasGo = new GameObject("VenueFadeOverlay");
        canvasGo.transform.SetParent(transform, false);

        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;
        canvasGo.AddComponent<GraphicRaycaster>();

        group = canvasGo.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;

        var imageGo = new GameObject("Black");
        imageGo.transform.SetParent(canvasGo.transform, false);
        var image = imageGo.AddComponent<Image>();
        image.color = Color.black;
        image.raycastTarget = true;
        var rt = image.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    public IEnumerator Cover()
    {
        Build();
        group.blocksRaycasts = true;
        yield return Fade(1f);
    }

    public IEnumerator Reveal()
    {
        Build();
        yield return Fade(0f);
        group.blocksRaycasts = false;
    }

    public void ResetImmediate()
    {
        Build();
        group.alpha = 0f;
        group.blocksRaycasts = false;
    }

    private IEnumerator Fade(float targetAlpha)
    {
        if (fadeDuration <= 0f)
        {
            group.alpha = targetAlpha;
            yield break;
        }

        float start = group.alpha;
        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            group.alpha = Mathf.Lerp(start, targetAlpha, t / fadeDuration);
            yield return null;
        }
        group.alpha = targetAlpha;
    }
}
