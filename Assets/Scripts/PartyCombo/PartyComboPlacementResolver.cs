using UnityEngine;

// Combo owns camera preferences; the shared resolver remains responsible for geometry.
public static class PartyComboPlacementResolver
{
    public static bool TryResolve(CharacteContext actor, Collider actorCollider,
        CharacterPlacementFootprint footprint, Transform target, AITargetIdentity targetIdentity,
        PartyComboExecutionProfile profile, Camera camera,
        CharacterPlacementReservationService reservations,
        out CharacterPlacementRequest selectedRequest, out CharacterPlacementResult selectedResult)
    {
        selectedRequest = null;
        selectedResult = default;
        Vector3 away = actor.transform.position - target.position;
        away.y = 0f;
        if (away.sqrMagnitude < 0.0001f)
            away = Vector3.ProjectOnPlane(-target.forward, Vector3.up);
        if (away.sqrMagnitude < 0.0001f)
            away = Vector3.back;
        away.Normalize();
        int count = Mathf.Clamp(profile.placementCandidateCount, 1, 16);
        PartyComboPlacementVisibility bestVisibility = default;
        for (int i = 0; i < count; i++)
        {
            float angle = i * (360f / count);
            Vector3 direction = Quaternion.AngleAxis(angle, Vector3.up) * away;
            Vector3 position = target.position + direction * profile.desiredRange +
                target.TransformVector(profile.localOffset);
            Vector3 facing = Vector3.ProjectOnPlane(target.position - position, Vector3.up);
            Quaternion rotation = facing.sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(facing.normalized, Vector3.up) : actor.transform.rotation;
            var candidate = new CharacterPlacementRequest.Candidate(position, rotation,
                Mathf.Abs(Mathf.DeltaAngle(0f, angle)), i);
            var request = new CharacterPlacementRequest(actor.transform, actorCollider, footprint,
                targetIdentity, target, CharacterPlacementRequest.AnchorSnapshot.Capture(target, targetIdentity),
                new[] { candidate }, null, 0f, null,
                profile.requireUnobstructedPosition ? Physics.DefaultRaycastLayers : 0,
                0, actor.transform, actor, effectivePlanarRootMotion: false,
                animationRequired: false, mobileActor: true,
                runtimePolicy: CharacterPlacementRuntimePolicy.CreateDefault(profile.requireNavMesh,
                    profile.navMeshSampleDistance, QueryTriggerInteraction.Ignore));
            if (!CharacterPlacementResolver.TryResolve(request, reservations, out CharacterPlacementResult result))
                continue;
            // Shared placement can return a least-penetrating pose. Combo's unobstructed
            // policy must not trade standing inside geometry for a prettier camera shot.
            if (profile.requireUnobstructedPosition &&
                (result.Score.MaxWorldPenetration > 0.0001f || result.Score.MaxActorPenetration > 0.0001f))
                continue;
            PartyComboPlacementVisibility visibility = PartyComboPlacementVisibility.Evaluate(camera,
                result.StartPosition, result.StartRotation, footprint, actor.transform, actorCollider,
                profile.visibilityObstructionLayers);
            int comparison = visibility.CompareTo(bestVisibility);
            if (selectedRequest != null && (comparison < 0 ||
                (comparison == 0 && !result.Score.IsBetterThan(selectedResult.Score))))
                continue;
            selectedRequest = request;
            selectedResult = result;
            bestVisibility = visibility;
        }
        return selectedRequest != null;
    }
}
