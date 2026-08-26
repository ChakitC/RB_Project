using UnityEngine;
using UnityEngine.AI;

public static class CharacterPlacementResolver
{
    const int MaxCandidateCount = 128;
    const int MaxDetailCount = 3;
    const int DefaultMaxTrajectorySamples = 240;
    const int MotionPenetrationSampleCount = 5;
    const int RotationSweepSampleCount = 4;
    const float ImpactTargetTolerance = 0.05f;
    const float RotationSweepAngleThreshold = 0.1f;

    static readonly Collider[] OverlapBuffer = new Collider[128];
    static readonly RaycastHit[] CastBuffer = new RaycastHit[128];
    static readonly CandidateEvaluation[] EvaluationBuffer = new CandidateEvaluation[MaxCandidateCount];
    static readonly int[] DetailIndices = new int[MaxDetailCount];

    public static bool TryResolve(
        CharacterPlacementRequest request,
        CharacterPlacementReservationService reservations,
        out CharacterPlacementResult result)
    {
        result = default;
        if (request == null)
        {
            result = CharacterPlacementResult.Failed("Character placement request is missing.");
            return false;
        }

        CharacterPlacementRequest.Candidate[] candidates = request.Candidates;
        if (candidates == null || candidates.Length == 0)
        {
            result = CharacterPlacementResult.Failed("Character placement candidates are missing.");
            return false;
        }

        if (candidates.Length > EvaluationBuffer.Length)
        {
            result = CharacterPlacementResult.Failed("Character placement candidate buffer is full.");
            return false;
        }

        if (request.Footprint.Radius <= 0f || request.Footprint.Height <= 0f)
        {
            result = CharacterPlacementResult.Failed("Character placement footprint is invalid.");
            return false;
        }

        CharacterPlacementAnimationInput animation = request.Animation;
        bool useAnimation = request.EffectivePlanarRootMotion &&
                            animation != null &&
                            animation.PlanarRootMotionEnabled &&
                            animation.HasSamples;
        if (request.AnimationRequired && !useAnimation)
        {
            result = CharacterPlacementResult.Failed(
                animation == null || !animation.HasSamples
                    ? "Required placement animation trajectory is missing."
                    : "Required placement animation trajectory is disabled by runtime root-motion policy.");
            return false;
        }

        int maxSamples = Mathf.Clamp(
            ResolveMaxTrajectorySamples(request),
            1,
            DefaultMaxTrajectorySamples);
        if (useAnimation && animation.Samples.Length > maxSamples)
        {
            result = CharacterPlacementResult.Failed("Placement animation trajectory exceeds the sample buffer.");
            return false;
        }

        for (int i = 0; i < candidates.Length; i++)
        {
            EvaluateCandidate(request, reservations, candidates[i], useAnimation, detailed: false, out EvaluationBuffer[i]);
        }

        bool hasCollisionFreeCandidate = false;
        for (int i = 0; i < candidates.Length; i++)
        {
            if (EvaluationBuffer[i].Valid && EvaluationBuffer[i].CollisionSampleCount == 0)
            {
                hasCollisionFreeCandidate = true;
                break;
            }
        }

        if (!hasCollisionFreeCandidate)
        {
            for (int i = 0; i < candidates.Length; i++)
            {
                if (!EvaluationBuffer[i].Valid)
                    continue;

                EvaluateCandidate(
                    request,
                    reservations,
                    candidates[i],
                    useAnimation,
                    detailed: true,
                    out EvaluationBuffer[i]);
            }
        }
        else
        {
            int detailCount = ResolveDetailIndices(candidates.Length, request);
            for (int i = 0; i < detailCount; i++)
            {
                int candidateIndex = DetailIndices[i];
                EvaluateCandidate(
                    request,
                    reservations,
                    candidates[candidateIndex],
                    useAnimation,
                    detailed: true,
                    out EvaluationBuffer[candidateIndex]);
            }
        }

        int bestIndex = -1;
        for (int i = 0; i < candidates.Length; i++)
        {
            CandidateEvaluation evaluation = EvaluationBuffer[i];
            if (!evaluation.Valid)
                continue;

            if (bestIndex < 0 || evaluation.Score.IsBetterThan(EvaluationBuffer[bestIndex].Score))
                bestIndex = i;
        }

        if (bestIndex < 0)
        {
            result = CharacterPlacementResult.Failed(
                "No placement candidate met the NavMesh, ground-support, or physics query requirements.");
            return false;
        }

        CandidateEvaluation best = EvaluationBuffer[bestIndex];
        result = CharacterPlacementResult.Success(
            best.StartPosition,
            candidates[bestIndex].Rotation,
            best.ImpactPosition,
            best.ImpactRotation,
            bestIndex,
            best.Score);
        return true;
    }

    static int ResolveDetailIndices(int candidateCount, CharacterPlacementRequest request)
    {
        int maxDetails = Mathf.Clamp(
            ResolveMaxDetailedCandidates(request),
            1,
            MaxDetailCount);
        int detailCount = 0;

        for (int candidateIndex = 0; candidateIndex < candidateCount; candidateIndex++)
        {
            CandidateEvaluation candidate = EvaluationBuffer[candidateIndex];
            if (!candidate.Valid)
                continue;

            if (detailCount >= maxDetails &&
                !candidate.Score.IsBetterThan(EvaluationBuffer[DetailIndices[maxDetails - 1]].Score))
                continue;

            int insertAt = Mathf.Min(detailCount, maxDetails - 1);

            while (insertAt > 0 &&
                   candidate.Score.IsBetterThan(EvaluationBuffer[DetailIndices[insertAt - 1]].Score))
            {
                DetailIndices[insertAt] = DetailIndices[insertAt - 1];
                insertAt--;
            }

            if (detailCount < maxDetails)
            {
                DetailIndices[insertAt] = candidateIndex;
                detailCount++;
            }
            else
                DetailIndices[insertAt] = candidateIndex;
        }

        return detailCount;
    }

    static void EvaluateCandidate(
        CharacterPlacementRequest request,
        CharacterPlacementReservationService reservations,
        CharacterPlacementRequest.Candidate candidate,
        bool useAnimation,
        bool detailed,
        out CandidateEvaluation evaluation)
    {
        evaluation = new CandidateEvaluation
        {
            Valid = true,
            StartPosition = candidate.Position,
            ImpactPosition = candidate.Position,
            ImpactRotation = candidate.Rotation,
            PreferredAngleError = candidate.PreferredAngleError,
            AuthoredCandidateOrder = candidate.AuthoredOrder,
        };

        Vector3 startPosition = candidate.Position;
        Vector3 snappedStart = startPosition;
        float snapDistance = 0f;
        if (RequiresNavMesh(request) &&
            !TrySnapToNavMesh(request, startPosition, out snappedStart, out snapDistance))
        {
            evaluation.Valid = false;
            return;
        }

        if (RequiresNavMesh(request))
        {
            startPosition = snappedStart;
            evaluation.NavMeshSnapDistance = snapDistance;
        }

        evaluation.StartPosition = startPosition;
        evaluation.ImpactPosition = startPosition;
        evaluation.ImpactRotation = candidate.Rotation;

        if (useAnimation &&
            candidate.DesiredImpactPosition.HasValue &&
            request.Animation.TrySample(
                request.ImpactNormalizedTime,
                out CharacterPlacementAnimationInput.Sample desiredImpactSample))
        {
            Vector3 predictedImpactPosition =
                startPosition + candidate.Rotation * desiredImpactSample.LocalPosition;
            if (Vector3.Distance(predictedImpactPosition, candidate.DesiredImpactPosition.Value) >
                ImpactTargetTolerance)
            {
                evaluation.Valid = false;
                return;
            }
        }

        EvaluatePose(
            request,
            reservations,
            startPosition,
            candidate.Rotation,
            0f,
            detailed,
            ref evaluation);
        if (!evaluation.Valid)
            return;

        if (useAnimation)
        {
            CharacterPlacementAnimationInput.Sample[] samples = request.Animation.Samples;
            Vector3 previousPosition = startPosition;
            Quaternion previousRotation = candidate.Rotation;
            float previousNormalizedTime = 0f;
            for (int i = 0; i < samples.Length; i++)
            {
                CharacterPlacementAnimationInput.Sample sample = samples[i];
                Vector3 worldPosition = startPosition + candidate.Rotation * sample.LocalPosition;
                Quaternion worldRotation = candidate.Rotation * Quaternion.Euler(0f, sample.LocalYaw, 0f);
                EvaluatePose(
                    request,
                    reservations,
                    worldPosition,
                    worldRotation,
                    sample.NormalizedTime,
                    detailed,
                    ref evaluation);
                if (!evaluation.Valid)
                    return;

                if (i > 0)
                {
                    EvaluateMotionSegment(
                        request,
                        reservations,
                        previousPosition,
                        previousRotation,
                        previousNormalizedTime,
                        worldPosition,
                        worldRotation,
                        sample.NormalizedTime,
                        detailed,
                        ref evaluation);
                    if (!evaluation.Valid)
                        return;
                }

                previousPosition = worldPosition;
                previousRotation = worldRotation;
                previousNormalizedTime = sample.NormalizedTime;
            }

            if (request.Animation.TrySample(
                    1f,
                    out CharacterPlacementAnimationInput.Sample endSample))
            {
                Vector3 endPosition = startPosition + candidate.Rotation * endSample.LocalPosition;
                Quaternion endRotation =
                    candidate.Rotation * Quaternion.Euler(0f, endSample.LocalYaw, 0f);
                EvaluatePose(
                    request,
                    reservations,
                    endPosition,
                    endRotation,
                    1f,
                    detailed,
                    ref evaluation);
                if (!evaluation.Valid)
                    return;

                if (endSample.NormalizedTime > previousNormalizedTime + 0.0001f)
                {
                    EvaluateMotionSegment(
                        request,
                        reservations,
                        previousPosition,
                        previousRotation,
                        previousNormalizedTime,
                        endPosition,
                        endRotation,
                        1f,
                        detailed,
                        ref evaluation);
                    if (!evaluation.Valid)
                        return;
                }
            }

            if (request.Animation.TrySample(
                    request.ImpactNormalizedTime,
                    out CharacterPlacementAnimationInput.Sample impactSample))
            {
                evaluation.ImpactPosition =
                    startPosition + candidate.Rotation * impactSample.LocalPosition;
                evaluation.ImpactRotation =
                    candidate.Rotation * Quaternion.Euler(0f, impactSample.LocalYaw, 0f);
            }
        }

        evaluation.Score = new CharacterPlacementScore(
            evaluation.MaxWorldPenetration,
            evaluation.TotalWorldPenetration,
            evaluation.MaxActorPenetration,
            evaluation.TotalActorPenetration,
            evaluation.CollisionSampleCount,
            evaluation.PreferredAngleError,
            evaluation.NavMeshSnapDistance,
            evaluation.AuthoredCandidateOrder);
    }

    static void EvaluatePose(
        CharacterPlacementRequest request,
        CharacterPlacementReservationService reservations,
        Vector3 position,
        Quaternion rotation,
        float normalizedTime,
        bool detailed,
        ref CandidateEvaluation evaluation)
    {
        if (RequiresNavMesh(request) &&
            !NavMesh.SamplePosition(
                position,
                out _,
                ResolveNavMeshSampleDistance(request),
                ResolveNavMeshAreaMask(request)))
        {
            evaluation.Valid = false;
            return;
        }

        if (RequiresNavMesh(request) && !IsFootprintOnNavMesh(request, position, rotation))
        {
            evaluation.Valid = false;
            return;
        }

        if (RequiresGroundSupport(request) && !HasGroundSupport(request, position))
        {
            evaluation.Valid = false;
            return;
        }

        int worldMask = request.WorldCollisionLayers.value;
        int actorMask = request.ActorCollisionLayers.value;
        int queryMask = worldMask | actorMask;
        bool poseCollided = false;

        if (queryMask != 0)
        {
            int hitCount = OverlapFootprint(request.Footprint, position, rotation, queryMask, request);
            if (hitCount >= OverlapBuffer.Length)
            {
                evaluation.Valid = false;
                evaluation.BufferFull = true;
                return;
            }

            bool targetIsAllowed = IsTargetContactWindow(request, normalizedTime);
            for (int i = 0; i < hitCount; i++)
            {
                Collider hit = OverlapBuffer[i];
                if (ShouldIgnoreCollider(request, hit, targetIsAllowed))
                    continue;

                if (!TryClassifyCollider(request, hit, out bool isWorld, out bool isActor))
                    continue;

                if (isActor &&
                    IsCoveredByTransientReservation(reservations, hit, request.ReservationOwner))
                    continue;

                float penetration = detailed
                    ? ResolvePenetration(request, position, rotation, hit)
                    : 1f;
                poseCollided = true;
                if (isWorld)
                {
                    evaluation.MaxWorldPenetration = Mathf.Max(
                        evaluation.MaxWorldPenetration,
                        penetration);
                    evaluation.TotalWorldPenetration += penetration;
                }
                else
                {
                    evaluation.MaxActorPenetration = Mathf.Max(
                        evaluation.MaxActorPenetration,
                        penetration);
                    evaluation.TotalActorPenetration += penetration;
                }
            }
        }

        if (reservations != null)
        {
            for (int i = 0; i < reservations.ActiveCount; i++)
            {
                if (!reservations.TryGetActiveAt(i, out CharacterPlacementReservationService.ReservationView reservation))
                    continue;
                if (reservation.Owner != null && reservation.Owner == request.ReservationOwner)
                    continue;

                if (!TryResolveReservationPose(
                        reservation.Request,
                        reservation.Result,
                        normalizedTime,
                        out Vector3 reservedPosition,
                        out Quaternion reservedRotation))
                    continue;

                float penetration = ResolveReservationPenetration(
                    request.Footprint,
                    position,
                    rotation,
                    reservation.Request.Footprint,
                    reservedPosition,
                    reservedRotation);
                if (penetration <= 0f)
                    continue;

                poseCollided = true;
                evaluation.MaxActorPenetration = Mathf.Max(
                    evaluation.MaxActorPenetration,
                    penetration);
                evaluation.TotalActorPenetration += penetration;
            }
        }

        if (request.AdditionalReservations != null)
        {
            for (int i = 0; i < request.AdditionalReservations.Count; i++)
            {
                CharacterPlacementReservationService.StaticReservation reservation =
                    request.AdditionalReservations[i];
                if (reservation.Owner != null && reservation.Owner == request.ReservationOwner)
                    continue;

                float penetration = ResolveReservationPenetration(
                    request.Footprint,
                    position,
                    rotation,
                    reservation.Footprint,
                    reservation.Position,
                    reservation.Rotation);
                if (penetration <= 0f)
                    continue;

                poseCollided = true;
                evaluation.MaxActorPenetration = Mathf.Max(
                    evaluation.MaxActorPenetration,
                    penetration);
                evaluation.TotalActorPenetration += penetration;
            }
        }

        if (poseCollided)
            evaluation.CollisionSampleCount++;

        if (request.PoseValidator != null &&
            !request.PoseValidator(position, rotation))
            evaluation.Valid = false;
    }

    static bool IsCoveredByTransientReservation(
        CharacterPlacementReservationService reservations,
        Collider hit,
        Object reservationOwner)
    {
        if (reservations == null || hit == null)
            return false;

        for (int i = 0; i < reservations.ActiveCount; i++)
        {
            if (!reservations.TryGetActiveAt(
                    i,
                    out CharacterPlacementReservationService.ReservationView reservation))
                continue;

            if (reservation.Request == null || !reservation.Request.TransientReservation)
                continue;
            if (reservation.Owner != null && reservation.Owner == reservationOwner)
                continue;

            Transform ownerRoot = reservation.Owner as Transform;
            if (ownerRoot != null && IsSameOrChild(hit.transform, ownerRoot))
                return true;
        }

        return false;
    }

    static void EvaluateMotionSegment(
        CharacterPlacementRequest request,
        CharacterPlacementReservationService reservations,
        Vector3 startPosition,
        Quaternion startRotation,
        float startNormalizedTime,
        Vector3 endPosition,
        Quaternion endRotation,
        float endNormalizedTime,
        bool detailed,
        ref CandidateEvaluation evaluation)
    {
        Vector3 delta = endPosition - startPosition;
        float distance = delta.magnitude;
        float rotationAngle = Quaternion.Angle(startRotation, endRotation);
        if (distance <= 0.0001f)
        {
            if (rotationAngle <= RotationSweepAngleThreshold)
                return;

            EvaluateRotationSweep(
                request,
                reservations,
                startPosition,
                startRotation,
                startNormalizedTime,
                endPosition,
                endRotation,
                endNormalizedTime,
                detailed,
                ref evaluation);
            return;
        }

        if (rotationAngle > RotationSweepAngleThreshold)
        {
            EvaluateRotationSweep(
                request,
                reservations,
                startPosition,
                startRotation,
                startNormalizedTime,
                endPosition,
                endRotation,
                endNormalizedTime,
                detailed,
                ref evaluation);
            if (!evaluation.Valid)
                return;
        }

        int worldMask = request.WorldCollisionLayers.value;
        int actorMask = request.ActorCollisionLayers.value;
        int queryMask = worldMask | actorMask;
        if (queryMask == 0)
            return;

        int hitCount = CastFootprint(
            request,
            startPosition,
            startRotation,
            delta / distance,
            distance,
            queryMask);
        if (hitCount >= CastBuffer.Length)
        {
            evaluation.Valid = false;
            evaluation.BufferFull = true;
            return;
        }

        // A swept segment that begins or ends in the configured contact window belongs to the
        // target-contact portion of the trajectory. Ignore the target for that whole segment so
        // the cast does not turn the approach into an actor collision just because the cast hit
        // the target's collider slightly before the sampled impact pose.
        bool targetContactSegment =
            IsTargetContactWindow(request, startNormalizedTime) ||
            IsTargetContactWindow(request, endNormalizedTime);

        bool segmentCollided = false;
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = CastBuffer[i];
            CastBuffer[i] = default;
            Collider collider = hit.collider;
            float hitTime = distance > 0.0001f
                ? Mathf.Lerp(startNormalizedTime, endNormalizedTime, Mathf.Clamp01(hit.distance / distance))
                : startNormalizedTime;

            if (collider == null ||
                ShouldIgnoreCollider(
                    request,
                    collider,
                    targetContactSegment || IsTargetContactWindow(request, hitTime)))
                continue;

            if (!TryClassifyCollider(request, collider, out bool isWorld, out bool isActor))
                continue;

            if (isActor &&
                IsCoveredByTransientReservation(reservations, collider, request.ReservationOwner))
                continue;

            float hitT = distance > 0.0001f
                ? Mathf.Clamp01(hit.distance / distance)
                : 0f;
            float penetration = detailed
                ? ResolveMaximumSegmentPenetration(
                    request,
                    startPosition,
                    startRotation,
                    endPosition,
                    endRotation,
                    hitT,
                    collider)
                : 1f;

            segmentCollided = true;
            if (isWorld)
            {
                evaluation.MaxWorldPenetration = Mathf.Max(
                    evaluation.MaxWorldPenetration,
                    penetration);
                evaluation.TotalWorldPenetration += penetration;
            }
            else if (isActor)
            {
                evaluation.MaxActorPenetration = Mathf.Max(
                    evaluation.MaxActorPenetration,
                    penetration);
                evaluation.TotalActorPenetration += penetration;
            }
        }

        if (segmentCollided)
            evaluation.CollisionSampleCount++;
    }

    static void EvaluateRotationSweep(
        CharacterPlacementRequest request,
        CharacterPlacementReservationService reservations,
        Vector3 startPosition,
        Quaternion startRotation,
        float startNormalizedTime,
        Vector3 endPosition,
        Quaternion endRotation,
        float endNormalizedTime,
        bool detailed,
        ref CandidateEvaluation evaluation)
    {
        for (int i = 1; i < RotationSweepSampleCount; i++)
        {
            float t = i / (float)RotationSweepSampleCount;
            EvaluatePose(
                request,
                reservations,
                Vector3.Lerp(startPosition, endPosition, t),
                Quaternion.Slerp(startRotation, endRotation, t),
                Mathf.Lerp(startNormalizedTime, endNormalizedTime, t),
                detailed,
                ref evaluation);
            if (!evaluation.Valid)
                return;
        }
    }

    static float ResolveMaximumSegmentPenetration(
        CharacterPlacementRequest request,
        Vector3 startPosition,
        Quaternion startRotation,
        Vector3 endPosition,
        Quaternion endRotation,
        float hitT,
        Collider hit)
    {
        float maximum = 0f;
        for (int i = 0; i < MotionPenetrationSampleCount; i++)
        {
            float t = i / (float)(MotionPenetrationSampleCount - 1);
            maximum = Mathf.Max(
                maximum,
                ResolvePenetration(
                    request,
                    Vector3.Lerp(startPosition, endPosition, t),
                    Quaternion.Slerp(startRotation, endRotation, t),
                    hit));
        }

        maximum = Mathf.Max(
            maximum,
            ResolvePenetration(
                request,
                Vector3.Lerp(startPosition, endPosition, hitT),
                Quaternion.Slerp(startRotation, endRotation, hitT),
                hit));
        return maximum;
    }

    static int CastFootprint(
        CharacterPlacementRequest request,
        Vector3 position,
        Quaternion rotation,
        Vector3 direction,
        float distance,
        int layerMask)
    {
        CharacterPlacementFootprint footprint = request.Footprint;
        Vector3 center = position + rotation * footprint.CenterOffset;
        Quaternion footprintRotation = rotation * footprint.Rotation;
        float padding = ResolvePadding(request);
        QueryTriggerInteraction triggerInteraction = ResolveTriggerInteraction(request);

        if (footprint.Shape == CharacterPlacementShape.Box)
        {
            return Physics.BoxCastNonAlloc(
                center,
                footprint.HalfExtents + Vector3.one * padding,
                direction,
                CastBuffer,
                footprintRotation,
                distance,
                layerMask,
                triggerInteraction);
        }

        float radius = footprint.Radius + padding;
        float segment = Mathf.Max(0f, footprint.Height * 0.5f - footprint.Radius);
        Vector3 axis = footprintRotation * footprint.Axis;
        return Physics.CapsuleCastNonAlloc(
            center - axis * segment,
            center + axis * segment,
            radius,
            direction,
            CastBuffer,
            distance,
            layerMask,
            triggerInteraction);
    }

    static bool TryClassifyCollider(
        CharacterPlacementRequest request,
        Collider hit,
        out bool isWorld,
        out bool isActor)
    {
        isWorld = false;
        isActor = false;
        if (hit == null)
            return false;

        int layerBit = 1 << hit.gameObject.layer;
        bool actorLayer = (request.ActorCollisionLayers.value & layerBit) != 0;
        bool worldLayer = (request.WorldCollisionLayers.value & layerBit) != 0;
        if (!actorLayer && !worldLayer)
            return false;

        // Some legacy callers intentionally pass a broad actor mask. Character identity must win
        // over the broad default world mask; otherwise every Player/Ally/Enemy collider becomes a
        // world penetration before actor penetration can be scored.
        bool isCharacterCollider = hit.GetComponentInParent<CharacteContext>() != null;
        if (actorLayer && isCharacterCollider)
        {
            isActor = true;
            return true;
        }

        if (worldLayer)
        {
            isWorld = true;
            return true;
        }

        isActor = actorLayer;
        return isActor;
    }

    static int OverlapFootprint(
        CharacterPlacementFootprint footprint,
        Vector3 position,
        Quaternion rotation,
        int layerMask,
        CharacterPlacementRequest request)
    {
        Vector3 center = position + rotation * footprint.CenterOffset;
        Quaternion footprintRotation = rotation * footprint.Rotation;
        float padding = ResolvePadding(request);
        QueryTriggerInteraction triggerInteraction = ResolveTriggerInteraction(request);

        if (footprint.Shape == CharacterPlacementShape.Box)
        {
            return Physics.OverlapBoxNonAlloc(
                center,
                footprint.HalfExtents + Vector3.one * padding,
                OverlapBuffer,
                footprintRotation,
                layerMask,
                triggerInteraction);
        }

        float radius = footprint.Radius + padding;
        float segment = Mathf.Max(0f, footprint.Height * 0.5f - footprint.Radius);
        Vector3 axis = footprintRotation * footprint.Axis;
        return Physics.OverlapCapsuleNonAlloc(
            center - axis * segment,
            center + axis * segment,
            radius,
            OverlapBuffer,
            layerMask,
            triggerInteraction);
    }

    static float ResolvePenetration(
        CharacterPlacementRequest request,
        Vector3 actorPosition,
        Quaternion actorRotation,
        Collider hit)
    {
        if (request.PositionCollider == null)
            return ResolveApproximatePenetration(request, actorPosition, actorRotation, hit);

        ResolveColliderPose(
            request,
            actorPosition,
            actorRotation,
            out Vector3 colliderPosition,
            out Quaternion colliderRotation);
        if (Physics.ComputePenetration(
                request.PositionCollider,
                colliderPosition,
                colliderRotation,
                hit,
                hit.transform.position,
                hit.transform.rotation,
                out _,
                out float distance))
        {
            return Mathf.Max(0f, distance);
        }

        return ResolveApproximatePenetration(request, actorPosition, actorRotation, hit);
    }

    static float ResolveApproximatePenetration(
        CharacterPlacementRequest request,
        Vector3 actorPosition,
        Quaternion actorRotation,
        Collider hit)
    {
        if (hit == null)
            return 0f;

        Vector3 center = actorPosition + actorRotation * request.Footprint.CenterOffset;
        float actorRadius = ResolveBoundingRadius(request.Footprint);
        Vector3 closestPoint = hit.ClosestPoint(center);
        float distance = Vector3.Distance(center, closestPoint);
        if (distance > 0.0001f)
            return Mathf.Max(0f, actorRadius - distance);

        Bounds bounds = hit.bounds;
        if (!bounds.Contains(center))
            return actorRadius;

        float distanceToSurface = Mathf.Min(
            center.x - bounds.min.x,
            bounds.max.x - center.x,
            center.y - bounds.min.y,
            bounds.max.y - center.y,
            center.z - bounds.min.z,
            bounds.max.z - center.z);
        return actorRadius + Mathf.Max(0f, distanceToSurface);
    }

    static float ResolveBoundingRadius(CharacterPlacementFootprint footprint)
    {
        if (footprint.Shape == CharacterPlacementShape.Box)
            return footprint.HalfExtents.magnitude;

        float segment = Mathf.Max(0f, footprint.Height * 0.5f - footprint.Radius);
        return Mathf.Sqrt(footprint.Radius * footprint.Radius + segment * segment);
    }

    static void ResolveColliderPose(
        CharacterPlacementRequest request,
        Vector3 actorPosition,
        Quaternion actorRotation,
        out Vector3 colliderPosition,
        out Quaternion colliderRotation)
    {
        Collider collider = request.PositionCollider;
        if (collider == null)
        {
            colliderPosition = actorPosition;
            colliderRotation = actorRotation;
            return;
        }

        if (request.ActorRoot == null)
        {
            colliderPosition = actorPosition;
            colliderRotation = actorRotation;
            return;
        }

        Vector3 localPosition = request.ActorRoot.InverseTransformPoint(collider.transform.position);
        Quaternion localRotation = Quaternion.Inverse(request.ActorRoot.rotation) * collider.transform.rotation;
        colliderPosition = actorPosition + actorRotation * localPosition;
        colliderRotation = actorRotation * localRotation;
    }

    static bool ShouldIgnoreCollider(
        CharacterPlacementRequest request,
        Collider hit,
        bool targetIsAllowed)
    {
        if (hit == null)
            return true;

        Transform hitTransform = hit.transform;
        if (IsSameOrChild(hitTransform, request.ActorRoot) ||
            IsSameOrChild(hitTransform, request.IgnoreRoot) ||
            hit == request.IgnoredCollider ||
            (request.PositionCollider != null && hit == request.PositionCollider))
            return true;

        return targetIsAllowed && IsSameOrChild(hitTransform, request.TargetRoot);
    }

    static bool TryResolveReservationPose(
        CharacterPlacementRequest request,
        CharacterPlacementResult result,
        float normalizedTime,
        out Vector3 position,
        out Quaternion rotation)
    {
        position = result.StartPosition;
        rotation = result.StartRotation;
        if (request == null ||
            !request.EffectivePlanarRootMotion ||
            request.Animation == null ||
            !request.Animation.HasSamples ||
            !request.Animation.TrySample(normalizedTime, out CharacterPlacementAnimationInput.Sample sample))
            return true;

        position += result.StartRotation * sample.LocalPosition;
        rotation = result.StartRotation * Quaternion.Euler(0f, sample.LocalYaw, 0f);
        return true;
    }

    static float ResolveReservationPenetration(
        CharacterPlacementFootprint a,
        Vector3 positionA,
        Quaternion rotationA,
        CharacterPlacementFootprint b,
        Vector3 positionB,
        Quaternion rotationB)
    {
        Vector3 centerA = positionA + rotationA * a.CenterOffset;
        Vector3 centerB = positionB + rotationB * b.CenterOffset;
        float planarDistance = PlanarDistance(centerA, centerB);
        float radius = ResolvePlanarRadius(a, rotationA) + ResolvePlanarRadius(b, rotationB);
        return Mathf.Max(0f, radius - planarDistance);
    }

    static float ResolvePlanarRadius(CharacterPlacementFootprint footprint, Quaternion actorRotation)
    {
        Vector3 worldAxis = actorRotation * footprint.Rotation * footprint.Axis;
        float planarAxis = new Vector2(worldAxis.x, worldAxis.z).magnitude;
        float segment = Mathf.Max(0f, footprint.Height * 0.5f - footprint.Radius);
        return footprint.Radius + segment * planarAxis;
    }

    static float PlanarDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    static bool TrySnapToNavMesh(
        CharacterPlacementRequest request,
        Vector3 position,
        out Vector3 snappedPosition,
        out float snapDistance)
    {
        snappedPosition = position;
        snapDistance = 0f;
        if (!NavMesh.SamplePosition(
                position,
                out NavMeshHit hit,
                ResolveNavMeshSampleDistance(request),
                ResolveNavMeshAreaMask(request)))
            return false;

        snappedPosition = hit.position;
        snapDistance = Vector3.Distance(position, snappedPosition);
        return true;
    }

    static bool HasGroundSupport(CharacterPlacementRequest request, Vector3 position)
    {
        int groundMask = request.RuntimePolicy.HasValue
            ? request.RuntimePolicy.GroundLayers.value
            : request.Policy != null
            ? request.Policy.groundLayers.value
            : Physics.DefaultRaycastLayers;
        if (groundMask == 0)
            groundMask = Physics.DefaultRaycastLayers;

        float rayHeight = request.RuntimePolicy.HasValue
            ? request.RuntimePolicy.GroundRaycastHeight
            : request.Policy != null
            ? Mathf.Max(0.1f, request.Policy.groundRaycastHeight)
            : 2f;
        float rayDistance = request.RuntimePolicy.HasValue
            ? request.RuntimePolicy.GroundRaycastDistance
            : request.Policy != null
            ? Mathf.Max(0.1f, request.Policy.groundRaycastDistance)
            : 8f;
        return Physics.Raycast(
            position + Vector3.up * rayHeight,
            Vector3.down,
            rayDistance,
            groundMask,
            QueryTriggerInteraction.Ignore);
    }

    static bool IsFootprintOnNavMesh(CharacterPlacementRequest request, Vector3 position, Quaternion rotation)
    {
        CharacterPlacementFootprint footprint = request.Footprint;
        Vector3 center = position + rotation * footprint.CenterOffset;
        Quaternion footprintRotation = rotation * footprint.Rotation;
        int areaMask = ResolveNavMeshAreaMask(request);
        float sampleDistance = ResolveNavMeshSampleDistance(request);

        if (footprint.Shape == CharacterPlacementShape.Box)
        {
            float x = footprint.HalfExtents.x;
            float z = footprint.HalfExtents.z;
            for (int xSign = -1; xSign <= 1; xSign += 2)
            {
                for (int zSign = -1; zSign <= 1; zSign += 2)
                {
                    Vector3 local = new Vector3(x * xSign, 0f, z * zSign);
                    if (!NavMesh.SamplePosition(
                            center + footprintRotation * local,
                            out _,
                            sampleDistance,
                            areaMask))
                        return false;
                }
            }

            return true;
        }

        float radius = ResolvePlanarRadius(footprint, rotation);
        Vector3 axisX = footprintRotation * Vector3.right;
        Vector3 axisZ = footprintRotation * Vector3.forward;
        for (int i = 0; i < 4; i++)
        {
            Vector3 offset = i == 0
                ? axisX * radius
                : i == 1
                    ? -axisX * radius
                    : i == 2
                        ? axisZ * radius
                        : -axisZ * radius;
            if (!NavMesh.SamplePosition(
                    center + offset,
                    out _,
                    sampleDistance,
                    areaMask))
                return false;
        }

        return true;
    }

    static bool RequiresNavMesh(CharacterPlacementRequest request)
    {
        return request.MobileActor ||
            (request.RuntimePolicy.HasValue
                ? request.RuntimePolicy.RequireNavMesh
                : request.Policy != null && request.Policy.requireNavMesh);
    }

    static bool RequiresGroundSupport(CharacterPlacementRequest request)
    {
        return request.RuntimePolicy.HasValue
            ? request.RuntimePolicy.RequireGroundSupport
            : request.Policy != null && request.Policy.requireGroundSupport;
    }

    static float ResolveNavMeshSampleDistance(CharacterPlacementRequest request)
    {
        return request.RuntimePolicy.HasValue
            ? request.RuntimePolicy.NavMeshSampleDistance
            : request.Policy != null
            ? Mathf.Max(0.05f, request.Policy.navMeshSampleDistance)
            : 0.75f;
    }

    static int ResolveNavMeshAreaMask(CharacterPlacementRequest request)
    {
        return request.RuntimePolicy.HasValue
            ? request.RuntimePolicy.NavMeshAreaMask
            : request.Policy != null ? request.Policy.navMeshAreaMask : NavMesh.AllAreas;
    }

    static float ResolvePadding(CharacterPlacementRequest request)
    {
        return request.RuntimePolicy.HasValue
            ? request.RuntimePolicy.CollisionPadding
            : request.Policy != null ? Mathf.Max(0f, request.Policy.collisionPadding) : 0.05f;
    }

    static QueryTriggerInteraction ResolveTriggerInteraction(CharacterPlacementRequest request)
    {
        return request.RuntimePolicy.HasValue
            ? request.RuntimePolicy.CollisionTriggerInteraction
            : request.Policy != null
            ? request.Policy.collisionTriggerInteraction
            : QueryTriggerInteraction.Ignore;
    }

    static int ResolveMaxDetailedCandidates(CharacterPlacementRequest request)
    {
        return request.RuntimePolicy.HasValue
            ? request.RuntimePolicy.MaxDetailedCandidates
            : request.Policy != null ? request.Policy.maxDetailedCandidates : MaxDetailCount;
    }

    static int ResolveMaxTrajectorySamples(CharacterPlacementRequest request)
    {
        return request.RuntimePolicy.HasValue
            ? request.RuntimePolicy.MaxTrajectorySamples
            : request.Policy != null ? request.Policy.maxTrajectorySamples : DefaultMaxTrajectorySamples;
    }

    static bool IsTargetContactWindow(CharacterPlacementRequest request, float normalizedTime)
    {
        CharacterPlacementPolicyDef policy = request.Policy;
        if (request.TargetRoot == null || (!request.RuntimePolicy.HasValue && policy == null))
            return false;

        float delta = normalizedTime - request.ImpactNormalizedTime;
        float before = request.RuntimePolicy.HasValue
            ? request.RuntimePolicy.TargetContactWindowBefore
            : Mathf.Max(0f, policy.targetContactWindowBefore);
        float after = request.RuntimePolicy.HasValue
            ? request.RuntimePolicy.TargetContactWindowAfter
            : Mathf.Max(0f, policy.targetContactWindowAfter);
        return delta >= -before && delta <= after;
    }

    static bool IsSameOrChild(Transform candidate, Transform root)
    {
        return candidate != null && root != null &&
               (candidate == root || candidate.IsChildOf(root));
    }

    struct CandidateEvaluation
    {
        public bool Valid;
        public bool BufferFull;
        public Vector3 StartPosition;
        public Vector3 ImpactPosition;
        public Quaternion ImpactRotation;
        public float MaxWorldPenetration;
        public float TotalWorldPenetration;
        public float MaxActorPenetration;
        public float TotalActorPenetration;
        public int CollisionSampleCount;
        public float PreferredAngleError;
        public float NavMeshSnapDistance;
        public int AuthoredCandidateOrder;
        public CharacterPlacementScore Score;
    }
}
