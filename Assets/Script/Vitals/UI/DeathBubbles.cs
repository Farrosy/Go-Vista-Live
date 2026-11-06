using UnityEngine;
using UnityEngine.UI;

/// Gelembung yang naik pelan di layar mati. Jumlahnya tetap dan dipakai ulang, jadi ringan di HP.
public sealed class DeathBubbles : MonoBehaviour {
    private const float RiseSpeedMin = 40f;
    private const float RiseSpeedMax = 90f;
    private const float ScaleMin = 0.4f;
    private const float ScaleMax = 1.2f;
    private const float SwayPixels = 14f;

    [SerializeField] private RectTransform[] bubbles;

    private RectTransform _area;
    private float[] _speeds;
    private float[] _baseX;

    private void Awake() {
        _area = (RectTransform)transform;
        _speeds = new float[bubbles.Length];
        _baseX = new float[bubbles.Length];
    }

    private void OnEnable() {
        for (int i = 0; i < bubbles.Length; i++) Respawn(i, Random.value);
    }

    private void Update() {
        float halfHeight = _area.rect.height * 0.5f;
        for (int i = 0; i < bubbles.Length; i++) {
            RectTransform bubble = bubbles[i];
            float y = bubble.anchoredPosition.y + _speeds[i] * Time.deltaTime;
            if (y > halfHeight + bubble.rect.height) {
                Respawn(i, 0f);
                continue;
            }
            float x = _baseX[i] + Mathf.Sin(Time.time + i) * SwayPixels;
            bubble.anchoredPosition = new Vector2(x, y);
        }
    }

    private void Respawn(int index, float heightFraction) {
        Rect rect = _area.rect;
        float x = Random.Range(rect.xMin, rect.xMax);
        float y = Mathf.Lerp(rect.yMin, rect.yMax, heightFraction);
        float scale = Random.Range(ScaleMin, ScaleMax);
        _baseX[index] = x;
        _speeds[index] = Random.Range(RiseSpeedMin, RiseSpeedMax);
        bubbles[index].anchoredPosition = new Vector2(x, y);
        bubbles[index].localScale = new Vector3(scale, scale, 1f);
    }
}
