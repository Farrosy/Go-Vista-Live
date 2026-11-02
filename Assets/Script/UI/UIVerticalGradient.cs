using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Gradasi warna atas ke bawah untuk Image/Text uGUI (tanpa sprite gradasi khusus).
/// Pasang di objek yang punya Image, set warna Image ke putih, lalu atur Top/Bottom di Inspector.
/// Bekerja dengan Image Sliced (sudut membulat tetap rapi) karena yang diwarnai hanya vertex-nya.
/// Dipakai tombol menu utama (LobyUISceneController.BuildMainMenu).
/// </summary>
[AddComponentMenu("UI/Effects/Vertical Gradient")]
public sealed class UIVerticalGradient : BaseMeshEffect {
    [SerializeField] private Color top = Color.white;
    [SerializeField] private Color bottom = Color.white;

    public void SetColors(Color topColor, Color bottomColor) {
        top = topColor;
        bottom = bottomColor;
        if (graphic != null)
            graphic.SetVerticesDirty();
    }

    public override void ModifyMesh(VertexHelper vh) {
        if (!IsActive() || vh.currentVertCount == 0)
            return;

        Rect rect = graphic.rectTransform.rect;
        UIVertex vertex = default;
        for (int i = 0; i < vh.currentVertCount; i++) {
            vh.PopulateUIVertex(ref vertex, i);
            float t = Mathf.InverseLerp(rect.yMin, rect.yMax, vertex.position.y);
            vertex.color = (Color)vertex.color * Color.Lerp(bottom, top, t);
            vh.SetUIVertex(vertex, i);
        }
    }
}
