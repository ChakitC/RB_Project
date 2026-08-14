using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public sealed class SummonPlacementSettings
{
    public bool ResolveGround = true;
    public bool RequireNavMesh;
    public LayerMask GroundMask = ~0;
    public LayerMask ClearanceMask = ~0;
    public QueryTriggerInteraction ClearanceTriggerInteraction = QueryTriggerInteraction.Ignore;
    public float GroundRaycastHeight = 2f;
    public float GroundRaycastDistance = 8f;
    public float NavMeshSampleRadius = 2f;
    public float Padding = 0.05f;
}

public readonly struct SummonPlacementCandidate
{
    public SummonPlacementCandidate(
        Vector3 position,
        Quaternion rotation,
        CharacterPlacementFootprint footprint)
    {
        Position = position;
        Rotation = rotation;
        Footprint = footprint;
    }

    public Vector3 Position { get; }
    public Quaternion Rotation { get; }
    public CharacterPlacementFootprint Footprint { get; }
}

public static class SummonPlacementResolver
{
    static readonly Collider[] ClearanceBuffer = new Collider[64];

    public static bool TryResolve(
        SummonSpawnContext request,
        Vector3 localOffset,
        Quaternion rotation,
        SummonPlacementSettings settings,
        IList<SummonPlacementCandidate> reserved,
        out SummonPlacementCandidate candidate,
        out string error)
    {
        Quaternion layoutRotation = request?.Caster != null
            ? request.Caster.transform.rotation
            : Quaternion.identity;
        return TryResolve(
            request,
            localOffset,
            layoutRotation,
            rotation,
            settings,
            reserved,
            out candidate,
            out error);
    }

    public static bool TryResolve(
        SummonSpawnContext request,
        Vector3 localOffset,
        Quaternion layoutRotation,
        Quaternion rotation,
        SummonPlacementSettings settings,
        IList<SummonPlacementCandidate> reserved,
        out SummonPlacementCandidate candidate,
        out string error)
    {
        candidate = default;
        error = string.Empty;
        if (request == null || request.Caster == null || request.Prefab == null)
        {
            error = "Summon placement request is incomplete.";
            return false;
        }

        settings ??= new SummonPlacementSettings();
        if (!CharacterPlacementProbeUtility.TryGetFootprint(
                request.Prefab,
                request.Mobility,
                out CharacterPlacementFootprint footprint,
                out error))
        {
            return false;
        }

        Vector3 position = request.Position + layoutRotation * localOffset;
        Collider groundCollider = null;
        if (settings.ResolveGround)
        {
            Vector3 rayOrigin = position + Vector3.up * Mathf.Max(0.1f, settings.GroundRaycastHeight);
            int groundMask = settings.GroundMask.value == 0 ? Physics.DefaultRaycastLayers : settings.GroundMask.value;
            if (!Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit groundHit,
                    Mathf.Max(0.1f, settings.GroundRaycastDistance), groundMask,
                    QueryTriggerInteraction.Ignore))
            {
                error = "No ground was found for summon placement.";
                return false;
            }

            position.y = groundHit.point.y;
            groundCollider = groundHit.collider;
        }

        if (settings.RequireNavMesh)
        {
            int areaMask = NavMesh.AllAreas;
            if (!NavMesh.SamplePosition(position, out NavMeshHit navHit,
                    Mathf.Max(settings.NavMeshSampleRadius, ResolvePlanarRadius(footprint, rotation)), areaMask))
            {
                error = "No NavMesh position was found for summon placement.";
                return false;
            }

            position = navHit.position;
        }

        candidate = new SummonPlacementCandidate(position, rotation, footprint);
        if (OverlapsReserved(candidate, reserved, settings.Padding))
        {
            error = "Summon placement overlaps another summon candidate.";
            return false;
        }

        if (OverlapsWorld(request.Caster, candidate, settings, groundCollider))
        {
            error = "Summon placement clearance is blocked.";
            return false;
        }

        return true;
    }

    static bool OverlapsReserved(
        SummonPlacementCandidate candidate,
        IList<SummonPlacementCandidate> reserved,
        float padding)
    {
        if (reserved == null)
            return false;

        Vector3 candidateCenter = ResolveCenter(candidate);
        for (int i = 0; i < reserved.Count; i++)
        {
            SummonPlacementCandidate other = reserved[i];
            if (OverlapsFootprints(candidate, candidateCenter, other, ResolveCenter(other), padding))
                return true;
        }

        return false;
    }

    static bool OverlapsWorld(
        CharacteContext caster,
        SummonPlacementCandidate candidate,
        SummonPlacementSettings settings,
        Collider groundCollider)
    {
        Vector3 center = ResolveCenter(candidate);
        int mask = settings.ClearanceMask.value == 0 ? Physics.DefaultRaycastLayers : settings.ClearanceMask.value;
        int hitCount;
        if (candidate.Footprint.Shape == CharacterPlacementShape.Box)
        {
            Vector3 padding = Vector3.one * Mathf.Max(0f, settings.Padding);
            hitCount = Physics.OverlapBoxNonAlloc(
                center,
                candidate.Footprint.HalfExtents + padding,
                ClearanceBuffer,
                ResolveRotation(candidate),
                mask,
                settings.ClearanceTriggerInteraction);
        }
        else
        {
            float radius = candidate.Footprint.Radius + Mathf.Max(0f, settings.Padding);
            float segment = Mathf.Max(
                0f,
                candidate.Footprint.Height * 0.5f - candidate.Footprint.Radius);
            Vector3 axis = ResolveRotation(candidate) * candidate.Footprint.Axis;
            Vector3 bottom = center - axis * segment;
            Vector3 top = center + axis * segment;
            hitCount = Physics.OverlapCapsuleNonAlloc(
                bottom,
                top,
                radius,
                ClearanceBuffer,
                mask,
                settings.ClearanceTriggerInteraction);
        }

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = ClearanceBuffer[i];
            if (hit == null)
                continue;

            if (hit == groundCollider)
                continue;

            if (caster != null && (hit.transform == caster.transform || hit.transform.IsChildOf(caster.transform)))
                continue;

            return true;
        }

        return false;
    }

    static Vector3 ResolveCenter(SummonPlacementCandidate candidate)
    {
        return candidate.Position + candidate.Rotation * candidate.Footprint.CenterOffset;
    }

    static Quaternion ResolveRotation(SummonPlacementCandidate candidate)
    {
        return candidate.Rotation * candidate.Footprint.Rotation;
    }

    static bool OverlapsFootprints(
        SummonPlacementCandidate a,
        Vector3 aCenter,
        SummonPlacementCandidate b,
        Vector3 bCenter,
        float padding)
    {
        if (a.Footprint.Shape == CharacterPlacementShape.Box &&
            b.Footprint.Shape == CharacterPlacementShape.Box)
        {
            return OverlapsBoxBox(aCenter, a, bCenter, b, padding);
        }

        if (a.Footprint.Shape == CharacterPlacementShape.Box)
            return OverlapsBoxCircle(aCenter, a, bCenter, ResolvePlanarRadius(b), padding);
        if (b.Footprint.Shape == CharacterPlacementShape.Box)
            return OverlapsBoxCircle(bCenter, b, aCenter, ResolvePlanarRadius(a), padding);

        Vector3 delta = aCenter - bCenter;
        delta.y = 0f;
        float radius = ResolvePlanarRadius(a) + ResolvePlanarRadius(b) + Mathf.Max(0f, padding);
        return delta.sqrMagnitude < radius * radius;
    }

    static bool OverlapsBoxBox(
        Vector3 aCenter,
        SummonPlacementCandidate a,
        Vector3 bCenter,
        SummonPlacementCandidate b,
        float padding)
    {
        Vector2[] axes =
        {
            ResolvePlanarAxis(a, Vector3.right),
            ResolvePlanarAxis(a, Vector3.forward),
            ResolvePlanarAxis(b, Vector3.right),
            ResolvePlanarAxis(b, Vector3.forward),
        };

        Vector2 delta = new Vector2(bCenter.x - aCenter.x, bCenter.z - aCenter.z);
        Vector2 aAxisX = axes[0];
        Vector2 aAxisZ = axes[1];
        Vector2 bAxisX = axes[2];
        Vector2 bAxisZ = axes[3];
        Vector2 aHalf = new Vector2(a.Footprint.HalfExtents.x, a.Footprint.HalfExtents.z);
        Vector2 bHalf = new Vector2(b.Footprint.HalfExtents.x, b.Footprint.HalfExtents.z);
        float safePadding = Mathf.Max(0f, padding);

        for (int i = 0; i < axes.Length; i++)
        {
            Vector2 axis = axes[i];
            float aRadius = Mathf.Abs(Vector2.Dot(aAxisX, axis)) * (aHalf.x + safePadding) +
                Mathf.Abs(Vector2.Dot(aAxisZ, axis)) * (aHalf.y + safePadding);
            float bRadius = Mathf.Abs(Vector2.Dot(bAxisX, axis)) * (bHalf.x + safePadding) +
                Mathf.Abs(Vector2.Dot(bAxisZ, axis)) * (bHalf.y + safePadding);
            if (Mathf.Abs(Vector2.Dot(delta, axis)) > aRadius + bRadius)
                return false;
        }

        return true;
    }

    static bool OverlapsBoxCircle(
        Vector3 boxCenter,
        SummonPlacementCandidate box,
        Vector3 circleCenter,
        float circleRadius,
        float padding)
    {
        Vector2 axisX = ResolvePlanarAxis(box, Vector3.right);
        Vector2 axisZ = ResolvePlanarAxis(box, Vector3.forward);
        Vector2 delta = new Vector2(circleCenter.x - boxCenter.x, circleCenter.z - boxCenter.z);
        Vector2 local = new Vector2(Vector2.Dot(delta, axisX), Vector2.Dot(delta, axisZ));
        Vector2 half = new Vector2(box.Footprint.HalfExtents.x, box.Footprint.HalfExtents.z);
        Vector2 closest = new Vector2(
            Mathf.Clamp(local.x, -half.x, half.x),
            Mathf.Clamp(local.y, -half.y, half.y));
        Vector2 separation = local - closest;
        float radius = circleRadius + Mathf.Max(0f, padding);
        return separation.sqrMagnitude < radius * radius;
    }

    static Vector2 ResolvePlanarAxis(SummonPlacementCandidate candidate, Vector3 localAxis)
    {
        Vector3 worldAxis = ResolveRotation(candidate) * localAxis;
        Vector2 planar = new Vector2(worldAxis.x, worldAxis.z);
        if (planar.sqrMagnitude <= 0.0001f)
            return localAxis == Vector3.forward ? Vector2.up : Vector2.right;

        return planar.normalized;
    }

    static float ResolvePlanarRadius(SummonPlacementCandidate candidate)
    {
        return ResolvePlanarRadius(candidate.Footprint, candidate.Rotation);
    }

    static float ResolvePlanarRadius(CharacterPlacementFootprint footprint, Quaternion actorRotation)
    {
        Vector3 worldAxis = actorRotation * footprint.Rotation * footprint.Axis;
        float planarAxis = new Vector2(worldAxis.x, worldAxis.z).magnitude;
        float segment = Mathf.Max(0f, footprint.Height * 0.5f - footprint.Radius);
        return footprint.Radius + segment * planarAxis;
    }
}
