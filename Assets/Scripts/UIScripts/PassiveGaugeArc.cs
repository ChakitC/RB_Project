using UnityEngine;

/// <summary>Editable vector ring. Geometry changes only when its displayed value changes.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class PassiveGaugeArc : UnityEngine.UI.MaskableGraphic
{
    [SerializeField, Range(0, 1)] float fill = 1;
    [SerializeField, Min(1)] float thickness = 5;
    [SerializeField, Min(1)] int segments = 1;
    [SerializeField, Range(0, 15)] float gapDegrees = 4;
    public float Fill => fill;
    public void Configure(float width, int count, float gap)
    { thickness = width; segments = Mathf.Max(1, count); gapDegrees = gap; raycastTarget = false; SetVerticesDirty(); }
    public void SetFill(float value)
    { value = Mathf.Clamp01(value); if (fill == value) return; fill = value; SetVerticesDirty(); }
    protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper vh)
    {
        vh.Clear();
        Rect rect = GetPixelAdjustedRect();
        float outer = Mathf.Min(rect.width, rect.height) * 0.5f;
        float inner = Mathf.Max(0, outer - thickness);
        for (int segment = 0; segment < segments; segment++)
        {
            float start = segment * 360f / segments + gapDegrees * 0.5f;
            float end = Mathf.Min((segment + 1) * 360f / segments - gapDegrees * 0.5f, fill * 360f);
            if (end <= start) continue;
            int count = Mathf.Max(1, Mathf.CeilToInt((end - start) / 3f));
            for (int i = 0; i < count; i++)
            {
                float a = Mathf.Lerp(start, end, i / (float)count) * Mathf.Deg2Rad;
                float b = Mathf.Lerp(start, end, (i + 1) / (float)count) * Mathf.Deg2Rad;
                Vector2 u = new(Mathf.Sin(a), Mathf.Cos(a)), v = new(Mathf.Sin(b), Mathf.Cos(b));
                int index = vh.currentVertCount;
                vh.AddVert(rect.center + u * outer, color, Vector2.zero);
                vh.AddVert(rect.center + v * outer, color, Vector2.zero);
                vh.AddVert(rect.center + v * inner, color, Vector2.zero);
                vh.AddVert(rect.center + u * inner, color, Vector2.zero);
                vh.AddTriangle(index, index + 1, index + 2); vh.AddTriangle(index, index + 2, index + 3);
            }
        }
    }
}
