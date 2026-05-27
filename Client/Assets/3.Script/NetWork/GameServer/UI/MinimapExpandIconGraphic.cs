using UnityEngine;
using UnityEngine.UI;

public sealed class MinimapExpandIconGraphic : MaskableGraphic
{
    [SerializeField] private bool _expanded;
    [SerializeField, Min(1f)] private float _lineWidth = 2.4f;

    public void SetExpanded(bool expanded)
    {
        if (_expanded == expanded)
            return;

        _expanded = expanded;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect rect = rectTransform.rect;
        if (rect.width <= 0f || rect.height <= 0f)
            return;

        float pad = Mathf.Min(rect.width, rect.height) * 0.18f;
        Vector2 min = new Vector2(rect.xMin + pad, rect.yMin + pad);
        Vector2 max = new Vector2(rect.xMax - pad, rect.yMax - pad);
        Vector2 center = rect.center;

        if (_expanded)
        {
            AddArrow(vh, new Vector2(rect.xMin + pad, rect.yMax - pad), center + new Vector2(-2f, 2f), _lineWidth, color);
            AddArrow(vh, new Vector2(rect.xMax - pad, rect.yMin + pad), center + new Vector2(2f, -2f), _lineWidth, color);
        }
        else
        {
            AddArrow(vh, center + new Vector2(-2f, 2f), max, _lineWidth, color);
            AddArrow(vh, center + new Vector2(2f, -2f), min, _lineWidth, color);
        }
    }

    private static void AddArrow(VertexHelper vh, Vector2 from, Vector2 to, float width, Color color)
    {
        AddLine(vh, from, to, width, color);

        Vector2 dir = (to - from).normalized;
        if (dir.sqrMagnitude <= 0.001f)
            return;

        Vector2 normal = new Vector2(-dir.y, dir.x);
        float head = width * 3.2f;
        AddLine(vh, to, to - dir * head + normal * head * 0.65f, width, color);
        AddLine(vh, to, to - dir * head - normal * head * 0.65f, width, color);
    }

    private static void AddLine(VertexHelper vh, Vector2 from, Vector2 to, float width, Color color)
    {
        Vector2 delta = to - from;
        if (delta.sqrMagnitude <= 0.001f)
            return;

        Vector2 normal = new Vector2(-delta.y, delta.x).normalized * (width * 0.5f);
        int start = vh.currentVertCount;
        UIVertex vertex = UIVertex.simpleVert;
        vertex.color = color;

        vertex.position = from - normal;
        vh.AddVert(vertex);
        vertex.position = from + normal;
        vh.AddVert(vertex);
        vertex.position = to + normal;
        vh.AddVert(vertex);
        vertex.position = to - normal;
        vh.AddVert(vertex);

        vh.AddTriangle(start, start + 1, start + 2);
        vh.AddTriangle(start, start + 2, start + 3);
    }
}
