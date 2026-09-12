using UnityEngine;

public static class ChainAttackTargetingUtility
{
    public static SkillTargetHandle CreateTargetHandle(Transform targetTransform)
    {
        if (targetTransform == null)
            return SkillTargetHandle.None;

        CharacteContext context = targetTransform.GetComponentInParent<CharacteContext>();
        if (context == null)
            context = targetTransform.GetComponentInChildren<CharacteContext>(true);
        if (context != null)
            return SkillTargetHandle.For(context);

        MonoBehaviour[] behaviours = targetTransform.GetComponentsInParent<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is IDamageable)
                return SkillTargetHandle.ForNonCharacterDamageable(behaviours[i]);
        }

        behaviours = targetTransform.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is IDamageable)
                return SkillTargetHandle.ForNonCharacterDamageable(behaviours[i]);
        }

        return SkillTargetHandle.None;
    }

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

        return TryResolveCommittedPlayerTarget(
            playerContext,
            sequenceDef,
            helperObject,
            out targetObject,
            out targetTransform,
            out anchorTransform);
    }

    /// <summary>
    /// The StaggerMeter that owns a resolved chain target. Callers get the same lookup the manual
    /// ChainReady path uses, so a target picked here and a target checked there cannot disagree.
    /// </summary>
    public static StaggerMeter ResolveStaggerMeter(Transform targetTransform)
    {
        if (targetTransform == null)
            return null;

        CharacteContext context = targetTransform.GetComponentInParent<CharacteContext>();
        if (context is EnemyContext enemy)
        {
            enemy.ResolveReferences();
            if (enemy.StaggerMeter != null)
                return enemy.StaggerMeter;
        }

        StaggerMeter meter = targetTransform.GetComponent<StaggerMeter>();
        if (meter == null)
            meter = targetTransform.GetComponentInChildren<StaggerMeter>(true);
        return meter != null ? meter : targetTransform.GetComponentInParent<StaggerMeter>();
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

        if (targetContext != null)
        {
            targetContext.ResolveReferences();
            if (targetContext.HealthSystem != null)
                return targetContext.HealthSystem.IsAlive;
            if (targetContext.stateHub != null)
                return targetContext.stateHub.IsAlive && !targetContext.stateHub.Isdown;
        }

        if (aiTargetable != null)
            return aiTargetable.IsAlive;

        if (damageable != null)
            return damageable.IsAlive;

        return true;
    }

    static bool TryResolveCommittedPlayerTarget(
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

        if (playerContext == null || sequenceDef == null)
            return false;

        playerContext.ResolveReferences();
        if (playerContext.Targeting == null ||
            !playerContext.Targeting.TryGetTarget(out CharacteContext committedTarget) ||
            !IsTargetAllowed(committedTarget.transform, playerContext, helperObject) ||
            !IsTargetAlive(committedTarget.transform) ||
            !IsLayerAllowed(committedTarget, sequenceDef.targetLayers) ||
            !TryResolveTargetAnchor(committedTarget.transform, out anchorTransform))
        {
            return false;
        }

        targetTransform = committedTarget.transform;
        targetObject = committedTarget.gameObject;
        return true;
    }

    static bool IsLayerAllowed(CharacteContext target, LayerMask allowedLayers)
    {
        if (target == null)
            return false;
        if (allowedLayers == ~0)
            return true;

        int rootLayerBit = 1 << target.gameObject.layer;
        int targetInfoLayerBit = target.TargetInfo != null
            ? 1 << target.TargetInfo.gameObject.layer
            : 0;
        return (allowedLayers.value & (rootLayerBit | targetInfoLayerBit)) != 0;
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
