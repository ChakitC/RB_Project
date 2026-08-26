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
    public int CandidateSearchCount = 8;
    public float CandidateSearchRadius = 0.75f;
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

        Vector3 requestedPosition = request.Position + layoutRotation * localOffset;
        int candidateCount = Mathf.Max(1, settings.CandidateSearchCount);
        float searchRadius = Mathf.Max(
            settings.CandidateSearchRadius,
            ResolvePlanarRadius(footprint, rotation) + settings.Padding);
        List<CharacterPlacementRequest.Candidate> placementCandidates =
            new(candidateCount);
        List<CharacterPlacementReservationService.StaticReservation> localReservations = null;
        if (reserved != null && reserved.Count > 0)
        {
            localReservations = new List<CharacterPlacementReservationService.StaticReservation>(
                reserved.Count);
            for (int i = 0; i < reserved.Count; i++)
            {
                SummonPlacementCandidate localCandidate = reserved[i];
                localReservations.Add(new CharacterPlacementReservationService.StaticReservation(
                    InflateReservationFootprint(localCandidate.Footprint, settings.Padding),
                    localCandidate.Position,
                    localCandidate.Rotation));
            }
        }

        Collider ignoredGroundCollider = null;

        for (int i = 0; i < candidateCount; i++)
        {
            float angle = i == 0
                ? 0f
                : (i - 1) * (360f / Mathf.Max(1, candidateCount - 1));
            Vector3 rawPosition = i == 0
                ? requestedPosition
                : requestedPosition + Quaternion.Euler(0f, angle, 0f) * (Vector3.forward * searchRadius);

            if (!TryPreparePosition(
                    rawPosition,
                    footprint,
                    rotation,
                    settings,
                    out Vector3 preparedPosition,
                    out Collider groundCollider,
                    out _))
                continue;

            ignoredGroundCollider ??= groundCollider;
            placementCandidates.Add(new CharacterPlacementRequest.Candidate(
                preparedPosition,
                rotation,
                preferredAngleError: i == 0 ? 0f : 1f,
                authoredOrder: i));
        }

        if (placementCandidates.Count == 0)
        {
            error = "No ground or NavMesh position was found for summon placement.";
            return false;
        }

        if (!TryResolveCentralWorldClearance(
                request.Caster,
                footprint,
                rotation,
                placementCandidates.ToArray(),
                settings,
                ignoredGroundCollider,
                request.Mobility == SummonMobility.Mobile,
                CharacterPlacementReservationRegistry.Shared,
                localReservations,
                out CharacterPlacementResult clearanceResult))
        {
            error = "Summon placement clearance is blocked.";
            return false;
        }

        candidate = new SummonPlacementCandidate(
            clearanceResult.StartPosition,
            rotation,
            footprint);
        return true;
    }

    public static bool TryReserve(
        SummonPlacementCandidate candidate,
        SummonPlacementSettings settings,
        Transform owner,
        out CharacterPlacementReservationService.Handle handle)
    {
        handle = default;
        if (owner == null)
            return false;

        LayerMask clearanceMask = settings != null ? settings.ClearanceMask : ~0;
        CharacterPlacementRequest request = new(
            actorRoot: owner,
            positionCollider: null,
            footprint: InflateReservationFootprint(
                candidate.Footprint,
                settings != null ? settings.Padding : 0f),
            targetIdentity: AITargetIdentity.Generic,
            targetRoot: null,
            targetAnchor: new CharacterPlacementRequest.AnchorSnapshot(
                Vector3.zero,
                Quaternion.identity,
                AITargetIdentity.Generic),
            candidates: new[]
            {
                new CharacterPlacementRequest.Candidate(
                    candidate.Position,
                    candidate.Rotation,
                    preferredAngleError: 0f,
                    authoredOrder: 0),
            },
            animation: null,
            impactNormalizedTime: 0f,
            policy: null,
            worldCollisionLayers: clearanceMask,
            actorCollisionLayers: ~0,
            ignoreRoot: owner,
            reservationOwner: owner,
            effectivePlanarRootMotion: false,
            animationRequired: false,
            mobileActor: false,
            transientReservation: true);
        CharacterPlacementResult result = CharacterPlacementResult.Success(
            candidate.Position,
            candidate.Rotation,
            candidate.Position,
            candidate.Rotation,
            candidateIndex: 0,
            score: new CharacterPlacementScore(0f, 0f, 0f, 0f, 0, 0f, 0f, 0));
        return CharacterPlacementReservationRegistry.Shared.TryReserve(request, result, out handle);
    }

    public static Transform ResolveReservationOwner(SummonedEntityRuntime spawned)
    {
        if (spawned == null)
            return null;

        return spawned.SummonContext != null
            ? spawned.SummonContext.transform
            : spawned.transform;
    }

    static CharacterPlacementFootprint InflateReservationFootprint(
        CharacterPlacementFootprint footprint,
        float padding)
    {
        padding = Mathf.Max(0f, padding);
        if (padding <= 0f)
            return footprint;

        Vector3 halfExtents = footprint.Shape == CharacterPlacementShape.Box
            ? footprint.HalfExtents + Vector3.one * padding
            : footprint.HalfExtents;
        float radius = footprint.Shape == CharacterPlacementShape.Circle
            ? footprint.Radius + padding
            : footprint.Radius;
        return new CharacterPlacementFootprint(
            footprint.Shape,
            footprint.CenterOffset,
            halfExtents,
            radius,
            footprint.Height,
            footprint.Rotation,
            footprint.Axis);
    }

    static bool TryResolveCentralWorldClearance(
        CharacteContext caster,
        CharacterPlacementFootprint footprint,
        Quaternion rotation,
        CharacterPlacementRequest.Candidate[] candidates,
        SummonPlacementSettings settings,
        Collider groundCollider,
        bool mobileActor,
        CharacterPlacementReservationService reservations,
        IReadOnlyList<CharacterPlacementReservationService.StaticReservation> localReservations,
        out CharacterPlacementResult result)
    {
        bool requireNavMesh = mobileActor || settings.RequireNavMesh;
        float navMeshSampleDistance = Mathf.Max(
            settings.NavMeshSampleRadius,
            ResolvePlanarRadius(footprint, rotation));
        CharacterPlacementRuntimePolicy runtimePolicy =
            CharacterPlacementRuntimePolicy.CreateDefault(
                requireNavMesh,
                navMeshSampleDistance,
                settings.ClearanceTriggerInteraction,
                settings.Padding,
                targetContactWindowBefore: 0f,
                targetContactWindowAfter: 0f);
        CharacterPlacementRequest request = new(
            actorRoot: caster != null ? caster.transform : null,
            positionCollider: null,
            footprint: footprint,
            targetIdentity: AITargetIdentity.Generic,
            targetRoot: null,
            targetAnchor: CharacterPlacementRequest.AnchorSnapshot.Capture(null),
            candidates: candidates,
            animation: null,
            impactNormalizedTime: 0.5f,
            policy: null,
            worldCollisionLayers: settings.ClearanceMask,
            actorCollisionLayers: ~0,
            ignoreRoot: caster != null ? caster.transform : null,
            reservationOwner: caster,
            effectivePlanarRootMotion: false,
            animationRequired: false,
            mobileActor: mobileActor,
            runtimePolicy: runtimePolicy,
            ignoredCollider: groundCollider,
            additionalReservations: localReservations);

        if (!CharacterPlacementResolver.TryResolve(request, reservations, out result))
            return false;

        return true;
    }

    static bool TryPreparePosition(
        Vector3 rawPosition,
        CharacterPlacementFootprint footprint,
        Quaternion rotation,
        SummonPlacementSettings settings,
        out Vector3 position,
        out Collider groundCollider,
        out string error)
    {
        position = rawPosition;
        groundCollider = null;
        error = string.Empty;

        if (settings.ResolveGround)
        {
            Vector3 rayOrigin = position + Vector3.up * Mathf.Max(0.1f, settings.GroundRaycastHeight);
            int groundMask = settings.GroundMask.value == 0
                ? Physics.DefaultRaycastLayers
                : settings.GroundMask.value;
            if (!Physics.Raycast(
                    rayOrigin,
                    Vector3.down,
                    out RaycastHit groundHit,
                    Mathf.Max(0.1f, settings.GroundRaycastDistance),
                    groundMask,
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
            if (!NavMesh.SamplePosition(
                    position,
                    out NavMeshHit navHit,
                    Mathf.Max(
                        settings.NavMeshSampleRadius,
                        ResolvePlanarRadius(footprint, rotation)),
                    areaMask))
            {
                error = "No NavMesh position was found for summon placement.";
                return false;
            }

            position = navHit.position;
        }

        return true;
    }

    static float ResolvePlanarRadius(CharacterPlacementFootprint footprint, Quaternion actorRotation)
    {
        Vector3 worldAxis = actorRotation * footprint.Rotation * footprint.Axis;
        float planarAxis = new Vector2(worldAxis.x, worldAxis.z).magnitude;
        float segment = Mathf.Max(0f, footprint.Height * 0.5f - footprint.Radius);
        return footprint.Radius + segment * planarAxis;
    }
}
