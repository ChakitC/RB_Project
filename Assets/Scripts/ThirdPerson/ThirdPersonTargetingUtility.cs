using UnityEngine;

public static class ThirdPersonTargetingUtility
{
    public static bool TryGetReticleScore(
        Camera camera,
        Vector3 targetPoint,
        out float score,
        float maximumViewportRadius = 0.4f)
    {
        score = float.PositiveInfinity;
        if (camera == null)
            return false;

        Vector3 viewport = camera.WorldToViewportPoint(targetPoint);
        if (viewport.z <= 0f)
            return false;

        Vector2 offset = new(viewport.x - 0.5f, viewport.y - 0.5f);
        float viewportDistance = offset.magnitude;
        if (viewportDistance > maximumViewportRadius)
            return false;

        score = viewportDistance * 1000f + viewport.z * 0.001f;
        return true;
    }

    public static bool HasLineOfSight(
        Vector3 origin,
        Vector3 targetPoint,
        Transform targetRoot,
        LayerMask obstacleMask,
        QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore,
        Transform ignoredRoot = null)
    {
        if (obstacleMask == 0)
            return true;

        Vector3 direction = targetPoint - origin;
        float distance = direction.magnitude;
        if (distance <= 0.001f)
            return true;

        RaycastHit[] hits = Physics.RaycastAll(
            origin,
            direction / distance,
            distance,
            obstacleMask,
            triggerInteraction);
        RaycastHit nearestHit = default;
        float nearestDistance = float.PositiveInfinity;
        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit candidate = hits[i];
            if (candidate.collider == null)
                continue;
            if (ignoredRoot != null &&
                (candidate.transform == ignoredRoot ||
                 candidate.transform.IsChildOf(ignoredRoot)))
            {
                continue;
            }

            if (candidate.distance >= nearestDistance)
                continue;

            nearestDistance = candidate.distance;
            nearestHit = candidate;
        }

        if (nearestDistance == float.PositiveInfinity)
            return true;

        return targetRoot != null &&
               (nearestHit.transform == targetRoot ||
                nearestHit.transform.IsChildOf(targetRoot));
    }

    public static void FacePlayerTowardSoftTarget(
        PlayerContext playerContext,
        float searchDistance,
        float searchRadius,
        LayerMask targetMask)
    {
        if (playerContext == null)
            return;

        Camera camera = Camera.main;
        Vector3 fallbackForward = GameplayCameraController.Instance != null
            ? GameplayCameraController.Instance.PlanarForward
            : playerContext.transform.forward;
        Vector3 origin = playerContext.transform.position + Vector3.up;
        Vector3 end = origin + fallbackForward * Mathf.Max(0.5f, searchDistance);

        Collider[] candidates = Physics.OverlapCapsule(
            origin,
            end,
            Mathf.Max(0.1f, searchRadius),
            targetMask,
            QueryTriggerInteraction.Ignore);

        Transform bestTarget = null;
        float bestScore = float.PositiveInfinity;
        for (int i = 0; i < candidates.Length; i++)
        {
            Collider candidate = candidates[i];
            if (candidate == null || candidate.transform.IsChildOf(playerContext.transform))
                continue;

            CharacteContext candidateContext =
                candidate.GetComponentInParent<CharacteContext>();
            if (candidateContext == null ||
                candidateContext.TargetIdentity != AITargetIdentity.Enemy)
            {
                continue;
            }

            Vector3 point = candidate.bounds.center;
            if (!TryGetReticleScore(camera, point, out float score, 0.28f) ||
                score >= bestScore ||
                !HasLineOfSight(
                    camera != null ? camera.transform.position : origin,
                    point,
                    candidateContext.transform,
                    ~0,
                    QueryTriggerInteraction.Ignore,
                    playerContext.transform))
            {
                continue;
            }

            bestScore = score;
            bestTarget = candidateContext.transform;
        }

        Vector3 facing = bestTarget != null
            ? bestTarget.position - playerContext.transform.position
            : fallbackForward;
        facing.y = 0f;
        if (facing.sqrMagnitude > 0.0001f)
            playerContext.transform.rotation = Quaternion.LookRotation(facing.normalized, Vector3.up);

        GameplayCameraController.Instance?.RequestCombatAlignment();
    }
}
