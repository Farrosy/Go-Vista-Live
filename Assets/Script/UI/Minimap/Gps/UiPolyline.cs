using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// Garis patah-patah sederhana untuk uGUI (rute GPS di peta layar penuh).
/// Titik dalam koordinat lokal RectTransform ini; tebal dalam unit canvas. Sambungan ditutup dengan kotak kecil
/// supaya tikungan tidak berlubang.
[RequireComponent(typeof(CanvasRenderer))]
public sealed class UiPolyline : MaskableGraphic {
    [SerializeField] private float thickness = 8f;

    private readonly List<Vector2> _points = new List<Vector2>();

    public float Thickness {
        get => thickness;
        set { thickness = value; SetVerticesDirty(); }
    }

    public void SetPoints(List<Vector2> points) {
        _points.Clear();
        _points.AddRange(points);
        SetVerticesDirty();
    }

    public void Clear() {
        if (_points.Count == 0) return;
        _points.Clear();
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh) {
        vh.Clear();
        if (_points.Count < 2) return;
        float half = thickness * 0.5f;
        for (int i = 1; i < _points.Count; i++) {
            Vector2 a = _points[i - 1], b = _points[i];
            Vector2 along = b - a;
            if (along.sqrMagnitude < 0.0001f) continue;
            Vector2 normal = new Vector2(-along.y, along.x).normalized * half;
            AddQuad(vh, a - normal, a + normal, b + normal, b - normal);
        }
        for (int i = 1; i < _points.Count - 1; i++) {
            Vector2 p = _points[i];
            AddQuad(vh, p + new Vector2(-half, -half), p + new Vector2(-half, half),
                p + new Vector2(half, half), p + new Vector2(half, -half));
        }
    }

    private void AddQuad(VertexHelper vh, Vector2 v0, Vector2 v1, Vector2 v2, Vector2 v3) {
        int start = vh.currentVertCount;
        vh.AddVert(v0, color, Vector2.zero);
        vh.AddVert(v1, color, Vector2.zero);
        vh.AddVert(v2, color, Vector2.zero);
        vh.AddVert(v3, color, Vector2.zero);
        vh.AddTriangle(start, start + 1, start + 2);
        vh.AddTriangle(start + 2, start + 3, start);
    }
}
