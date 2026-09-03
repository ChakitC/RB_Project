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
        out Transform anchorTransform,
        bool preferChainReadyTargets = false)
    {
        targetObject = null;
        targetTransform = null;
        anchorTransform = null;

        if (sequenceDef == null)
            return false;

        if (sequenceDef.targetSource != ChainTargetSource.AimTargetOnly &&
            explicitTargetTransform != null)
        {
            return TryResolveExplicitTarget(
                explicitTargetTransform,
                playerContext,
                helperObject,
                out targetObject,
                out targetTransform,
                out anchorTransform);
        }

        if (sequenceDef.targetSource == ChainTargetSource.ExplicitTargetOnly)
            return false;

        return TryResolveTargetFromAim(
            playerContext,
            sequenceDef,
            helperObject,
            out targetObject,
            out targetTransform,
            out anchorTransform,
            preferChainReadyTargets);
    }

    /// <summary>
    /// The StaggerMeter that owns a resolved chain target. Callers get the same lookup the manual
    /// ChainReady path uses, so a target picked here and a target checked there cannot disagree.
    /// </summary>
    public static StaggerMeter ResolveStaggerMeter(Transform targetTransform)
    {
        if (targetTransform == null)
            return null;

        StaggerMeter meter = targetTransform.GetComponentInParent<StaggerMeter>();
        return meter != null ? meter : targetTransform.GetComponentInChildren<StaggerMeter>(true);
    }

    /// <summary>True when this target is sitting in an unclaimed ChainReady window.</summary>
    public static bool IsChainReadyForManualChain(Transform targetTransform)
    {
        StaggerMeter meter = ResolveStaggerMeter(targetTransform);
        return meter != null && meter.IsChainReady && !meter.IsChainExecutionActive;
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

        Transform rootTransform = ResolveTargetRoot(targetTransform);
        if (rootTransform == null)
            return false;

        AITargetInfo childTargetInfo = rootTransform.GetComponentInChildren<AITargetInfo>(true);
        if (childTargetInfo != null && childTargetInfo.ChainAttackPoint != null)
        {
            anchorTransform = childTargetInfo.ChainAttackPoint;
            return true;
        }

        IAITargetable childTargetable = FindInterfaceInChildren<IAITargetable>(rootTransform);
        if (childTargetable != null && childTargetable.AimPoint != null)
        {
            anchorTransform = childTargetable.AimPoint;
            return true;
        }

        anchorTransform = rootTransform;
        return true;
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
        out Transform anchorTransform,
        bool preferChainReadyTargets)
    {
        targetObject = null;
        targetTransform = null;
        anchorTransform = null;

        if (playerContext == null || playerContext.aimTarget == null || sequenceDef == null)
            return false;

        Vector3 aimPoint = playerContext.aimTarget.position;
        Camera gameplayCamera = Camera.main;
        Vector3 searchOrigin = gameplayCamera != null
            ? gameplayCamera.transform.position
            : playerContext.transform.position + Vector3.up;
        Collider[] hits = Physics.OverlapCapsule(
            searchOrigin,
            aimPoint,
            Mathf.Max(0.1f, sequenceDef.aimSearchRadius),
            sequenceDef.targetLayers,
            sequenceDef.targetTriggerInteraction);

        var best = new CandidateSelection { Score = float.PositiveInfinity };

        EvaluateCandidates(
            hits,
            playerContext,
            sequenceDef,
            helperObject,
            gameplayCamera,
            searchOrigin,
            preferChainReadyTargets,
            chainReadyOnly: false,
            ref best);

        // The aim capsule hangs off the camera, so a ChainReady enemy that is behind the player, or
        // simply outside the aim cone, is invisible to it — the prompt says [F] and the press finds
        // nothing. A second sweep centred on the player picks those up, and it only ever accepts
        // ChainReady candidates so ordinary targeting keeps its existing reach.
        if (preferChainReadyTargets && !best.IsChainReady)
        {
            float sweepRadius = sequenceDef.ResolvedChainReadySearchRadius;
            if (sweepRadius > 0f)
            {
                Collider[] nearby = Physics.OverlapSphere(
                    playerContext.transform.position,
                    sweepRadius,
                    sequenceDef.targetLayers,
                    sequenceDef.targetTriggerInteraction);

                EvaluateCandidates(
                    nearby,
                    playerContext,
                    sequenceDef,
                    helperObject,
                    gameplayCamera,
                    searchOrigin,
                    preferChainReadyTargets: true,
                    chainReadyOnly: true,
                    ref best);
            }
        }

        targetObject = best.Object;
        targetTransform = best.Root;
        anchorTransform = best.Anchor;
        return targetObject != null && anchorTransform != null;
    }

    /// <summary>Best candidate found so far, carried across both sweeps.</summary>
    struct CandidateSelection
    {
        public GameObject Object;
        public Transform Root;
        public Transform Anchor;
        public float Score;
        public bool IsChainReady;
    }

    // Reticle scores are normalised screen distances, so any constant well above 1 keeps every
    // off-screen ChainReady candidate ranked below every on-screen one while still ordering them
    // sensibly among themselves (by distance from the player).
    const float OffScreenScoreBase = 1000f;

    static void EvaluateCandidates(
        Collider[] hits,
        PlayerContext playerContext,
        ChainAttackSequenceDef sequenceDef,
        GameObject helperObject,
        Camera gameplayCamera,
        Vector3 searchOrigin,
        bool preferChainReadyTargets,
        bool chainReadyOnly,
        ref CandidateSelection best)
    {
        if (hits == null)
            return;

        Vector3 playerPosition = playerContext.transform.position;

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

            bool candidateIsChainReady =
                preferChainReadyTargets && IsChainReadyForManualChain(candidateTransform);

            if (chainReadyOnly && !candidateIsChainReady)
                continue;

            // Once a ChainReady candidate is held, nothing without ChainReady can take the slot.
            if (best.IsChainReady && !candidateIsChainReady)
                continue;

            if (candidateObject == best.Object)
                continue;

            bool winsOnChainReady = candidateIsChainReady && !best.IsChainReady;

            Vector3 candidatePoint = candidateAnchor != null ? candidateAnchor.position : candidateTransform.position;
            if (!ThirdPersonTargetingUtility.TryGetReticleScore(
                    gameplayCamera,
                    candidatePoint,
                    out float score))
            {
                // Ordinary targeting still requires the target to be on screen.
                if (!candidateIsChainReady)
                    continue;

                score = OffScreenScoreBase + Vector3.Distance(playerPosition, candidatePoint);
            }

            if (!winsOnChainReady && score >= best.Score)
                continue;

            if (sequenceDef.requireAimLineOfSight &&
                !ThirdPersonTargetingUtility.HasLineOfSight(
                    searchOrigin,
                    candidatePoint,
                    candidateTransform,
                    sequenceDef.aimObstacleLayers,
                    sequenceDef.targetTriggerInteraction,
                    playerContext.transform))
            {
                continue;
            }

            best.Score = score;
            best.IsChainReady = candidateIsChainReady;
            best.Object = candidateObject;
            best.Root = candidateTransform;
            best.Anchor = candidateAnchor;
        }
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

    static T FindInterfaceInChildren<T>(Transform start) where T : class
    {
        if (start == null)
            return null;

        MonoBehaviour[] behaviours = start.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is T match)
                return match;
        }

        return null;
    }
}
