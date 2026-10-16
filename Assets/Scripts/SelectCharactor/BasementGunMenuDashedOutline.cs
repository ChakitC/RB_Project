using UnityEngine;

// A single UI mesh: it follows the rotated card and the existing viewport mask.
[RequireComponent(typeof(CanvasRenderer), typeof(RectTransform))]
public sealed class BasementGunMenuDashedOutline : UnityEngine.UI.MaskableGraphic
{
    protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper mesh)
    {
        mesh.Clear();
        var r = GetPixelAdjustedRect();
        const float thickness = 4f;
        const float dash = 17f;
        const float gap = 10f;
        for (float x = r.xMin + 3; x < r.xMax - 3; x += dash + gap)
        {
            float end = Mathf.Min(x + dash, r.xMax - 3);
            Quad(mesh, x, r.yMin, end, r.yMin + thickness);
            Quad(mesh, x, r.yMax - thickness, end, r.yMax);
        }
        for (float y = r.yMin + 9; y < r.yMax - 9; y += dash + gap)
        {
            float end = Mathf.Min(y + dash, r.yMax - 9);
            Quad(mesh, r.xMin, y, r.xMin + thickness, end);
            Quad(mesh, r.xMax - thickness, y, r.xMax, end);
        }
    }

    void Quad(UnityEngine.UI.VertexHelper mesh, float left, float bottom, float right, float top)
    {
        int i = mesh.currentVertCount;
        mesh.AddVert(new Vector3(left, bottom), color, Vector2.zero);
        mesh.AddVert(new Vector3(left, top), color, Vector2.zero);
        mesh.AddVert(new Vector3(right, top), color, Vector2.zero);
        mesh.AddVert(new Vector3(right, bottom), color, Vector2.zero);
        mesh.AddTriangle(i, i + 1, i + 2);
        mesh.AddTriangle(i + 2, i + 3, i);
    }
}
