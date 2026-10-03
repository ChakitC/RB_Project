using UnityEngine;

// FollowUp owns camera preferences; the shared resolver remains responsible for geometry.
public static class PartyFollowUpPlacementResolver
{
    public static bool TryResolve(CharacteContext actor, Collider actorCollider,
        CharacterPlacementFootprint footprint, Transform target, AITargetIdentity targetIdentity,
        PartyFollowUpExecutionProfile profile, Camera camera,
        CharacterPlacementReservationService reservations,
        out CharacterPlacementRequest selectedRequest, out CharacterPlacementResult selectedResult,
        CharacterPlacementAnimationInput animation = null, float placementNormalized = 0f,
        LayerMask rootMotionIgnoredLayers = default)
    {
        selectedRequest = null;
        selectedResult = default;
        bool alignPlacement = profile.placementPolicy == PartyFollowUpPlacementPolicy.AlignPlacementPointToEventTarget;
        CharacterPlacementAnimationInput.Sample impact = default;
        if (alignPlacement && (animation == null || !animation.PlanarRootMotionEnabled ||
            !animation.TrySample(placementNormalized, out impact)))
        {
            selectedResult = CharacterPlacementResult.Failed("FollowUp placement requires a sampled root-motion trajectory.");
            return false;
        }
        Vector3 away = actor.transform.position - target.position;
        away.y = 0f;
        if (away.sqrMagnitude < 0.0001f)
            away = Vector3.ProjectOnPlane(-target.forward, Vector3.up);
        if (away.sqrMagnitude < 0.0001f)
            away = Vector3.back;
        away.Normalize();
        int count = Mathf.Clamp(profile.placementCandidateCount, 1, 16);
        PartyFollowUpPlacementVisibility bestVisibility = default;
        for (int i = 0; i < count; i++)
        {
            float angle = i * (360f / count);
            Vector3 direction = Quaternion.AngleAxis(angle, Vector3.up) * away;
            Vector3 position = target.position + direction * profile.desiredRange +
                target.TransformVector(profile.localOffset);
            Vector3 facing = Vector3.ProjectOnPlane(target.position - position, Vector3.up);
            Quaternion rotation = facing.sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(facing.normalized, Vector3.up) : actor.transform.rotation;
            Vector3? desiredImpact = null;
            if (alignPlacement)
            {
                desiredImpact = position;
                // The ring anchors the jump's ground plane. Keep S on that plane;
                // the authored Y curve determines root height at A.
                if (animation.PreserveVerticalMotion)
                    desiredImpact += Vector3.up * impact.LocalPosition.y;
                rotation *= Quaternion.Inverse(Quaternion.Euler(0f, impact.LocalYaw, 0f));
                Vector3 displacement = impact.LocalPosition;
                if (animation.PreserveVerticalMotion) displacement.y = 0f;
                position -= rotation * displacement;
            }
            var candidate = new CharacterPlacementRequest.Candidate(position, rotation,
                Mathf.Abs(Mathf.DeltaAngle(0f, angle)), i, desiredImpact);
            var request = new CharacterPlacementRequest(actor.transform, actorCollider, footprint,
                targetIdentity, target, CharacterPlacementRequest.AnchorSnapshot.Capture(target, targetIdentity),
                new[] { candidate }, alignPlacement ? animation : null, alignPlacement ? placementNormalized : 0f, null,
                profile.requireUnobstructedPosition
                    ? Physics.DefaultRaycastLayers & ~(alignPlacement ? rootMotionIgnoredLayers.value : 0) : 0,
                0, actor.transform, actor, effectivePlanarRootMotion: alignPlacement,
                animationRequired: alignPlacement, mobileActor: true,
                runtimePolicy: CharacterPlacementRuntimePolicy.CreateDefault(profile.requireNavMesh,
                    profile.navMeshSampleDistance, QueryTriggerInteraction.Ignore));
            if (!CharacterPlacementResolver.TryResolve(request, reservations, out CharacterPlacementResult result))
                continue;
            // Shared placement can return a least-penetrating pose. FollowUp's unobstructed
            // policy must not trade standing inside geometry for a prettier camera shot.
            if (profile.requireUnobstructedPosition &&
                (result.Score.MaxWorldPenetration > 0.0001f || result.Score.MaxActorPenetration > 0.0001f))
                continue;
            PartyFollowUpPlacementVisibility visibility = PartyFollowUpPlacementVisibility.Evaluate(camera,
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
