using UnityEngine;

public static class ThirdPersonTargetingUtility
{
    static readonly RaycastHit[] sharedLineOfSightHits = new RaycastHit[64];

    public static bool TryGetReticleScore(
        Camera camera,
        Vector3 targetPoint,
        out float score,
        float maximumViewportRadius = 0.4f)
    {
        score = float.PositiveInfinity;
        if (camera == null)
            return false;

        Vector3 screen = camera.WorldToScreenPoint(targetPoint);
        if (screen.z <= 0f || Screen.height <= 0 ||
            screen.x < 0f || screen.x > Screen.width ||
            screen.y < 0f || screen.y > Screen.height)
            return false;

        Vector2 reticle = new(Screen.width * 0.5f, Screen.height * 0.5f);
        float normalizedScreenDistance = Vector2.Distance(
            new Vector2(screen.x, screen.y),
            reticle) / Screen.height;
        if (normalizedScreenDistance > maximumViewportRadius)
            return false;

        score = normalizedScreenDistance;
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

        return HasLineOfSightNonAlloc(
            origin,
            targetPoint,
            targetRoot,
            obstacleMask,
            sharedLineOfSightHits,
            triggerInteraction,
            ignoredRoot);
    }

    public static bool HasLineOfSightNonAlloc(
        Vector3 origin,
        Vector3 targetPoint,
        Transform targetRoot,
        LayerMask obstacleMask,
        RaycastHit[] hitBuffer,
        QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore,
        Transform ignoredRoot = null)
    {
        if (obstacleMask == 0)
            return true;

        Vector3 direction = targetPoint - origin;
        float distance = direction.magnitude;
        if (distance <= 0.001f)
            return true;
        if (hitBuffer == null || hitBuffer.Length == 0)
            return false;

        int hitCount = Physics.RaycastNonAlloc(
            origin,
            direction / distance,
            hitBuffer,
            distance,
            obstacleMask,
            triggerInteraction);

        // A full NonAlloc buffer is incomplete. Failing closed prevents an omitted obstacle from
        // turning into visibility through a wall.
        if (hitCount >= hitBuffer.Length)
            return false;

        float nearestDistance = float.PositiveInfinity;
        Transform nearestTransform = null;
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit candidate = hitBuffer[i];
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
            nearestTransform = candidate.transform;
        }

        if (nearestDistance == float.PositiveInfinity)
            return true;

        return targetRoot != null &&
               (nearestTransform == targetRoot ||
                nearestTransform.IsChildOf(targetRoot));
    }

    public static void FacePlayerTowardSoftTarget(PlayerContext playerContext, float actionRange)
    {
        if (playerContext == null)
            return;

        playerContext.ResolveReferences();
        Vector3 fallbackForward = playerContext.thirdPersonAim != null
            ? playerContext.thirdPersonAim.GetPlanarCameraForward()
            : GameplayCameraController.Instance != null
                ? GameplayCameraController.Instance.PlanarForward
                : playerContext.transform.forward;

        Vector3 facing = fallbackForward;
        if (playerContext.Targeting != null &&
            playerContext.Targeting.TryGetTarget(out CharacteContext target) &&
            playerContext.Targeting.TryValidateForAction(target, actionRange))
        {
            facing = target.transform.position - playerContext.transform.position;
        }

        facing.y = 0f;
        if (facing.sqrMagnitude > 0.0001f)
            playerContext.transform.rotation = Quaternion.LookRotation(facing.normalized, Vector3.up);

        GameplayCameraController.Instance?.RequestCombatAlignment();
    }

    public static void FacePlayerTowardSoftTarget(
        PlayerContext playerContext,
        float searchDistance,
        float searchRadius,
        LayerMask targetMask)
    {
        FacePlayerTowardSoftTarget(playerContext, searchDistance);
    }
}
