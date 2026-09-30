using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(UnityEngine.UI.Image))]
public sealed class SlantedBarFillEffect : BaseMeshEffect
{
    readonly List<UIVertex> source = new List<UIVertex>(6);
    readonly List<UIVertex> output = new List<UIVertex>(12);
    readonly List<UIVertex> polygon = new List<UIVertex>(4);

    Vector2 axis;
    float start;
    float end;
    float fillAmount = 1f;

    public float FillAmount => fillAmount;

    public void Configure(Vector2 fillAxis, float startProjection, float endProjection)
    {
        axis = fillAxis.normalized;
        start = startProjection;
        end = endProjection;
        graphic.SetVerticesDirty();
    }

    public void SetFillAmount(float amount)
    {
        amount = Mathf.Clamp01(amount);
        if (Mathf.Approximately(fillAmount, amount))
            return;

        fillAmount = amount;
        graphic.SetVerticesDirty();
    }

    public override void ModifyMesh(VertexHelper mesh)
    {
        if (!IsActive() || fillAmount >= 1f || axis == Vector2.zero)
            return;

        if (fillAmount <= 0f)
        {
            mesh.Clear();
            return;
        }

        Rect rect = graphic.rectTransform.rect;
        if (rect.width <= 0f || rect.height <= 0f)
            return;

        float limit = Mathf.Lerp(start, end, fillAmount);
        source.Clear();
        output.Clear();
        mesh.GetUIVertexStream(source);

        for (int i = 0; i < source.Count; i += 3)
        {
            polygon.Clear();
            for (int edge = 0; edge < 3; edge++)
            {
                UIVertex from = source[i + edge];
                UIVertex to = source[i + (edge + 1) % 3];
                float fromDistance = Projection(from.position, rect) - limit;
                float toDistance = Projection(to.position, rect) - limit;
                bool fromInside = fromDistance <= 0f;
                bool toInside = toDistance <= 0f;

                if (fromInside != toInside)
                    polygon.Add(Interpolate(from, to,
                        fromDistance / (fromDistance - toDistance)));
                if (toInside)
                    polygon.Add(to);
            }

            for (int vertex = 1; vertex < polygon.Count - 1; vertex++)
            {
                output.Add(polygon[0]);
                output.Add(polygon[vertex]);
                output.Add(polygon[vertex + 1]);
            }
        }

        mesh.Clear();
        mesh.AddUIVertexTriangleStream(output);
    }

    float Projection(Vector3 position, Rect rect)
    {
        var normalized = new Vector2((position.x - rect.xMin) / rect.width,
            (position.y - rect.yMin) / rect.height);
        return Vector2.Dot(normalized, axis);
    }

    static UIVertex Interpolate(UIVertex from, UIVertex to, float amount)
    {
        UIVertex result = from;
        result.position = Vector3.Lerp(from.position, to.position, amount);
        result.color = Color.Lerp(from.color, to.color, amount);
        result.uv0 = Vector4.Lerp(from.uv0, to.uv0, amount);
        result.uv1 = Vector4.Lerp(from.uv1, to.uv1, amount);
        result.uv2 = Vector4.Lerp(from.uv2, to.uv2, amount);
        result.uv3 = Vector4.Lerp(from.uv3, to.uv3, amount);
        result.normal = Vector3.Lerp(from.normal, to.normal, amount);
        result.tangent = Vector4.Lerp(from.tangent, to.tangent, amount);
        return result;
    }
}
