using UnityEngine;

public readonly struct PartyComboPlacementVisibility
{
    static readonly RaycastHit[] Hits = new RaycastHit[64];
    public readonly int VisibleSamples;
    public readonly int InFrameSamples;
    public readonly float EdgeMargin;

    PartyComboPlacementVisibility(int visible, int inFrame, float edgeMargin)
    {
        VisibleSamples = visible;
        InFrameSamples = inFrame;
        EdgeMargin = edgeMargin;
    }

    public int CompareTo(PartyComboPlacementVisibility other)
    {
        int comparison = VisibleSamples.CompareTo(other.VisibleSamples);
        if (comparison != 0) return comparison;
        comparison = InFrameSamples.CompareTo(other.InFrameSamples);
        return comparison != 0 ? comparison : EdgeMargin.CompareTo(other.EdgeMargin);
    }

    // Evaluate the resolved (NavMesh-snapped) body pose against the current gameplay
    // camera before Combo focus starts. The target is deliberately NOT ignored.
    public static PartyComboPlacementVisibility Evaluate(Camera camera, Vector3 position,
        Quaternion rotation, CharacterPlacementFootprint footprint, Transform actorRoot,
        Collider actorCollider, LayerMask obstructionLayers)
    {
        if (camera == null)
            return default;
        int visible = 0;
        int inFrame = 0;
        float margin = 0f;
        Vector3 center = position + rotation * footprint.CenterOffset;
        for (int i = 0; i < 5; i++)
        {
            float height = i == 0 ? 0.4f : i == 2 ? -0.15f : 0.15f;
            float side = i == 3 ? -0.7f : i == 4 ? 0.7f : 0f;
            Vector3 point = center + rotation * (Vector3.up * (footprint.Height * height) +
                Vector3.right * (footprint.Radius * side));
            Vector3 viewport = camera.WorldToViewportPoint(point);
            if (viewport.z < camera.nearClipPlane || viewport.z > camera.farClipPlane ||
                viewport.x < 0f || viewport.x > 1f || viewport.y < 0f || viewport.y > 1f)
                continue;
            inFrame++;
            margin += Mathf.Min(Mathf.Min(viewport.x, 1f - viewport.x),
                Mathf.Min(viewport.y, 1f - viewport.y));

            Ray ray = camera.ViewportPointToRay(viewport);
            float distance = Vector3.Dot(point - ray.origin, ray.direction);
            int count = Physics.RaycastNonAlloc(ray, Hits, Mathf.Max(0f, distance - 0.01f),
                obstructionLayers, QueryTriggerInteraction.Ignore);
            bool blocked = count == Hits.Length; // An incomplete query must not imply visibility.
            for (int hitIndex = 0; hitIndex < count && !blocked; hitIndex++)
            {
                Collider hit = Hits[hitIndex].collider;
                if (hit == null || hit == actorCollider ||
                    (actorRoot != null && hit.transform.IsChildOf(actorRoot)))
                    continue;
                blocked = true;
            }
            if (!blocked)
                visible++;
        }
        return new PartyComboPlacementVisibility(visible, inFrame, margin);
    }
}
