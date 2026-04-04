using UnityEngine;

public static class ChainAttackTargetingUtility
{
    public static bool TryResolveLockedTarget(
        PlayerContext playerContext,
        ChainAttackSequenceDef sequenceDef,
        Transform explicitTargetTransform,
        GameObject helperObject,
        out GameObject targetObject,
        out Transform targetTransform,
        out Transform anchorTransform)
    {
        targetObject = null;
        targetTransform = null;
        anchorTransform = null;

        if (sequenceDef == null)
            return false;

        if (sequenceDef.targetSource != ChainTargetSource.AimTargetOnly &&
            explicitTargetTransform != null &&
            TryResolveExplicitTarget(
                explicitTargetTransform,
                playerContext,
                helperObject,
                out targetObject,
                out targetTransform,
                out anchorTransform))
        {
            return true;
        }

        if (sequenceDef.targetSource == ChainTargetSource.ExplicitTargetOnly)
            return false;

        return TryResolveTargetFromAim(
            playerContext,
            sequenceDef,
            helperObject,
            out targetObject,
            out targetTransform,
            out anchorTransform);
    }

    public static bool TryResolveExplicitTarget(
        Transform explicitTargetTransform,
        PlayerContext playerContext,
        GameObject helperObject,
        out GameObject targetObject,
        out Transform targetTransform,
        out Transform anchorTransform)
    {
        targetObject = null;
        targetTransform = null;
        anchorTransform = null;

        if (explicitTargetTransform == null)
            return false;

        Transform resolvedRoot = ResolveTargetRoot(explicitTargetTransform);
        if (resolvedRoot == null)
            return false;

        if (!IsTargetAllowed(resolvedRoot, playerContext, helperObject))
            return false;

        if (!IsTargetAlive(resolvedRoot))
            return false;

        if (!TryResolveTargetAnchor(explicitTargetTransform, out anchorTransform))
            return false;

        targetTransform = resolvedRoot;
        targetObject = resolvedRoot.gameObject;
        return true;
    }

    public static bool TryResolveTargetAnchor(Transform targetTransform, out Transform anchorTransform)
    {
        anchorTransform = null;

        if (targetTransform == null)
            return false;

        AITargetInfo targetInfo = targetTransform.GetComponentInParent<AITargetInfo>();
        if (targetInfo != null && targetInfo.ChainAttackPoint != null)
        {
            anchorTransform = targetInfo.ChainAttackPoint;
            return true;
        }

        IAITargetable aiTargetable = FindInterfaceInParents<IAITargetable>(targetTransform);
        if (aiTargetable != null && aiTargetable.AimPoint != null)
        {
            anchorTransform = aiTargetable.AimPoint;
            return true;
        }

        anchorTransform = ResolveTargetRoot(targetTransform);
        return anchorTransform != null;
    }

    public static bool IsTargetAlive(Transform targetTransform)
    {
        if (targetTransform == null)
            return false;

        Transform rootTransform = ResolveTargetRoot(targetTransform);
        if (rootTransform == null)
            return false;

        CharacteContext targetContext = rootTransform.GetComponentInParent<CharacteContext>();
        IAITargetable aiTargetable = FindInterfaceInParents<IAITargetable>(rootTransform);
        IDamageable damageable = FindInterfaceInParents<IDamageable>(rootTransform);

        if (targetContext != null && targetContext.stateHub != null)
            return targetContext.stateHub.IsAlive && !targetContext.stateHub.Isdown;

        if (aiTargetable != null)
            return aiTargetable.IsAlive;

        if (damageable != null)
            return damageable.IsAlive;

        return true;
    }

    static bool TryResolveTargetFromAim(
        PlayerContext playerContext,
        ChainAttackSequenceDef sequenceDef,
        GameObject helperObject,
        out GameObject targetObject,
        out Transform targetTransform,
        out Transform anchorTransform)
    {
        targetObject = null;
        targetTransform = null;
        anchorTransform = null;

        if (playerContext == null || playerContext.aimTarget == null || sequenceDef == null)
            return false;

        Vector3 aimPoint = playerContext.aimTarget.position;
        Collider[] hits = Physics.OverlapSphere(
            aimPoint,
            Mathf.Max(0.1f, sequenceDef.aimSearchRadius),
            sequenceDef.targetLayers,
            sequenceDef.targetTriggerInteraction);

        float bestDistanceSqr = float.PositiveInfinity;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider hit = hits[i];
            if (hit == null)
                continue;

            if (!TryResolveTargetCandidate(
                    hit,
                    playerContext,
                    helperObject,
                    out GameObject candidateObject,
                    out Transform candidateTransform,
                    out Transform candidateAnchor))
            {
                continue;
            }

            Vector3 candidatePoint = candidateAnchor != null ? candidateAnchor.position : candidateTransform.position;
            if (sequenceDef.requireAimLineOfSight &&
                !HasAimLineOfSight(
                    aimPoint,
                    candidatePoint,
                    sequenceDef.aimObstacleLayers,
                    sequenceDef.targetTriggerInteraction))
            {
                continue;
            }

            float distanceSqr = (candidatePoint - aimPoint).sqrMagnitude;
            if (distanceSqr >= bestDistanceSqr)
                continue;

            bestDistanceSqr = distanceSqr;
            targetObject = candidateObject;
            targetTransform = candidateTransform;
            anchorTransform = candidateAnchor;
        }

        return targetObject != null && anchorTransform != null;
    }

    static bool TryResolveTargetCandidate(
        Collider hit,
        PlayerContext playerContext,
        GameObject helperObject,
        out GameObject candidateObject,
        out Transform candidateTransform,
        out Transform candidateAnchor)
    {
        candidateObject = null;
        candidateTransform = null;
        candidateAnchor = null;

        if (hit == null)
            return false;

        Transform rootTransform = ResolveTargetRoot(hit.transform);
        if (rootTransform == null)
            return false;

        if (!IsTargetAllowed(rootTransform, playerContext, helperObject))
            return false;

        if (!IsTargetAlive(rootTransform))
            return false;

        if (!TryResolveTargetAnchor(rootTransform, out candidateAnchor))
            return false;

        candidateTransform = rootTransform;
        candidateObject = rootTransform.gameObject;
        return true;
    }

    static bool IsTargetAllowed(Transform rootTransform, PlayerContext playerContext, GameObject helperObject)
    {
        if (rootTransform == null)
            return false;

        if (playerContext != null && rootTransform == playerContext.transform.root)
            return false;

        if (helperObject != null && rootTransform == helperObject.transform.root)
            return false;

        return true;
    }

    static Transform ResolveTargetRoot(Transform start)
    {
        if (start == null)
            return null;

        CharacteContext targetContext = start.GetComponentInParent<CharacteContext>();
        if (targetContext != null)
            return targetContext.transform;

        Rigidbody rigidbody = start.GetComponentInParent<Rigidbody>();
        if (rigidbody != null)
            return rigidbody.transform;

        return start.root != null ? start.root : start;
    }

    static bool HasAimLineOfSight(
        Vector3 origin,
        Vector3 targetPoint,
        LayerMask obstacleLayers,
        QueryTriggerInteraction triggerInteraction)
    {
        if (obstacleLayers == 0)
            return true;

        Vector3 direction = targetPoint - origin;
        float distance = direction.magnitude;
        if (distance <= 0.001f)
            return true;

        return !Physics.Raycast(
            origin,
            direction / distance,
            distance,
            obstacleLayers,
            triggerInteraction);
    }

    static T FindInterfaceInParents<T>(Transform start) where T : class
    {
        if (start == null)
            return null;

        MonoBehaviour[] behaviours = start.GetComponentsInParent<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is T match)
                return match;
        }

        return null;
    }
}
